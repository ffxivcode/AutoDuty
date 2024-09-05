global using static AutoDuty.Data.Enums;
global using static AutoDuty.Data.Extensions;
global using static AutoDuty.Data.Classes;
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
using System.Diagnostics;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Game.ClientState.Conditions;
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
    internal List<string> ListBoxPOSText { get; set; } = [];
    internal int CurrentLoop = 0;
    internal KeyValuePair<ushort, Job?> CurrentPlayerItemLevelandClassJob = new(0, null); 
    private Content? currentTerritoryContent = null;
    internal Content? CurrentTerritoryContent
    {
        get => currentTerritoryContent;
        set
        {
            CurrentPlayerItemLevelandClassJob = new(InventoryHelper.CurrentItemLevel, Player.Job);
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
            }
            _stage = value;
            Svc.Log.Debug($"Stage={EnumString(_stage)}");
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
    internal IGameObject? ClosestInteractableEventObject = null;
    internal IGameObject? ClosestTargetableBattleNpc = null;
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
    private string _action = "";
    private float _actionTollerance = 0.25f;
    private List<object> _actionParams = [];
    private List<object> _actionPosition = [];
    private readonly TinyMessageBus _messageBusSend = new("AutoDutyBroadcaster");
    private readonly TinyMessageBus _messageBusReceive = new("AutoDutyBroadcaster");
    private bool _recentlyWatchedCutscene = false;
    private bool _lootTreasure;
    private SettingsActive _settingsActive = SettingsActive.None;

    public AutoDuty()
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(PluginInterface, Plugin, Module.DalamudReflector, Module.ObjectFunctions);
            
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            ConfigTab.BuildManuals();
            _configDirectory = PluginInterface.ConfigDirectory;
            PathsDirectory = new(_configDirectory.FullName + "/paths");
            AssemblyFileInfo = PluginInterface.AssemblyLocation;
            AssemblyDirectoryInfo = AssemblyFileInfo.Directory;
            Configuration.Version = PluginInterface.Manifest.AssemblyVersion.Revision;
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
            FileHelper.OnStart();
            FileHelper.Init();
            Chat = new();
            _overrideAFK = new();
            _ipcProvider = new();
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

            if (Configuration.ShowOverlay && (!Configuration.HideOverlayWhenStopped || States.HasFlag(PluginState.Looping) || States.HasFlag(PluginState.Navigating)))
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
        }
        catch (Exception e) { Svc.Log.Info($"Failed loading plugin\n{e}");
        }
    }

    private void DutyState_DutyStarted(object? sender, ushort e) => DutyState = DutyState.DutyStarted;
    private void DutyState_DutyWiped(object? sender, ushort e) => DutyState = DutyState.DutyWiped;
    private void DutyState_DutyRecommenced(object? sender, ushort e) => DutyState = DutyState.DutyRecommenced;
    private void DutyState_DutyCompleted(object? sender, ushort e) => DutyState = DutyState.DutyComplete;

    private void MessageReceived(string messageJson)
    {
        if (!Player.Available || messageJson.IsNullOrEmpty())
            return;

        var message = System.Text.Json.JsonSerializer.Deserialize<Message>(messageJson);

        if (message == null) return;

        if (message.Sender == Player.Name || message.Action.Count == 0 || !Svc.Party.Any(x => x.Name.ExtractText() == message.Sender))
            return;

        message.Action.Each(x => _actions.InvokeAction(x.Item1, [x.Item2]));
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

    private unsafe bool StopLoop => Configuration.EnableTerminationActions && (CurrentTerritoryContent == null ||
                                    (Configuration.StopLevel && Player.Level >= Configuration.StopLevelInt) || 
                                    (Configuration.StopNoRestedXP && AgentHUD.Instance()->ExpRestedExperience == 0) || 
                                    (Configuration.StopItemQty && Configuration.StopItemQtyItemDictionary.Any(x => InventoryManager.Instance()->GetInventoryItemCount(x.Key) >= x.Value.Value)));

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
       
        CurrentTerritoryType = t;
        MainListClicked = false;

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
                TaskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "Loop-WaitPlayerReady");
                if (Configuration.EnableBetweenLoopActions)
                {
                    TaskManager.Enqueue(() => { Action = $"Waiting {Configuration.WaitTimeBeforeAfterLoopActions}s"; }, "Loop-WaitTimeBeforeAfterLoopActionsActionSet");
                    TaskManager.DelayNext("Loop-WaitTimeBeforeAfterLoopActions", Configuration.WaitTimeBeforeAfterLoopActions * 1000);
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
                TaskManager.Enqueue(() => Svc.Log.Debug($"Loops Done"), "Loop-Debug");
                TaskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "Loop-WaitPlayerReady");
                TaskManager.Enqueue(() => Svc.Log.Debug($"Loop {CurrentLoop} == {Configuration.LoopTimes} we are done Looping, Invoking LoopsCompleteActions"), "Loop-Debug");
                TaskManager.Enqueue(() => LoopsCompleteActions(), "Loop-LoopCompleteActions");
            }
        }
    }
    
    private void Condition_ConditionChange(ConditionFlag flag, bool value)
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
        if (Stage != Stage.Dead && Stage != Stage.Revived && !_recentlyWatchedCutscene && !Conditions.IsWatchingCutscene && flag != ConditionFlag.WatchingCutscene && flag != ConditionFlag.WatchingCutscene78 && flag != ConditionFlag.OccupiedInCutSceneEvent && Stage != Stage.Action && value && States.HasFlag(PluginState.Navigating) && (flag == ConditionFlag.BetweenAreas || flag == ConditionFlag.BetweenAreas51 || flag == ConditionFlag.Jumping61))
        {
            Svc.Log.Info($"Condition_ConditionChange: Indexer Increase and Change Stage to Condition");
            Indexer++;
            VNavmesh_IPCSubscriber.Path_Stop();
            Stage = Stage.Condition;
        }
        if (Conditions.IsWatchingCutscene || flag == ConditionFlag.WatchingCutscene || flag == ConditionFlag.WatchingCutscene78 || flag == ConditionFlag.OccupiedInCutSceneEvent)
        {
            _recentlyWatchedCutscene = true;
            SchedulerHelper.ScheduleAction("RecentlyWatchedCutsceneTimer", () => _recentlyWatchedCutscene = false, 5000);
        }
    }

    public void Run(uint territoryType = 0, int loops = 0, bool bareMode = false)
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
        TaskManager.Abort();
        Svc.Log.Info($"Running {CurrentTerritoryContent.Name} {Configuration.LoopTimes} Times");
        if (!InDungeon)
        {
            if (Configuration.EnablePreLoopActions)
            {
                if (Configuration.ExecuteCommandsPreLoop)
                {
                    TaskManager.Enqueue(() => Svc.Log.Debug($"ExecutingCommandsPreLoop, executing {Configuration.CustomCommandsTermination.Count} commands"));
                    Configuration.CustomCommandsPreLoop.Each(x => TaskManager.Enqueue(() => Chat.ExecuteCommand(x), "Run-ExecuteCommandsPreLoop"));
                }

                if (Configuration.AutoConsume)
                {
                    TaskManager.Enqueue(() => Svc.Log.Debug($"AutoConsume PreLoop Action"));
                    Configuration.AutoConsumeItemsList.Each(x =>
                    {
                        if (Configuration.AutoConsumeIgnoreStatus)
                            TaskManager.Enqueue(() => InventoryHelper.UseItemUntilAnimationLock(x.Value.ItemId, x.Value.CanBeHq), $"Run-AutoConsume({x.Value.Name})");
                        else
                            TaskManager.Enqueue(() => InventoryHelper.UseItemUntilStatus(x.Value.ItemId, x.Key, x.Value.CanBeHq), $"Run-AutoConsume({x.Value.Name})");
                        TaskManager.Enqueue(() => ObjectHelper.IsReadyFull, "Run-WaitPlayerIsReadyFull");
                    });
                }

                if (Configuration.AutoRepair && InventoryHelper.CanRepair())
                {
                    TaskManager.Enqueue(() => Svc.Log.Debug($"AutoRepair PreLoop Action"));
                    TaskManager.Enqueue(() => RepairHelper.Invoke(), "Run-AutoRepair");
                    TaskManager.DelayNext("Run-AutoRepairDelay50", 50);
                    TaskManager.Enqueue(() => RepairHelper.State != ActionState.Running, int.MaxValue, "Run-WaitAutoRepairComplete");
                    TaskManager.Enqueue(() => ObjectHelper.IsReadyFull, "Run-WaitAutoRepairIsReadyFull");
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
        TaskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, "Run-WaitDutyStarted");
        TaskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "Run-WaitNavIsReady");
        TaskManager.Enqueue(() => Svc.Log.Debug($"Start Navigation"));
        TaskManager.Enqueue(() => StartNavigation(true), "Run-StartNavigation");
        CurrentLoop = 1;
    }

    private unsafe void LoopTasks()
    {
        if (CurrentTerritoryContent == null) return;

        if (Configuration.EnableBetweenLoopActions)
        {
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

            if (Configuration.AutoConsume)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"AutoConsume Between Loop Actions"));
                Configuration.AutoConsumeItemsList.Each(x =>
                {
                    if (Configuration.AutoConsumeIgnoreStatus)
                        TaskManager.Enqueue(() => InventoryHelper.UseItemUntilAnimationLock(x.Value.ItemId, x.Value.CanBeHq), $"Loop-AutoConsume({x.Value.Name})");
                    else
                        TaskManager.Enqueue(() => InventoryHelper.UseItemUntilStatus(x.Value.ItemId, x.Key, x.Value.CanBeHq), $"Loop-AutoConsume({x.Value.Name})");
                    TaskManager.Enqueue(() => ObjectHelper.IsReadyFull, "Loop-WaitPlayerIsReadyFull");
                });
            }

            if (Configuration.AutoEquipRecommendedGear)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"AutoEquipRecommendedGear Between Loop Action"));
                TaskManager.Enqueue(() => AutoEquipHelper.Invoke(), "Loop-AutoEquip");
                TaskManager.DelayNext("Loop-Delay50", 50);
                TaskManager.Enqueue(() => AutoEquipHelper.State != ActionState.Running, int.MaxValue, "Loop-WaitAutoEquipComplete");
                TaskManager.Enqueue(() => ObjectHelper.IsReadyFull, "Loop-WaitANotIsOccupied");
            }

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
                TaskManager.Enqueue(() => ObjectHelper.IsReadyFull, "Loop-WaitIsReadyFull");
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

            if (Configuration.AutoGCTurnin && (!Configuration.AutoGCTurninSlotsLeftBool || InventoryManager.Instance()->GetEmptySlotsInBag() <= Configuration.AutoGCTurninSlotsLeft) && ObjectHelper.GrandCompanyRank > 5)
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
        if (LevelingEnabled)
        {
            Svc.Log.Info("Leveling Enabled");
            Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(LevelingModeEnum == LevelingMode.Trust);
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
        TaskManager.Enqueue(() => Svc.Log.Debug($"Registering New Loop"));
        Queue(CurrentTerritoryContent);
        TaskManager.Enqueue(() => Svc.Log.Debug($"Incrementing LoopCount, Setting Action Var, Wait for CorrectTerritory, PlayerIsValid, DutyStarted, and NavIsReady"));
        TaskManager.Enqueue(() => CurrentLoop++, "Loop-IncrementCurrentLoop");
        TaskManager.Enqueue(() => { Action = $"Looping: {CurrentTerritoryContent.Name} {CurrentLoop} of {Configuration.LoopTimes}"; }, "Loop-SetAction");
        TaskManager.Enqueue(() => Svc.ClientState.TerritoryType == CurrentTerritoryContent.TerritoryType, int.MaxValue, "Loop-WaitCorrectTerritory");
        TaskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "Loop-WaitPlayerValid");
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

                TaskManager.Enqueue(() => ObjectHelper.IsReady);
                TaskManager.DelayNext(2000);
                TaskManager.Enqueue(() => Chat.ExecuteCommand($"/logout"));
                TaskManager.Enqueue(() => AddonHelper.ClickSelectYesno());
                TaskManager.Enqueue(() => States &= ~PluginState.Looping);
                TaskManager.Enqueue(() => CurrentLoop = 0);
                TaskManager.Enqueue(() => Stage = Stage.Stopped);
            }
            else if (Configuration.TerminationMethodEnum == TerminationMode.Start_AR_Multi_Mode)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"Starting AR Multi Mode"));
                TaskManager.Enqueue(() => Chat.ExecuteCommand($"/ays multi"));
                TaskManager.Enqueue(() => States &= ~PluginState.Looping);
                TaskManager.Enqueue(() => CurrentLoop = 0);
                TaskManager.Enqueue(() => Stage = Stage.Stopped);
            }
        }
        Svc.Log.Debug($"Removing Looping, Setting CurrentLoop to 0, and Setting Stage to Stopped");
        States &= ~PluginState.Looping;
        CurrentLoop = 0;
        SchedulerHelper.ScheduleAction("SetStageStopped", () => Stage = Stage.Stopped, 1);
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
        TaskManager.Enqueue(() => !ObjectHelper.IsValid, "Queue-WaitNotValid");
        TaskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "Queue-WaitValid");
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
        Chat.ExecuteCommand($"/vbm cfg AIConfig Enable true");
        if (IPCSubscriber_Common.IsReady("BossModReborn"))
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
            if (ExitDutyHelper.State != ActionState.Running)
                ExitDuty();
            if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin)
                SetRotationPluginSettings(false);
            
            Chat.ExecuteCommand($"/vbmai off");
            Chat.ExecuteCommand($"/vbm cfg AIConfig Enable false");
            States &= ~PluginState.Navigating;
        }
        else
            Stage = Stage.Stopped;
    }

    private void GetGeneralSettings()
    {
        if (Configuration.AutoManageVnavAlignCamera && VNavmesh_IPCSubscriber.IsEnabled && VNavmesh_IPCSubscriber.Path_GetAlignCamera())
            _settingsActive |= SettingsActive.Vnav_Align_Camera_Off;

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
        if (!Configuration.AutoManageRotationPluginState && !ignoreConfig) return;

        if (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
        {
            if (on)
            {
                if (ReflectionHelper.RotationSolver_Reflection.GetStateType != ReflectionHelper.RotationSolver_Reflection.StateTypeEnum.Auto)
                    ReflectionHelper.RotationSolver_Reflection.RotationAuto();
            }
            else if (ReflectionHelper.RotationSolver_Reflection.GetStateType != ReflectionHelper.RotationSolver_Reflection.StateTypeEnum.Off)
                ReflectionHelper.RotationSolver_Reflection.RotationStop();
        }
        else if (BossMod_IPCSubscriber.IsEnabled)
        {
            if (on)
            {
                //check if our preset does not exist
                if (BossMod_IPCSubscriber.Presets_Get("AutoDuty") == null)
                {
                    //load it
                    Svc.Log.Debug($"AutoDuty Preset Loaded: {BossMod_IPCSubscriber.Presets_Create(Resources.AutoDutyPreset, true)}");
                }

                //set it as the active preset for both
                if (BossMod_IPCSubscriber.Presets_GetActive() != "AutoDuty")
                    BossMod_IPCSubscriber.Presets_SetActive("AutoDuty");

                if (BossMod_IPCSubscriber.AI_GetPreset() != "AutoDuty")
                    BossMod_IPCSubscriber.AI_SetPreset("AutoDuty");
            }
            else
            {
                //set disabled as preset
                if (!BossMod_IPCSubscriber.Presets_GetForceDisabled())
                    BossMod_IPCSubscriber.Presets_SetForceDisabled();
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

        Chat.ExecuteCommand($"/vbm cfg AIConfig ForbidActions false");
        Chat.ExecuteCommand($"/vbm cfg AIConfig ForbidMovement false");
        Chat.ExecuteCommand($"/vbm cfg AIConfig FollowDuringCombat {Configuration.FollowDuringCombat}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig FollowDuringActiveBossModule {Configuration.FollowDuringActiveBossModule}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig FollowOutOfCombat {Configuration.FollowOutOfCombat}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig FollowTarget {Configuration.FollowTarget}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig MaxDistanceToTarget {Configuration.MaxDistanceToTargetFloat}");
        Chat.ExecuteCommand($"/vbm cfg AIConfig MaxDistanceToSlot {Configuration.MaxDistanceToSlotFloat}");
        Chat.ExecuteCommand($"/vbmai follow {(Configuration.FollowSelf ? Player.Name : ((Configuration.FollowRole && !ConfigTab.FollowName.IsNullOrEmpty()) ? ConfigTab.FollowName : (Configuration.FollowSlot ? $"Slot{Configuration.FollowSlotInt}" : Player.Name)))}");

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

    private unsafe void ActionInvoke()
    {
        if (!TaskManager.IsBusy && !_action.IsNullOrEmpty())
        {
            if (_action.Equals("Boss"))
            {
                if (Configuration.DutyModeEnum == DutyMode.Regular && Svc.Party.PartyId > 0)
                {
                    Message message = new()
                    {
                        Sender = Player.Name,
                        Action =
                        [
                            ("Follow", "null"),
                            ("SetBMSettings", "true")
                        ]
                    };

                    var messageJson = System.Text.Json.JsonSerializer.Serialize(message);

                    _messageBusSend.PublishAsync(Encoding.UTF8.GetBytes(messageJson));
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

    public void Framework_Update(IFramework framework)
    {
        if (Stage == Stage.Stopped)
            return;

        if (EzThrottler.Throttle("OverrideAFK") && States.HasFlag(PluginState.Navigating) && ObjectHelper.IsValid)
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

        if (States.HasFlag(PluginState.Navigating) && Configuration.LootTreasure && (!Configuration.LootBossTreasureOnly || (_action == "Boss" && Stage == Stage.Action)) && (treasureCofferGameObject = ObjectHelper.GetObjectsByObjectKind(ObjectKind.Treasure)?.FirstOrDefault(x => ObjectHelper.GetDistanceToPlayer(x) < 2)) != null)
            ObjectHelper.InteractWithObject(treasureCofferGameObject, false);

        if (Indexer >= ListBoxPOSText.Count && ListBoxPOSText.Count > 0 && States.HasFlag(PluginState.Navigating))
            DoneNavigating();

        if (Stage > Stage.Condition && !States.HasFlag(PluginState.Other))
            Action = EnumString(Stage);
        
        switch (Stage)
        {
            case Stage.Reading_Path:
                if (!ObjectHelper.IsValid || !EzThrottler.Check("PathFindFailure") || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"{(ListBoxPOSText.Count >= Indexer ? Plugin.ListBoxPOSText[Indexer] : "")}";
                //Backwards Compatibility
                if (ListBoxPOSText[Indexer].Contains('|') || ListBoxPOSText[Indexer].StartsWith("<--", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (ListBoxPOSText[Indexer].StartsWith("<--", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Svc.Log.Debug($"Skipping path entry {ListBoxPOSText[Indexer]} because it is a comment");
                        Indexer++;
                        return;
                    }

                    _actionPosition = [];
                    _actionParams = [.. ListBoxPOSText[Indexer].Split('|')];
                    _action = (string)_actionParams[0];
                    _actionTollerance = _action == "Interactable" ? 2f : 0.25f;

                    if (_action.StartsWith("Unsynced", StringComparison.InvariantCultureIgnoreCase)) 
                    {
                        if (!Configuration.Unsynced && Configuration.DutyModeEnum.EqualsAny(DutyMode.Raid, DutyMode.Regular, DutyMode.Trial))
                        {
                            Svc.Log.Debug($"Skipping path entry {ListBoxPOSText[Indexer]} because we are not unsynced");
                            Indexer++;
                            return;
                        }
                        else
                            _action = _action.Remove(0, 8);
                    }

                    if (_action.StartsWith("Synced", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (Configuration.Unsynced)
                        {
                            Svc.Log.Debug($"Skipping path entry {ListBoxPOSText[Indexer]} because we are not synced");
                            Indexer++;
                            return;
                        }
                        else
                            _action = _action.Remove(0, 6);
                    }

                    

                    if ((SkipTreasureCoffer || !Configuration.LootTreasure || Configuration.LootBossTreasureOnly) && _action.Equals("TreasureCoffer", StringComparison.InvariantCultureIgnoreCase))
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
                        States &= ~PluginState.Looping;
                        States &= ~PluginState.Navigating;
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

                if (Configuration.DutyModeEnum == DutyMode.Regular && Svc.Party.PartyId > 0)
                {
                    Message message = new()
                    {
                        Sender = Player.Name,
                        Action =
                        [
                            ("Follow", $"{Player.Name}")
                        ]
                    };

                    var messageJson = System.Text.Json.JsonSerializer.Serialize(message);

                    _messageBusSend.PublishAsync(Encoding.UTF8.GetBytes(messageJson));
                }
                
                Action = $"{Plugin.ListBoxPOSText[Indexer]}";
                if (Player.Object.InCombat() && Plugin.StopForCombat)
                {
                    if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin)
                        SetRotationPluginSettings(true);
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

                if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin && !Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent])
                    SetRotationPluginSettings(true);

                if (!TaskManager.IsBusy)
                {
                    Stage = Stage.Reading_Path;
                    Indexer++;
                    return;
                }
                break;
            case Stage.Waiting_For_Combat:
                if (!EzThrottler.Throttle("CombatCheck", 250) || !ObjectHelper.IsReady || Indexer == -1 || Indexer >= ListBoxPOSText.Count)
                    return;

                Action = $"Waiting For Combat";

                if(ReflectionHelper.Avarice_Reflection.PositionalChanged(out Positional positional))
                    Chat.ExecuteCommand($"/vbm cfg AIConfig DesiredPositional {positional}");

                if (_action.Equals("Boss") && _actionPosition.Count > 0 && ObjectHelper.GetDistanceToPlayer((Vector3)_actionPosition[0]) < 50)
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
                    if (Svc.Targets.Target == null)
                    {
                        //find and target closest attackable npc, if we are not targeting
                        var gos = ObjectHelper.GetObjectsByObjectKind(ObjectKind.BattleNpc)?.FirstOrDefault(o => ObjectFunctions.GetNameplateKind(o) is NameplateKind.HostileEngagedSelfUndamaged or NameplateKind.HostileEngagedSelfDamaged && ObjectHelper.GetBattleDistanceToPlayer(o) <= 75);

                        if (gos != null)
                            Svc.Targets.Target = gos;
                    }
                    if (Configuration.AutoManageBossModAISettings)
                    {
                        var gotMDT = float.TryParse(BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget"], false)[0], out float floatMDT);

                        if (!gotMDT)
                            return;

                        if (Svc.Targets.Target != null)
                        {
                            var enemyCount = ObjectFunctions.GetAttackableEnemyCountAroundPoint(Svc.Targets.Target.Position, 15);
                            
                            if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_IsRunning())
                                VNavmesh_IPCSubscriber.Path_Stop();

                            if (enemyCount > 2 && floatMDT != Configuration.MaxDistanceToTargetAoEFloat)
                            {
                                Svc.Log.Debug($"Changing MaxDistanceToTarget to {Configuration.MaxDistanceToTargetAoEFloat}, because BM MaxDistanceToTarget={floatMDT} and enemy count = {enemyCount}");
                                BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetAoEFloat}"], false);
                            }
                            else if (enemyCount <3 && floatMDT != Configuration.MaxDistanceToTargetFloat)
                            {
                                Svc.Log.Debug($"Changing MaxDistanceToTarget to {Configuration.MaxDistanceToTargetFloat}, because BM MaxDistanceToTarget={floatMDT} and enemy count = {enemyCount}");
                                BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"], false);
                                BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"], false);
                            }
                        }
                    }
                    else if(!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_IsRunning())
                        VNavmesh_IPCSubscriber.Path_Stop();
                }
                else if (!Player.Object.InCombat() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                {
                    if (Configuration.AutoManageBossModAISettings)
                    {
                        var gotMDT = float.TryParse(BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget"], false)[0], out float floatMDT);

                        if (gotMDT && floatMDT != Configuration.MaxDistanceToTargetFloat)
                        {
                            Svc.Log.Debug($"Changing MaxDistanceToTarget to {Configuration.MaxDistanceToTargetFloat}, because BM  MaxDistanceToTarget={floatMDT}");
                            BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"], false);
                            BossMod_IPCSubscriber.Configuration(["AIConfig", "MaxDistanceToTarget", $"{Configuration.MaxDistanceToTargetFloat}"], false);
                        }
                    }

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
        States = PluginState.None;
        TaskManager?.SetStepMode(false);
        TaskManager?.Abort();
        MainListClicked = false;
        CurrentLoop = 0;
        Chat.ExecuteCommand($"/vbmai off");
        Chat.ExecuteCommand($"/vbm cfg AIConfig Enable false");
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
        if (DeathHelper.DeathState == PlayerLifeState.Revived)
            DeathHelper.Stop();
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
                if (argsArray.Length <= 3 || !UInt32.TryParse(argsArray[1], out uint territoryType) || !Int32.TryParse(argsArray[2], out int loopTimes))
                {
                    Svc.Log.Info($"Run Error: Incorrect Usage\ncorrect use /autoduty run TerritoryTypeInteger LoopTimesInteger (optional) BareModeBool\nexample: /autoduty run 1036 10 true\nYou can get the TerritoryTypeInteger from /autoduty tt name of territory (will be logged and copied to clipboard)");
                    return;
                }

                Run(territoryType, loopTimes, argsArray.Length > 3 && bool.TryParse(argsArray[3], out bool parsedBool) && parsedBool);
                break;
            case "tt":
                var tt = Svc.Data.Excel.GetSheet<TerritoryType>()?.FirstOrDefault(x => x.ContentFinderCondition.Value != null && x.ContentFinderCondition.Value.Name.RawString.Equals(args.Replace("tt ", ""), StringComparison.InvariantCultureIgnoreCase)) ?? Svc.Data.Excel.GetSheet<TerritoryType>()?.GetRow(1);
                Svc.Log.Info($"{tt?.RowId}");
                ImGui.SetClipboardText($"{tt?.RowId}");
                break;
            case "spew":
                var targetObject = ObjectHelper.GetObjectByName(Svc.Targets.Target?.Name.TextValue ?? "");
                Svc.Log.Info($"DataId: {targetObject?.DataId} EntityId: {targetObject?.EntityId} GameObjectId: {targetObject?.GameObjectId}");
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
