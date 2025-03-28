global using static AutoDuty.Data.Enums;
global using static AutoDuty.Data.Extensions;
global using static AutoDuty.Data.Classes;
global using static AutoDuty.AutoDuty;
global using AutoDuty.Managers;
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
using AutoDuty.Windows;
using AutoDuty.IPC;
using ECommons.Automation.LegacyTaskManager;
using AutoDuty.External;
using AutoDuty.Helpers;
using ECommons.Throttlers;
using Dalamud.Game.ClientState.Objects.Types;
using System.Linq;
using ECommons.GameFunctions;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.IoC;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using AutoDuty.Properties;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Serilog.Events;
using AutoDuty.Updater;

namespace AutoDuty;

using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Data;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using static Data.Classes;
using ReflectionHelper = Helpers.ReflectionHelper;

// TODO:
// Scrapped interable list, going to implement an internal list that when a interactable step end in fail, the Dataid gets add to the list and is scanned for from there on out, if found we goto it and get it, then remove from list.
// Need to expand AutoRepair to include check for level and stuff to see if you are eligible for self repair. and check for dark matter
// make config saving per character
// drap drop on build is jacked when theres scrolling

// WISHLIST for VBM:
// Generic (Non Module) jousting respects navmesh out of bounds (or dynamically just adds forbiddenzones as Obstacles using Detour) (or at very least, vbm NavigationDecision can use ClosestPointonMesh in it's decision making) (or just spit balling here as no idea if even possible, add Everywhere non tiled as ForbiddenZones /shrug)

