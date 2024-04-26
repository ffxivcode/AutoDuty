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
using ECommons.GameFunctions;
using TinyIpc.Messaging;

namespace AutoDuty;

// TODO:
// Need to expand AutoRepair to include check for level and stuff to see if you are eligible for self repair. and check for dark matter
// Add auto GC turn in and Auto desynth
// make config saving per character
// drap drop on build is jacked when theres scrolling

// WISHLIST for VBM:
// Generic (Non Module) jousting respects navmesh out of bounds (or dynamically just adds forbiddenzones as Obstacles using Detour) (or at very least, vbm NavigationDecision can use ClosestPointonMesh in it's decision making) (or just spit balling here as no idea if even possible, add Everywhere non tiled as ForbiddenZones /shrug)
// Generic Jousting (for non forbiddenzone AoE, where it just runs to edge of arena and keeps running (happens very often)) is toggleable (so i can turn it the fuck off)

public class AutoDuty : IDalamudPlugin
{
    internal List<string> ListBoxPOSText { get; set; } = [];
    internal int CurrentLoop = 0;
    internal ContentHelper.Content? CurrentTerritoryContent = null;
    internal uint CurrentTerritoryType = 0;
    internal string Name => "AutoDuty";
    internal static AutoDuty Plugin { get; private set; }
    internal bool StopForCombat = true;
    internal DirectoryInfo PathsDirectory;
    internal FileInfo AssemblyFileInfo;
    internal DirectoryInfo? AssemblyDirectoryInfo;
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
    private RegularDutyManager _regularDutyManager;
    private TrustManager _trustManager;
    private SquadronManager _squadronManager;
    private OverrideAFK _overrideAFK;
    private bool _dead = false;
    private GameObject? treasureCofferGameObject = null;
    private string _action = "";
    private float _actionTollerance = 0.25f;
    private List<object> _actionParams = [];
    private List<object> _actionPosition = [];
    private bool _stopped = false;
    private TinyMessageBus _messageBusSend = new("AutoDutyBroadcaster");
    private TinyMessageBus _messageBusReceive = new("AutoDutyBroadcaster");
    private bool _messageSender = false;

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
            AssemblyFileInfo = pluginInterface.AssemblyLocation;
            AssemblyDirectoryInfo = AssemblyFileInfo.Directory;

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
            FileHelper.OnStart();
            FileHelper.Init();
            _chat = new();
            _overrideAFK = new();
            _repairManager = new(_taskManager);
            _gotoManager = new(_taskManager);
            _dutySupportManager = new(_taskManager);
            _regularDutyManager = new(_taskManager);
            _trustManager = new(_taskManager);
            _squadronManager = new(_taskManager);
            _actions = new(this, _chat, _taskManager);
            _messageBusReceive.MessageReceived +=
                (sender, e) => MessageReceived(Encoding.UTF8.GetString((byte[])e.Message));
            BuildTab.ActionsList = _actions.ActionsList;
            MainWindow = new();
            OverrideCamera = new();

