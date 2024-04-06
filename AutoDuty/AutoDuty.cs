using System;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.IO;
using ECommons;
using ECommons.DalamudServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using AutoDuty.Managers;
using AutoDuty.Windows;
using AutoDuty.IPC;
using ECommons.Automation;
using AutoDuty.External;
using AutoDuty.Helpers;
using ECommons.Throttlers;
using Dalamud.Game.ClientState.Objects.Types;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using System.Text.Json;
using System.Text;
using ECommons.ExcelServices;

namespace AutoDuty;

// TODO:
// Need to add 4-Box capability and add dungeons not in support
// Need to expand AutoRepair to include check for level and stuff to see if you are eligible for self repair. and check for dark matter
// Add auto GC turn in and Auto desynth
// make config saving per character
// Mostly just need to modify and change paths, other than that i think we are there.

public class AutoDuty : IDalamudPlugin
{
    internal List<string> ListBoxPOSText { get; set; } = [];
    internal int LoopTimes = 1;
    internal int CurrentLoop = 0;
    internal ContentHelper.Content? CurrentTerritoryContent = null;
    internal uint CurrentTerritoryType = 0;
    internal string Name => "AutoDuty";
    internal static AutoDuty Plugin { get; private set; }
    internal bool StopForCombat = true;
    internal DirectoryInfo PathsDirectory;
    internal Configuration Configuration { get; init; }
    internal WindowSystem WindowSystem = new("AutoDuty");
    internal int Stage = 0;
    internal int Indexer = -1;
    internal bool MainListClicked = false;
    internal bool Started = false;
    internal bool Running = false;
    internal PlayerCharacter? Player = null;
    internal Vector3 PlayerPosition = Vector3.Zero;
    internal BattleChara? BossObject;
    internal GameObject? ClosestInteractableEventObject = null;
    internal GameObject? ClosestTargetableBattleNpc = null;
    internal OverrideCamera OverrideCamera;
    internal MainWindow MainWindow { get; init; }
    internal bool Support = false;
    internal bool Trust = false;
    internal bool Squadron = false;
    internal bool Regular = false;
    internal bool Unsynced = false;
    internal bool Repairing = false;
    internal bool Goto = false;
    internal bool InDungeon = false;
    internal string Action = "";
    internal string PathFile = "";

    private const string CommandName = "/autoduty";
    private DirectoryInfo _configDirectory;
    private ActionsManager _actions;
    private Chat _chat;
    private TaskManager _taskManager;
    private RepairManager _repairManager;
    private GotoManager _gotoManager;
    private DutySupportManager _dutySupportManager;
    private TrustManager _trustManager;
    private SquadronManager _squadronManager;
    private OverrideAFK _overrideAFK;
    private OverrideMovement _overrideMovement;
    private bool _dead = false;
    private GameObject? treasureCofferGameObject = null;
    private string _action = "";
    private List<object> _actionParams = [];
    private List<object> _actionPosition = [];