public sealed class AutoDuty : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    internal List<PathAction> Actions { get; set; } = [];
    internal List<uint> Interactables { get; set; } = [];
    internal int CurrentLoop = 0;
    internal KeyValuePair<ushort, Job?> CurrentPlayerItemLevelandClassJob = new(0, null);
    private Content? currentTerritoryContent = null;
    internal Content? CurrentTerritoryContent
    {
        get => currentTerritoryContent;
        set
        {
            CurrentPlayerItemLevelandClassJob = PlayerHelper.IsValid ? new(InventoryHelper.CurrentItemLevel, Player.Job) : new(0, null);
            currentTerritoryContent = value;
        }
    }
    internal uint CurrentTerritoryType = 0;
    internal int CurrentPath = -1;

    internal bool SupportLevelingEnabled => LevelingModeEnum == LevelingMode.Support;
    internal bool TrustLevelingEnabled => LevelingModeEnum == LevelingMode.Trust;
    internal bool LevelingEnabled => LevelingModeEnum != LevelingMode.None;

    internal static string Name => "AutoDuty";
    internal static AutoDuty Plugin { get; private set; }
    internal bool StopForCombat = true;
    internal DirectoryInfo PathsDirectory;
    internal FileInfo AssemblyFileInfo;
    internal FileInfo ConfigFile;
    internal DirectoryInfo? DalamudDirectory;
    internal DirectoryInfo? AssemblyDirectoryInfo;
    internal Configuration Configuration { get; init; }
    internal WindowSystem WindowSystem = new("AutoDuty");
    internal Stage PreviousStage = Stage.Stopped;
    internal Stage Stage
    {
        get => _stage;
        set
        {
            switch (value)
            {
                case Stage.Stopped:
                    StopAndResetALL();
                    break;
                case Stage.Paused:
                    PreviousStage = Stage;
                    if (VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                        VNavmesh_IPCSubscriber.Path_Stop();
                    FollowHelper.SetFollow(null);
                    TaskManager.SetStepMode(true);
                    States |= PluginState.Paused;
                    break;
                case Stage.Action:
                    ActionInvoke();
                    break;
                case Stage.Condition:
                    Action = $"ConditionChange";
                    SchedulerHelper.ScheduleAction("ConditionChangeStageReadingPath", () => _stage = Stage.Reading_Path, () => !Svc.Condition[ConditionFlag.BetweenAreas] && !Svc.Condition[ConditionFlag.BetweenAreas51] && !Svc.Condition[ConditionFlag.Jumping61]);
                    break;
                case Stage.Waiting_For_Combat:
                    BossMod_IPCSubscriber.SetRange(Plugin.Configuration.MaxDistanceToTargetFloat);
                    break;
            }
            _stage = value;
            Svc.Log.Debug($"Stage={_stage.ToCustomString()}");
        }
    }
    internal LevelingMode LevelingModeEnum
    {
        get => levelingModeEnum;
        set
        {
            if (value != LevelingMode.None)
            {
                Svc.Log.Debug($"Setting Leveling mode to {value}");
                Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(value == LevelingMode.Trust);

                if (duty != null)
                {
                    levelingModeEnum = value;
                    MainTab.DutySelected = ContentPathsManager.DictionaryPaths[duty.TerritoryType];
                    CurrentTerritoryContent = duty;
                    MainTab.DutySelected.SelectPath(out CurrentPath);
                    Svc.Log.Debug($"Leveling Mode: Setting duty to {duty.Name}");
                }
                else
                {
                    MainTab.DutySelected = null;
                    MainListClicked = false;
                    CurrentTerritoryContent = null;
                    levelingModeEnum = LevelingMode.None;
                    Svc.Log.Debug($"Leveling Mode: No appropriate leveling duty found");
                }
            }
            else
            {
                MainTab.DutySelected = null;
                MainListClicked = false;
                CurrentTerritoryContent = null;
                levelingModeEnum = LevelingMode.None;
            }
        }
    }
    internal PluginState States = PluginState.None;
    internal int Indexer = -1;
    internal bool MainListClicked = false;
    internal IBattleChara? BossObject;
    internal static IGameObject? ClosestObject => Svc.Objects.Where(o => o.IsTargetable && o.ObjectKind.EqualsAny(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj, Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)).OrderBy(ObjectHelper.GetDistanceToPlayer).TryGetFirst(out var gameObject) ? gameObject : null;
    internal OverrideCamera OverrideCamera;
    internal MainWindow MainWindow { get; init; }
    internal Overlay Overlay { get; init; }
    internal bool InDungeon => ContentHelper.DictionaryContent.ContainsKey(Svc.ClientState.TerritoryType);
    internal bool SkipTreasureCoffer = false;
    internal string Action = "";
    internal string PathFile = "";
    internal TaskManager TaskManager;
    internal Job JobLastKnown;
    internal DutyState DutyState = DutyState.None;
    internal Chat Chat;
    internal PathAction PathAction = new();
    internal List<Data.Classes.LogMessage> DalamudLogEntries = [];
    private LevelingMode levelingModeEnum = LevelingMode.None;
    private Stage _stage = Stage.Stopped;
    private const string CommandName = "/autoduty";
    private readonly DirectoryInfo _configDirectory;
    private readonly ActionsManager _actions;
    private readonly SquadronManager _squadronManager;
    private readonly VariantManager _variantManager;
    private readonly OverrideAFK _overrideAFK;
    private readonly IPCProvider _ipcProvider;
    private IGameObject? treasureCofferGameObject = null;
    //private readonly TinyMessageBus _messageBusSend = new("AutoDutyBroadcaster");
    //private readonly TinyMessageBus _messageBusReceive = new("AutoDutyBroadcaster");
    private         bool           _recentlyWatchedCutscene = false;
    private         bool           _lootTreasure;
    private         SettingsActive _settingsActive         = SettingsActive.None;
    private         SettingsActive _bareModeSettingsActive = SettingsActive.None;
    private         DateTime       _lastRotationSetTime    = DateTime.MinValue;
    public readonly bool           isDev;

    public AutoDuty()
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(PluginInterface, Plugin, Module.DalamudReflector, Module.ObjectFunctions);

            this.isDev = PluginInterface.IsDev;

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            ConfigTab.BuildManuals();
            _configDirectory = PluginInterface.ConfigDirectory;
            ConfigFile = PluginInterface.ConfigFile;
            DalamudDirectory = ConfigFile.Directory?.Parent;
            PathsDirectory = new(_configDirectory.FullName + "/paths");
            AssemblyFileInfo = PluginInterface.AssemblyLocation;
            AssemblyDirectoryInfo = AssemblyFileInfo.Directory;
            
            Configuration.Version = 
                ((PluginInterface.IsDev     ? new Version(0,0,0, 200) :
                  PluginInterface.IsTesting ? PluginInterface.Manifest.TestingAssemblyVersion ?? PluginInterface.Manifest.AssemblyVersion : PluginInterface.Manifest.AssemblyVersion)!).Revision;
            Configuration.Save();

            if (!_configDirectory.Exists)
                _configDirectory.Create();
            if (!PathsDirectory.Exists)
                PathsDirectory.Create();

            TaskManager = new()
            {
                AbortOnTimeout = false,
                TimeoutSilently = true
            };

            TrustHelper.PopulateTrustMembers();
            ContentHelper.PopulateDuties();
            RepairNPCHelper.PopulateRepairNPCs();
            FileHelper.Init();
            Patcher.Patch(startup: true);
            Chat = new();
            _overrideAFK = new();
            _ipcProvider = new();
            _squadronManager = new(TaskManager);
            _variantManager = new(TaskManager);
            _actions = new(Plugin, Chat, TaskManager);
            BuildTab.ActionsList = _actions.ActionsList;
            OverrideCamera = new();
            Overlay = new();
            MainWindow = new();
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(Overlay);

            if (Configuration.ShowOverlay && (!Configuration.HideOverlayWhenStopped || States.HasFlag(PluginState.Looping) || States.HasFlag(PluginState.Navigating)))
                SchedulerHelper.ScheduleAction("ShowOverlay", () => Overlay.IsOpen = true, () => PlayerHelper.IsReady);

            if (Configuration.ShowMainWindowOnStartup)
                SchedulerHelper.ScheduleAction("ShowMainWindowOnStartup", () => OpenMainUI(), () => PlayerHelper.IsReady);

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
            Svc.Log.MinimumLogLevel = LogEventLevel.Debug;
        }
        catch (Exception e)
        {
            Svc.Log.Info($"Failed loading plugin\n{e}");
        }
    }

    private void DutyState_DutyStarted(object? sender, ushort e) => DutyState = DutyState.DutyStarted;
    private void DutyState_DutyWiped(object? sender, ushort e) => DutyState = DutyState.DutyWiped;
    private void DutyState_DutyRecommenced(object? sender, ushort e) => DutyState = DutyState.DutyRecommenced;
    private void DutyState_DutyCompleted(object? sender, ushort e)
    {
        DutyState = DutyState.DutyComplete;
        this.CheckFinishing();
    }

    private void MessageReceived(string messageJson)
    {
        if (!Player.Available || messageJson.IsNullOrEmpty())
            return;

        var message = System.Text.Json.JsonSerializer.Deserialize<Message>(messageJson, BuildTab.jsonSerializerOptions);

        if (message == null) return;

        if (message.Sender == Player.Name || message.Action.Count == 0 || Svc.Party.All(x => x.Name.ExtractText() != message.Sender))
            return;

        message.Action.Each(_actions.InvokeAction);
    }

    internal void ExitDuty() => _actions.ExitDuty(new());

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
                    Actions.Clear();
                    PathFile = "";
                    return;
                }
            }

            Actions.Clear();
            if (!ContentPathsManager.DictionaryPaths.TryGetValue(Svc.ClientState.TerritoryType, out ContentPathsManager.ContentPathContainer? container))
            {
                PathFile = $"{PathsDirectory.FullName}{Path.DirectorySeparatorChar}({Svc.ClientState.TerritoryType}) {CurrentTerritoryContent?.EnglishName?.Replace(":", "")}.json";
                return;
            }

            ContentPathsManager.DutyPath? path = CurrentPath < 0 ?
                                                     container.SelectPath(out CurrentPath) :
                                                     container.Paths[CurrentPath > -1 ? CurrentPath : 0];

            PathFile = path?.FilePath ?? "";
            Actions = [.. path?.Actions];
            //Svc.Log.Info($"Loading Path: {CurrentPath} {ListBoxPOSText.Count}");
        }
        catch (Exception e)
        {
            Svc.Log.Error(e.ToString());
            //throw;
        }
    }

    private unsafe bool StopLoop => Configuration.EnableTerminationActions && 
                                        (CurrentTerritoryContent == null ||
                                        (Configuration.StopLevel && Player.Level >= Configuration.StopLevelInt) ||
                                        (Configuration.StopNoRestedXP && AgentHUD.Instance()->ExpRestedExperience == 0) ||
                                        (Configuration.StopItemQty && (Configuration.StopItemAll 
                                            ? Configuration.StopItemQtyItemDictionary.All(x => InventoryManager.Instance()->GetInventoryItemCount(x.Key) >= x.Value.Value)
                                            : Configuration.StopItemQtyItemDictionary.Any(x => InventoryManager.Instance()->GetInventoryItemCount(x.Key) >= x.Value.Value))));

    private void TrustLeveling()
    {
        if (TrustLevelingEnabled && TrustHelper.Members.Any(tm => tm.Value.Level < tm.Value.LevelCap))
        {
            TaskManager.Enqueue(() => Svc.Log.Debug($"Trust Leveling Enabled"), "TrustLeveling-Debug");
            TaskManager.Enqueue(() => TrustHelper.ClearCachedLevels(CurrentTerritoryContent!), "TrustLeveling-ClearCachedLevels");
            TaskManager.Enqueue(() => TrustHelper.GetLevels(CurrentTerritoryContent), "TrustLeveling-GetLevels");
            TaskManager.DelayNext(50);
            TaskManager.Enqueue(() => TrustHelper.State != ActionState.Running, "TrustLeveling-RecheckingTrustLevels");
        }
    }

    private void ClientState_TerritoryChanged(ushort t)
    {
        if (Stage == Stage.Stopped) return;

        Svc.Log.Debug($"ClientState_TerritoryChanged: t={t}");

        CurrentTerritoryType         = t;
        MainListClicked              = false;
        this.Framework_Update_InDuty = _ => { };
        if (t == 0)
            return;
        CurrentPath = -1;

        LoadPath();

        if (!States.HasFlag(PluginState.Looping) || GCTurninHelper.State == ActionState.Running || RepairHelper.State == ActionState.Running || GotoHelper.State == ActionState.Running || GotoInnHelper.State == ActionState.Running || GotoBarracksHelper.State == ActionState.Running || GotoHousingHelper.State == ActionState.Running || CurrentTerritoryContent == null)
        {
            Svc.Log.Debug("We Changed Territories but are doing after loop actions or not running at all or in a Territory not supported by AutoDuty");
            return;
        }

        if (Configuration.ShowOverlay && Configuration.HideOverlayWhenStopped && !States.HasFlag(PluginState.Looping))
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
                TaskManager.Enqueue(() => Svc.Log.Debug($"Loop {CurrentLoop} of {Configuration.LoopTimes}"), "Loop-Debug");
                TaskManager.Enqueue(() => { Stage = Stage.Looping; }, "Loop-SetStage=99");
                TaskManager.Enqueue(() => { States &= ~PluginState.Navigating; }, "Loop-RemoveNavigationState");
                TaskManager.Enqueue(() => PlayerHelper.IsReady, int.MaxValue, "Loop-WaitPlayerReady");
                if (Configuration.EnableBetweenLoopActions)
                {
                    TaskManager.Enqueue(() => { Action = $"Waiting {Configuration.WaitTimeBeforeAfterLoopActions}s"; }, "Loop-WaitTimeBeforeAfterLoopActionsActionSet");
                    TaskManager.Enqueue(() => EzThrottler.Throttle("Loop-WaitTimeBeforeAfterLoopActions", Configuration.WaitTimeBeforeAfterLoopActions * 1000), "Loop-WaitTimeBeforeAfterLoopActionsThrottle");
                    TaskManager.Enqueue(() => EzThrottler.Check("Loop-WaitTimeBeforeAfterLoopActions"), Configuration.WaitTimeBeforeAfterLoopActions * 1000, "Loop-WaitTimeBeforeAfterLoopActionsCheck");
                    TaskManager.Enqueue(() => { Action = $"After Loop Actions"; }, "Loop-AfterLoopActionsSetAction");
                }

                TrustLeveling();

                TaskManager.Enqueue(() =>
                {
                    if (StopLoop)
                    {
                        TaskManager.Enqueue(() => Svc.Log.Info($"Loop Stop Condition Encountered, Stopping Loop"));
                        LoopsCompleteActions();
                    }
                    else
                        LoopTasks();
                }, "Loop-CheckStopLoop");

            }
            else
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"Loops Done"),                                                                                         "Loop-Debug");
                TaskManager.Enqueue(() => PlayerHelper.IsReady,                                                                                                 int.MaxValue, "Loop-WaitPlayerReady");
                TaskManager.Enqueue(() => Svc.Log.Debug($"Loop {CurrentLoop} == {Configuration.LoopTimes} we are done Looping, Invoking LoopsCompleteActions"), "Loop-Debug");
                TaskManager.Enqueue(() =>
                                    {
                                        if (this.Configuration.ExecuteBetweenLoopActionLastLoop)
                                            this.LoopTasks(false);
                                        else
                                            this.LoopsCompleteActions();
                                    },     "Loop-LoopCompleteActions");
            }
        }
    }

    private unsafe void Condition_ConditionChange(ConditionFlag flag, bool value)
    {
        if (Stage == Stage.Stopped) return;

        if (flag == ConditionFlag.Unconscious)
        {
            if (value && (Stage != Stage.Dead || DeathHelper.DeathState != PlayerLifeState.Dead))
            {
                Svc.Log.Debug($"We Died, Setting Stage to Dead");
                DeathHelper.DeathState = PlayerLifeState.Dead;
                Stage = Stage.Dead;
            }
            else if (!value && (Stage != Stage.Revived || DeathHelper.DeathState != PlayerLifeState.Revived))
            {
                Svc.Log.Debug($"We Revived, Setting Stage to Revived");
                DeathHelper.DeathState = PlayerLifeState.Revived;
                Stage = Stage.Revived;
            }
            return;
        }
        //Svc.Log.Debug($"{flag} : {value}");
        if (Stage != Stage.Dead && Stage != Stage.Revived && !_recentlyWatchedCutscene && !Conditions.Instance()->WatchingCutscene && flag != ConditionFlag.WatchingCutscene && flag != ConditionFlag.WatchingCutscene78 && flag != ConditionFlag.OccupiedInCutSceneEvent && Stage != Stage.Action && value && States.HasFlag(PluginState.Navigating) && (flag == ConditionFlag.BetweenAreas || flag == ConditionFlag.BetweenAreas51 || flag == ConditionFlag.Jumping61))
        {
            Svc.Log.Info($"Condition_ConditionChange: Indexer Increase and Change Stage to Condition");
            Indexer++;
            VNavmesh_IPCSubscriber.Path_Stop();
            Stage = Stage.Condition;
        }
        if (Conditions.Instance()->WatchingCutscene || flag == ConditionFlag.WatchingCutscene || flag == ConditionFlag.WatchingCutscene78 || flag == ConditionFlag.OccupiedInCutSceneEvent)
        {
            _recentlyWatchedCutscene = true;
            SchedulerHelper.ScheduleAction("RecentlyWatchedCutsceneTimer", () => _recentlyWatchedCutscene = false, 5000);
        }
    }

    public void Run(uint territoryType = 0, int loops = 0, bool startFromZero = true, bool bareMode = false)
    {
        Svc.Log.Debug($"Run: territoryType={territoryType} loops={loops} bareMode={bareMode}");
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

        if (CurrentTerritoryContent == null)
            return;

        if (loops > 0)
            Configuration.LoopTimes = loops;

        if (bareMode)
        {
            _bareModeSettingsActive |= SettingsActive.BareMode_Active;
            if (Configuration.EnablePreLoopActions)
                _bareModeSettingsActive |= SettingsActive.PreLoop_Enabled;
            if (Configuration.EnableBetweenLoopActions)
                _bareModeSettingsActive |= SettingsActive.BetweenLoop_Enabled;
            if (Configuration.EnableTerminationActions)
                _bareModeSettingsActive |= SettingsActive.TerminationActions_Enabled;
            Configuration.EnablePreLoopActions = false;
            Configuration.EnableBetweenLoopActions = false;
            Configuration.EnableTerminationActions = false;
        }

        Svc.Log.Info($"Running AutoDuty in {CurrentTerritoryContent.EnglishName}, Looping {Configuration.LoopTimes} times{(bareMode ? " in BareMode (No Pre, Between or Termination Loop Actions)" : "")}");

        //MainWindow.OpenTab("Mini");
        if (Configuration.ShowOverlay)
        {
            //MainWindow.IsOpen = false;
            Overlay.IsOpen = true;
        }
        Stage = Stage.Looping;
        States |= PluginState.Looping;
        SetGeneralSettings(false);
        if (!VNavmesh_IPCSubscriber.Path_GetMovementAllowed())
            VNavmesh_IPCSubscriber.Path_SetMovementAllowed(true);
        TaskManager.Abort();
        Svc.Log.Info($"Running {CurrentTerritoryContent.Name} {Configuration.LoopTimes} Times");
        if (!InDungeon)
        {
            CurrentLoop = 0;
            if (Configuration.EnablePreLoopActions)
            {
                if (Configuration.ExecuteCommandsPreLoop)
                {
                    TaskManager.Enqueue(() => Svc.Log.Debug($"ExecutingCommandsPreLoop, executing {Configuration.CustomCommandsTermination.Count} commands"));
                    Configuration.CustomCommandsPreLoop.Each(x => TaskManager.Enqueue(() => Chat.ExecuteCommand(x), "Run-ExecuteCommandsPreLoop"));
                }

                AutoConsume();

                if (LevelingModeEnum == LevelingMode.None)
                    AutoEquipRecommendedGear();

                if (Configuration.AutoRepair && InventoryHelper.CanRepair())
                {
                    TaskManager.Enqueue(() => Svc.Log.Debug($"AutoRepair PreLoop Action"));
                    TaskManager.Enqueue(() => RepairHelper.Invoke(), "Run-AutoRepair");
                    TaskManager.DelayNext("Run-AutoRepairDelay50", 50);
                    TaskManager.Enqueue(() => RepairHelper.State != ActionState.Running, int.MaxValue, "Run-WaitAutoRepairComplete");
                    TaskManager.Enqueue(() => PlayerHelper.IsReadyFull, "Run-WaitAutoRepairIsReadyFull");
                }

                if (Configuration.DutyModeEnum != DutyMode.Squadron && Configuration.RetireMode)
                {
                    TaskManager.Enqueue(() => Svc.Log.Debug($"Retire PreLoop Action"));
                    if (Configuration.RetireLocationEnum == RetireLocation.GC_Barracks)
                        TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Run-GotoBarracksInvoke");
                    else if (Configuration.RetireLocationEnum == RetireLocation.Inn)
                        TaskManager.Enqueue(() => GotoInnHelper.Invoke(), "Run-GotoInnInvoke");
                    else
                        TaskManager.Enqueue(() => GotoHousingHelper.Invoke((Housing)Configuration.RetireLocationEnum), "Run-GotoHousingInvoke");
                    TaskManager.DelayNext("Run-RetireModeDelay50", 50);
                    TaskManager.Enqueue(() => GotoHousingHelper.State != ActionState.Running && GotoBarracksHelper.State != ActionState.Running && GotoInnHelper.State != ActionState.Running, int.MaxValue, "Run-WaitGotoComplete");
                }
            }

            TaskManager.Enqueue(() => Svc.Log.Debug($"Queueing First Run"));
            Queue(CurrentTerritoryContent);
        }
        TaskManager.Enqueue(() => Svc.Log.Debug($"Done Queueing-WaitDutyStarted, NavIsReady"));
        TaskManager.Enqueue(() => Svc.DutyState.IsDutyStarted,          "Run-WaitDutyStarted");
        TaskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "Run-WaitNavIsReady");
        TaskManager.Enqueue(() => Svc.Log.Debug($"Start Navigation"));
        TaskManager.Enqueue(() => StartNavigation(startFromZero), "Run-StartNavigation");
        if (CurrentLoop == 0)
            CurrentLoop = 1;
    }

    private unsafe void LoopTasks(bool queue = true)
    {
        if (CurrentTerritoryContent == null) return;

        if (Configuration.EnableBetweenLoopActions)
        {
            if (Configuration.ExecuteCommandsBetweenLoop)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"ExecutingCommandsBetweenLoops, executing {Configuration.CustomCommandsBetweenLoop.Count} commands"));
                Configuration.CustomCommandsBetweenLoop.Each(x => Chat.ExecuteCommand(x));
                TaskManager.DelayNext("Loop-DelayAfterCommands", 1000);
            }

            if (Configuration.AutoOpenCoffers)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"AutoCoffers Between Loop Action"));
                TaskManager.Enqueue(CofferHelper.Invoke, "Loop-AutoCoffers");
                TaskManager.DelayNext("Loop-Delay50", 50);
                TaskManager.Enqueue(() => CofferHelper.State != ActionState.Running, int.MaxValue, "Loop-WaitAutoCofferComplete");
            }

            if (Configuration.EnableAutoRetainer && AutoRetainer_IPCSubscriber.IsEnabled && AutoRetainer_IPCSubscriber.AreAnyRetainersAvailableForCurrentChara())
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"AutoRetainer BetweenLoop Actions"));
                if (Configuration.EnableAutoRetainer)
                {
                    TaskManager.Enqueue(() => AutoRetainerHelper.Invoke(), "Loop-AutoRetainer");
                    TaskManager.DelayNext("Loop-Delay50", 50);
                    TaskManager.Enqueue(() => AutoRetainerHelper.State != ActionState.Running, int.MaxValue, "Loop-WaitAutoRetainerComplete");
                }
                else
                {
                    TaskManager.Enqueue(() => AutoRetainer_IPCSubscriber.IsBusy(), 15000, "Loop-AutoRetainerIntegrationDisabledWait15sRetainerSense");
                    TaskManager.Enqueue(() => !AutoRetainer_IPCSubscriber.IsBusy(), int.MaxValue, "Loop-AutoRetainerIntegrationDisabledWaitARNotBusy");
                    TaskManager.Enqueue(() => AutoRetainerHelper.Stop(), "Loop-AutoRetainerStop");
                }
            }

            AutoConsume();

            AutoEquipRecommendedGear();

            if (Configuration.AM)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"AutoMarket Between Loop Action"));
                TaskManager.Enqueue(() => AMHelper.Invoke(), "Loop-AM");
                TaskManager.DelayNext("Loop-Delay50", 50);
                TaskManager.Enqueue(() => AMHelper.State != ActionState.Running, int.MaxValue, "Loop-WaitAMComplete");
            }

            if (Configuration.AutoRepair && InventoryHelper.CanRepair())
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"AutoRepair Between Loop Action"));
                TaskManager.Enqueue(() => RepairHelper.Invoke(), "Loop-AutoRepair");
                TaskManager.DelayNext("Loop-Delay50", 50);
                TaskManager.Enqueue(() => RepairHelper.State != ActionState.Running, int.MaxValue, "Loop-WaitAutoRepairComplete");
                TaskManager.Enqueue(() => PlayerHelper.IsReadyFull, "Loop-WaitIsReadyFull");
            }

            if (Configuration.AutoExtract && (QuestManager.IsQuestComplete(66174)))
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"AutoExtract Between Loop Action"));
                TaskManager.Enqueue(() => ExtractHelper.Invoke(), "Loop-AutoExtract");
                TaskManager.DelayNext("Loop-Delay50", 50);
                TaskManager.Enqueue(() => ExtractHelper.State != ActionState.Running, int.MaxValue, "Loop-WaitAutoExtractComplete");
            }

            if (Configuration.AutoDesynth)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"AutoDesynth Between Loop Action"));
                TaskManager.Enqueue(() => DesynthHelper.Invoke(), "Loop-AutoDesynth");
                TaskManager.DelayNext("Loop-Delay50", 50);
                TaskManager.Enqueue(() => DesynthHelper.State != ActionState.Running, int.MaxValue, "Loop-WaitAutoDesynthComplete");
            }

            if (Configuration.AutoGCTurnin && (!Configuration.AutoGCTurninSlotsLeftBool || InventoryManager.Instance()->GetEmptySlotsInBag() <= Configuration.AutoGCTurninSlotsLeft) && PlayerHelper.GetGrandCompanyRank() > 5)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"GC Turnin Between Loop Action"));
                TaskManager.Enqueue(() => GCTurninHelper.Invoke(), "Loop-AutoGCTurnin");
                TaskManager.DelayNext("Loop-Delay50", 50);
                TaskManager.Enqueue(() => GCTurninHelper.State != ActionState.Running, int.MaxValue, "Loop-WaitAutoGCTurninComplete");
            }

            if (Configuration.DutyModeEnum != DutyMode.Squadron && Configuration.RetireMode)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"Retire Between Loop Action"));
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
                TaskManager.Enqueue(() => GotoHousingHelper.State != ActionState.Running && GotoBarracksHelper.State != ActionState.Running && GotoInnHelper.State != ActionState.Running, int.MaxValue, "Loop-WaitGotoComplete");
            }
        }

        if (!queue)
        {
            LoopsCompleteActions();
            return;
        }

        if (LevelingEnabled)
        {
            Svc.Log.Info("Leveling Enabled");
            Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(LevelingModeEnum == LevelingMode.Trust);
            if (duty != null)
            {
                if (this.LevelingModeEnum == LevelingMode.Support && Configuration.PreferTrustOverSupportLeveling && duty.ClassJobLevelRequired > 70)
                {
                    levelingModeEnum           = LevelingMode.Trust;
                    Configuration.dutyModeEnum = DutyMode.Trust;

                    Content? dutyTrust = LevelingHelper.SelectHighestLevelingRelevantDuty(true);

                    if (duty != dutyTrust)
                    {
                        levelingModeEnum           = LevelingMode.Support;
                        Configuration.dutyModeEnum = DutyMode.Support;
                    }
                }

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
        TaskManager.Enqueue(() => Svc.Log.Debug($"Registering New Loop"));
        Queue(CurrentTerritoryContent);
        TaskManager.Enqueue(() => Svc.Log.Debug($"Incrementing LoopCount, Setting Action Var, Wait for CorrectTerritory, PlayerIsValid, DutyStarted, and NavIsReady"));
        TaskManager.Enqueue(() => CurrentLoop++, "Loop-IncrementCurrentLoop");
        TaskManager.Enqueue(() => { Action = $"Looping: {CurrentTerritoryContent.Name} {CurrentLoop} of {Configuration.LoopTimes}"; }, "Loop-SetAction");
        TaskManager.Enqueue(() => Svc.ClientState.TerritoryType == CurrentTerritoryContent.TerritoryType, int.MaxValue, "Loop-WaitCorrectTerritory");
        TaskManager.Enqueue(() => PlayerHelper.IsValid, int.MaxValue, "Loop-WaitPlayerValid");
        TaskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, int.MaxValue, "Loop-WaitDutyStarted");
        TaskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "Loop-WaitNavReady");
        TaskManager.Enqueue(() => Svc.Log.Debug($"StartNavigation"));
        TaskManager.Enqueue(() => StartNavigation(true), "Loop-StartNavigation");
    }

    private void LoopsCompleteActions()
    {

        SetGeneralSettings(false);

        if (Configuration.EnableTerminationActions)
        {
            TaskManager.Enqueue(() => PlayerHelper.IsReadyFull);
            TaskManager.Enqueue(() => Svc.Log.Debug($"TerminationActions are Enabled"));
            if (Configuration.ExecuteCommandsTermination)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"ExecutingCommandsTermination, executing {Configuration.CustomCommandsTermination.Count} commands"));
                Configuration.CustomCommandsTermination.Each(x => Chat.ExecuteCommand(x));
            }

            if (Configuration.PlayEndSound)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"Playing End Sound"));
                SoundHelper.StartSound(Configuration.PlayEndSound, Configuration.CustomSound, Configuration.SoundEnum);
            }

            if (Configuration.TerminationMethodEnum == TerminationMode.Kill_PC)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"Killing PC"));
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
                TaskManager.Enqueue(() => Svc.Log.Debug($"Killing Client"));
                if (!Configuration.TerminationKeepActive)
                {
                    Configuration.TerminationMethodEnum = TerminationMode.Do_Nothing;
                    Configuration.Save();
                }

                Chat.ExecuteCommand($"/xlkill");
            }
            else if (Configuration.TerminationMethodEnum == TerminationMode.Logout)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"Logging Out"));
                if (!Configuration.TerminationKeepActive)
                {
                    Configuration.TerminationMethodEnum = TerminationMode.Do_Nothing;
                    Configuration.Save();
                }

                TaskManager.Enqueue(() => PlayerHelper.IsReady);
                TaskManager.DelayNext(2000);
                TaskManager.Enqueue(() => Chat.ExecuteCommand($"/logout"));
                TaskManager.Enqueue(() => AddonHelper.ClickSelectYesno());
            }
            else if (Configuration.TerminationMethodEnum == TerminationMode.Start_AR_Multi_Mode)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"Starting AR Multi Mode"));
                TaskManager.Enqueue(() => Chat.ExecuteCommand($"/ays multi e"));
            }
        }

        Svc.Log.Debug($"Removing Looping, Setting CurrentLoop to 0, and Setting Stage to Stopped");

        States      &= ~PluginState.Looping;
        CurrentLoop =  0;
        TaskManager.Enqueue(() => SchedulerHelper.ScheduleAction("SetStageStopped", () => Stage = Stage.Stopped, 1));
    }

    private void AutoEquipRecommendedGear()
    {
        if (Configuration.AutoEquipRecommendedGear)
        {
            TaskManager.Enqueue(() => Svc.Log.Debug($"AutoEquipRecommendedGear Between Loop Action"));
            TaskManager.Enqueue(() => AutoEquipHelper.Invoke(), "AutoEquipRecommendedGear-Invoke");
            TaskManager.DelayNext("AutoEquipRecommendedGear-Delay50", 50);
            TaskManager.Enqueue(() => AutoEquipHelper.State != ActionState.Running, int.MaxValue, "AutoEquipRecommendedGear-WaitAutoEquipComplete");
            TaskManager.Enqueue(() => PlayerHelper.IsReadyFull, "AutoEquipRecommendedGear-WaitANotIsOccupied");
        }
    }

    private void AutoConsume()
    {
        if (Configuration.AutoConsume)
        {
            TaskManager.Enqueue(() => Svc.Log.Debug($"AutoConsume PreLoop Action"));
            Configuration.AutoConsumeItemsList.Each(x =>
            {
                var isAvailable = InventoryHelper.IsItemAvailable(x.Value.ItemId, x.Value.CanBeHq);
                if (isAvailable)
                {
                    if (Configuration.AutoConsumeIgnoreStatus)
                        TaskManager.Enqueue(() => InventoryHelper.UseItemUntilAnimationLock(x.Value.ItemId, x.Value.CanBeHq), $"AutoConsume - {x.Value.Name} is available: {isAvailable}");
                    else
                        TaskManager.Enqueue(() => InventoryHelper.UseItemUntilStatus(x.Value.ItemId, x.Key, Plugin.Configuration.AutoConsumeTime * 60, x.Value.CanBeHq), $"AutoConsume - {x.Value.Name} is available: {isAvailable}");
                }
                TaskManager.DelayNext("AutoConsume-DelayNext50", 50);
                TaskManager.Enqueue(() => PlayerHelper.IsReadyFull, "AutoConsume-WaitPlayerIsReadyFull");
                TaskManager.DelayNext("AutoConsume-DelayNext250", 250);
            });
        }
    }

    private void Queue(Content content)
    {
        if (Configuration.DutyModeEnum == DutyMode.Variant)
            _variantManager.RegisterVariantDuty(content);
        else if (Configuration.DutyModeEnum.EqualsAny(DutyMode.Regular, DutyMode.Trial, DutyMode.Raid, DutyMode.Support, DutyMode.Trust))
        {
            TaskManager.Enqueue(() => QueueHelper.Invoke(content, Configuration.DutyModeEnum), "Queue-Invoke");
            TaskManager.DelayNext("Queue-Delay50", 50);
            TaskManager.Enqueue(() => QueueHelper.State != ActionState.Running, int.MaxValue, "Queue-WaitQueueComplete");
        }
        else if (Configuration.DutyModeEnum == DutyMode.Squadron)
        {
            TaskManager.Enqueue(() => GotoBarracksHelper.Invoke(), "Queue-GotoBarracksInvoke");
            TaskManager.DelayNext("Queue-GotoBarracksDelay50", 50);
            TaskManager.Enqueue(() => GotoBarracksHelper.State != ActionState.Running && GotoInnHelper.State != ActionState.Running, int.MaxValue, "Queue-WaitGotoComplete");
            _squadronManager.RegisterSquadron(content);
        }
        TaskManager.Enqueue(() => !PlayerHelper.IsValid, "Queue-WaitNotValid");
        TaskManager.Enqueue(() => PlayerHelper.IsValid, int.MaxValue, "Queue-WaitValid");
    }

    private void StageReadingPath()
    {
        if (!PlayerHelper.IsValid || !EzThrottler.Check("PathFindFailure") || Indexer == -1 || Indexer >= Actions.Count)
            return;

        Action = $"{(Actions.Count >= Indexer ? Plugin.Actions[Indexer].ToCustomString() : "")}";

        PathAction = Actions[Indexer];

        bool sync = !this.Configuration.Unsynced || !this.Configuration.DutyModeEnum.EqualsAny(DutyMode.Raid, DutyMode.Regular, DutyMode.Trial);
        if (PathAction.Tag.HasFlag(ActionTag.Unsynced) && sync)
        {
            Svc.Log.Debug($"Skipping path entry {Actions[Indexer]} because we are synced");
            Indexer++;
            return;
        }

        if (PathAction.Tag.HasFlag(ActionTag.W2W) && !Configuration.IsW2W(unsync: !sync))
        {
            Svc.Log.Debug($"Skipping path entry {Actions[Indexer]} because we are not W2W-ing");
            this.Indexer++;
            return;
        }

        if (PathAction.Tag.HasFlag(ActionTag.Synced) && Configuration.Unsynced)
        {
            Svc.Log.Debug($"Skipping path entry {Actions[Indexer]} because we are unsynced");
            Indexer++;
            return;
        }

        if (PathAction.Tag.HasFlag(ActionTag.Comment))
        {
            Svc.Log.Debug($"Skipping path entry {Actions[Indexer].Name} because it is a comment");
            Indexer++;
            return;
        }

        if (PathAction.Tag.HasFlag(ActionTag.Revival))
        {
            Svc.Log.Debug($"Skipping path entry {Actions[Indexer].Name} because it is a Revival Tag");
            Indexer++;
            return;
        }

        if ((SkipTreasureCoffer || !Configuration.LootTreasure || Configuration.LootBossTreasureOnly) && PathAction.Tag.HasFlag(ActionTag.Treasure))
        {
            Svc.Log.Debug($"Skipping path entry {Actions[Indexer].Name} because we are either in revival mode, LootTreasure is off or BossOnly");
            Indexer++;
            return;
        }

        if (PathAction.Position == Vector3.Zero)
        {
            Stage = Stage.Action;
            return;
        }

        if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && !VNavmesh_IPCSubscriber.Path_IsRunning())
        {
            Chat.Instance.ExecuteCommand("/automove off");
            VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
            if (PathAction.Name == "MoveTo" && PathAction.Arguments.Count > 0 && bool.TryParse(PathAction.Arguments[0], out bool useMesh) && !useMesh)
            {
                VNavmesh_IPCSubscriber.Path_MoveTo([PathAction.Position], false);
            }
            else
                VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(PathAction.Position, false);
            Stage = Stage.Moving;
        }
    }

    private void StageMoving()
    {
        if (!PlayerHelper.IsReady || Indexer == -1 || Indexer >= Actions.Count)
            return;

        if (Configuration.DutyModeEnum == DutyMode.Regular && Svc.Party.PartyId > 0)
        {
            Message message = new()
            {
                Sender = Player.Name,
                Action =
                [
                    new PathAction(){ Name = "Follow", Arguments = [$"{Player.Name}"] }
                ]
            };

            var messageJson = System.Text.Json.JsonSerializer.Serialize(message, BuildTab.jsonSerializerOptions);

            //_messageBusSend.PublishAsync(Encoding.UTF8.GetBytes(messageJson));
        }

        Action = $"{Plugin.Actions[Indexer].ToCustomString()}";
        if (PlayerHelper.InCombat && Plugin.StopForCombat)
        {
            if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin)
                SetRotationPluginSettings(true);
            VNavmesh_IPCSubscriber.Path_Stop();
            Stage = Stage.Waiting_For_Combat;
            return;
        }

        if (StuckHelper.IsStuck(out byte stuckCount))
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            if (Configuration.RebuildNavmeshOnStuck && stuckCount >= Configuration.RebuildNavmeshAfterStuckXTimes)
                VNavmesh_IPCSubscriber.Nav_Rebuild();
            Stage = Stage.Reading_Path;
            return;
        }

        if ((!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0) || (!PathAction.Name.IsNullOrEmpty() && PathAction.Position != Vector3.Zero && ObjectHelper.GetDistanceToPlayer(PathAction.Position) <= (PathAction.Name.EqualsIgnoreCase("Interactable") ? 2f : 0.25f)))
        {
            if (PathAction.Name.IsNullOrEmpty() || PathAction.Name.Equals("MoveTo") || PathAction.Name.Equals("TreasureCoffer") || PathAction.Name.Equals("Revival"))
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

        if (EzThrottler.Throttle("BossChecker", 25) && PathAction.Equals("Boss") && PathAction.Position != Vector3.Zero && ObjectHelper.BelowDistanceToPlayer(PathAction.Position, 50, 10))
        {
            BossObject = ObjectHelper.GetBossObject(25);
            if (BossObject != null)
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                Stage = Stage.Action;
                return;
            }
        }
    }

    private void StageAction()
    {
        if (Indexer == -1 || Indexer >= Actions.Count)
            return;
        
        if (this.Configuration is { AutoManageRotationPluginState: true, UsingAlternativeRotationPlugin: false } && !Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent])
            SetRotationPluginSettings(true);
        
        if (!TaskManager.IsBusy)
        {
            Stage = Stage.Reading_Path;
            Indexer++;
            return;
        }
    }

    private void StageWaitingForCombat()
    {
        if (!EzThrottler.Throttle("CombatCheck", 250) || !PlayerHelper.IsReady || Indexer == -1 || Indexer >= Actions.Count || PathAction == null)
            return;

        Action = $"Waiting For Combat";

        if (ReflectionHelper.Avarice_Reflection.PositionalChanged(out Positional positional) && !Plugin.Configuration.UsingAlternativeBossPlugin && IPCSubscriber_Common.IsReady("BossModReborn"))
            Chat.ExecuteCommand($"/vbm cfg AIConfig DesiredPositional {positional}");

        if (PathAction.Name.Equals("Boss") && PathAction.Position != Vector3.Zero && ObjectHelper.GetDistanceToPlayer(PathAction.Position) < 50)
        {
            BossObject = ObjectHelper.GetBossObject(25);
            if (BossObject != null)
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                Stage = Stage.Action;
                return;
            }
        }

        if (PlayerHelper.InCombat)
        {
            if (Svc.Targets.Target == null)
            {
                //find and target closest attackable npc, if we are not targeting
                var gos = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)?.FirstOrDefault(o => o.GetNameplateKind() is NameplateKind.HostileEngagedSelfUndamaged or NameplateKind.HostileEngagedSelfDamaged && ObjectHelper.GetBattleDistanceToPlayer(o) <= 75);

                if (gos != null)
                    Svc.Targets.Target = gos;
            }
            if (Configuration.AutoManageBossModAISettings)
            {
                var mdt = BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget"], false)[0];

                if (mdt.IsNullOrEmpty()) return;

                var gotMDT = float.TryParse(mdt, out float floatMDT);

                if (!gotMDT)
                    return;

                if (Svc.Targets.Target != null)
                {
                    var enemyCount = ObjectFunctions.GetAttackableEnemyCountAroundPoint(Svc.Targets.Target.Position, 15);

                    if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_IsRunning())
                        VNavmesh_IPCSubscriber.Path_Stop();

                    if (enemyCount > 2 && Math.Abs(floatMDT - this.Configuration.MaxDistanceToTargetAoEFloat) > 0.01f)
                    {
                        Svc.Log.Debug($"Changing MaxDistanceToTarget to {Configuration.MaxDistanceToTargetAoEFloat}, because BM MaxDistanceToTarget={floatMDT} and enemy count = {enemyCount}");
                        BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetAoEFloat}"], false);
                    }
                    else if (enemyCount < 3 && Math.Abs(floatMDT - this.Configuration.MaxDistanceToTargetFloat) > 0.01f)
                    {
                        Svc.Log.Debug($"Changing MaxDistanceToTarget to {Configuration.MaxDistanceToTargetFloat}, because BM MaxDistanceToTarget={floatMDT} and enemy count = {enemyCount}");
                        BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"], false);
                        //BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"], false);
                    }
                }
            }
            else if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.Path_Stop();
        }
        else if (!PlayerHelper.InCombat && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
        {
            if (Configuration.AutoManageBossModAISettings)
            {
                var mdt = BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget"], false)[0];

                if (mdt.IsNullOrEmpty()) return;

                var gotMDT = float.TryParse(mdt, out float floatMDT);

                if (gotMDT && Math.Abs(floatMDT - this.Configuration.MaxDistanceToTargetFloat) > 0.01f)
                {
                    Svc.Log.Debug($"Changing MaxDistanceToTarget to {Configuration.MaxDistanceToTargetFloat}, because BM  MaxDistanceToTarget={floatMDT}");
                    BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"], false);
                    BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"], false);
                }
            }

            VNavmesh_IPCSubscriber.Path_Stop();
            Stage = Stage.Reading_Path;
        }
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
        States |= PluginState.Navigating;
        StopForCombat = true;
        if (Configuration.AutoManageVnavAlignCamera && !VNavmesh_IPCSubscriber.Path_GetAlignCamera())
            VNavmesh_IPCSubscriber.Path_SetAlignCamera(true);

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
        States &= ~PluginState.Navigating;
        this.CheckFinishing();
    }

    private void CheckFinishing()
    {
        //we finished lets exit the duty or stop
        if ((Configuration.AutoExitDuty || CurrentLoop < Configuration.LoopTimes))
        {
            if (!Stage.EqualsAny(Stage.Stopped, Stage.Paused)                                     &&
                (!Configuration.OnlyExitWhenDutyDone || this.DutyState == DutyState.DutyComplete) &&
                !this.States.HasFlag(PluginState.Navigating))
            {
                if (ExitDutyHelper.State != ActionState.Running)
                    ExitDuty();
                if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin)
                    SetRotationPluginSettings(false);
                if (Configuration.AutoManageBossModAISettings)
                {
                    Chat.ExecuteCommand($"/vbmai off");
                    if (!IPCSubscriber_Common.IsReady("BossModReborn"))
                        Chat.ExecuteCommand($"/vbm cfg AIConfig Enable false");
                }
            }
        }
        else
            Stage = Stage.Stopped;
    }

    private void GetGeneralSettings()
    {
        /*
        if (Configuration.AutoManageVnavAlignCamera && VNavmesh_IPCSubscriber.IsEnabled && VNavmesh_IPCSubscriber.Path_GetAlignCamera())
            _settingsActive |= SettingsActive.Vnav_Align_Camera_Off;
        */
        if (ReflectionHelper.YesAlready_Reflection.IsEnabled && ReflectionHelper.YesAlready_Reflection.GetPluginEnabled())
            _settingsActive |= SettingsActive.YesAlready;

        if (PandorasBox_IPCSubscriber.IsEnabled && PandorasBox_IPCSubscriber.GetFeatureEnabled("Auto-interact with Objects in Instances"))
            _settingsActive |= SettingsActive.Pandora_Interact_Objects;

        Svc.Log.Debug($"General Settings Active: {_settingsActive}");
    }

    internal void SetGeneralSettings(bool on)
    {
        if (!on)
            GetGeneralSettings();

        if (Configuration.AutoManageVnavAlignCamera && _settingsActive.HasFlag(SettingsActive.Vnav_Align_Camera_Off))
        {
            Svc.Log.Debug($"Setting VnavAlignCamera: {on}");
            VNavmesh_IPCSubscriber.Path_SetAlignCamera(on);
        }
        if (PandorasBox_IPCSubscriber.IsEnabled && _settingsActive.HasFlag(SettingsActive.Pandora_Interact_Objects))
        {
            Svc.Log.Debug($"Setting PandorasBos Auto-interact with Objects in Instances: {on}");
            PandorasBox_IPCSubscriber.SetFeatureEnabled("Auto-interact with Objects in Instances", on);
        }
        if (ReflectionHelper.YesAlready_Reflection.IsEnabled && _settingsActive.HasFlag(SettingsActive.YesAlready))
        {
            Svc.Log.Debug($"Setting YesAlready Enabled: {on}");
            ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(on);
        }
    }

    internal void SetRotationPluginSettings(bool on, bool ignoreConfig = false)
    {
        // Only try to set the rotation state every few seconds
        if (on && (DateTime.Now - _lastRotationSetTime).TotalSeconds < 5)
            return;
        _lastRotationSetTime = DateTime.Now;

        if (!ignoreConfig && !this.Configuration.AutoManageRotationPluginState)
            return;
        bool bmEnabled     = BossMod_IPCSubscriber.IsEnabled;
        bool foundRotation = false;

        if (Wrath_IPCSubscriber.IsEnabled)
        {
            bool wrathRotationReady = true;
            if (on)
                wrathRotationReady = Wrath_IPCSubscriber.IsCurrentJobAutoRotationReady() ||
                                     this.Configuration.Wrath_AutoSetupJobs && Wrath_IPCSubscriber.SetJobAutoReady();

            if (!on || wrathRotationReady)
            {
                Wrath_IPCSubscriber.SetAutoMode(on);
                foundRotation = true;
            }
        }

        if (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
        {
            if (on && !foundRotation)
            {
                if (ReflectionHelper.RotationSolver_Reflection.GetStateType != ReflectionHelper.RotationSolver_Reflection.StateTypeEnum.Auto)
                    ReflectionHelper.RotationSolver_Reflection.RotationAuto();
                foundRotation = true;
            }
            else
            {
                if (ReflectionHelper.RotationSolver_Reflection.GetStateType != ReflectionHelper.RotationSolver_Reflection.StateTypeEnum.Off)
                    ReflectionHelper.RotationSolver_Reflection.RotationStop();
            }
        }


        if (bmEnabled)
        {
            if (on)
            {
                BossMod_IPCSubscriber.SetRange(Plugin.Configuration.MaxDistanceToTargetFloat);
                if (!foundRotation)
                {
                    BossMod_IPCSubscriber.AddPreset("AutoDuty", Resources.AutoDutyPreset);
                    BossMod_IPCSubscriber.SetPreset("AutoDuty");
                }
                else if(this.Configuration.AutoManageBossModAISettings)
                {
                    BossMod_IPCSubscriber.AddPreset("AutoDuty Passive", Resources.AutoDutyPassivePreset);
                    BossMod_IPCSubscriber.SetPreset("AutoDuty Passive");
                }
            } 
            else if(!foundRotation || this.Configuration.AutoManageBossModAISettings)
            {
                BossMod_IPCSubscriber.DisablePresets();
            }
        }
    }

    internal void SetBMSettings(bool defaults = false)
    {
        BMRoleChecks();
        var bmr = IPCSubscriber_Common.IsReady("BossModReborn");

        if (defaults)
        {
            Configuration.FollowDuringCombat = true;
            Configuration.FollowDuringActiveBossModule = true;
            Configuration.FollowOutOfCombat = false;
            Configuration.FollowTarget = true;
            Configuration.FollowSelf = true;
            Configuration.FollowSlot = false;
            Configuration.FollowRole = false;
            Configuration.MaxDistanceToTargetRoleBased = true;
            Configuration.PositionalRoleBased = true;
        }
        Chat.ExecuteCommand($"/vbmai on");
        if(!bmr)
            Chat.ExecuteCommand($"/vbm cfg AIConfig Enable true");

        Chat.ExecuteCommand($"/vbm cfg AIConfig ForbidActions false");
        Chat.ExecuteCommand($"/vbm cfg AIConfig ForbidMovement false");
        if (bmr)
        {
            Chat.ExecuteCommand($"/vbm cfg AIConfig FollowDuringCombat {Configuration.FollowDuringCombat}");
            Chat.ExecuteCommand($"/vbm cfg AIConfig FollowDuringActiveBossModule {Configuration.FollowDuringActiveBossModule}");
            Chat.ExecuteCommand($"/vbm cfg AIConfig FollowOutOfCombat {Configuration.FollowOutOfCombat}");
            Chat.ExecuteCommand($"/vbm cfg AIConfig FollowTarget {Configuration.FollowTarget}");
            Chat.ExecuteCommand($"/vbm cfg AIConfig MaxDistanceToSlot {Configuration.MaxDistanceToSlotFloat}");
            Chat.ExecuteCommand($"/vbm cfg AIConfig DesiredPositional {Configuration.PositionalEnum}");
        }

        BossMod_IPCSubscriber.SetRange(Plugin.Configuration.MaxDistanceToTargetFloat);

        Chat.ExecuteCommand($"/vbmai follow {(Configuration.FollowSelf ? Player.Name : ((Configuration.FollowRole && !ConfigTab.FollowName.IsNullOrEmpty()) ? ConfigTab.FollowName : (Configuration.FollowSlot ? $"Slot{Configuration.FollowSlotInt}" : Player.Name)))}");

        if (!bmr && false)
        {
            Chat.ExecuteCommand($"/vbm cfg AIConfig OverridePositional true");
            Chat.ExecuteCommand($"/vbm cfg AIConfig OverrideRange true");
        }
    }

    internal void BMRoleChecks()
    {
        //RoleBased Positional
        if (PlayerHelper.IsValid && Configuration.PositionalRoleBased && Configuration.PositionalEnum != (Player.Object.ClassJob.Value.GetJobRole() == JobRole.Melee ? Positional.Rear : Positional.Any))
        {
            Configuration.PositionalEnum = (Player.Object.ClassJob.Value.GetJobRole() == JobRole.Melee ? Positional.Rear : Positional.Any);
            Configuration.Save();
        }

        //RoleBased MaxDistanceToTarget
        float maxDistanceToTarget = (Player.Object.ClassJob.Value.GetJobRole() == JobRole.Melee || Player.Object.ClassJob.Value.GetJobRole() == JobRole.Tank ? 
                                         Plugin.Configuration.MaxDistanceToTargetRoleMelee : Plugin.Configuration.MaxDistanceToTargetRoleRanged);
        if (PlayerHelper.IsValid && Configuration.MaxDistanceToTargetRoleBased && Math.Abs(this.Configuration.MaxDistanceToTargetFloat - maxDistanceToTarget) > 0.01f)
        {
            Configuration.MaxDistanceToTargetFloat = maxDistanceToTarget;
            Configuration.Save();
        }

        //RoleBased MaxDistanceToTargetAoE
        float maxDistanceToTargetAoE = (Player.Object.ClassJob.Value!.GetJobRole() == JobRole.Melee || Player.Object.ClassJob.Value!.GetJobRole() == JobRole.Tank || Player.Object.ClassJob.ValueNullable?.JobIndex == 18 ?
                                            Plugin.Configuration.MaxDistanceToTargetRoleMelee : Plugin.Configuration.MaxDistanceToTargetRoleRanged);
        if (PlayerHelper.IsValid && Configuration.MaxDistanceToTargetRoleBased && Math.Abs(this.Configuration.MaxDistanceToTargetAoEFloat - maxDistanceToTargetAoE) > 0.01f)
        {
            Configuration.MaxDistanceToTargetAoEFloat = maxDistanceToTargetAoE;
            Configuration.Save();
        }

        //FollowRole
        if (PlayerHelper.IsValid && Configuration.FollowRole && ConfigTab.FollowName != ObjectHelper.GetPartyMemberFromRole($"{Configuration.FollowRoleEnum}")?.Name.ExtractText())
            ConfigTab.FollowName = ObjectHelper.GetPartyMemberFromRole($"{Configuration.FollowRoleEnum}")?.Name.ExtractText() ?? "";
    }

    private unsafe void ActionInvoke()
    {
        if (PathAction == null) return;

        if (!TaskManager.IsBusy && !PathAction.Name.IsNullOrEmpty())
        {
            if (PathAction.Name.Equals("Boss"))
            {

                if (Configuration.DutyModeEnum == DutyMode.Regular && Svc.Party.PartyId > 0)
                {
                    Message message = new()
                    {
                        Sender = Player.Name,
                        Action =
                        [
                            new PathAction(){ Name = "Follow", Arguments = [$"null"] },
                            new PathAction(){ Name = "SetBMSettings", Arguments = [$"true"] }
                        ]
                    };

                    var messageJson = System.Text.Json.JsonSerializer.Serialize(message, BuildTab.jsonSerializerOptions);

                    //_messageBusSend.PublishAsync(Encoding.UTF8.GetBytes(messageJson));
                }
            }
            _actions.InvokeAction(PathAction);
            PathAction = new();
        }
    }

    private void GetJobAndLevelingCheck()
    {
        Job curJob = Player.Object.GetJob();
        if (curJob != JobLastKnown)
        {
            if (LevelingEnabled)
            {
                Svc.Log.Info($"{(Configuration.DutyModeEnum == DutyMode.Support || Configuration.DutyModeEnum == DutyMode.Trust) && (Configuration.DutyModeEnum == DutyMode.Support || SupportLevelingEnabled) && (Configuration.DutyModeEnum != DutyMode.Trust || TrustLevelingEnabled)} ({Configuration.DutyModeEnum == DutyMode.Support} || {Configuration.DutyModeEnum == DutyMode.Trust}) && ({Configuration.DutyModeEnum == DutyMode.Support} || {SupportLevelingEnabled}) && ({Configuration.DutyModeEnum != DutyMode.Trust} || {TrustLevelingEnabled})");
                Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(LevelingModeEnum == LevelingMode.Trust);
                if (duty != null)
                {
                    Plugin.CurrentTerritoryContent = duty;
                    MainListClicked = true;
                    ContentPathsManager.DictionaryPaths[Plugin.CurrentTerritoryContent.TerritoryType].SelectPath(out CurrentPath);
                }
                else
                {
                    Plugin.CurrentTerritoryContent = null;
                    CurrentPath = -1;
                }
            }
        }

        JobLastKnown = curJob;
    }

    private void CheckRetainerWindow()
    {
        if (AutoRetainerHelper.State == ActionState.Running || AMHelper.State == ActionState.Running || AutoRetainer_IPCSubscriber.IsBusy() || AM_IPCSubscriber.IsRunning() || Stage == Stage.Paused)
            return;

        if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            AutoRetainerHelper.CloseRetainerWindows();
    }

    private void InteractablesCheck()
    {
        if (Interactables.Count == 0) return;

        var list = Svc.Objects.Where(x => Interactables.Contains(x.DataId));

        if (!list.Any()) return;

        var index = Actions.Select((Value, Index) => (Value, Index)).Where(x => Interactables.Contains(x.Value.Arguments.Any(y => y.Any(z => z == ' ')) ? uint.Parse(x.Value.Arguments[0].Split(" ")[0]) : uint.Parse(x.Value.Arguments[0]))).First().Index;

        if (index > Indexer)
        {
            Indexer = index;
            Stage = Stage.Reading_Path;
        }
    }

    private void PreStageChecks()
    {
        if (Stage == Stage.Stopped)
            return;

        CheckRetainerWindow();

        InteractablesCheck();

        if (EzThrottler.Throttle("OverrideAFK") && States.HasFlag(PluginState.Navigating) && PlayerHelper.IsValid)
            _overrideAFK.ResetTimers();

        if (!Player.Available) return;

        if (!InDungeon && CurrentTerritoryContent != null)
            GetJobAndLevelingCheck();

        if (!PlayerHelper.IsValid || !BossMod_IPCSubscriber.IsEnabled || !VNavmesh_IPCSubscriber.IsEnabled) return;

        if (!ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled && !BossMod_IPCSubscriber.IsEnabled && !Configuration.UsingAlternativeRotationPlugin) return;

        if (CurrentTerritoryType == 0 && Svc.ClientState.TerritoryType != 0 && InDungeon)
            ClientState_TerritoryChanged(Svc.ClientState.TerritoryType);

        if (States.HasFlag(PluginState.Navigating) && Configuration.LootTreasure && (!Configuration.LootBossTreasureOnly || (PathAction?.Name == "Boss" && Stage == Stage.Action)) &&
            (treasureCofferGameObject = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure)
                                                   ?.FirstOrDefault(x => ObjectHelper.GetDistanceToPlayer(x) < 2)) != null)
        {
            BossMod_IPCSubscriber.SetRange(30f);
            ObjectHelper.InteractWithObject(this.treasureCofferGameObject, false);
        }

        if (Indexer >= Actions.Count && Actions.Count > 0 && States.HasFlag(PluginState.Navigating))
            DoneNavigating();

        if (Stage > Stage.Condition && !States.HasFlag(PluginState.Other))
            Action = Stage.ToCustomString();
    }

    public void Framework_Update(IFramework framework)
    {
        PreStageChecks();

        this.Framework_Update_InDuty(framework);

        switch (Stage)
        {
            case Stage.Reading_Path:
                StageReadingPath();
                break;
            case Stage.Moving:
                StageMoving();
                break;
            case Stage.Action:
                StageAction();
                break;
            case Stage.Waiting_For_Combat:
                StageWaitingForCombat();
                break;
            default:
                break;
        }
    }

    public event IFramework.OnUpdateDelegate Framework_Update_InDuty = _ => {};

    private void StopAndResetALL()
    {
        if (_bareModeSettingsActive != SettingsActive.None)
        {
            Configuration.EnablePreLoopActions = _bareModeSettingsActive.HasFlag(SettingsActive.PreLoop_Enabled);
            Configuration.EnableBetweenLoopActions = _bareModeSettingsActive.HasFlag(SettingsActive.BetweenLoop_Enabled);
            Configuration.EnableTerminationActions = _bareModeSettingsActive.HasFlag(SettingsActive.TerminationActions_Enabled);
            _bareModeSettingsActive = SettingsActive.None;
        }
        States = PluginState.None;
        TaskManager?.SetStepMode(false);
        TaskManager?.Abort();
        MainListClicked              = false;
        this.Framework_Update_InDuty = _ => {};
        if (!InDungeon)
            CurrentLoop = 0;
        if (Configuration.AutoManageBossModAISettings)
        {
            Chat.ExecuteCommand($"/vbmai off");
            if(!IPCSubscriber_Common.IsReady("BossModReborn"))
                Chat.ExecuteCommand($"/vbm cfg AIConfig Enable false");
        }
        SetGeneralSettings(true);
        if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin)
            SetRotationPluginSettings(false);
        if (Indexer > 0 && !MainListClicked)
            Indexer = -1;
        if (Configuration.ShowOverlay && Configuration.HideOverlayWhenStopped)
            Overlay.IsOpen = false;
        if (VNavmesh_IPCSubscriber.IsEnabled && VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
            VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
        FollowHelper.SetFollow(null);
        if (ExtractHelper.State == ActionState.Running)
            ExtractHelper.Stop();
        if (GCTurninHelper.State == ActionState.Running)
            GCTurninHelper.Stop();
        if (DesynthHelper.State == ActionState.Running)
            DesynthHelper.Stop();
        if (GotoHelper.State == ActionState.Running)
            GotoHelper.Stop();
        if (GotoInnHelper.State == ActionState.Running)
            GotoInnHelper.Stop();
        if (GotoBarracksHelper.State == ActionState.Running)
            GotoBarracksHelper.Stop();
        if (RepairHelper.State == ActionState.Running)
            RepairHelper.Stop();
        if (QueueHelper.State == ActionState.Running)
            QueueHelper.Stop();
        if (AMHelper.State == ActionState.Running)
            AMHelper.Stop();
        if (AutoRetainerHelper.State == ActionState.Running)
            AutoRetainerHelper.Stop();
        if (VNavmesh_IPCSubscriber.IsEnabled && VNavmesh_IPCSubscriber.Path_IsRunning())
            VNavmesh_IPCSubscriber.Path_Stop();
        if (MapHelper.State == ActionState.Running)
            MapHelper.StopMoveToMapMarker();
        if (GotoHousingHelper.State == ActionState.Running)
            GotoHousingHelper.Stop();
        if (ExitDutyHelper.State == ActionState.Running)
            ExitDutyHelper.Stop();
        if (AutoEquipHelper.State == ActionState.Running)
            AutoEquipHelper.Stop();
        if (CofferHelper.State == ActionState.Running)
            CofferHelper.Stop();
        if (DeathHelper.DeathState == PlayerLifeState.Revived)
            DeathHelper.Stop();
         
        Wrath_IPCSubscriber.Release();
        Action = "";
    }

    public void Dispose()
    {
        GitHubHelper.Dispose();
        StopAndResetALL();
        Svc.Framework.Update -= Framework_Update;
        Svc.Framework.Update -= SchedulerHelper.ScheduleInvoker;
        FileHelper.FileSystemWatcher.Dispose();
        FileHelper.FileWatcher.Dispose();
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
        Match        match   = RegexHelper.ArgumentParserRegex().Match(args.ToLower());
        List<string> matches = [];

        while (match.Success)
        {
            matches.Add(match.Groups[match.Groups[1].Length > 0 ? 1 : 0].Value);
            match = match.NextMatch();
        }

        string[] argsArray = matches.Count > 0 ? matches.ToArray() : [string.Empty];

        switch (argsArray[0])
        {
            case "config" or "cfg":
                if (argsArray.Length < 2)
                    OpenConfigUI();
                else if (argsArray[1].Equals("list"))
                    ConfigHelper.ListConfig();
                else
                    ConfigHelper.ModifyConfig(argsArray[1], argsArray[2..]);
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
                        GotoInnHelper.Invoke(argsArray.Length > 2 ? Convert.ToUInt32(argsArray[2]) : PlayerHelper.GetGrandCompany());
                        break;
                    case "barracks":
                        GotoBarracksHelper.Invoke();
                        break;
                    case "gcsupply":
                        GotoHelper.Invoke(PlayerHelper.GetGrandCompanyTerritoryType(PlayerHelper.GetGrandCompany()), [GCTurninHelper.GCSupplyLocation], 0.25f, 2f, false);
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
                if (PlayerHelper.GetGrandCompanyRank() > 5)
                    GCTurninHelper.Invoke();
                else
                    Svc.Log.Info("GC Turnin requires GC Rank 6 or Higher");
                break;
            case "desynth":
                DesynthHelper.Invoke();
                break;
            case "repair":
                if (InventoryHelper.CanRepair(100))
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
                IGameObject? obj = null;
                if (argsArray.Length == 2)
                    obj = Svc.Objects[int.TryParse(argsArray[1], out int index) ? index : -1] ?? null;
                else
                    obj = ObjectHelper.GetObjectByName(Svc.Targets.Target?.Name.TextValue ?? "");

                Svc.Log.Info($"{obj?.DataId}");
                ImGui.SetClipboardText($"{obj?.DataId}");
                break;
            case "moveto":
                var argss = args.Replace("moveto ", "").Split("|");
                var vs = argss[1].Split(", ");
                var v3 = new Vector3(float.Parse(vs[0]), float.Parse(vs[1]), float.Parse(vs[2]));

                GotoHelper.Invoke(Convert.ToUInt32(argss[0]), [v3], argss.Length > 2 ? float.Parse(argss[2]) : 0.25f, argss.Length > 3 ? float.Parse(argss[3]) : 0.25f);
                break;
            case "exitduty":
                _actions.ExitDuty(new());
                break;
            case "queue":
                QueueHelper.Invoke(ContentHelper.DictionaryContent.FirstOrDefault(x => x.Value.Name!.Equals(args.ToLower().Replace("queue ", ""), StringComparison.InvariantCultureIgnoreCase)).Value ?? null, Configuration.DutyModeEnum);
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
                if (States.HasFlag(PluginState.Navigating))
                {
                    Indexer++;
                    Stage = Stage.Reading_Path;
                }
                break;
            case "am":
                Configuration.UnhideAM ^= true;
                Configuration.Save();
                break;
            case "movetoflag":
                MapHelper.MoveToMapMarker();
                break;
            case "run":
                var failPreMessage = "Run Error: Incorrect usage: ";
                var failPostMessage = "\nCorrect usage: /autoduty run DutyMode TerritoryTypeInteger LoopTimesInteger (optional)BareModeBool\nexample: /autoduty run Support 1036 10 true\nYou can get the TerritoryTypeInteger from /autoduty tt name of territory (will be logged and copied to clipboard)";
                if (argsArray.Length < 4)
                {
                    Svc.Log.Info($"{failPreMessage}Argument count must be at least 3, you inputed {argsArray.Length - 1}{failPostMessage}");
                    return;
                }
                if (!Enum.TryParse(argsArray[1], true, out DutyMode dutyMode))
                {
                    Svc.Log.Info($"{failPreMessage}Argument 1 must be a DutyMode enum Type, you inputed {argsArray[1]}{failPostMessage}");
                    return;
                }
                if (!uint.TryParse(argsArray[2], out uint territoryType))
                {
                    Svc.Log.Info($"{failPreMessage}Argument 2 must be an unsigned integer, you inputed {argsArray[2]}{failPostMessage}");
                    return;
                }
                if (!int.TryParse(argsArray[3], out int loopTimes))
                {
                    Svc.Log.Info($"{failPreMessage}Argument 3 must be an integer, you inputed {argsArray[3]}{failPostMessage}");
                    return;
                }
                if (!ContentHelper.DictionaryContent.TryGetValue(territoryType, out var content))
                {
                    Svc.Log.Info($"{failPreMessage}Argument 2 value was not in our ContentList or has no Path, you inputed {argsArray[2]}{failPostMessage}");
                    return;
                }
                if (!content.DutyModes.HasFlag(dutyMode))
                {
                    Svc.Log.Info($"{failPreMessage}Argument 2 value was not of type {dutyMode}, which you inputed in Argument 1, Argument 2 value was {argsArray[2]}{failPostMessage}");
                    return;
                }
                if (!content.CanRun(trust: dutyMode == DutyMode.Trust))
                {
                    var failReason = !UIState.IsInstanceContentCompleted(content.Id) ? "You dont have it unlocked" : (!ContentPathsManager.DictionaryPaths.ContainsKey(content.TerritoryType) ? "There is no path file" : (PlayerHelper.GetCurrentLevelFromSheet() < content.ClassJobLevelRequired ? $"Your Lvl({PlayerHelper.GetCurrentLevelFromSheet()}) is less than {content.ClassJobLevelRequired}" : (InventoryHelper.CurrentItemLevel < content.ItemLevelRequired ? $"Your iLvl({InventoryHelper.CurrentItemLevel}) is less than {content.ItemLevelRequired}" : "Your trust party is not of correct levels")));
                    Svc.Log.Info($"Unable to run {content.Name}, {failReason} {content.CanTrustRun()}");
                    return;
                }

                Configuration.DutyModeEnum = dutyMode;

                Run(territoryType, loopTimes, bareMode: argsArray.Length > 4 && bool.TryParse(argsArray[4], out bool parsedBool) && parsedBool);
                break;
            case "tt":
                var tt = Svc.Data.Excel.GetSheet<TerritoryType>()?.FirstOrDefault(x => x.ContentFinderCondition.ValueNullable != null && x.ContentFinderCondition.Value.Name.ToString().Equals(args.Replace("tt ", ""), StringComparison.InvariantCultureIgnoreCase)) ?? Svc.Data.Excel.GetSheet<TerritoryType>()?.GetRow(1);
                Svc.Log.Info($"{tt?.RowId}");
                ImGui.SetClipboardText($"{tt?.RowId}");
                break;
            case "range":
                if (float.TryParse(argsArray[1], out float newRange))
                    BossMod_IPCSubscriber.SetRange(Math.Clamp(newRange, 1, 30));
                break;
            case "spew":
                IGameObject? spewObj = null;
                if (argsArray.Length == 2)
                    spewObj = ObjectHelper.GetObjectByDataId(uint.TryParse(argsArray[1], out uint dataId) ? dataId : 0);
                else
                    spewObj = ObjectHelper.GetObjectByName(Svc.Targets.Target?.Name.TextValue ?? "");

                if (spewObj == null) return;

                GameObject gObj = *spewObj.Struct();
                try { Svc.Log.Info($"Spewing Object Information for: {gObj.NameString}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"Spewing Object Information for: {gObj.GetName()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                //DrawObject: {gObj.DrawObject}\n
                //LayoutInstance: { gObj.LayoutInstance}\n
                //EventHandler: { gObj.EventHandler}\n
                //LuaActor: {gObj.LuaActor}\n
                try { Svc.Log.Info($"DefaultPosition: {gObj.DefaultPosition}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"DefaultRotation: {gObj.DefaultRotation}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"EventState: {gObj.EventState}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"EntityId {gObj.EntityId}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"LayoutId: {gObj.LayoutId}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"BaseId {gObj.BaseId}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"OwnerId: {gObj.OwnerId}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"ObjectIndex: {gObj.ObjectIndex}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"ObjectKind {gObj.ObjectKind}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"SubKind: {gObj.SubKind}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"Sex: {gObj.Sex}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"YalmDistanceFromPlayerX: {gObj.YalmDistanceFromPlayerX}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"TargetStatus: {gObj.TargetStatus}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"YalmDistanceFromPlayerZ: {gObj.YalmDistanceFromPlayerZ}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"TargetableStatus: {gObj.TargetableStatus}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"Position: {gObj.Position}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"Rotation: {gObj.Rotation}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"Scale: {gObj.Scale}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"Height: {gObj.Height}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"VfxScale: {gObj.VfxScale}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"HitboxRadius: {gObj.HitboxRadius}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"DrawOffset: {gObj.DrawOffset}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"EventId: {gObj.EventId.Id}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"FateId: {gObj.FateId}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"NamePlateIconId: {gObj.NamePlateIconId}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"RenderFlags: {gObj.RenderFlags}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"GetGameObjectId().ObjectId: {gObj.GetGameObjectId().ObjectId}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"GetGameObjectId().Type: {gObj.GetGameObjectId().Type}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"GetObjectKind: {gObj.GetObjectKind()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"GetIsTargetable: {gObj.GetIsTargetable()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"GetName: {gObj.GetName()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"GetRadius: {gObj.GetRadius()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"GetHeight: {gObj.GetHeight()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"GetDrawObject: {*gObj.GetDrawObject()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"GetNameId: {gObj.GetNameId()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"IsDead: {gObj.IsDead()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"IsNotMounted: {gObj.IsNotMounted()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"IsCharacter: {gObj.IsCharacter()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
                try { Svc.Log.Info($"IsReadyToDraw: {gObj.IsReadyToDraw()}"); } catch (Exception ex) { Svc.Log.Info($": {ex}"); };
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