            WindowSystem.AddWindow(MainWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "\n/autoduty -> opens main window\n" +
                "/autoduty config or cfg -> opens config window\n" +
                "/autoduty start -> starts autoduty when in a Duty\n" +
                "/autoduty stop -> stops everything\n"
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

    private void MessageReceived(string message)
    {
        if (Svc.ClientState.LocalPlayer is null || message.IsNullOrEmpty() || _messageSender)
            return;

        var messageArray = message.Split('|');

        switch (messageArray[0])
        {
            case "Follow":
                if (messageArray[1] == "OFF")
                    FollowHelper.SetFollow(null);
                var gameObject = ObjectHelper.GetObjectByName(messageArray[1]);
                if (gameObject == null || (FollowHelper.IsFollowing && gameObject.Name.TextValue == messageArray[1]))
                    return;
                FollowHelper.SetFollow(gameObject);
                break;
            case "Action":
                break;
            default:
                break;
        }
    }

    internal void LoadPath()
    {
        try
        {
            if (CurrentTerritoryContent == null || (CurrentTerritoryContent != null && CurrentTerritoryContent.TerritoryType != Svc.ClientState.TerritoryType))
            {
                if (ContentHelper.DictionaryContent.TryGetValue(Svc.ClientState.TerritoryType, out var content))
                    CurrentTerritoryContent = content;
                else
                {
                    InDungeon = false;
                    ListBoxPOSText.Clear();
                    PathFile = "";
                    return;
                }
                    
            }

            InDungeon = true;

            PathFile = $"{Plugin.PathsDirectory.FullName}/({Svc.ClientState.TerritoryType}) {CurrentTerritoryContent?.Name}.json";
   
            ListBoxPOSText.Clear();
            if (!File.Exists(PathFile))
                return;
                
            using StreamReader streamReader = new(PathFile, Encoding.UTF8);
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
        Action = "";
        CurrentTerritoryType = t;
        MainListClicked = false;

        if (t == 0)
            return;

        LoadPath();

        if (!Running || Repairing || Goto || CurrentTerritoryContent == null)
            return;

        if (t != CurrentTerritoryContent.TerritoryType)
        {
            if (CurrentLoop < Configuration.LoopTimes)
            {
                _taskManager.Enqueue(() => Stage = 99, "Loop");
                _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Loop");
                _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "Loop");
                _taskManager.Enqueue(() => _repairManager.Repair(), int.MaxValue, "Loop");
                if (Configuration.Trust)
                    _taskManager.Enqueue(() => _trustManager.RegisterTrust(CurrentTerritoryContent), int.MaxValue, "Loop");
                else if (Configuration.Support)
                    _taskManager.Enqueue(() => _dutySupportManager.RegisterDutySupport(CurrentTerritoryContent), int.MaxValue, "Loop");
                else if (Configuration.Squadron)
                {
                    _gotoManager.Goto(true, false);
                    _taskManager.Enqueue(() => _squadronManager.RegisterSquadron(CurrentTerritoryContent), int.MaxValue, "Loop");
                }
                else if (Configuration.Regular || Configuration.Trial || Configuration.Raid)
                    _regularDutyManager.RegisterRegularDuty(CurrentTerritoryContent);
                _taskManager.Enqueue(() => CurrentLoop++, "Loop");
            }
            else
            {
                Running = false;
                CurrentLoop = 0;
                Stage = 0;
                MainWindow.OpenTab("Main");
            }
        }
    }

    private void Condition_ConditionChange(Dalamud.Game.ClientState.Conditions.ConditionFlag flag, bool value)
    {
        //Svc.Log.Debug($"{flag} : {value}");
        if (Stage != 3 && value && Started && (flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas || flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51 || flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.Jumping61))
        {
            Stage = 1;
            VNavmesh_IPCSubscriber.Path_Stop();
        }
    }

    public void GotoAction(string where)
    {
        if (where.IsNullOrEmpty())
            return;
        MainWindow.OpenTab("Mini");
        Stage = 99;
        Svc.Log.Info($"Going To: {where}");
        Running = true;
        switch (where)
        {
            case "Barracks":
                _gotoManager.Goto(true, false);
                _taskManager.Enqueue(() => Stage = 0, "Goto");
                break;
            case "Inn":
                _gotoManager.Goto(false, true);
                _taskManager.Enqueue(() => Stage = 0, "Goto");
                break;
            case "Repair":
                _repairManager.Repair(true, false);
                _taskManager.Enqueue(() => Stage = 0, "Goto");
                break;
            default:
                MainWindow.ShowPopup("Error", $"{where} is not a valid Goto Destination");
                _taskManager.Enqueue(() => Stage = 0, "Goto");
                break;
        }
    }

    public void Run(uint territoryType = 0, int loops = 0)
    {
        if (territoryType > 0)
        {
            if (ContentHelper.DictionaryContent.TryGetValue(territoryType, out var content))
                CurrentTerritoryContent = content;
            else
            {
                Svc.Log.Error($"({territoryType}) is not in our Dictionary as a compatible Duty");
                return;
            }
        }

        if (loops > 0)
            Configuration.LoopTimes = loops;

        if (CurrentTerritoryContent == null)
            return;

        MainWindow.OpenTab("Mini");
        Stage = 99;
        Running = true;
        Svc.Log.Info($"Running {CurrentTerritoryContent.Name} {Configuration.LoopTimes} Times");
        if (!Configuration.Squadron)
            _gotoManager.Goto(Configuration.RetireToBarracksBeforeLoops, Configuration.RetireToInnBeforeLoops);
        _repairManager.Repair();
        if (Configuration.Trust)
            _trustManager.RegisterTrust(CurrentTerritoryContent);
        else if (Configuration.Support)
            _dutySupportManager.RegisterDutySupport(CurrentTerritoryContent);
        else if (Configuration.Regular || Configuration.Trial || Configuration.Raid)
            _regularDutyManager.RegisterRegularDuty(CurrentTerritoryContent);
        else if (Configuration.Squadron)
        {
            _gotoManager.Goto(true, false);
            _squadronManager.RegisterSquadron(CurrentTerritoryContent);
        }
        CurrentLoop = 1;
    }

    public void StartNavigation(bool startFromZero = true)
    {
        if (ContentHelper.DictionaryContent.TryGetValue(Svc.ClientState.TerritoryType, out var content))
        {
            CurrentTerritoryContent = content;
            PathFile = $"{Plugin.PathsDirectory.FullName}/({Svc.ClientState.TerritoryType}) {content.Name}.json";
            LoadPath();
        }
        else
        {
            CurrentTerritoryContent = null;
            PathFile = "";
            MainWindow.ShowPopup("Error", "Unable to load content for Territory");
            return;
        }
        MainWindow.OpenTab("Mini");
        MainListClicked = false;
        Stage = 1;
        Started = true;
        ExecSkipTalk.IsEnabled = true;
        _chat.ExecuteCommand($"/vbmai on");
        ReflectionHelper.RotationSolver_Reflection.RotationAuto();
        Svc.Log.Info("Starting Navigation");
        if (startFromZero)
            Indexer = 0;
    }

    private void OnDeath()
    {
        _dead = true;
        if (VNavmesh_IPCSubscriber.Path_IsRunning())
            VNavmesh_IPCSubscriber.Path_Stop();
        if (_taskManager.IsBusy)
            _taskManager.Abort();
        Stage = 6;
    }

    private unsafe void OnRevive()
    {
        _dead = false;
        GameObject? gameObject = ObjectHelper.GetObjectByName("Shortcut");
        if (gameObject == null || !gameObject.IsTargetable)
        {
            Stage = 1;
            return;
        }

        Stage = 7;
        var oldindex = Indexer;
        Indexer = FindWaypoint();
        //Svc.Log.Info($"We Revived: we died at Index: {oldindex} and now are moving to index: {Indexer} which should be right after previous boss, and the shortcut is {gameObject.Name} at {ObjectHelper.GetDistanceToPlayer(gameObject)} Distance, moving there.");
        _taskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2));
        _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno"), int.MaxValue);
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
            //Svc.Log.Info($"Finding Closest Waypoint {ListBoxPOSText.Count}");
            float closestWaypointDistance = float.MaxValue;
            int closestWaypointIndex = -1;
            float currentDistance = 0;

