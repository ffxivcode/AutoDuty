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
using ECommons.Automation.LegacyTaskManager;
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
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoDuty;

// TODO:
// Need to expand AutoRepair to include check for level and stuff to see if you are eligible for self repair. and check for dark matter
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
    internal int CurrentPath = -1;
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
    internal IPlayerCharacter? Player = null;
    internal Vector3 PlayerPosition = Vector3.Zero;
    internal IBattleChara? BossObject;
    internal IGameObject? ClosestInteractableEventObject = null;
    internal IGameObject? ClosestTargetableBattleNpc = null;
    internal OverrideCamera OverrideCamera;
    internal MainWindow MainWindow { get; init; }
    internal bool InDungeon = false;
    internal string Action = "";
    internal string PathFile = "";
    internal TaskManager TaskManager;

    private const string CommandName = "/autoduty";
    private DirectoryInfo _configDirectory;
    private ActionsManager _actions;
    private Chat _chat;
    private DutySupportManager _dutySupportManager;
    private RegularDutyManager _regularDutyManager;
    private TrustManager _trustManager;
    private SquadronManager _squadronManager;
    private OverrideAFK _overrideAFK;
    private bool _dead = false;
    private IGameObject? treasureCofferGameObject = null;
    private string _action = "";
    private float _actionTollerance = 0.25f;
    private List<object> _actionParams = [];
    private List<object> _actionPosition = [];
    private bool _stopped = false;
    private TinyMessageBus _messageBusSend = new("AutoDutyBroadcaster");
    private TinyMessageBus _messageBusReceive = new("AutoDutyBroadcaster");
    private bool _messageSender = false;

    public AutoDuty(IDalamudPluginInterface pluginInterface)
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

            TaskManager = new()
            {
                AbortOnTimeout = false,
                TimeoutSilently = true
            };

            ContentHelper.PopulateDuties();
            FileHelper.OnStart();
            FileHelper.Init();
            _chat = new();
            _overrideAFK = new();
            _dutySupportManager = new(TaskManager);
            _regularDutyManager = new(TaskManager);
            _trustManager = new(TaskManager);
            _squadronManager = new(TaskManager);
            _actions = new(this, _chat, TaskManager);
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
                "/autoduty stop -> stops everything\n" +
                "/autoduty pause -> pause route\n" +
                "/autoduty resume -> resume route\n" +
                "/autoduty turnin -> GC Turnin\n"
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

    internal void ExitDuty()
    {
        _actions.ExitDuty("");
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
            ListBoxPOSText.Clear();
            if (!FileHelper.DictionaryPathFiles.TryGetValue(Svc.ClientState.TerritoryType, out List<string>? curPaths))
            {
                this.PathFile = $"{Plugin.PathsDirectory.FullName}{Path.DirectorySeparatorChar}({Svc.ClientState.TerritoryType}) {CurrentTerritoryContent?.Name?.Replace(":", "")}.json";
                return;
            }

            if(Plugin.CurrentPath < 0 && Svc.ClientState.LocalPlayer != null)
                Plugin.CurrentPath = MultiPathHelper.BestPathIndex();
            //Svc.Log.Info("Loading Path: " + Plugin.CurrentPath);
            this.PathFile = $"{Plugin.PathsDirectory.FullName}{Path.DirectorySeparatorChar}{curPaths![Math.Clamp(Plugin.CurrentPath, 0, curPaths.Count - 1)]}";

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
    
    private unsafe void ClientState_TerritoryChanged(ushort t)
    {
        Svc.Log.Debug($"ClientState_TerritoryChanged: t={t}");
       
        CurrentTerritoryType = t;
        MainListClicked = false;

        if (t == 0)
            return;
        this.CurrentPath = -1;
        LoadPath();

        if (!Running || GCTurninHelper.GCTurninRunning || RepairHelper.RepairRunning || GotoHelper.GotoRunning || GotoInnHelper.GotoInnRunning || GotoBarracksHelper.GotoBarracksRunning || CurrentTerritoryContent == null)
        {
            Svc.Log.Debug("We Changed Territories but are doing after loop actions or not running at all");
            return;
        }

        Action = "";

        if (t != CurrentTerritoryContent.TerritoryType)
        {
            if (CurrentLoop < Configuration.LoopTimes)
            {
                TaskManager.Abort();
                TaskManager.Enqueue(() => { Stage = 99; }, "Loop-SetStage=99");
                TaskManager.Enqueue(() => { Started = false; }, "Loop-SetStarted=false");
                TaskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "Loop-WaitPlayerReady");
                if (Configuration.AutoRepair && InventoryHelper.LowestEquippedCondition() <= Configuration.AutoRepairPct)
                {
                    TaskManager.Enqueue(() => RepairHelper.Invoke(), "Loop-AutoRepair");
                    TaskManager.DelayNext("Loop-Delay50", 50);
                    TaskManager.Enqueue(() => !RepairHelper.RepairRunning, int.MaxValue, "Loop-WaitAutoRepairComplete");
                    TaskManager.Enqueue(() => !ObjectHelper.IsOccupied,"Loop-WaitANotIsOccupied");
                }
                if (Configuration.AutoExtract && (QuestManager.IsQuestComplete(66174)))
                {
                    TaskManager.Enqueue(() => ExtractHelper.Invoke(), "Loop-AutoExtract");
                    TaskManager.DelayNext("Loop-Delay50", 50);
                    TaskManager.Enqueue(() => !ExtractHelper.ExtractRunning, int.MaxValue, "Loop-WaitAutoExtractComplete");
                }
                if (Configuration.AutoGCTurnin && UIState.Instance()->PlayerState.GetGrandCompanyRank() > 5)
                {
                    TaskManager.Enqueue(() => GCTurninHelper.Invoke(), "Loop-AutoGCTurnin");
                    TaskManager.DelayNext("Loop-Delay50", 50);
                    TaskManager.Enqueue(() => !GCTurninHelper.GCTurninRunning, int.MaxValue, "Loop-WaitAutoGCTurninComplete");
                }
                if (Configuration.AutoDesynth)
                {
                    TaskManager.Enqueue(() => DesynthHelper.Invoke(), "Loop-AutoDesynth");
                    TaskManager.DelayNext("Loop-Delay50", 50);
                    TaskManager.Enqueue(() => !DesynthHelper.DesynthRunning, int.MaxValue, "Loop-WaitAutoDesynthComplete");
                }
                if (!Configuration.Squadron)
                {
                    if (Configuration.RetireToBarracksBeforeLoops)
                        TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Loop-GotoBarracksInvoke");
                    else if (Configuration.RetireToInnBeforeLoops)
                        TaskManager.Enqueue(() => GotoInnHelper.Invoke(), "Loop-GotoInnInvoke");
                    TaskManager.DelayNext("Loop-Delay50", 50);
                    TaskManager.Enqueue(() => !GotoBarracksHelper.GotoBarracksRunning && !GotoInnHelper.GotoInnRunning, int.MaxValue, "Loop-WaitGotoComplete");
                }
                if (Configuration.Trust)
                    _trustManager.RegisterTrust(CurrentTerritoryContent);
                else if (Configuration.Support)
                    _dutySupportManager.RegisterDutySupport(CurrentTerritoryContent);
                else if (Configuration.Squadron)
                {
                    TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Loop-GotoBarracksInvoke");
                    TaskManager.DelayNext("Loop-Delay50", 50);
                    TaskManager.Enqueue(() => !GotoBarracksHelper.GotoBarracksRunning && !GotoInnHelper.GotoInnRunning, int.MaxValue, "Loop-WaitGotoComplete");
                    _squadronManager.RegisterSquadron(CurrentTerritoryContent);
                }
                else if (Configuration.Regular || Configuration.Trial || Configuration.Raid)
                    _regularDutyManager.RegisterRegularDuty(CurrentTerritoryContent);
                TaskManager.Enqueue(() => CurrentLoop++, "Loop-IncrementCurrentLoop");
                TaskManager.Enqueue(() => !ObjectHelper.IsReady, "Loop-WaitPlayerNotReady");
            }
            else
            {
                if (Configuration.AutoKillClient)
                    _chat.ExecuteCommand($"/xlkill");
                else if (Configuration.AutoLogout)
                {
                    TaskManager.Enqueue(() => ObjectHelper.IsReady);
                    TaskManager.DelayNext(2000);
                    TaskManager.Enqueue(() => _chat.ExecuteCommand($"/logout"));
                    TaskManager.Enqueue(() => AddonHelper.ClickSelectYesno());
                    TaskManager.Enqueue(() => Running = false);
                    TaskManager.Enqueue(() => CurrentLoop = 0);
                    TaskManager.Enqueue(() => Stage = 0);
                    TaskManager.Enqueue(() => MainWindow.OpenTab("Main"));
                }
                else if (Configuration.AutoARMultiEnable) {
                    TaskManager.Enqueue(() => _chat.ExecuteCommand($"/ays multi"));
                    TaskManager.Enqueue(() => Running = false);
                    TaskManager.Enqueue(() => CurrentLoop = 0);
                    TaskManager.Enqueue(() => Stage = 0);
                    TaskManager.Enqueue(() => MainWindow.OpenTab("Main"));
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

    public void Run(uint territoryType = 0, int loops = 0)
    {
        Svc.Log.Debug($"Run: territoryType={territoryType} loops={loops}");
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
        TaskManager.Abort();
        Svc.Log.Info($"Running {CurrentTerritoryContent.DisplayName} {Configuration.LoopTimes} Times");
        if (!InDungeon)
        {
            if (Configuration.AutoRepair && InventoryHelper.LowestEquippedCondition() <= Configuration.AutoRepairPct)
            {
                TaskManager.Enqueue(() => RepairHelper.Invoke(), "Run-AutoRepair");
                TaskManager.DelayNext("Run-Delay50", 50);
                TaskManager.Enqueue(() => !RepairHelper.RepairRunning, int.MaxValue, "Run-WaitAutoRepairComplete");
                TaskManager.Enqueue(() => !ObjectHelper.IsOccupied, "Run-WaitANotIsOccupied");
            }
            if (!Configuration.Squadron)
            {
                if (Configuration.RetireToBarracksBeforeLoops)
                    TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Run-GotoBarracksInvoke");
                else if (Configuration.RetireToInnBeforeLoops)
                    TaskManager.Enqueue(() => GotoInnHelper.Invoke(), "Run-GotoInnInvoke");
                TaskManager.DelayNext("Run-Delay50", 50);
                TaskManager.Enqueue(() => !GotoBarracksHelper.GotoBarracksRunning && !GotoInnHelper.GotoInnRunning, int.MaxValue, "Run-WaitGotoComplete");
            }
            if (Configuration.Trust)
                _trustManager.RegisterTrust(CurrentTerritoryContent);
            else if (Configuration.Support)
                _dutySupportManager.RegisterDutySupport(CurrentTerritoryContent);
            else if (Configuration.Regular || Configuration.Trial || Configuration.Raid)
                _regularDutyManager.RegisterRegularDuty(CurrentTerritoryContent);
            else if (Configuration.Squadron)
            {
                TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Run-GotoBarracksInvoke");
                TaskManager.DelayNext("Run-Delay50", 50);
                TaskManager.Enqueue(() => !GotoBarracksHelper.GotoBarracksRunning && !GotoInnHelper.GotoInnRunning, int.MaxValue, "Run-WaitGotoComplete");
                _squadronManager.RegisterSquadron(CurrentTerritoryContent);
            }
            TaskManager.Enqueue(() => !ObjectHelper.IsValid, "Run");
            TaskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "Run");
        }
        TaskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, int.MaxValue, "Run");
        TaskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "Run");
        TaskManager.Enqueue(() => StartNavigation(true), "Run");
        CurrentLoop = 1;
    }

    public void StartNavigation(bool startFromZero = true)
    {
        Svc.Log.Debug($"StartNavigation: startFromZero={startFromZero}");
        if (ContentHelper.DictionaryContent.TryGetValue(Svc.ClientState.TerritoryType, out var content))
        {
            CurrentTerritoryContent = content;
            PathFile = $"{Plugin.PathsDirectory.FullName}/({Svc.ClientState.TerritoryType}) {content.Name?.Replace(":", "")}.json";
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
        _chat.ExecuteCommand($"/vbm cfg AIConfig Enable true");
        _chat.ExecuteCommand($"/vbmai on");
        _chat.ExecuteCommand($"/vnav aligncamera enable");
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
        if (TaskManager.IsBusy)
            TaskManager.Abort();
        Stage = 6;
    }

    private unsafe void OnRevive()
    {
        _dead = false;
        TaskManager.DelayNext(5000);
        TaskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting);
        IGameObject? gameObject = ObjectHelper.GetObjectByName("Shortcut");
        if (gameObject == null || !gameObject.IsTargetable)
        {
            TaskManager.Enqueue(() => { Stage = 1; } );
            return;
        }

        Stage = 7;
        var oldindex = Indexer;
        Indexer = FindWaypoint();
        //Svc.Log.Info($"We Revived: we died at Index: {oldindex} and now are moving to index: {Indexer} which should be right after previous boss, and the shortcut is {gameObject.Name} at {ObjectHelper.GetDistanceToPlayer(gameObject)} Distance, moving there.");
        TaskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2));
        TaskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno"), int.MaxValue);
        TaskManager.Enqueue(() => AddonHelper.ClickSelectYesno());
        TaskManager.Enqueue(() => !ObjectHelper.IsValid, 500);
        TaskManager.Enqueue(() => ObjectHelper.IsValid);
        TaskManager.Enqueue(() => { if (Indexer == 0) Indexer = FindWaypoint(); });
        TaskManager.Enqueue(() => Stage = 1);
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
                string node = this.ListBoxPOSText[i];

                if (node.Contains("Boss|") && node.Replace("Boss|", "").All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                {
                    currentDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[2], System.Globalization.CultureInfo.InvariantCulture)));
                    //Svc.Log.Info($"cd: {currentDistance}");
                    if (currentDistance < closestWaypointDistance)
                    {
                        closestWaypointDistance = currentDistance;
                        closestWaypointIndex    = i;
                    }
                }
                else if (node.All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
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
                if (ListBoxPOSText[i].Contains("Revival|") && i != Indexer)
                    return i;
            }
        }

        return 0;
    }

    int currentStage = -1;
    public void Framework_Update(IFramework framework)
    {
        //Svc.Log.Info($"{ReflectionHelper.YesAlready_Reflection.GetState}");
        if (currentStage != Stage)
        {
            Svc.Log.Info($"Stage = {Stage}");
            currentStage = Stage;
        }
        
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
                ExitDuty();
                Started = false;
                ReflectionHelper.RotationSolver_Reflection.RotationStop();
                _chat.ExecuteCommand($"/vbmai off");
                _chat.ExecuteCommand($"/vbm cfg AIConfig Enable false");
            }
            if (!Running)
                Stage = 0;

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
                if (ObjectHelper.InCombat(Player) && AutoDuty.Plugin.StopForCombat)
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
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
                /*if (Configuration.LootTreasure && !Configuration.LootBossTreasureOnly && EzThrottler.Throttle("TreasureCofferCheck", 25))
                {
                    treasureCofferGameObject = ObjectHelper.GetObjectsByObjectKind(ObjectKind.Treasure)?.FirstOrDefault(o => ObjectHelper.GetDistanceToPlayer(o) <= Plugin.Configuration.TreasureCofferScanDistance);
                    if (treasureCofferGameObject == null || !treasureCofferGameObject.IsTargetable)
                        return;
                    VNavmesh_IPCSubscriber.Path_Stop();
                    Stage = 8;
                    return;
                }*/
                break;
            //Action
            case 3:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                if (EzThrottler.Throttle("RSCombat"))
                    ReflectionHelper.RotationSolver_Reflection.RotationAuto();

                if (!TaskManager.IsBusy)
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

                if (EzThrottler.Throttle("RSCombat"))
                    ReflectionHelper.RotationSolver_Reflection.RotationAuto();

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

                    if (!IPCSubscriber_Common.IsReady("BossModReborn"))
                    {
                        if (false && Svc.Targets.Target != null && BossMod_IPCSubscriber.ForbiddenZonesCount() == 0 && (ObjectFunctions.GetAttackableEnemyCountAroundPoint(Svc.Targets.Target.Position, 12) > 2 && ObjectHelper.GetBattleDistanceToPlayer(Svc.Targets.Target) > ObjectHelper.AoEJobRange || ObjectHelper.GetBattleDistanceToPlayer(Svc.Targets.Target) > ObjectHelper.JobRange))
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
                    else
                        VNavmesh_IPCSubscriber.Path_Stop();
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
                if (!TaskManager.IsBusy && ObjectHelper.IsValid)
                    Stage = 1;
                break;
            //TreasureCoffer
            case 8:
                //if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"Step: Looting Treasure";
                if (ObjectHelper.InCombat(Player))
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
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

                if (!TaskManager.IsBusy && !_action.IsNullOrEmpty())
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
                if (TaskManager.IsBusy)
                    Stage = 3;

                break;
            //Looping
            case 99:
                if (!ObjectHelper.IsReady)
                    return;

                if (!RepairHelper.RepairRunning && !GotoHelper.GotoRunning && !GotoInnHelper.GotoInnRunning && !GotoBarracksHelper.GotoBarracksRunning && !GCTurninHelper.GCTurninRunning && !ExtractHelper.ExtractRunning && !DesynthHelper.DesynthRunning)
                {
                    Action = $"Step: Looping: {CurrentTerritoryContent?.DisplayName} {CurrentLoop} of {Configuration.LoopTimes}";
                    if (!TaskManager.IsBusy && ObjectHelper.IsValid && Svc.DutyState.IsDutyStarted && Svc.ClientState.TerritoryType == CurrentTerritoryContent?.TerritoryType && VNavmesh_IPCSubscriber.Nav_IsReady())
                        StartNavigation(true);
                }
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
        MainWindow.OpenTab("Main");
        if (Indexer > 0 && !MainListClicked)
            Indexer = -1;
        if (VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
            VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
        if (TaskManager.IsBusy)
            TaskManager.Abort();
        if (ExecSkipTalk.IsEnabled)
            ExecSkipTalk.IsEnabled = false;
        FollowHelper.SetFollow(null);
        if (ExtractHelper.ExtractRunning)
            ExtractHelper.Stop();
        if (GCTurninHelper.GCTurninRunning)
            GCTurninHelper.Stop();
        if (DesynthHelper.DesynthRunning)
            DesynthHelper.Stop();
        if (GotoHelper.GotoRunning)
            GotoHelper.Stop();
        if (GotoInnHelper.GotoInnRunning)
            GotoInnHelper.Stop();
        if (GotoBarracksHelper.GotoBarracksRunning)
            GotoBarracksHelper.Stop();
        if (RepairHelper.RepairRunning)
            RepairHelper.Stop();
        if (VNavmesh_IPCSubscriber.Path_IsRunning())
            VNavmesh_IPCSubscriber.Path_Stop();
        Action = "";
    }

    public void Dispose()
    {
        StopAndResetALL();
        Svc.Framework.Update -= Framework_Update;
        FileHelper.FileSystemWatcher.Dispose();
        WindowSystem.RemoveAllWindows();
        ECommonsMain.Dispose();
        ExecSkipTalk.Shutdown();
        MainWindow.Dispose();
        OverrideCamera.Dispose();
        Svc.ClientState.TerritoryChanged -= ClientState_TerritoryChanged;
        Svc.Condition.ConditionChange -= Condition_ConditionChange;
        Svc.Commands.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command
        switch (args.Split(" ")[0])
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
            case "pause":
                Plugin.Stage = 5;
                break;
            case "resume":
                Plugin.Stage = 1;
                break;
            case "goto":
                var argsss = args.ToUpper().Split(" ");
                switch (argsss[1])
                {
                    case "INN":
                        GotoInnHelper.Invoke(argsss.Length > 2 ? Convert.ToUInt32(argsss[2]) : ObjectHelper.GrandCompany);
                        break;
                    case "BARRACKS":
                        GotoBarracksHelper.Invoke();
                        break;
                    case "GCSUPPLY":
                        GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(ObjectHelper.GrandCompany), [GCTurninHelper.GCSupplyLocation], 0.25f, 3f);
                        break;
                    default:
                        break;
                }
                //GotoAction(args.Replace("goto ", ""));
                break;
            case "turnin":
                if (ObjectHelper.GrandCompanyRank > 5)
                    GCTurninHelper.Invoke();
                else
                    Svc.Log.Info("GC Turnin requires GC Rank 6 or Higher");
                break;
            case "desynth":
                DesynthHelper.Invoke();
                break;
            case "repair":
                if (InventoryHelper.LowestEquippedCondition() < Configuration.AutoRepairPct)
                    RepairHelper.Invoke();
                break;
            case "extract":
                if (QuestManager.IsQuestComplete(66174))
                    ExtractHelper.Invoke();
                else
                    Svc.Log.Info("Materia Extraction requires having completed quest: Forging the Spirit");
                break;
            case "dataid":
                Svc.Log.Info($"{ObjectHelper.GetObjectByName(Svc.Targets.Target?.Name.TextValue ?? "")?.DataId}");
                ImGui.SetClipboardText($"{ObjectHelper.GetObjectByName(Svc.Targets.Target?.Name.TextValue ?? "")?.DataId}");
                break;
            case "moveto":
                var argss = args.Replace("moveto ", "").Split("|");
                var vs = argss[1].Split(", ");
                var v3 = new Vector3(float.Parse(vs[0]), float.Parse(vs[1]), float.Parse(vs[2]));

                GotoHelper.Invoke(Convert.ToUInt32(argss[0]), [v3], argss.Length > 2 ? float.Parse(argss[2]) : 0.25f, argss.Length > 3 ? float.Parse(argss[3]) : 0.25f);
                break;
            case "exitduty":
                ExitDutyHelper.Invoke();
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