    public AutoDuty(DalamudPluginInterface pluginInterface)
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector, Module.ObjectFunctions);
            ExecSkipTalk.Init();

            Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(pluginInterface);

            _configDirectory = pluginInterface.ConfigDirectory;
            PathsDirectory = new(_configDirectory.FullName + "/paths");

            if (!_configDirectory.Exists)
                _configDirectory.Create();
            if (!PathsDirectory.Exists)
                PathsDirectory.Create();

            _taskManager = new()
            {
                AbortOnTimeout = false,
                TimeoutSilently = true
            };

            ContentHelper.PopulateDuties();
            FileHelper.Init();
            _chat = new();
            _overrideMovement = new();
            _overrideAFK = new();
            _repairManager = new(_taskManager);
            _gotoManager = new(_taskManager);
            _dutySupportManager = new(_taskManager);
            _trustManager = new(_taskManager);
            _squadronManager = new(_taskManager);
            _actions = new(this, _chat, _taskManager, _overrideMovement);
            BuildTab.ActionsList = _actions.ActionsList;
            MainWindow = new(this);
            OverrideCamera = new();

            WindowSystem.AddWindow(MainWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "\n/autoduty->opens main window\n" +
                "/autoduty config or cfg->opens config window\n"
            });

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
            pluginInterface.UiBuilder.OpenMainUi += OpenMainUI;

            Svc.Framework.Update += Framework_Update;
            Svc.ClientState.TerritoryChanged += ClientState_TerritoryChanged;
            Svc.Condition.ConditionChange += Condition_ConditionChange;
        }
        catch (Exception e) { Svc.Log.Info($"Failed loading plugin\n{e}");
        }
    }

    internal void LoadPath()
    {
        try
        {
            using StreamReader streamReader = new(PathFile, Encoding.UTF8);
            ListBoxPOSText.Clear();
            var json = streamReader.ReadToEnd();
            List<string>? paths;
            if ((paths = JsonSerializer.Deserialize<List<string>>(json)) != null)
                ListBoxPOSText = paths;
        }
        catch (Exception e)
        {
            Svc.Log.Error(e.ToString());
            //throw;
        }
    }

    private void ClientState_TerritoryChanged(ushort t)
    {
        CurrentTerritoryType = t;

        if (t == 0)
            return;

        if (CurrentTerritoryContent == null)
        {
            if (ContentHelper.DictionaryContent.TryGetValue(t, out var content))
                CurrentTerritoryContent = content;
        }

        InDungeon = ExcelTerritoryHelper.Get(t).TerritoryIntendedUse == 3;
        if (InDungeon && ContentHelper.DictionaryContent.TryGetValue(t, out var territoryContent))
            PathFile = $"{Plugin.PathsDirectory.FullName}/({t}) {territoryContent.Name}.json";
        else
            PathFile = "";

        if (!PathFile.IsNullOrEmpty() && File.Exists(PathFile)) 
            LoadPath();
        else
            ListBoxPOSText.Clear();

        if (!Running || Repairing || Goto || CurrentTerritoryContent == null)
            return;

        if (t != CurrentTerritoryContent.TerritoryType)
        {
            if (CurrentLoop < LoopTimes)
            {
                _taskManager.Enqueue(() => Stage = 99, "Loop");
                _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Loop");
                _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "Loop");
                _taskManager.Enqueue(_repairManager.Repair, int.MaxValue, "Loop");
                if (Trust)
                    _taskManager.Enqueue(() => _trustManager.RegisterTrust(CurrentTerritoryContent), int.MaxValue, "Loop");
                else if (Support)
                    _taskManager.Enqueue(() => _dutySupportManager.RegisterDutySupport(CurrentTerritoryContent), int.MaxValue, "Loop");
                else if (Squadron)
                {
                    _gotoManager.Goto(true, false);
                    _taskManager.Enqueue(() => _squadronManager.RegisterSquadron(CurrentTerritoryContent), int.MaxValue, "Loop");
                }
                _taskManager.Enqueue(() => CurrentLoop++, "Loop");
            }
            else
            {
                Running = false;
                CurrentLoop = 0;
                Stage = 0;
                MainWindow.SetWindowSize(new Vector2(425, 375));
            }
        }
    }

    private void Condition_ConditionChange(Dalamud.Game.ClientState.Conditions.ConditionFlag flag, bool value)
    {
        //Svc.Log.Debug($"{flag} : {value}");
        if (Stage != 3 && value && Started && (flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas || flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51))
        {
            Stage = 1;
            MovementHelper.Stop();
        }
    }

    public void Run()
    {
        if (CurrentTerritoryContent == null)
            return;

        Stage = 99;
        Svc.Log.Info($"Running {CurrentTerritoryContent.Name} {LoopTimes} Times");
        Running = true;
        if (!Squadron)
            _gotoManager.Goto(Configuration.RetireToBarracksBeforeLoops, Configuration.RetireToInnBeforeLoops);
        _repairManager.Repair();
        if (Trust)
            _trustManager.RegisterTrust(CurrentTerritoryContent);
        else if (Support)
            _dutySupportManager.RegisterDutySupport(CurrentTerritoryContent);
        else if (Squadron)
        {
            _gotoManager.Goto(true, false);
            _squadronManager.RegisterSquadron(CurrentTerritoryContent);
        }
        CurrentLoop = 1;
    }

    public void StartNavigation(bool startFromZero)
    {
        Stage = 1;
        Started = true;
        ExecSkipTalk.IsEnabled = true;
        _chat.ExecuteCommand($"/vbmai on");
        _chat.ExecuteCommand($"/rotation auto");
        Svc.Log.Info("Starting Navigation");
        if (startFromZero)
            Indexer = 0;
    }

    private void OnDeath()
    {
        _dead = true;
        if (VNavmesh_IPCSubscriber.Path_IsRunning())
            MovementHelper.Stop();
        if (_taskManager.IsBusy)
            _taskManager.Abort();
        Stage = 6;
    }

    private unsafe void OnRevive()
    {
        _dead = false;
        GameObject? gameObject = ObjectHelper.GetObjectByName("Shortcut");
        if (gameObject == null || !gameObject.IsTargetable)
            return;

        Stage = 7;
        var oldindex = Indexer;
        Indexer = FindWaypoint();
        //Svc.Log.Info($"We Revived: we died at Index: {oldindex} and now are moving to index: {Indexer} which should be right after previous boss, and the shortcut is {gameObject.Name} at {ObjectHelper.GetDistanceToPlayer(gameObject)} Distance, moving there.");
        _taskManager.Enqueue(() => MovementHelper.PathfindAndMove(gameObject, 0.25f, 2));
        _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno"));
        _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno());
        _taskManager.Enqueue(() => !ObjectHelper.IsValid, 500);
        _taskManager.Enqueue(() => ObjectHelper.IsValid);
        _taskManager.Enqueue(() => { if (Indexer == 0) Indexer = FindWaypoint(); });
        _taskManager.Enqueue(() => Stage = 1);
    }

    private int FindWaypoint()
    {
        if (Indexer == 0)
        {
            Svc.Log.Info($"Finding Closest Waypoint {ListBoxPOSText.Count}");
            float closestWaypointDistance = float.MaxValue;
            int closestWaypointIndex = -1;
            float currentDistance = 0;

            for (int i = 0; i < ListBoxPOSText.Count; i++)
            {
                if (ListBoxPOSText[i].Contains("Boss|") && ListBoxPOSText[i].Replace("Boss|", "").All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                {
                    currentDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[0]), float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[1]), float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[2])));
                    Svc.Log.Info($"cd: {currentDistance}");
                    if (currentDistance < closestWaypointDistance)
                    {
                        closestWaypointDistance = currentDistance;
                        closestWaypointIndex = i;
                    }
                }
                else if (ListBoxPOSText[i].All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                {
                    currentDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0]), float.Parse(ListBoxPOSText[Indexer].Split(',')[1]), float.Parse(ListBoxPOSText[Indexer].Split(',')[2])));
                    Svc.Log.Info($"cd: {currentDistance}");
                    if (currentDistance < closestWaypointDistance)
                    {
                        closestWaypointDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0]), float.Parse(ListBoxPOSText[Indexer].Split(',')[1]), float.Parse(ListBoxPOSText[Indexer].Split(',')[2])));
                        closestWaypointIndex = i;
                    }
                }
            }
            Svc.Log.Info($"Closest Waypoint was {closestWaypointIndex}");
            return closestWaypointIndex + 1;
        }

        if (Indexer != -1)
        {
            Svc.Log.Info("Finding Last Boss");
            for (int i = Indexer; i >= 0; i--)
            {
                if (ListBoxPOSText[i].Contains("Boss|") && i != Indexer)
                    return i + 1;
            }
        }

        return 0;
    }
    public void Framework_Update(IFramework framework)
    {
        if (EzThrottler.Throttle("OverrideAFK") && Started && ObjectHelper.IsValid)
            _overrideAFK.ResetTimers();

        if ((Player = Svc.ClientState.LocalPlayer) == null)
        {
            PlayerPosition = Vector3.Zero;
            return;
        }
        else
            PlayerPosition = Player.Position;

        if (!BossMod_IPCSubscriber.IsEnabled)
            return;

        if (!VNavmesh_IPCSubscriber.IsEnabled)
            return;

        if (!ObjectHelper.IsValid)
            return;

        if (CurrentTerritoryType == 0 && Svc.ClientState.TerritoryType !=0)
            ClientState_TerritoryChanged(Svc.ClientState.TerritoryType);

        if (EzThrottler.Throttle("ClosestInteractableEventObject", 25))
            ClosestInteractableEventObject = ObjectHelper.GetObjectsByObjectKind(ObjectKind.EventObj)?.FirstOrDefault(o => o.IsTargetable);

        if (EzThrottler.Throttle("ClosestTargetableBattleNpc", 25))
            ClosestTargetableBattleNpc = ObjectHelper.GetObjectsByObjectKind(ObjectKind.BattleNpc)?.FirstOrDefault(o => o.IsTargetable);

        if (!_dead && Started && Player.CurrentHp == 0)
            OnDeath();

        if (_dead && Started && Player.CurrentHp > 0)
            OnRevive();

        if (Indexer >= ListBoxPOSText.Count && ListBoxPOSText.Count > 0)
        {
            Stage = 0;
            Indexer = -1;
        }
        switch (Stage)
        {
            //AutoDuty is stopped or has not started
            case 0:
                if (VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0 && Started || Running)
                {
                    Started = false;
                    Running = false;
                    CurrentLoop = 0;
                    MovementHelper.Stop();
                }
                else if (Started)
                    Started = false;
                if (VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
                    VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
                if (_taskManager.IsBusy)
                    _taskManager.Abort();
                if (Indexer > 0 && !MainListClicked)
                    Indexer = -1;
                if (ExecSkipTalk.IsEnabled)
                    ExecSkipTalk.IsEnabled = false;
                Action = "Stopped";
                break;
            //We are started lets call what we need to based off our index
            case 1:
                if (!ObjectHelper.IsReady)
                    return;

                Action = $"Step: {Plugin.ListBoxPOSText[Plugin.Indexer]}";
                //Backwards Compatibility
                if (ListBoxPOSText[Indexer].Contains('|'))
                {
                    _actionPosition = [];
                    _actionParams = [.. ListBoxPOSText[Indexer].Split('|')];
                    _action = (string)_actionParams[0];
                    //Backwards Compatibility
                    if (_actionParams.Count < 3)
                    {
                        if (_action.Equals("Boss"))
                            _actionParams.Add("");
                        else
                        {
                            _actionParams.RemoveAt(0);
                            Stage = 9;
                            return;
                        }
                    }
                    var destinationVector = new Vector3(float.Parse(((string)_actionParams[1]).Split(',')[0]), float.Parse(((string)_actionParams[1]).Split(',')[1]), float.Parse(((string)_actionParams[1]).Split(',')[2]));;
                    _actionPosition.Add(destinationVector);
                    _actionParams.RemoveRange(0, 2);
                    if (destinationVector == Vector3.Zero)
                    {
                        Stage = 9;
                        return;
                    }
                    if (MovementHelper.Pathfind(Player.Position, destinationVector, false))
                        Stage = 2;
                }
                else
                {
                    var destinationVector = new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0]), float.Parse(ListBoxPOSText[Indexer].Split(',')[1]), float.Parse(ListBoxPOSText[Indexer].Split(',')[2]));
                    if (!VNavmesh_IPCSubscriber.Path_GetMovementAllowed())
                        VNavmesh_IPCSubscriber.Path_SetMovementAllowed(true);
                    if (VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
                        VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
                    if (MovementHelper.Pathfind(Player.Position, destinationVector, false))
                        Stage = 2;
                }
                break;
            //Navigation
            case 2:
                if (!ObjectHelper.IsReady)
                    return;
                Action = $"Step: {Plugin.ListBoxPOSText[Plugin.Indexer]}";
                if (MovementHelper.PathfindTask != null && MovementHelper.PathfindTask.IsCompleted)
                {
                    if (MovementHelper.MoveWaypoints.Count == 0)
                        MovementHelper.MoveWaypoints = MovementHelper.PathfindTask.Result;

                    if (MovementHelper.MoveWaypoints.Count > 1 && MovementHelper.MoveWaypoints[MovementHelper.MoveWaypoints.Count - 1] == MovementHelper.MoveWaypoints[MovementHelper.MoveWaypoints.Count - 2])
                        MovementHelper.MoveWaypoints.RemoveAt(MovementHelper.MoveWaypoints.Count - 1);

                    if (MovementHelper.Move(MovementHelper.MoveWaypoints))
                    {
                        MovementHelper.PathfindTask = null;
                    }

                    return;
                }

                if (ObjectHelper.InCombat(Player))
                {
                    MovementHelper.Stop();
                    _chat.ExecuteCommand($"/rotation auto");
                    Stage = 4;
                    return;
                }

                if (VNavmesh_IPCSubscriber.Nav_PathfindInProgress() && MovementHelper.MoveWaypoints.Count > VNavmesh_IPCSubscriber.Path_NumWaypoints() && MovementHelper.MoveWaypoints.Count != 1)
                {
                    MovementHelper.MoveWaypoints.TryDequeue(out _);
                    return;
                }

                if (!VNavmesh_IPCSubscriber.Nav_PathfindInProgress() && MovementHelper.MoveWaypoints.Count == 0 && MovementHelper.PathfindTask == null)
                {
                    if (_action.IsNullOrEmpty())
                    {
                        MovementHelper.MoveWaypoints = [];
                        Stage = 1;
                        Indexer++;
                    }
                    else
                    {
                        MovementHelper.MoveWaypoints = [];
                        Stage = 9;
                    }
                    return;
                }
                if (EzThrottler.Throttle("BossChecker", 25) && _action.Equals("Boss"))
                {
                    BossObject = ObjectHelper.GetBossObject(25);
                    if (BossObject != null)
                    {
                        MovementHelper.Stop();
                        _actionParams = _actionPosition;
                        Stage = 9;
                        return;
                    }
                }
                if (Configuration.LootTreasure && EzThrottler.Throttle("TreasureCofferCheck", 25))
                {
                    treasureCofferGameObject = ObjectHelper.GetObjectByObjectKind(ObjectKind.Treasure);
                    if (treasureCofferGameObject == null || !treasureCofferGameObject.IsTargetable)
                        return;

                    if (ObjectHelper.GetDistanceToPlayer(treasureCofferGameObject) <= 60)
                    {
                        MovementHelper.Stop();
                        VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(treasureCofferGameObject.Position, false);
                        Stage = 8;
                        return;
                    }
                }
                break;
            //Action
            case 3:
                if (!_taskManager.IsBusy)
                {
                    Stage = 1;
                    Indexer++;
                }
                break;
            //InCombat
            case 4:
                if (!ObjectHelper.IsReady)
                    return;
                Action = $"Step: Waiting For Combat";
                if (EzThrottler.Throttle("BossChecker", 25) && _action.Equals("Boss"))
                {
                    BossObject = ObjectHelper.GetBossObject(25);
                    if (BossObject != null)
                    {
                        MovementHelper.Stop();
                        _actionParams = _actionPosition;
                        Stage = 9;
                        return;
                    }
                }

                if (ObjectHelper.InCombat(Player))
                {
                    var range = ObjectHelper.JobRange;
                    if (Svc.Targets.Target != null && ObjectHelper.GetBattleDistanceToPlayer(Svc.Targets.Target) > range && BossMod_IPCSubscriber.ForbiddenZonesCount() == 0 && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                    {
                        VNavmesh_IPCSubscriber.Path_SetTolerance(range);
                        VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(Svc.Targets.Target.Position, false);
                    }
                }
                else if (!ObjectHelper.InCombat(Player) && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                {
                    MovementHelper.Stop();
                    Stage = 1;
                }
                break;
            //Paused
            case 5:
                Action = $"Paused";
                if (VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                    MovementHelper.Stop();
                break;
            //OnDeath
            case 6:
                Action = $"Died";
                //litterally do nothing, until i code auto revive
                break;
            //OnRevive
            case 7:
                Action = $"Revived";
                if (!_taskManager.IsBusy && ObjectHelper.IsValid)
                    Stage = 1;
                break;
            //TreasureCoffer
            case 8:
                if (!ObjectHelper.IsReady) 
                    return;
                Action = $"Step: Looting Treasure";
                if (VNavmesh_IPCSubscriber.Path_IsRunning())
                    return;
                if (EzThrottler.Throttle("TreasureCofferInteract", 250))
                    ObjectHelper.InteractWithObject(treasureCofferGameObject);
                if (treasureCofferGameObject == null || !treasureCofferGameObject.IsTargetable)
                    Stage = 1;
                break;
            //ActionInvoke
            case 9:
                if (!ObjectHelper.IsReady)
                    return;
                if (!_taskManager.IsBusy && !_action.IsNullOrEmpty())
                {
                    if (_action.Equals("Boss"))
                        _actionParams = _actionPosition;
                    _actions.InvokeAction(_action, [.. _actionParams]);
                    _action = "";
                    _actionParams = [];
                    _actionPosition = [];
                }
                if (_taskManager.IsBusy)
                {
                    Stage = 3;
                }
                break;
            case 98:
                if (!_taskManager.IsBusy && ObjectHelper.IsValid)
                    Stage = 0;
                break;
            //Looping
            case 99:
                if (Plugin.Repairing)
                    Action = $"Step: Repairing";
                if (Plugin.Goto)
                    Action = $"Step: Retiring";
                else
                    Action = $"Step: Looping: {CurrentTerritoryContent?.Name} {CurrentLoop} of {LoopTimes}";
                if (!_taskManager.IsBusy && ObjectHelper.IsValid)
                    Stage = 0;
                break;
            default:
                break;
        }
    }

    public void Dispose()
    {
        FileHelper.FileSystemWatcher.Dispose();
        WindowSystem.RemoveAllWindows();
        ECommonsMain.Dispose();
        ExecSkipTalk.Shutdown();
        MainWindow.Dispose();
        OverrideCamera.Dispose();
        _overrideMovement.Dispose();
        Svc.Framework.Update -= Framework_Update;
        Svc.ClientState.TerritoryChanged -= ClientState_TerritoryChanged;
        Svc.Condition.ConditionChange -= Condition_ConditionChange;
        Svc.Commands.RemoveHandler(CommandName);
    }

    private unsafe void OnCommand(string command, string args)
    {
        // in response to the slash command
        switch (args)
        {
            case "config" or "cfg":
                OpenConfigUI(); 
                break;
            default:
                OpenMainUI(); 
                break;
        }
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void OpenConfigUI()
    {
        if (MainWindow != null)
        {
            MainWindow.IsOpen = true;
            MainWindow.OpenConfig();
        }
    }

    public void OpenMainUI()
    {
        if (MainWindow != null)
            MainWindow.IsOpen = true;
    }
}