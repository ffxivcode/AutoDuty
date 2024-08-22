global using static AutoDuty.Data.Enum;
global using ECommons.GameHelpers;
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
using System.Text;
using ECommons.GameFunctions;
using TinyIpc.Messaging;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.IoC;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Diagnostics;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Game.ClientState.Conditions;
using static AutoDuty.Windows.ConfigTab;
using AutoDuty.Properties;

namespace AutoDuty;

// TODO:
// Need to expand AutoRepair to include check for level and stuff to see if you are eligible for self repair. and check for dark matter
// make config saving per character
// drap drop on build is jacked when theres scrolling

// WISHLIST for VBM:
// Generic (Non Module) jousting respects navmesh out of bounds (or dynamically just adds forbiddenzones as Obstacles using Detour) (or at very least, vbm NavigationDecision can use ClosestPointonMesh in it's decision making) (or just spit balling here as no idea if even possible, add Everywhere non tiled as ForbiddenZones /shrug)
// Generic Jousting (for non forbiddenzone AoE, where it just runs to edge of arena and keeps running (happens very often)) is toggleable (so i can turn it the fuck off)

public sealed class AutoDuty : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    internal                        List<string>            ListBoxPOSText  { get; set; }         = [];
    internal                        int                     CurrentLoop             = 0;
    internal                        ContentHelper.Content?  CurrentTerritoryContent = null;
    internal                        uint                    CurrentTerritoryType    = 0;
    internal                        int                     CurrentPath             = -1;

    internal bool SupportLeveling = false;
    internal bool SupportLevelingEnabled => Configuration.Support && SupportLeveling;

    internal bool TrustLeveling = false;
    internal bool TrustLevelingEnabled => Configuration.Trust && TrustLeveling;

    internal bool LevelingEnabled => (Configuration.Support || Configuration.Trust) &&
                                     (!Configuration.Support || SupportLevelingEnabled) &&
                                     (!Configuration.Trust || TrustLevelingEnabled);


    internal static string Name => "AutoDuty";
    internal static AutoDuty Plugin { get; private set; }
    internal bool StopForCombat = true;
    internal DirectoryInfo PathsDirectory;
    internal FileInfo AssemblyFileInfo;
    internal DirectoryInfo? AssemblyDirectoryInfo;
    internal Configuration Configuration { get; init; }
    internal WindowSystem WindowSystem = new("AutoDuty");
    internal Stage Stage
    {
        get => _stage;
        set
        {
            _stage = value;
            Svc.Log.Debug($"Stage={EnumString(_stage)}");
            switch (value)
            {
                case Stage.Stopped:
                    StopAndResetALL();
                    break;
                case Stage.Paused:
                    if (VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                        VNavmesh_IPCSubscriber.Path_Stop();
                    FollowHelper.SetFollow(null);
                    break;
                case Stage.Action:
                    ActionInvoke();
                    break;
                case Stage.Dead:
                    OnDeath();
                    break;
                case Stage.Revived:
                    OnRevive();
                    break;
            }
        }
    }
    internal State States = State.None;
    internal int Indexer = -1;
    internal bool MainListClicked = false;
    internal IBattleChara? BossObject;
    internal IGameObject? ClosestInteractableEventObject = null;
    internal IGameObject? ClosestTargetableBattleNpc = null;
    internal OverrideCamera OverrideCamera;
    internal MainWindow MainWindow { get; init; }
    internal Overlay Overlay { get; init; }
    internal bool InDungeon => ContentHelper.DictionaryContent.ContainsKey(Svc.ClientState.TerritoryType);
    internal string Action = "";
    internal string PathFile = "";
    internal TaskManager TaskManager;
    internal Job JobLastKnown;
    internal TrustManager TrustManager;
    internal DutyState DutyState = DutyState.None;
    internal Chat Chat;

    private Stage _stage = Stage.Stopped;
    private const string CommandName = "/autoduty";
    private readonly DirectoryInfo _configDirectory;
    private readonly ActionsManager _actions;
    private readonly DutySupportManager _dutySupportManager;
    private readonly SquadronManager _squadronManager;
    private readonly VariantManager _variantManager;
    private readonly OverrideAFK _overrideAFK;
    private IGameObject? treasureCofferGameObject = null;
    private string _action = "";
    private float _actionTollerance = 0.25f;
    private List<object> _actionParams = [];
    private List<object> _actionPosition = [];
    private readonly TinyMessageBus _messageBusSend = new("AutoDutyBroadcaster");
    private readonly TinyMessageBus _messageBusReceive = new("AutoDutyBroadcaster");
    private bool _messageSender = false;
    private bool _recentlyWatchedCutscene = false;
    private bool _lootTreasure;
    private bool _vnavAlignCameraState = false;

    public AutoDuty()
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(PluginInterface, Plugin, Module.DalamudReflector, Module.ObjectFunctions);

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            _configDirectory = PluginInterface.ConfigDirectory;
            PathsDirectory = new(_configDirectory.FullName + "/paths");
            AssemblyFileInfo = PluginInterface.AssemblyLocation;
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

            TrustManager.PopulateTrustMembers();
            ContentHelper.PopulateDuties();
            RepairNPCHelper.PopulateRepairNPCs();
            FileHelper.OnStart();
            FileHelper.Init();
            Chat = new();
            _overrideAFK = new();
            _dutySupportManager = new(TaskManager);
            TrustManager = new(TaskManager);
            _squadronManager = new(TaskManager);
            _variantManager = new(TaskManager); 
            _actions = new(Plugin, Chat, TaskManager);
            _messageBusReceive.MessageReceived +=
                (sender, e) => MessageReceived(Encoding.UTF8.GetString((byte[])e.Message));
            BuildTab.ActionsList = _actions.ActionsList;
            OverrideCamera = new();
            Overlay = new();
            MainWindow = new();
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(Overlay);

            if (Configuration.ShowOverlay && (!Configuration.HideOverlayWhenStopped || States.HasFlag(State.Looping) || States.HasFlag(State.Navigating)))
                SchedulerHelper.ScheduleAction("ShowOverlay", () => Overlay.IsOpen = true, () => ObjectHelper.IsReady);

            if (Configuration.ShowMainWindowOnStartup)
                SchedulerHelper.ScheduleAction("ShowMainWindowOnStartup", () => OpenMainUI(), () => ObjectHelper.IsReady);

            Svc.Commands.AddHandler("/ad", new CommandInfo(OnCommand) { });
            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "\n/autoduty or /ad -> opens main window\n" +
                "/autoduty or /ad config or cfg -> opens config window / modifies config\n" +
                "/autoduty or /ad start -> starts autoduty when in a Duty\n" +
                "/autoduty or /ad stop -> stops everything\n" +
                "/autoduty or /ad pause -> pause route\n" +
                "/autoduty or /ad resume -> resume route\n" +
                "/autoduty or /ad turnin -> GC Turnin\n" +
                "/autoduty or /ad desynth -> Desynth's your inventory\n" +
                "/autoduty or /ad repair -> Repairs your gear\n" +
                "/autoduty or /ad equiprec-> Equips recommended gear\n" +
                "/autoduty or /ad extract -> Extract's materia from equipment\n" +
                "/autoduty or /ad turnin -> GC Turnin\n" +
                "/autoduty or /ad goto -> goes to\n" +
                "/autoduty or /ad dataid -> Logs and copies your target's dataid to clipboard\n" +
                "/autoduty or /ad exitduty -> exits duty\n" +
                "/autoduty or /ad queue -> queues duty\n" +
                "/autoduty or /ad moveto -> move's to territorytype and location sent\n" +
                "/autoduty or /ad overlay -> opens overlay\n" +
                "/autoduty or /ad overlay lock-> toggles locking the overlay\n" +
                "/autoduty or /ad overlay nobg-> toggles the overlay's background\n" +
                "/autoduty or /ad movetoflag -> moves to the flag map marker\n" +
                "/autoduty or /ad run -> starts auto duty in territory type specified\n" +
                "/autoduty or /ad tt -> logs and copies to clipboard the Territory Type number for duty specified\n"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += OpenMainUI;

            Svc.Framework.Update += Framework_Update;
            Svc.Framework.Update += SchedulerHelper.ScheduleInvoker;
            Svc.ClientState.TerritoryChanged += ClientState_TerritoryChanged;
            Svc.Condition.ConditionChange += Condition_ConditionChange;
            Svc.DutyState.DutyStarted += DutyState_DutyStarted;
            Svc.DutyState.DutyWiped += DutyState_DutyWiped;
            Svc.DutyState.DutyRecommenced += DutyState_DutyRecommenced;
            Svc.DutyState.DutyCompleted += DutyState_DutyCompleted;

            _vnavAlignCameraState = VNavmesh_IPCSubscriber.Path_GetAlignCamera();
        }
        catch (Exception e) { Svc.Log.Info($"Failed loading plugin\n{e}");
        }
    }

    private void DutyState_DutyStarted(object? sender, ushort e) => DutyState = DutyState.DutyStarted;
    private void DutyState_DutyWiped(object? sender, ushort e) => DutyState = DutyState.DutyWiped;
    private void DutyState_DutyRecommenced(object? sender, ushort e) => DutyState = DutyState.DutyRecommenced;
    private void DutyState_DutyCompleted(object? sender, ushort e) => DutyState = DutyState.DutyComplete;

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

    internal void ExitDuty() => _actions.ExitDuty("");

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
                    ListBoxPOSText.Clear();
                    PathFile = "";
                    return;
                }
            }
            
            ListBoxPOSText.Clear();
            if (!ContentPathsManager.DictionaryPaths.TryGetValue(Svc.ClientState.TerritoryType, out ContentPathsManager.ContentPathContainer? container))
            {
                PathFile = $"{PathsDirectory.FullName}{Path.DirectorySeparatorChar}({Svc.ClientState.TerritoryType}) {CurrentTerritoryContent?.EnglishName?.Replace(":", "")}.json";
                return;
            }
            
            ContentPathsManager.DutyPath? path = CurrentPath < 0 && Player.Available ?
                                                     container.SelectPath(out CurrentPath) :
                                                     container.Paths[CurrentPath > -1 ? CurrentPath : 0];

            PathFile       = path?.FilePath ?? "";
            ListBoxPOSText = [.. path?.Actions];

            //Svc.Log.Info($"Loading Path: {CurrentPath} {ListBoxPOSText.Count}");
        }
        catch (Exception e)
        {
            Svc.Log.Error(e.ToString());
            //throw;
        }
    }

    private unsafe bool StopLoop => CurrentTerritoryContent == null ||
                                    (Configuration.StopLevel && ECommons.GameHelpers.Player.Level >= Configuration.StopLevelInt) || 
                                    (Configuration.StopNoRestedXP && AgentHUD.Instance()->ExpRestedExperience == 0) || 
                                    (Configuration.StopItemQty && Configuration.StopItemQtyItemDictionary.Any(x => InventoryManager.Instance()->GetInventoryItemCount(x.Key) >= x.Value.Value));

    private void ClientState_TerritoryChanged(ushort t)
    {
        if (Stage == Stage.Stopped) return;

        Svc.Log.Debug($"ClientState_TerritoryChanged: t={t}");
       
        CurrentTerritoryType = t;
        MainListClicked = false;

        if (t == 0)
            return;
        CurrentPath = -1;

        LoadPath();

        if (!States.HasFlag(State.Looping) || GCTurninHelper.GCTurninRunning || RepairHelper.RepairRunning || GotoHelper.GotoRunning || GotoInnHelper.GotoInnRunning || GotoBarracksHelper.GotoBarracksRunning || GotoHousingHelper.GotoHousingRunning || CurrentTerritoryContent == null)
        {
            Svc.Log.Debug("We Changed Territories but are doing after loop actions or not running at all or in a Territory not supported by AutoDuty");
            return;
        }

        if (Configuration.ShowOverlay && Configuration.HideOverlayWhenStopped && !States.HasFlag(State.Looping))
        {
            Overlay.IsOpen = false;
            MainWindow.IsOpen = true;
        }

        Action = "";

        if (t != CurrentTerritoryContent.TerritoryType)
        {
            if (CurrentLoop < Configuration.LoopTimes)
            {
                TaskManager.Abort();
                TaskManager.Enqueue(() => { Stage   = Stage.Looping; },    "Loop-SetStage=99");
                TaskManager.Enqueue(() => { States &= ~State.Navigating; }, "Loop-RemoveNavigationState");
                TaskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "Loop-WaitPlayerReady");
                TaskManager.Enqueue(() => { Action = $"Waiting {Configuration.WaitTimeBeforeAfterLoopActions}s"; }, "Loop-WaitTimeBeforeAfterLoopActionsActionSet");
                TaskManager.DelayNext("Loop-WaitTimeBeforeAfterLoopActions", Configuration.WaitTimeBeforeAfterLoopActions * 1000);
                TaskManager.Enqueue(() => { Action = $"After Loop Actions"; }, "Loop-AfterLoopActionsSetAction");
                if (Configuration.AutoBoiledEgg)
                {
                    TaskManager.Enqueue(() => { InventoryHelper.UseItemIfAvailable(4650);/*&& !PlayerHelper.HasStatus(48)*/}, "Loop-AutoBoiledEgg");
                    TaskManager.DelayNext("Loop-Delay2000", 2000);
                    TaskManager.Enqueue(() => ObjectHelper.IsReady);
                }

                if (Configuration.AutoEquipRecommendedGear)
                {
                    TaskManager.Enqueue(() => AutoEquipHelper.Invoke(), "Loop-AutoEquip");
                    TaskManager.DelayNext("Loop-Delay50", 50);
                    TaskManager.Enqueue(() => !AutoEquipHelper.AutoEquipRunning, int.MaxValue, "Loop-WaitAutoEquipComplete");
                    TaskManager.Enqueue(() => !ObjectHelper.IsOccupied, "Loop-WaitANotIsOccupied");
                }

                if (TrustLevelingEnabled)
                {
                    TrustManager.ClearCachedLevels(CurrentTerritoryContent);
                    TrustManager.GetLevels(CurrentTerritoryContent);
                    TaskManager.DelayNext(50);
                    TaskManager.Enqueue(() => TrustManager.GetLevelsCheck(), "Loop-RecheckingTrustLevels");
                }

                TaskManager.Enqueue(() => 
                {
                    if (StopLoop)
                    {
                        Svc.Log.Info($"Loop Stop Condition Encountered, Stopping Loop");
                        LoopsCompleteActions();
                    }
                    else
                        LoopTasks();
                },"Loop-CheckStopLoop");
            }
            else
                LoopsCompleteActions();
        }
    }
    
    private void Condition_ConditionChange(ConditionFlag flag, bool value)
    {
        if (Stage == Stage.Stopped) return;
        //Svc.Log.Debug($"{flag} : {value}");
        if (Stage != Stage.Dead && Stage != Stage.Revived && !_recentlyWatchedCutscene && !Conditions.IsWatchingCutscene && flag != ConditionFlag.WatchingCutscene && flag != ConditionFlag.WatchingCutscene78 && flag != ConditionFlag.OccupiedInCutSceneEvent && Stage != Stage.Action && value && States.HasFlag(State.Navigating) && (flag == ConditionFlag.BetweenAreas || flag == ConditionFlag.BetweenAreas51 || flag == ConditionFlag.Jumping61))
        {
            Indexer++;
            Stage = Stage.Reading_Path;
            VNavmesh_IPCSubscriber.Path_Stop();
        }
        if (Conditions.IsWatchingCutscene || flag == ConditionFlag.WatchingCutscene || flag == ConditionFlag.WatchingCutscene78 || flag == ConditionFlag.OccupiedInCutSceneEvent)
        {
            _recentlyWatchedCutscene = true;
            SchedulerHelper.ScheduleAction("RecentlyWatchedCutsceneTimer", () => _recentlyWatchedCutscene = false, 5000);
        }
    }

    private unsafe void LoopTasks()
    {
        if (CurrentTerritoryContent == null) return;

        if (Configuration.EnableAutoRetainer && AutoRetainer_IPCSubscriber.IsEnabled && AutoRetainer_IPCSubscriber.AreAnyRetainersAvailableForCurrentChara())
        {
            TaskManager.Enqueue(() => AutoRetainerHelper.Invoke(), "Loop-AutoRetainer");
            TaskManager.DelayNext("Loop-Delay50", 50);
            TaskManager.Enqueue(() => !AutoRetainerHelper.AutoRetainerRunning, int.MaxValue, "Loop-WaitAutoRetainerComplete");
        }

        if (Configuration.AM)
        {
            TaskManager.Enqueue(() => AMHelper.Invoke(), "Loop-AM");
            TaskManager.DelayNext("Loop-Delay50", 50);
            TaskManager.Enqueue(() => !AMHelper.AMRunning, int.MaxValue, "Loop-WaitAMComplete");
        }

        if (Configuration.AutoRepair && InventoryHelper.CanRepair())
        {
            TaskManager.Enqueue(() => RepairHelper.Invoke(), "Loop-AutoRepair");
            TaskManager.DelayNext("Loop-Delay50", 50);
            TaskManager.Enqueue(() => !RepairHelper.RepairRunning, int.MaxValue, "Loop-WaitAutoRepairComplete");
            TaskManager.Enqueue(() => !ObjectHelper.IsOccupied, "Loop-WaitANotIsOccupied");
        }

        if (Configuration.AutoExtract && (QuestManager.IsQuestComplete(66174)))
        {
            TaskManager.Enqueue(() => ExtractHelper.Invoke(), "Loop-AutoExtract");
            TaskManager.DelayNext("Loop-Delay50", 50);
            TaskManager.Enqueue(() => !ExtractHelper.ExtractRunning, int.MaxValue, "Loop-WaitAutoExtractComplete");
        }

        if (Configuration.AutoGCTurnin && (!Configuration.AutoGCTurninSlotsLeftBool || InventoryManager.Instance()->GetEmptySlotsInBag() <= Configuration.AutoGCTurninSlotsLeft) && ObjectHelper.GrandCompanyRank > 5)
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
        
        if (!Configuration.Squadron && Configuration.RetireMode)
        {
            if (Configuration.RetireLocationEnum == RetireLocation.GC_Barracks)
                TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Loop-GotoBarracksInvoke");
            else if (Configuration.RetireLocationEnum == RetireLocation.Inn)
                TaskManager.Enqueue(() => GotoInnHelper.Invoke(), "Loop-GotoInnInvoke");
            else
            {
                Svc.Log.Info($"{(Housing)Configuration.RetireLocationEnum} {Configuration.RetireLocationEnum}");
                TaskManager.Enqueue(() => GotoHousingHelper.Invoke((Housing)Configuration.RetireLocationEnum), "Loop-GotoHousingInvoke");
            }
            TaskManager.DelayNext("Loop-Delay50", 50);
            TaskManager.Enqueue(() => !GotoHousingHelper.GotoHousingRunning && !GotoBarracksHelper.GotoBarracksRunning && !GotoInnHelper.GotoInnRunning, int.MaxValue, "Loop-WaitGotoComplete");
        }

        if (LevelingEnabled)
        {
            Svc.Log.Info("Leveling Enabled");
            ContentHelper.Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(Configuration.Trust);
            if (duty != null)
            {
                Svc.Log.Info("Next Leveling Duty: " + duty.Name);
                CurrentTerritoryContent = duty;
                ContentPathsManager.DictionaryPaths[duty.TerritoryType].SelectPath(out CurrentPath);
            }
            else
            {
                CurrentLoop = Configuration.LoopTimes;
                LoopsCompleteActions();
                return;
            }
        }

        if (Configuration.Trust)
            TrustManager.RegisterTrust(CurrentTerritoryContent);

        else if (Configuration.Support)
            _dutySupportManager.RegisterDutySupport(CurrentTerritoryContent);

        else if (Configuration.Variant)
            _variantManager.RegisterVariantDuty(CurrentTerritoryContent);

        else if (Configuration.Squadron)
        {
            TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Loop-GotoBarracksInvoke");
            TaskManager.DelayNext("Loop-Delay50", 50);
            TaskManager.Enqueue(() => !GotoBarracksHelper.GotoBarracksRunning && !GotoInnHelper.GotoInnRunning, int.MaxValue, "Loop-WaitGotoComplete");
            _squadronManager.RegisterSquadron(CurrentTerritoryContent);
        }
        else if (Configuration.Regular || Configuration.Trial || Configuration.Raid)
        {
            TaskManager.Enqueue(() => QueueHelper.Invoke(CurrentTerritoryContent), "Loop-Queue");
            TaskManager.DelayNext("Loop-Delay50", 50);
            TaskManager.Enqueue(() => !QueueHelper.QueueRunning, int.MaxValue, "Loop-WaitQueueComplete");
        }

        TaskManager.Enqueue(() => CurrentLoop++, "Loop-IncrementCurrentLoop");
        TaskManager.Enqueue(() => { Action = $"Looping: {CurrentTerritoryContent.Name} {CurrentLoop} of {Configuration.LoopTimes}"; }, "Loop-SetAction");
        TaskManager.Enqueue(() => Svc.ClientState.TerritoryType == CurrentTerritoryContent.TerritoryType, int.MaxValue, "Loop-WaitCorrectTerritory");
        TaskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "Loop-WaitPlayerValid");
        TaskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, int.MaxValue, "Loop-WaitDutyStarted");
        TaskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "Loop-WaitNavReady");
        TaskManager.Enqueue(() => StartNavigation(true), "Loop-StartNavigation");
    }

    private void LoopsCompleteActions()
    {
        if (!_vnavAlignCameraState && VNavmesh_IPCSubscriber.Path_GetAlignCamera())
            VNavmesh_IPCSubscriber.Path_SetAlignCamera(false);
        
        if (Configuration.TerminationMethodEnum == TerminationMode.Kill_PC)
        {
            if (!Configuration.TerminationKeepActive)
            {
                Configuration.TerminationMethodEnum = TerminationMode.Do_Nothing;
                Configuration.Save();
            }

            if (OperatingSystem.IsWindows())
            {
                ProcessStartInfo startinfo = new("shutdown.exe", "-s -t 20");
                Process.Start(startinfo);
            } 
            else if (OperatingSystem.IsLinux())
            {
                //Educated guess
                ProcessStartInfo startinfo = new("shutdown", "-t 20");
                Process.Start(startinfo);
            } 
            else if (OperatingSystem.IsMacOS())
            {
                //hell if I know
            }
            Chat.ExecuteCommand($"/xlkill");
        }
        else if (Configuration.TerminationMethodEnum == TerminationMode.Kill_Client)
        {
            if (!Configuration.TerminationKeepActive)
            {
                Configuration.TerminationMethodEnum = TerminationMode.Do_Nothing;
                Configuration.Save();
            }

            Chat.ExecuteCommand($"/xlkill");
        }
        else if (Configuration.TerminationMethodEnum == TerminationMode.Logout)
        {
            if (!Configuration.TerminationKeepActive)
            {
                Configuration.TerminationMethodEnum = TerminationMode.Do_Nothing;
                Configuration.Save();
            }

            TaskManager.Enqueue(() => ObjectHelper.IsReady);
            TaskManager.DelayNext(2000);
            TaskManager.Enqueue(() => Chat.ExecuteCommand($"/logout"));
            TaskManager.Enqueue(() => AddonHelper.ClickSelectYesno());
            TaskManager.Enqueue(() => States &= ~State.Looping);
            TaskManager.Enqueue(() => CurrentLoop = 0);
            TaskManager.Enqueue(() => Stage = Stage.Stopped);
        }
        else if (Configuration.TerminationMethodEnum == TerminationMode.Start_AR_Multi_Mode)
        {
            TaskManager.Enqueue(() => Chat.ExecuteCommand($"/ays multi"));
            TaskManager.Enqueue(() => States &= ~State.Looping);
            TaskManager.Enqueue(() => CurrentLoop = 0);
            TaskManager.Enqueue(() => Stage = Stage.Stopped);
        }
        else
        {
            States &= ~State.Looping;
            CurrentLoop = 0;
            Stage = Stage.Stopped;
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

        //MainWindow.OpenTab("Mini");
        if (Configuration.ShowOverlay)
        {
            //MainWindow.IsOpen = false;
            Overlay.IsOpen = true;
        }
        Stage = Stage.Looping;
        States |= State.Looping;
        TaskManager.Abort();
        Svc.Log.Info($"Running {CurrentTerritoryContent.Name} {Configuration.LoopTimes} Times");
        if (!InDungeon)
        {
            if (Configuration.AutoBoiledEgg /*&& !PlayerHelper.HasStatus(48)*/)
            {
                TaskManager.Enqueue(() => InventoryHelper.UseItemIfAvailable(4650), "Run-AutoBoiledEgg");
                TaskManager.DelayNext("Run-AutoBoiledEggDelay50", 50);
                TaskManager.Enqueue(() => ObjectHelper.IsReady, "Run-WaitAutoBoiledEggIsReady");
            }
            if (Configuration.AutoRepair && InventoryHelper.CanRepair())
            {
                TaskManager.Enqueue(() => RepairHelper.Invoke(), "Run-AutoRepair");
                TaskManager.DelayNext("Run-AutoRepairDelay50", 50);
                TaskManager.Enqueue(() => !RepairHelper.RepairRunning, int.MaxValue, "Run-WaitAutoRepairComplete");
                TaskManager.Enqueue(() => !ObjectHelper.IsOccupied, "Run-WaitAutoRepairNotIsOccupied");
            }
            if (!Configuration.Squadron && Configuration.RetireMode)
            {
                if (Configuration.RetireLocationEnum == RetireLocation.GC_Barracks)
                    TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Run-GotoBarracksInvoke");
                else if (Configuration.RetireLocationEnum == RetireLocation.Inn)
                    TaskManager.Enqueue(() => GotoInnHelper.Invoke(), "Run-GotoInnInvoke");
                else
                    TaskManager.Enqueue(() => GotoHousingHelper.Invoke((Housing)Configuration.RetireLocationEnum), "Run-GotoHousingInvoke");
                TaskManager.DelayNext("Run-RetireModeDelay50", 50);
                TaskManager.Enqueue(() => !GotoHousingHelper.GotoHousingRunning && !GotoBarracksHelper.GotoBarracksRunning && !GotoInnHelper.GotoInnRunning, int.MaxValue, "Run-WaitGotoComplete");
            }
            if (Configuration.Trust)
                TrustManager.RegisterTrust(CurrentTerritoryContent);
            else if (Configuration.Support)
                _dutySupportManager.RegisterDutySupport(CurrentTerritoryContent);
            else if (Configuration.Variant)
                _variantManager.RegisterVariantDuty(CurrentTerritoryContent);
            else if (Configuration.Regular || Configuration.Trial || Configuration.Raid)
            {
                TaskManager.Enqueue(() => QueueHelper.Invoke(CurrentTerritoryContent), "Run-Queue");
                TaskManager.DelayNext("Run-QueueDelay50", 50);
                TaskManager.Enqueue(() => !QueueHelper.QueueRunning, int.MaxValue, "Run-WaitQueueComplete");
            }
            else if (Configuration.Squadron)
            {
                TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Run-GotoBarracksInvoke");
                TaskManager.DelayNext("Run-GotoBarracksDelay50", 50);
                TaskManager.Enqueue(() => !GotoBarracksHelper.GotoBarracksRunning && !GotoInnHelper.GotoInnRunning, int.MaxValue, "Run-WaitGotoComplete");
                _squadronManager.RegisterSquadron(CurrentTerritoryContent);
            }
            TaskManager.Enqueue(() => !ObjectHelper.IsValid, "Run-WaitNotValid");
            TaskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "Run-WaitValid");
        }
        TaskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, "Run-WaitDutyStarted");
        TaskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "Run-WaitNavIsReady");
        TaskManager.Enqueue(() => StartNavigation(true), "Run-StartNavigation");
        CurrentLoop = 1;
    }

    public void StartNavigation(bool startFromZero = true)
    {
        Svc.Log.Debug($"StartNavigation: startFromZero={startFromZero}");
        if (ContentHelper.DictionaryContent.TryGetValue(Svc.ClientState.TerritoryType, out var content))
        {
            CurrentTerritoryContent = content;
            PathFile = $"{Plugin.PathsDirectory.FullName}/({Svc.ClientState.TerritoryType}) {content.EnglishName?.Replace(":", "")}.json";
            LoadPath();
        }
        else
        {
            CurrentTerritoryContent = null;
            PathFile = "";
            MainWindow.ShowPopup("Error", "Unable to load content for Territory");
            return;
        }
        //MainWindow.OpenTab("Mini");
        if (Configuration.ShowOverlay)
        {
            //MainWindow.IsOpen = false;
            Overlay.IsOpen = true;
        }
        MainListClicked = false;
        Stage = Stage.Reading_Path;
        States |= State.Navigating;
        StopForCombat = true;
        if (Configuration.AutoManageVnavAlignCamera && !VNavmesh_IPCSubscriber.Path_GetAlignCamera())
            VNavmesh_IPCSubscriber.Path_SetAlignCamera(true);
        Chat.ExecuteCommand($"/vbm cfg AIConfig Enable true");
        //if (IPCSubscriber_Common.IsReady("BossModReborn"))  -- Remove after veyn merges
            Chat.ExecuteCommand($"/vbmai on");
        if (Configuration.AutoManageBossModAISettings)
            SetBMSettings();
        if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin)
            SetRotationPluginSettings(true);
        if (Configuration.LootTreasure)
        {
            if (PandorasBox_IPCSubscriber.IsEnabled)
                PandorasBox_IPCSubscriber.SetFeatureEnabled("Automatically Open Chests", Configuration.LootMethodEnum == LootMethod.Pandora || Configuration.LootMethodEnum == LootMethod.All);
            if (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
                ReflectionHelper.RotationSolver_Reflection.SetConfigValue("OpenCoffers", $"{Configuration.LootMethodEnum == LootMethod.RotationSolver || Configuration.LootMethodEnum == LootMethod.All}");
            _lootTreasure = Configuration.LootMethodEnum == LootMethod.AutoDuty || Configuration.LootMethodEnum == LootMethod.All;
        }
        else
        {
            if (PandorasBox_IPCSubscriber.IsEnabled)
                PandorasBox_IPCSubscriber.SetFeatureEnabled("Automatically Open Chests", false);
                ReflectionHelper.RotationSolver_Reflection.SetConfigValue("OpenCoffers", "false");
            _lootTreasure = false;
        }
        Svc.Log.Info("Starting Navigation");
        if (startFromZero)
            Indexer = 0;
    }

    private void DoneNavigating()
    {
        //we finished lets exit the duty or stop
        if (Configuration.AutoExitDuty || CurrentLoop < Configuration.LoopTimes)
        {
            if (!ExitDutyHelper.ExitDutyRunning)
                ExitDuty();
            if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin)
                SetRotationPluginSettings(false);
            Chat.ExecuteCommand($"/vbmai off");
            Chat.ExecuteCommand($"/vbm cfg AIConfig Enable false");
            States &= ~State.Navigating;
        }
        else
            Stage = Stage.Stopped;
    }

    private void SetRotationPluginSettings(bool on)
    {
        if (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
        {
            if (on)
                ReflectionHelper.RotationSolver_Reflection.RotationAuto();
            else
                ReflectionHelper.RotationSolver_Reflection.RotationStop();
        }
        else if (BossMod_IPCSubscriber.IsEnabled)
        {
            if (on)
            {
                //check if our preset does not exist
                Svc.Log.Info(BossMod_IPCSubscriber.Presets_Get("AutoDuty") ?? "null");
                if (BossMod_IPCSubscriber.Presets_Get("AutoDuty") == null)
                {
                    //load it
                    Svc.Log.Debug($"AutoDuty Preset Loaded: {BossMod_IPCSubscriber.Presets_Create(Resources.AutoDutyPreset, true)}");
                }

                //set it as the active preset for both
                if (BossMod_IPCSubscriber.Presets_GetActive() != "AutoDuty")
                    BossMod_IPCSubscriber.Presets_SetActive("AutoDuty");

                //if (BossMod_IPCSubscriber.AI_GetPreset() != "AutoDuty")  -- Remove once veyn Merges
                    BossMod_IPCSubscriber.AI_SetPreset("AutoDuty");
            }
            else
            {
                //set disabled as preset
                //if (!BossMod_IPCSubscriber.Presets_GetForceDisabled())  -- Remove once veyn Merges
                //  BossMod_IPCSubscriber.Presets_SetForceDisabled();  -- Remove once veyn Merges
            }
        }
    }

    internal void SetBMSettings()
    {
        BMRoleChecks();
        var bmr = IPCSubscriber_Common.IsReady("BossModReborn");
        var rsr = ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled;
        
        Chat.ExecuteCommand($"/vbm cfg AIConfig ForbidActions {/*(rsr ? "true" : */"false"/*)*/}");//forbidActions currently disables followTarget in vbm.
        Chat.ExecuteCommand($"/vbm cfg AIConfig ForbidMovement false");
        Chat.ExecuteCommand($"/vbm cfg AIConfig FollowDuringCombat {Configuration.FollowDuringCombat}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig FollowDuringActiveBossModule {Configuration.FollowDuringActiveBossModule}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig FollowOutOfCombat {Configuration.FollowOutOfCombat}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig FollowTarget {Configuration.FollowTarget}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig MaxDistanceToTarget {Configuration.MaxDistanceToTargetFloat}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig MaxDistanceToSlot {Configuration.MaxDistanceToSlotFloat}");
        Chat.ExecuteCommand($"/vbmai follow {(Configuration.FollowSelf ? Player.Name : ((Configuration.FollowRole && !FollowName.IsNullOrEmpty()) ? FollowName : (Configuration.FollowSlot ? $"Slot{Configuration.FollowSlotInt}" : Player.Name)))}");

        if (!bmr)
        {
            Chat.ExecuteCommand($"/vbm cfg AIConfig OverridePositional true");
            Chat.ExecuteCommand($"/vbm cfg AIConfig OverrideRange true");
        }
        Chat.ExecuteCommand($"/vbm cfg AIConfig DesiredPositional {Configuration.PositionalEnum}");
    }

    internal void BMRoleChecks()
    {
        //RoleBased Positional
        if (ObjectHelper.IsValid && Configuration.PositionalRoleBased && Configuration.PositionalEnum != (ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Melee ? Positional.Rear : Positional.Any))
        {
            Configuration.PositionalEnum = (ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Melee ? Positional.Rear : Positional.Any);
            Configuration.Save();
        }

        //RoleBased MaxDistanceToTarget
        if (ObjectHelper.IsValid && Configuration.MaxDistanceToTargetRoleBased && Configuration.MaxDistanceToTargetFloat != (ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Melee || ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Tank ? 2.6f : 10))
        {
            Configuration.MaxDistanceToTargetFloat = (ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Melee || ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Tank ? 2.6f : 10);
            Configuration.Save();
        }

        //RoleBased MaxDistanceToTargetAoE
        if (ObjectHelper.IsValid && Configuration.MaxDistanceToTargetRoleBased && Configuration.MaxDistanceToTargetAoEFloat != (ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Melee || ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Tank ? 2.6f : 10))
        {
            Configuration.MaxDistanceToTargetAoEFloat = (ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Melee || ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Tank || Player.Object.ClassJob.GameData?.JobIndex==18 ? 2.6f : 10);
            Configuration.Save();
        }

        //FollowRole
        if (ObjectHelper.IsValid && Configuration.FollowRole && ConfigTab.FollowName != ObjectHelper.GetPartyMemberFromRole($"{Configuration.FollowRoleEnum}")?.Name.ExtractText())
            ConfigTab.FollowName = ObjectHelper.GetPartyMemberFromRole($"{Configuration.FollowRoleEnum}")?.Name.ExtractText() ?? "";
    }

    private unsafe void OnDeath()
    {
        StopForCombat = true;
        if (VNavmesh_IPCSubscriber.Path_IsRunning())
            VNavmesh_IPCSubscriber.Path_Stop();
        if (TaskManager.IsBusy)
            TaskManager.Abort();
        if (Configuration.Regular || Configuration.Trial || Configuration.Raid)
        {
            TaskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno));
            TaskManager.Enqueue(() => AddonHelper.ClickSelectYesno());
        }
    }

    private unsafe void OnRevive()
    {
        TaskManager.DelayNext(5000);
        TaskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting);
        IGameObject? gameObject = ObjectHelper.GetObjectByDataId(2000700);
        if (gameObject == null || !gameObject.IsTargetable)
        {
            TaskManager.Enqueue(() => { Stage = Stage.Reading_Path; } );
            return;
        }

        var oldindex = Indexer;
        Indexer = FindWaypoint();
        TaskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2));
        TaskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno"), int.MaxValue);
        TaskManager.Enqueue(() => AddonHelper.ClickSelectYesno());
        TaskManager.Enqueue(() => !ObjectHelper.IsValid, 500);
        TaskManager.Enqueue(() => ObjectHelper.IsValid);
        TaskManager.Enqueue(() => { if (Indexer == 0) Indexer = FindWaypoint(); });
        TaskManager.Enqueue(() => Stage = Stage.Reading_Path);
    }

    private unsafe void ActionInvoke()
    {
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
                string node = ListBoxPOSText[i];

                if (node.Contains("Boss|") && node.Replace("Boss|", "").All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                {
                    currentDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Replace("Boss|", "").Split(',')[2], System.Globalization.CultureInfo.InvariantCulture)));

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
            bool revivalFound = ContentPathsManager.DictionaryPaths[CurrentTerritoryType].Paths[CurrentPath].RevivalFound;

            //Svc.Log.Info("Finding Last Boss");
            for (int i = Indexer; i >= 0; i--)
            {
                if (revivalFound)
                {
                    if (ListBoxPOSText[i].Contains("Revival|") && i != Indexer)
                        return i;
                }
                else
                {
                    if (ListBoxPOSText[i].Contains("Boss|") && i != Indexer)
                        return i + 1;
                }
            }
        }

        return 0;
    }
    
    private void GetJobAndLevelingCheck()
    {
        Job curJob = Player.Object.GetJob();
        if (curJob != JobLastKnown)
        {
            if (LevelingEnabled)
            {
                Svc.Log.Info($"{(Configuration.Support || Configuration.Trust) && (Configuration.Support || SupportLevelingEnabled) && (!Configuration.Trust || TrustLevelingEnabled)} ({Configuration.Support} || {Configuration.Trust}) && ({Configuration.Support} || {SupportLevelingEnabled}) && ({!Configuration.Trust} || {TrustLevelingEnabled})");
                Svc.Log.Info($"Leveling2 {LevelingEnabled} {SupportLeveling} {SupportLevelingEnabled} {TrustLeveling} {TrustLevelingEnabled}");
                ContentHelper.Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(Configuration.Trust);
                if (duty != null)
                {
                    Plugin.CurrentTerritoryContent = duty;
                    MainListClicked = true;
                    ContentPathsManager.DictionaryPaths[Plugin.CurrentTerritoryContent.TerritoryType].SelectPath(out CurrentPath);
                }
                else
                {
                    Plugin.CurrentTerritoryContent = null;
                    if (Configuration.Support)
                        SupportLeveling = false;
                    else if (Configuration.Trust)
                        TrustLeveling = false;
                    CurrentPath = -1;
                }
            }
        }

        JobLastKnown = curJob;
    }

    public void Framework_Update(IFramework framework)
    {
        if (Stage == Stage.Stopped)
            return;

        if (EzThrottler.Throttle("OverrideAFK") && States.HasFlag(State.Navigating) && ObjectHelper.IsValid)
            _overrideAFK.ResetTimers();

        if (!Player.Available)
            return;

        if (!InDungeon && CurrentTerritoryContent != null)
            GetJobAndLevelingCheck();

        if (!ObjectHelper.IsValid || !BossMod_IPCSubscriber.IsEnabled || !VNavmesh_IPCSubscriber.IsEnabled)
            return;

        if (!ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled && !BossMod_IPCSubscriber.IsEnabled && !Configuration.UsingAlternativeRotationPlugin)
            return;

        if (CurrentTerritoryType == 0 && Svc.ClientState.TerritoryType !=0 && InDungeon)
            ClientState_TerritoryChanged(Svc.ClientState.TerritoryType);

        if (EzThrottler.Throttle("ClosestInteractableEventObject", 25) && MainWindow.CurrentTabName == "Build")
            ClosestInteractableEventObject = ObjectHelper.GetObjectsByObjectKind(ObjectKind.EventObj)?.FirstOrDefault(o => o.IsTargetable);

        if (EzThrottler.Throttle("ClosestTargetableBattleNpc", 25) && MainWindow.CurrentTabName == "Build")
            ClosestTargetableBattleNpc = ObjectHelper.GetObjectsByObjectKind(ObjectKind.BattleNpc)?.FirstOrDefault(o => o.IsTargetable);

        if (States.HasFlag(State.Navigating) && Configuration.LootTreasure && (!Configuration.LootBossTreasureOnly || (_action == "Boss" && Stage == Stage.Action)) && (treasureCofferGameObject = ObjectHelper.GetObjectsByObjectKind(ObjectKind.Treasure)?.FirstOrDefault(x => ObjectHelper.GetDistanceToPlayer(x) < 2)) != null)
            ObjectHelper.InteractWithObject(treasureCofferGameObject, false);

        if (Stage != Stage.Dead && States.HasFlag(State.Navigating) && Player.Object.CurrentHp == 0)
            Stage = Stage.Dead;

        if (Stage == Stage.Dead && States.HasFlag(State.Navigating) && Player.Object.CurrentHp > 0)
            Stage = Stage.Revived;

        if (Indexer >= ListBoxPOSText.Count && ListBoxPOSText.Count > 0 && States.HasFlag(State.Navigating))
            DoneNavigating();

        if (Stage > Stage.Looping && !States.HasFlag(State.Other))
            Action = EnumString(Stage);
        //Svc.Log.Info($"{Stage} {States}");
        switch (Stage)
        {
            case Stage.Reading_Path:
                if (!ObjectHelper.IsValid || !EzThrottler.Check("PathFindFailure") || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"{(ListBoxPOSText.Count >= Indexer ? Plugin.ListBoxPOSText[Indexer] : "")}";
                //Backwards Compatibility
                if (ListBoxPOSText[Indexer].Contains('|'))
                {
                    _actionPosition = [];
                    _actionParams = [.. ListBoxPOSText[Indexer].Split('|')];
                    _action = (string)_actionParams[0];
                    _actionTollerance = _action == "Interactable" ? 2f : 0.25f;

                    if ((!Configuration.LootTreasure || Configuration.LootBossTreasureOnly) && _action.Equals("TreasureCoffer", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Indexer++;
                        return;
                    }

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
                            Stage = Stage.Action;
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
                        Stage = Stage.Action;
                        return;
                    }

                    if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && !VNavmesh_IPCSubscriber.Path_IsRunning())
                    {
                        if (_action == "MoveTo" && bool.TryParse((string)_actionParams[0], out bool useMesh) && !useMesh)
                            VNavmesh_IPCSubscriber.Path_MoveTo([destinationVector], false);
                        else
                            VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destinationVector, false);
                        Stage = Stage.Moving;
                    }
                }
                //also backwards compat
                else
                {
                    if (!ListBoxPOSText[Indexer].All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                    {
                        MainWindow.ShowPopup("Error", $"Error in line {Indexer} of path file\nFormat: Action|123, 0, 321|ActionParams(if needed)");
                        States &= ~State.Looping;
                        States &= ~State.Navigating;
                        CurrentLoop = 0;
                        MainListClicked = false;
                        Stage = 0;
                        return;
                    }

                    var destinationVector = new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ListBoxPOSText[Indexer].Split(',')[2], System.Globalization.CultureInfo.InvariantCulture));

                    if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                    {
                        VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destinationVector, false);
                        Stage = Stage.Moving;
                    }
                }
                break;
            case Stage.Moving:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                if (Configuration.Regular && Svc.Party.PartyId > 0)
                {
                    _messageSender = true;
                    _messageBusSend.PublishAsync(Encoding.UTF8.GetBytes($"Follow|{Player.Name}"));
                }
                
                Action = $"{Plugin.ListBoxPOSText[Indexer]}";
                if (Player.Object.InCombat() && Plugin.StopForCombat)
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
                    Stage = Stage.Waiting_For_Combat;
                    break;
                }

                if (StuckHelper.IsStuck())
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
                    Stage = Stage.Reading_Path;
                    return;
                }

                if ((!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0) || (!_action.IsNullOrEmpty() && _actionPosition.Count > 0 && _actionTollerance > 0.25f && ObjectHelper.GetDistanceToPlayer((Vector3)_actionPosition[0]) <= _actionTollerance))
                {
                    if (_action.IsNullOrEmpty() || _action.Equals("MoveTo") || _action.Equals("TreasureCoffer") || _action.Equals("Revival") )
                    {
                        Stage = Stage.Reading_Path;
                        Indexer++;
                    }
                    else
                    {
                        VNavmesh_IPCSubscriber.Path_Stop();
                        Stage = Stage.Action;
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
                        Stage = Stage.Action;
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
            case Stage.Action:
                if (Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                if (!TaskManager.IsBusy)
                {
                    Stage = Stage.Reading_Path;
                    Indexer++;
                    return;
                }
                break;
            case Stage.Waiting_For_Combat:
                if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"Waiting For Combat";

                if(EzThrottler.Throttle("PositionalChecker", 25) && ReflectionHelper.Avarice_Reflection.PositionalChanged(out Positional positional))
                    Chat.ExecuteCommand($"/vbm cfg AIConfig DesiredPositional {positional}");


                if (EzThrottler.Throttle("BossChecker", 25) && _action.Equals("Boss") && _actionPosition.Count > 0 && ObjectHelper.GetDistanceToPlayer((Vector3)_actionPosition[0]) < 50)
                {
                    BossObject = ObjectHelper.GetBossObject(25);
                    if (BossObject != null)
                    {
                        VNavmesh_IPCSubscriber.Path_Stop();
                        _actionParams = _actionPosition;
                        Stage = Stage.Action;
                        return;
                    }
                }

                if (Player.Object.InCombat())
                {
                    if (Svc.Targets.Target == null && EzThrottler.Throttle("TargetCheck"))
                    {
                        //find and target closest attackable npc, if we are not targeting
                        var gos = ObjectHelper.GetObjectsByObjectKind(ObjectKind.BattleNpc)?.FirstOrDefault(o => ObjectFunctions.GetNameplateKind(o) is NameplateKind.HostileEngagedSelfUndamaged or NameplateKind.HostileEngagedSelfDamaged && ObjectHelper.GetBattleDistanceToPlayer(o) <= 75);

                        if (gos != null)
                            Svc.Targets.Target = gos;
                    }
                    if (Configuration.AutoManageBossModAISettings)
                    {
                        if (Svc.Targets.Target != null)
                        {
                            VNavmesh_IPCSubscriber.Path_Stop();

                            if (ObjectFunctions.GetAttackableEnemyCountAroundPoint(Svc.Targets.Target.Position, 15) > 2 && !BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget"])[0].Equals(Configuration.MaxDistanceToTargetAoEFloat))
                                BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetAoEFloat}"]);
                            else if (!BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget"])[0].Equals(Configuration.MaxDistanceToTargetFloat))
                                BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"]);

                        }
                        else if (!BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget"])[0].Equals(Configuration.MaxDistanceToTargetFloat))
                            BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"]);
                    }
                    else
                        VNavmesh_IPCSubscriber.Path_Stop();
                }
                else if (!Player.Object.InCombat() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
                    Stage = Stage.Reading_Path;
                }
                break;
            case Stage.Looting_Treasure:
                //if (!ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                if (Player.Object.InCombat())
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
                    Stage = Stage.Waiting_For_Combat;
                    return;
                }

                if (treasureCofferGameObject == null || !treasureCofferGameObject.IsTargetable)
                {
                    Stage = Stage.Reading_Path;
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
            default:
                break;
        }
    }

    private void StopAndResetALL()
    {
        States = State.None;
        MainListClicked = false;
        CurrentLoop = 0;
        Chat.ExecuteCommand($"/vbmai off");
        Chat.ExecuteCommand($"/vbm cfg AIConfig Enable false");
        if (!_vnavAlignCameraState && VNavmesh_IPCSubscriber.Path_GetAlignCamera())
            VNavmesh_IPCSubscriber.Path_SetAlignCamera(false);
        if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin)
            SetRotationPluginSettings(false);
        if (Indexer > 0 && !MainListClicked)
            Indexer = -1;
        if (Configuration.ShowOverlay && Configuration.HideOverlayWhenStopped)
            Overlay.IsOpen = false;
        if (VNavmesh_IPCSubscriber.IsEnabled && VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
            VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
        if (TaskManager.IsBusy)
            TaskManager.Abort();
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
        if (QueueHelper.QueueRunning)
            QueueHelper.Stop();
        if (AMHelper.AMRunning)
            AMHelper.Stop();
        if (AutoRetainerHelper.AutoRetainerRunning)
            AutoRetainerHelper.Stop();
        if (VNavmesh_IPCSubscriber.IsEnabled && VNavmesh_IPCSubscriber.Path_IsRunning())
            VNavmesh_IPCSubscriber.Path_Stop();
        if (MapHelper.MoveToMapMarkerRunning)
            MapHelper.StopMoveToMapMarker();
        if (GotoHousingHelper.GotoHousingRunning)
            GotoHousingHelper.Stop();
        if (ExitDutyHelper.ExitDutyRunning)
            ExitDutyHelper.Stop();

        Action = "";
    }

    public void Dispose()
    {
        StopAndResetALL();
        Svc.Framework.Update -= Framework_Update;
        Svc.Framework.Update -= SchedulerHelper.ScheduleInvoker;
        FileHelper.FileSystemWatcher.Dispose();
        WindowSystem.RemoveAllWindows();
        ECommonsMain.Dispose();
        MainWindow.Dispose();
        OverrideCamera.Dispose();
        Svc.ClientState.TerritoryChanged -= ClientState_TerritoryChanged;
        Svc.Condition.ConditionChange -= Condition_ConditionChange;
        Svc.Commands.RemoveHandler(CommandName);
    }

    private unsafe void OnCommand(string command, string args)
    {
        // in response to the slash command
        var argsArray = args.ToLower().Split(" ");
        switch (argsArray[0])
        {
            case "config" or "cfg":
                if (argsArray.Length < 2)
                    OpenConfigUI();
                else if (argsArray[1].Equals("list"))
                    ConfigHelper.ListConfig();
                else
                    ConfigHelper.ModifyConfig(argsArray[1], argsArray[2]);
                break;
            case "start":
                StartNavigation();
                break;
            case "stop":
                StopAndResetALL();
                break;
            case "pause":
                Plugin.Stage = Stage.Paused;
                break;
            case "resume":
                Plugin.Stage = Stage.Reading_Path;
                break;
            case "goto":
                switch (argsArray[1])
                {
                    case "inn":
                        GotoInnHelper.Invoke(argsArray.Length > 2 ? Convert.ToUInt32(argsArray[2]) : ObjectHelper.GrandCompany);
                        break;
                    case "barracks":
                        GotoBarracksHelper.Invoke();
                        break;
                    case "gcsupply":
                        GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(ObjectHelper.GrandCompany), [GCTurninHelper.GCSupplyLocation], 0.25f, 2f, false);
                        break;
                    case "summoningbell":
                        SummoningBellHelper.Invoke(Configuration.PreferredSummoningBellEnum);
                        break;
                    case "apartment":
                        GotoHousingHelper.Invoke(Housing.Apartment);
                        break;
                    case "personalhome":
                        GotoHousingHelper.Invoke(Housing.Personal_Home);
                        break;
                    case "fcestate":
                        GotoHousingHelper.Invoke(Housing.FC_Estate);
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
                if (InventoryHelper.CanRepair())
                    RepairHelper.Invoke();
                break;
            case "autoretainer":
            case "ar":
                AutoRetainerHelper.Invoke();
                break;
            case "equiprec":
                AutoEquipHelper.Invoke();
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
                _actions.ExitDuty("");
                break;
            case "queue":
                QueueHelper.Invoke(ContentHelper.DictionaryContent.FirstOrDefault(x => x.Value.Name!.Equals(args.ToLower().Replace("queue ", ""), StringComparison.InvariantCultureIgnoreCase)).Value ?? null);
                break;
            case "overlay":
                if (argsArray.Length == 1)
                    Overlay.IsOpen = true;
                else
                {
                    switch (argsArray[1].ToLower())
                    {
                        case "lock":
                            if (Overlay.Flags.HasFlag(ImGuiWindowFlags.NoMove))
                                Overlay.Flags -= ImGuiWindowFlags.NoMove;
                            else
                                Overlay.Flags |= ImGuiWindowFlags.NoMove;
                            break;
                        case "nobg":
                            if (Overlay.Flags.HasFlag(ImGuiWindowFlags.NoBackground))
                                Overlay.Flags -= ImGuiWindowFlags.NoBackground;
                            else
                                Overlay.Flags |= ImGuiWindowFlags.NoBackground;
                            break;
                    }
                }    
                break;
            case "skipstep":
                if (States.HasFlag(State.Navigating))
                {
                    Indexer++;
                    Stage = Stage.Reading_Path;
                }
                break;
            case "am":
                if (!Configuration.UnhideAM)
                {
                    Configuration.UnhideAM = true;
                    Configuration.Save();
                }
                else
                    AMHelper.Invoke();
                break;
            case "movetoflag":
                MapHelper.MoveToMapMarker();
                break;
            case "run":
                if (argsArray.Length <= 2 || !UInt32.TryParse(argsArray[1], out uint territoryType) || !Int32.TryParse(argsArray[2], out int loopTimes))
                {
                    Svc.Log.Info($"Run Error: Incorrect Usage\ncorrect use /autoduty run TerritoryTypeInteger LoopTimesInteger\nexample: /autoduty run 1036 10\nYou can get the TerritoryTypeInteger from /autoduty tt name of territory (will be logged and copied to clipboard)");
                    return;
                }
                Run(territoryType, loopTimes);
                break;
            case "tt":
                var tt = Svc.Data.Excel.GetSheet<TerritoryType>()?.FirstOrDefault(x => x.ContentFinderCondition.Value != null && x.ContentFinderCondition.Value.Name.RawString.Equals(args.Replace("tt ", ""), StringComparison.InvariantCultureIgnoreCase)) ?? Svc.Data.Excel.GetSheet<TerritoryType>()?.GetRow(1);
                Svc.Log.Info($"{tt?.RowId}");
                ImGui.SetClipboardText($"{tt?.RowId}");
                break;
            default:
                OpenMainUI(); 
                break;
        }
    }

    private void DrawUI() => WindowSystem.Draw();

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