            for (int i = 0; i < ListBoxPOSText.Count; i++)
            {
                if (ListBoxPOSText[i].Contains("Boss|") && ListBoxPOSText[i].Replace("Boss|", "").All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                {
                    currentDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[2], System.Globalization.CultureInfo.InvariantCulture)));
                    //Svc.Log.Info($"cd: {currentDistance}");
                    if (currentDistance < closestWaypointDistance)
                    {
                        closestWaypointDistance = currentDistance;
                        closestWaypointIndex = i;
                    }
                }
                else if (ListBoxPOSText[i].All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                {
                    currentDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Split(',')[2], System.Globalization.CultureInfo.InvariantCulture)));
                    //Svc.Log.Info($"cd: {currentDistance}");
                    if (currentDistance < closestWaypointDistance)
                    {
                        closestWaypointDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Split(',')[2], System.Globalization.CultureInfo.InvariantCulture)));
                        closestWaypointIndex = i;
                    }
                }
            }
            //Svc.Log.Info($"Closest Waypoint was {closestWaypointIndex}");
            return closestWaypointIndex + 1;
        }

        if (Indexer != -1)
        {
            //Svc.Log.Info("Finding Last Boss");
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

        if (!ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
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

        if (Indexer >= ListBoxPOSText.Count && ListBoxPOSText.Count > 0 && Started)
        {
            //we finished lets exit the duty
            if (Configuration.AutoExitDuty || Running)
            {
                _actions.ExitDuty("");
                ReflectionHelper.RotationSolver_Reflection.RotationStop();
            }
            if (!Running)
                Stage = 0;
            else
                Stage = 99;
            Indexer = -1;
        }
        if (Stage > 0)
            _stopped = false;
        switch (Stage)
        {
            //AutoDuty is stopped or has not started
            case 0:
                if (EzThrottler.Throttle("Stop", 25) && !_stopped)
                {
                    StopAndResetALL();
                    _stopped = true;
                    Action = "Stopped";
                }
                break;
            //We are started lets call what we need to based off our index
            case 1:
                if (!ObjectHelper.IsReady || !EzThrottler.Check("PathFindFailure") || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"Step: {(ListBoxPOSText.Count >= Indexer ? Plugin.ListBoxPOSText[Indexer] : "")}";
                //Backwards Compatibility
                if (ListBoxPOSText[Indexer].Contains('|'))
                {
                    _actionPosition = [];
                    _actionParams = [.. ListBoxPOSText[Indexer].Split('|')];
                    _action = (string)_actionParams[0];
                    _actionTollerance = _action == "Interactable" ? 2f : 0.25f;

                    if (!VNavmesh_IPCSubscriber.Path_GetMovementAllowed())
                        VNavmesh_IPCSubscriber.Path_SetMovementAllowed(true);
                    if (VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
                        VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);

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

                    if (!((string)_actionParams[1]).All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                    {
                        MainWindow.ShowPopup("Error", $"Error in line {Indexer} of path file\nFormat: Action|123, 0, 321|ActionParams(if needed)");
                        StopAndResetALL();
                        return;
                    }

                    var destinationVector = new Vector3(float.Parse(((string)_actionParams[1]).Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(((string)_actionParams[1]).Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(((string)_actionParams[1]).Split(',')[2], System.Globalization.CultureInfo.InvariantCulture));;
                    _actionPosition.Add(destinationVector);
                    _actionParams.RemoveRange(0, 2);

                    if (destinationVector == Vector3.Zero)
                    {
                        Stage = 9;
                        return;
                    }

                    if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                    {
                        VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destinationVector, false);
                        Stage = 2;
                    }
                }
                //also backwards compat
                else
                {
                    if (!ListBoxPOSText[Indexer].All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                    {
                        MainWindow.ShowPopup("Error", $"Error in line {Indexer} of path file\nFormat: Action|123, 0, 321|ActionParams(if needed)");
                        Running = false;
                        CurrentLoop = 0;
                        MainListClicked = false;
                        Started = false;
                        Stage = 0;
                        return;
                    }

                    var destinationVector = new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Split(',')[2], System.Globalization.CultureInfo.InvariantCulture));

                    if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                    {
                        VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destinationVector, false);
                        Stage = 2;
                    }
                }
                break;
            //Navigation
            case 2:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                if (Configuration.Regular && Svc.Party.PartyId > 0)
                {
                    _messageSender = true;
                    _messageBusSend.PublishAsync(Encoding.UTF8.GetBytes($"Follow|{Player.Name}"));
                }
                
                Action = $"Step: {Plugin.ListBoxPOSText[Indexer]}";
                if (ObjectHelper.InCombat(Player))
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
                    ReflectionHelper.RotationSolver_Reflection.RotationAuto();
                    Stage = 4;
                    break;
                }

                if ((!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0) || (!_action.IsNullOrEmpty() && _actionPosition.Count > 0 && _actionTollerance > 0.25f && ObjectHelper.GetDistanceToPlayer((Vector3)_actionPosition[0]) <= _actionTollerance))
                {
                    if (_action.IsNullOrEmpty())
                    {
                        Stage = 1;
                        Indexer++;
                    }
                    else
                    {
                        VNavmesh_IPCSubscriber.Path_Stop();
                        Stage = 9;
                    }

                    return;
                }

                if (EzThrottler.Throttle("BossChecker", 25) && _action.Equals("Boss") && _actionPosition.Count > 0 && ObjectHelper.GetDistanceToPlayer((Vector3)_actionPosition[0]) < 50)
                {
                    BossObject = ObjectHelper.GetBossObject(25);
                    if (BossObject != null)
                    {
                        VNavmesh_IPCSubscriber.Path_Stop();
                        _actionParams = _actionPosition;
                        Stage = 9;
                        return;
                    }
                }
                if (Configuration.LootTreasure && !Configuration.LootBossTreasureOnly && EzThrottler.Throttle("TreasureCofferCheck", 25))
                {
                    treasureCofferGameObject = ObjectHelper.GetObjectsByObjectKind(ObjectKind.Treasure)?.FirstOrDefault(o => ObjectHelper.GetDistanceToPlayer(o) <= Plugin.Configuration.TreasureCofferScanDistance);
                    if (treasureCofferGameObject == null || !treasureCofferGameObject.IsTargetable)
                        return;
                    VNavmesh_IPCSubscriber.Path_Stop();
                    Stage = 8;
                    return;
                }
                break;
            //Action
            case 3:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                if (!_taskManager.IsBusy)
                {
                    Stage = 1;
                    Indexer++;
                    return;
                }
                break;
            //InCombat
            case 4:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"Step: Waiting For Combat";
                if (EzThrottler.Throttle("BossChecker", 25) && _action.Equals("Boss") && _actionPosition.Count > 0 && ObjectHelper.GetDistanceToPlayer((Vector3)_actionPosition[0]) < 50)
                {
                    BossObject = ObjectHelper.GetBossObject(25);
                    if (BossObject != null)
                    {
                        VNavmesh_IPCSubscriber.Path_Stop();
                        _actionParams = _actionPosition;
                        Stage = 9;
                        return;
                    }
                }

                if (ObjectHelper.InCombat(Player))
                {
                    if (Svc.Targets.Target == null && EzThrottler.Throttle("TargetCheck"))
                    {
                        //find and target closest attackable npc, if we are not targeting
                        var gos = ObjectHelper.GetObjectsByObjectKind(ObjectKind.BattleNpc)?.FirstOrDefault(o => ObjectFunctions.GetNameplateColor(o.Address) is 9 or 11 && ObjectHelper.GetBattleDistanceToPlayer(o) <= 75);

                        if (gos != null)
                            Svc.Targets.Target = gos;
                    }

                    if (Svc.Targets.Target != null && BossMod_IPCSubscriber.ForbiddenZonesCount() == 0 && (ObjectFunctions.GetAttackableEnemyCountAroundPoint(Svc.Targets.Target.Position, 12) > 2 && ObjectHelper.GetBattleDistanceToPlayer(Svc.Targets.Target) > ObjectHelper.AoEJobRange || ObjectHelper.GetBattleDistanceToPlayer(Svc.Targets.Target) > ObjectHelper.JobRange))
                    {
                        if (ObjectFunctions.GetAttackableEnemyCountAroundPoint(Svc.Targets.Target.Position, 12) > 2)
                            VNavmesh_IPCSubscriber.Path_SetTolerance(ObjectHelper.AoEJobRange);
                        else
                            VNavmesh_IPCSubscriber.Path_SetTolerance(ObjectHelper.JobRange);
                        if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                            VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(Svc.Targets.Target.Position, false);
                    }
                    else
                    {
                        VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
                        VNavmesh_IPCSubscriber.Path_Stop();
                    }
                }
                else if (!ObjectHelper.InCombat(Player) && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
                    Stage = 1;
                }
                break;
            //Paused
            case 5:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"Paused";
                if (VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                    VNavmesh_IPCSubscriber.Path_Stop();
                FollowHelper.SetFollow(null);
                break;
            //OnDeath
            case 6:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"Died";
                //litterally do nothing, until i code auto revive
                break;
            //OnRevive
            case 7:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"Revived";
                if (!_taskManager.IsBusy && ObjectHelper.IsValid)
                    Stage = 1;
                break;
            //TreasureCoffer
            case 8:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"Step: Looting Treasure";
                if (ObjectHelper.InCombat(Player))
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
                    ReflectionHelper.RotationSolver_Reflection.RotationAuto();
                    Stage = 4;
                    return;
                }

                if (treasureCofferGameObject == null || !treasureCofferGameObject.IsTargetable)
                {
                    Stage = 1;
                    return;
                }

                if (ObjectHelper.GetDistanceToPlayer(treasureCofferGameObject) > 2 && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && !VNavmesh_IPCSubscriber.Path_IsRunning())
                {
                    VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
                    VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(treasureCofferGameObject.Position, false);
                }

                if (ObjectHelper.GetDistanceToPlayer(treasureCofferGameObject) <= 2)
                {
                    if (VNavmesh_IPCSubscriber.Path_IsRunning())
                        VNavmesh_IPCSubscriber.Path_Stop();

                    if (EzThrottler.Throttle("TreasureCofferInteract", 250))
                        ObjectHelper.InteractWithObject(treasureCofferGameObject);
                }
                
                break;
            //ActionInvoke
            case 9:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                if (!_taskManager.IsBusy && !_action.IsNullOrEmpty())
                {
                    if (_action.Equals("Boss"))
                    {
                        if (Configuration.Regular && Svc.Party.PartyId > 0)
                        {
                            _messageSender = true;
                            _messageBusSend.PublishAsync(Encoding.UTF8.GetBytes($"Follow|OFF"));
                        }
                        _actionParams = _actionPosition;
                    }
                    _actions.InvokeAction(_action, [.. _actionParams]);
                    _action = "";
                    _actionParams = [];
                    _actionPosition = [];
                    _actionTollerance = 0.25f;
                }
                if (_taskManager.IsBusy)
                    Stage = 3;

                break;
            //Looping
            case 99:
                if (!ObjectHelper.IsReady)
                    return;

                if (Plugin.Repairing)
                    Action = $"Step: Repairing";
                else if (!Plugin.Goto)
                    Action = $"Step: Looping: {CurrentTerritoryContent?.Name} {CurrentLoop} of {Configuration.LoopTimes}";
                if (!_taskManager.IsBusy && ObjectHelper.IsValid && Svc.ClientState.TerritoryType == CurrentTerritoryContent?.TerritoryType)
                    Stage = 1;
                break;
            default:
                break;
        }
    }

    internal void StopAndResetALL()
    {
        Running = false;
        CurrentLoop = 0;
        MainListClicked = false;
        Started = false;
        Stage = 0;
        CurrentLoop = 0;
        Goto = false;
        Repairing = false;
        MainWindow.OpenTab("Main");
        if (Indexer > 0 && !MainListClicked)
            Indexer = -1;
        if (VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
            VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
        if (_taskManager.IsBusy)
            _taskManager.Abort();
        if (ExecSkipTalk.IsEnabled)
            ExecSkipTalk.IsEnabled = false;
        VNavmesh_IPCSubscriber.Path_Stop();
        FollowHelper.SetFollow(null);
    }

    public void Dispose()
    {
        FileHelper.FileSystemWatcher.Dispose();
        WindowSystem.RemoveAllWindows();
        ECommonsMain.Dispose();
        ExecSkipTalk.Shutdown();
        MainWindow.Dispose();
        OverrideCamera.Dispose();
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
            case "start":
                StartNavigation();
                break;
            case "stop":
                StopAndResetALL();
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
            MainWindow.OpenTab("Config");
        }
    }

    public void OpenMainUI()
    {
        if (MainWindow != null)
            MainWindow.IsOpen = true;
    }
}