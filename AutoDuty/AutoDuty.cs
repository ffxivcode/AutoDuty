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
using AutoDuty.External;
using AutoDuty.Helpers;
using ECommons.Throttlers;
using Dalamud.Game.ClientState.Objects.Types;
using System.Linq;
using ECommons.GameFunctions;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
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

using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility.Numerics;
using Data;
using ECommons.Configuration;
using ECommons.SimpleGui;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Lumina.Excel.Sheets;
using Pictomancy;
using Serilog;
using static Data.Classes;
using TaskManager = ECommons.Automation.LegacyTaskManager.TaskManager;

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
    internal bool TrustLevelingEnabled => LevelingModeEnum.IsTrustLeveling();
    internal bool LevelingEnabled => LevelingModeEnum != LevelingMode.None;

    internal static string Name => "AutoDuty";
    internal static AutoDuty Plugin { get; private set; }
    internal bool StopForCombat = true;
    internal DirectoryInfo PathsDirectory;
    internal FileInfo AssemblyFileInfo;
    internal FileInfo ConfigFile;
    internal DirectoryInfo? DalamudDirectory;
    internal DirectoryInfo? AssemblyDirectoryInfo;

    internal Configuration Configuration => ConfigurationMain.Instance.GetCurrentConfig;
    internal WindowSystem WindowSystem = new("AutoDuty");

    public   int   Version { get; set; }
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
                case Stage.Reading_Path:
                    ConfigurationMain.MultiboxUtility.MultiboxBlockingNextStep = true;
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
                Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(value);

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

    private readonly (string[], string, Action<string[]>)[] commands;

    public AutoDuty()
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(PluginInterface, Plugin, Module.DalamudReflector, Module.ObjectFunctions);
            PictoService.Initialize(PluginInterface);

            this.isDev = PluginInterface.IsDev;

            //EzConfig.Init<ConfigurationMain>();
            EzConfig.DefaultSerializationFactory = new AutoDutySerializationFactory();
            (ConfigurationMain.Instance = EzConfig.Init<ConfigurationMain>()).Init();



            //Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            
            ConfigTab.BuildManuals();
            _configDirectory      = PluginInterface.ConfigDirectory;
            ConfigFile            = PluginInterface.ConfigFile;
            DalamudDirectory      = ConfigFile.Directory?.Parent;
            PathsDirectory        = new(_configDirectory.FullName + "/paths");
            AssemblyFileInfo      = PluginInterface.AssemblyLocation;
            AssemblyDirectoryInfo = AssemblyFileInfo.Directory;
            
            Version = 
                ((PluginInterface.IsDev     ? new Version(0,0,0, 239) :
                  PluginInterface.IsTesting ? PluginInterface.Manifest.TestingAssemblyVersion ?? PluginInterface.Manifest.AssemblyVersion : PluginInterface.Manifest.AssemblyVersion)!).Revision;

            if (!_configDirectory.Exists)
                _configDirectory.Create();
            if (!PathsDirectory.Exists)
                PathsDirectory.Create();

            TaskManager = new()
            {
                AbortOnTimeout  = false,
                TimeoutSilently = true
            };

            TrustHelper.PopulateTrustMembers();
            ContentHelper.PopulateDuties();
            RepairNPCHelper.PopulateRepairNPCs();
            FileHelper.Init();
            Patcher.Patch(startup: true);

            _overrideAFK         = new();
            _ipcProvider         = new();
            _squadronManager     = new(TaskManager);
            _variantManager      = new(TaskManager);
            _actions             = new(Plugin, TaskManager);
            BuildTab.ActionsList = _actions.ActionsList;
            OverrideCamera       = new();
            Overlay              = new();
            MainWindow           = new();
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(Overlay);

            if (Svc.ClientState.IsLoggedIn) 
                this.ClientStateOnLogin();
            
            ActiveHelper.InvokeAllHelpers();

            this.commands = [
                (["config", "cfg"], "opens config window / modifies config", argsArray =>
                                                                             {
                                                                                 if (argsArray.Length < 2)
                                                                                     this.OpenConfigUI();
                                                                                 else if (argsArray[1].Equals("list"))
                                                                                     ConfigHelper.ListConfig();
                                                                                 else
                                                                                     ConfigHelper.ModifyConfig(argsArray[1], argsArray[2..]);
                                                                             }),
                (["start"], "starts autoduty when in a Duty", _ => this.StartNavigation()),
                (["stop"], "stops everything", _ => Plugin.Stage = Stage.Stopped),
                (["pause"], "pause route", _ => Plugin.Stage     = Stage.Paused),
                (["resume"], "resume route", _ =>
                                             {
                                                 if (Plugin.Stage == Stage.Paused)
                                                 {
                                                     Plugin.TaskManager.SetStepMode(false);
                                                     Plugin.Stage  =  Plugin.PreviousStage;
                                                     Plugin.States &= ~PluginState.Paused;
                                                 }
                                             }),
                (["dataid"], "Logs and copies your target's dataid to clipboard", argsArray =>
                                                                                  {
                                                                                      IGameObject? obj = null;
                                                                                      if (argsArray.Length == 2)
                                                                                          obj = Svc.Objects[int.TryParse(argsArray[1], out int index) ? index : -1] ?? null;
                                                                                      else
                                                                                          obj = ObjectHelper.GetObjectByName(Svc.Targets.Target?.Name.TextValue ?? "");

                                                                                      Svc.Log.Info($"{obj?.DataId}");
                                                                                      ImGui.SetClipboardText($"{obj?.DataId}");
                                                                                  }),
                (["queue"], "queues duty", argsArray =>
                                           {
                                               QueueHelper.Invoke(ContentHelper.DictionaryContent.FirstOrDefault(x => x.Value.Name!.Equals(string.Join(" ", argsArray).Replace("queue ", string.Empty), StringComparison.InvariantCultureIgnoreCase)).Value ?? null,
                                                                  this.Configuration.DutyModeEnum);
                                           }),
                (["overlay"], "opens overlay", argsArray =>
                                               {
                                                   if (argsArray.Length == 1)
                                                   {
                                                       this.Configuration.ShowOverlay = true;
                                                       this.Overlay.IsOpen            = true;

                                                       if (!Plugin.States.HasAnyFlag(PluginState.Looping, PluginState.Navigating))
                                                           this.Configuration.HideOverlayWhenStopped = false;
                                                   }
                                                   else
                                                   {
                                                       switch (argsArray[1].ToLower())
                                                       {
                                                           case "lock":
                                                               if (this.Overlay.Flags.HasFlag(ImGuiWindowFlags.NoMove))
                                                                   this.Overlay.Flags -= ImGuiWindowFlags.NoMove;
                                                               else
                                                                   this.Overlay.Flags |= ImGuiWindowFlags.NoMove;
                                                               break;
                                                           case "nobg":
                                                               if (this.Overlay.Flags.HasFlag(ImGuiWindowFlags.NoBackground))
                                                                   this.Overlay.Flags -= ImGuiWindowFlags.NoBackground;
                                                               else
                                                                   this.Overlay.Flags |= ImGuiWindowFlags.NoBackground;
                                                               break;
                                                       }
                                                   }
                                               }),
                (["skipstep"], "skips the current step", _ =>
                                                         {
                                                             if (this.States.HasFlag(PluginState.Navigating))
                                                             {
                                                                 this.Indexer++;
                                                                 this.Stage = Stage.Reading_Path;
                                                             }
                                                         }),
                (["movetoflag"], "moves to the flag map marker", _ => MapHelper.MoveToMapMarker()),
                (["run"], "starts auto duty in territory type specified", argsArray =>
                                                                          {
                                                                              const string failPreMessage  = "Run Error: Incorrect usage: ";
                                                                              const string failPostMessage = "\nCorrect usage: /autoduty run DutyMode TerritoryTypeInteger LoopTimesInteger (optional)BareModeBool\nexample: /autoduty run Support 1036 10 true\nYou can get the TerritoryTypeInteger from /autoduty tt name of territory (will be logged and copied to clipboard)";
                                                                              if (argsArray.Length < 4)
                                                                              {
                                                                                  Svc.Log.Info($"{failPreMessage}Argument count must be at least 3, you inputted {argsArray.Length - 1}{failPostMessage}");
                                                                                  return;
                                                                              }

                                                                              if (!Enum.TryParse(argsArray[1], true, out DutyMode dutyMode))
                                                                              {
                                                                                  Svc.Log.Info($"{failPreMessage}Argument 1 must be a DutyMode enum Type, you inputted {argsArray[1]}{failPostMessage}");
                                                                                  return;
                                                                              }

                                                                              if (!uint.TryParse(argsArray[2], out uint territoryType))
                                                                              {
                                                                                  Svc.Log.Info($"{failPreMessage}Argument 2 must be an unsigned integer, you inputted {argsArray[2]}{failPostMessage}");
                                                                                  return;
                                                                              }

                                                                              if (!int.TryParse(argsArray[3], out int loopTimes))
                                                                              {
                                                                                  Svc.Log.Info($"{failPreMessage}Argument 3 must be an integer, you inputted {argsArray[3]}{failPostMessage}");
                                                                                  return;
                                                                              }

                                                                              if (!ContentHelper.DictionaryContent.TryGetValue(territoryType, out Content? content))
                                                                              {
                                                                                  Svc.Log.Info($"{failPreMessage}Argument 2 value was not in our ContentList or has no Path, you inputted {argsArray[2]}{failPostMessage}");
                                                                                  return;
                                                                              }

                                                                              if (!content.DutyModes.HasFlag(dutyMode))
                                                                              {
                                                                                  Svc.Log.Info($"{failPreMessage}Argument 2 value was not of type {dutyMode}, which you inputted in Argument 1, Argument 2 value was {argsArray[2]}{failPostMessage}");
                                                                                  return;
                                                                              }

                                                                              if (!content.CanRun(trust: dutyMode == DutyMode.Trust))
                                                                              {
                                                                                  string failReason = !UIState.IsInstanceContentCompleted(content.Id) ?
                                                                                                          "You dont have it unlocked" :
                                                                                                          (!ContentPathsManager.DictionaryPaths.ContainsKey(content.TerritoryType) ?
                                                                                                               "There is no path file" :
                                                                                                               (PlayerHelper.GetCurrentLevelFromSheet() < content.ClassJobLevelRequired ?
                                                                                                                    $"Your Lvl({PlayerHelper.GetCurrentLevelFromSheet()}) is less than {content.ClassJobLevelRequired}" :
                                                                                                                    (InventoryHelper.CurrentItemLevel < content.ItemLevelRequired ?
                                                                                                                         $"Your iLvl({InventoryHelper.CurrentItemLevel}) is less than {content.ItemLevelRequired}" :
                                                                                                                         "Your trust party is not of correct levels")));
                                                                                  Svc.Log.Info($"Unable to run {content.Name}, {failReason} {content.CanTrustRun()}");
                                                                                  return;
                                                                              }

                                                                              this.Configuration.DutyModeEnum = dutyMode;

                                                                              this.Run(territoryType, loopTimes, bareMode: argsArray.Length > 4 && bool.TryParse(argsArray[4], out bool parsedBool) && parsedBool);
                                                                          }),
            ];
            this.commands = this.commands.Concat(ActiveHelper.activeHelpers.Where(iah => iah.Commands != null).
                                                              Select<IActiveHelper, (string[], string, Action<string[]>)>(iah => (iah.Commands!, iah.CommandDescription!, iah.OnCommand))).ToArray();

            Svc.Commands.AddHandler("/ad", new CommandInfo(this.OnCommand));
            Svc.Commands.AddHandler(CommandName, new CommandInfo(this.OnCommand)
                                                 {
                                                     HelpMessage = string.Join("\n", this.commands.Select(tuple => $"/autoduty or /ad {string.Join(" / ", tuple.Item1)} -> {tuple.Item2}"))
                                                 });


            PluginInterface.UiBuilder.Draw         += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
            PluginInterface.UiBuilder.OpenMainUi   += OpenMainUI;

            Svc.Framework.Update             += Framework_Update;
            Svc.Framework.Update             += SchedulerHelper.ScheduleInvoker;
            Svc.ClientState.TerritoryChanged += ClientState_TerritoryChanged;
            Svc.ClientState.Login            += ClientStateOnLogin;
            Svc.Condition.ConditionChange    += Condition_ConditionChange;
            Svc.DutyState.DutyStarted        += DutyState_DutyStarted;
            Svc.DutyState.DutyWiped          += DutyState_DutyWiped;
            Svc.DutyState.DutyRecommenced    += DutyState_DutyRecommenced;
            Svc.DutyState.DutyCompleted      += DutyState_DutyCompleted;
            Svc.Log.MinimumLogLevel          =  LogEventLevel.Debug;
            PluginInterface.UiBuilder.Draw   += UiBuilderOnDraw;
        }
        catch (Exception e)
        {
            Svc.Log.Info($"Failed loading plugin\n{e}");
        }
    }

    private unsafe void OnCommand(string command, string args)
    {
        Match        match   = RegexHelper.ArgumentParserRegex().Match(args.ToLower());
        List<string> matches = [];

        while (match.Success)
        {
            matches.Add(match.Groups[match.Groups[1].Length > 0 ? 1 : 0].Value);
            match = match.NextMatch();
        }

        string[] argsArray = matches.Count > 0 ? matches.ToArray() : [string.Empty];
        string check = argsArray[0];

        Svc.Log.Debug("command with: " + args);

        foreach ((string[] keywords, _, Action<string[]> action) in commands)
            if (keywords.Any(key => check.StartsWith(key)))
            {
                Svc.Log.Debug("Activating command: " + string.Join(" / ", keywords));
                action(argsArray);
                return;
            }

        switch (argsArray[0])
        {
            case "moveto":
                var argss = args.Replace("moveto ", "").Split("|");
                var vs    = argss[1].Split(", ");
                var v3    = new Vector3(float.Parse(vs[0]), float.Parse(vs[1]), float.Parse(vs[2]));

                GotoHelper.Invoke(Convert.ToUInt32(argss[0]), [v3], argss.Length > 2 ? float.Parse(argss[2]) : 0.25f, argss.Length > 3 ? float.Parse(argss[3]) : 0.25f);
                break;
            case "spew":
                IGameObject? spewObj = null;
                spewObj = argsArray.Length == 2 ? 
                              ObjectHelper.GetObjectByDataId(uint.TryParse(argsArray[1], out uint dataId) ? dataId : 0) : 
                              ObjectHelper.GetObjectByName(Svc.Targets.Target?.Name.TextValue ?? "");

                if (spewObj == null) 
                    return;

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
                this.OpenMainUI();
                break;
        }
    }

    private void ClientStateOnLogin()
    {
        ConfigurationMain.Instance.SetProfileToDefault();

        SchedulerHelper.ScheduleAction("LoginConfig", () =>
                                                      {
                                                          if (this.Configuration.ShowOverlay &&
                                                              (!this.Configuration.HideOverlayWhenStopped || this.States.HasFlag(PluginState.Looping) ||
                                                               this.States.HasFlag(PluginState.Navigating)))
                                                              SchedulerHelper.ScheduleAction("ShowOverlay", () => this.Overlay.IsOpen = true, () => PlayerHelper.IsReady);

                                                          if (this.Configuration.ShowMainWindowOnStartup)
                                                              SchedulerHelper.ScheduleAction("ShowMainWindowOnStartup", this.OpenMainUI, () => PlayerHelper.IsReady);
                                                      }, () => ConfigurationMain.Instance.Initialized);
                                
    }

    private void UiBuilderOnDraw()
    {
        if (PlayerHelper.IsValid)
        {
            using PctDrawList? drawList = PictoService.Draw();

            if (drawList != null)
            {
                BuildTab.DrawHelper(drawList);

                if (Plugin.Configuration.PathDrawEnabled && CurrentTerritoryContent?.TerritoryType == Svc.ClientState.TerritoryType && this.Actions.Any() && (this.Indexer < 0 || !this.Actions[this.Indexer].Name.Equals("Boss") || Stage != Stage.Action))
                {
                    Vector3 lastPos         = Player.Position;
                    float   stepCountFactor = (1f / this.Configuration.PathDrawStepCount);

                    for (int index = Math.Clamp(this.Indexer, 0, this.Actions.Count-1); index < this.Actions.Count; index++)
                    {
                        PathAction action = this.Actions[index];
                        if (action.Position.LengthSquared() > 1)
                        {
                            float alpha = MathF.Max(0f, 1f - (index - this.Indexer) * stepCountFactor);

                            if (alpha > 0)
                            {
                                drawList.AddCircle(action.Position, 3, ImGui.GetColorU32(new Vector4(1f, 0.2f, 0f, alpha)), 0, 3);

                                if (index > 0)
                                    drawList.AddLine(lastPos, action.Position, 0f, ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, alpha)));
                                if (index == this.Indexer)
                                    drawList.AddLine(Player.Position, action.Position, 0, 0x00FFFFFF);

                                drawList.AddText(action.Position, ImGui.GetColorU32(new Vector4(alpha + 0.25f)), index.ToString(), 20f);
                            }

                            lastPos = action.Position;
                        }
                    }
                }
            }
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
                TaskManager.Enqueue(() => { States &= ~PluginState.Navigating; },                                                                               "Loop-RemoveNavigationState");
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

    internal unsafe void LoopTasks(bool queue = true)
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
                EnqueueActiveHelper<CofferHelper>();

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
                    TaskManager.Enqueue(() => AutoRetainerHelper.ForceStop(), "Loop-AutoRetainerStop");
                }
            }

            AutoConsume();

            AutoEquipRecommendedGear();

            if (Configuration.AutoRepair && InventoryHelper.CanRepair()) 
                EnqueueActiveHelper<RepairHelper>();

            if (Configuration.AutoExtract && QuestManager.IsQuestComplete(66174)) 
                EnqueueActiveHelper<ExtractHelper>();

            if (Configuration.AutoDesynth) 
                EnqueueActiveHelper<DesynthHelper>();

            if (Configuration.AutoGCTurnin && (!Configuration.AutoGCTurninSlotsLeftBool || InventoryManager.Instance()->GetEmptySlotsInBag() <= Configuration.AutoGCTurninSlotsLeft) && PlayerHelper.GetGrandCompanyRank() > 5)
                EnqueueActiveHelper<GCTurninHelper>();

            if (Configuration.TripleTriadEnabled)
            {
                if (Configuration.TripleTriadRegister) 
                    EnqueueActiveHelper<TripleTriadCardUseHelper>();
                if (Configuration.TripleTriadSell) 
                    EnqueueActiveHelper<TripleTriadCardSellHelper>();
            }

            if (Configuration.DiscardItems) 
                EnqueueActiveHelper<DiscardHelper>();

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

        void EnqueueActiveHelper<T>() where T : ActiveHelperBase<T>, new()
        {
            TaskManager.Enqueue(() => Svc.Log.Debug($"Enqueueing {typeof(T).Name}"), "Loop-ActiveHelper");
            TaskManager.Enqueue(() => ActiveHelperBase<T>.Invoke(), $"Loop-{typeof(T).Name}");
            TaskManager.DelayNext("Loop-Delay50", 50);
            TaskManager.Enqueue(() => ActiveHelperBase<T>.State != ActionState.Running, int.MaxValue, $"Loop-Wait-{typeof(T).Name}-Complete");
            TaskManager.Enqueue(() => PlayerHelper.IsReadyFull, "Loop-WaitIsReadyFull");
        }


        if (!queue)
        {
            LoopsCompleteActions();
            return;
        }

        if (LevelingEnabled)
        {
            Svc.Log.Info("Leveling Enabled");
            Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(this.LevelingModeEnum);
            if (duty != null)
            {
                if (this.LevelingModeEnum == LevelingMode.Support && this.Configuration.PreferTrustOverSupportLeveling && duty.ClassJobLevelRequired > 70)
                {
                    levelingModeEnum           = LevelingMode.Trust_Solo;
                    Configuration.dutyModeEnum = DutyMode.Trust;

                    Content? dutyTrust = LevelingHelper.SelectHighestLevelingRelevantDuty(this.LevelingModeEnum);

                    if (duty != dutyTrust)
                    {
                        this.levelingModeEnum        = LevelingMode.Support;
                        this.Configuration.dutyModeEnum = DutyMode.Support;
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
                TaskManager.Enqueue(() =>
                                    {
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
                                    }, "Enqueuing SystemShutdown");
                TaskManager.Enqueue(() => Chat.ExecuteCommand($"/xlkill"), "Killing the game");
            }
            else if (Configuration.TerminationMethodEnum == TerminationMode.Kill_Client)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"Killing Client"));
                if (!Configuration.TerminationKeepActive)
                {
                    Configuration.TerminationMethodEnum = TerminationMode.Do_Nothing;
                    Configuration.Save();
                }

                TaskManager.Enqueue(() => Chat.ExecuteCommand($"/xlkill"), "Killing the game");
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
            else if (Configuration.TerminationMethodEnum == TerminationMode.Start_AR_Night_Mode)
            {
                TaskManager.Enqueue(() => Svc.Log.Debug($"Starting AR Night Mode"));
                TaskManager.Enqueue(() => Chat.ExecuteCommand($"/ays night e"));
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
        if (!PlayerHelper.IsValid || !EzThrottler.Check("PathFindFailure") || Indexer == -1 || Indexer >= Actions.Count || ConfigurationMain.MultiboxUtility.MultiboxBlockingNextStep)
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

        ConfigurationMain.MultiboxUtility.MultiboxBlockingNextStep = false;

        if (PathAction.Position == Vector3.Zero)
        {
            Stage = Stage.Action;
            return;
        }

        if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && !VNavmesh_IPCSubscriber.Path_IsRunning())
        {
            Chat.ExecuteCommand("/automove off");
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

        Action = $"{Plugin.Actions[Indexer].ToCustomString()}";
        if (PartyHelper.PartyInCombat() && Plugin.StopForCombat)
        {
            if (this.Configuration is { AutoManageRotationPluginState: true, UsingAlternativeRotationPlugin: false })
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
            BossMod_IPCSubscriber.SetPositional(positional);

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

        if (PartyHelper.PartyInCombat())
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
                if (Svc.Targets.Target != null)
                {
                    var enemyCount = ObjectFunctions.GetAttackableEnemyCountAroundPoint(Svc.Targets.Target.Position, 15);

                    if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_IsRunning())
                        VNavmesh_IPCSubscriber.Path_Stop();

                    if (enemyCount > 2)
                    {
                        Svc.Log.Debug($"Changing MaxDistanceToTarget to {Configuration.MaxDistanceToTargetAoEFloat}, because enemy count = {enemyCount}");
                        BossMod_IPCSubscriber.SetRange(Configuration.MaxDistanceToTargetAoEFloat);
                    }
                    else
                    {
                        Svc.Log.Debug($"Changing MaxDistanceToTarget to {this.Configuration.MaxDistanceToTargetFloat}, because enemy count = {enemyCount}");
                        BossMod_IPCSubscriber.SetRange(this.Configuration.MaxDistanceToTargetFloat);
                    }
                }
            }
            else if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.Path_Stop();
        }
        else if (!PartyHelper.PartyInCombat() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
        {
            BossMod_IPCSubscriber.SetRange(Configuration.MaxDistanceToTargetFloat);

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

        if (this.Configuration is { AutoManageBossModAISettings: true, BM_UpdatePresetsAutomatically: true })
        {
            BossMod_IPCSubscriber.RefreshPreset("AutoDuty",         Resources.AutoDutyPreset);
            BossMod_IPCSubscriber.RefreshPreset("AutoDuty Passive", Resources.AutoDutyPassivePreset);
        }

        if (Configuration.AutoManageBossModAISettings)
            SetBMSettings();
        if (this.Configuration is { AutoManageRotationPluginState: true, UsingAlternativeRotationPlugin: false })
            SetRotationPluginSettings(true);
        if (Configuration.LootTreasure)
        {
            if (PandorasBox_IPCSubscriber.IsEnabled)
                PandorasBox_IPCSubscriber.SetFeatureEnabled("Automatically Open Chests", this.Configuration.LootMethodEnum is LootMethod.Pandora or LootMethod.All);
            this._lootTreasure = this.Configuration.LootMethodEnum is LootMethod.AutoDuty or LootMethod.All;
        }
        else
        {
            if (PandorasBox_IPCSubscriber.IsEnabled)
                PandorasBox_IPCSubscriber.SetFeatureEnabled("Automatically Open Chests", false);
            this._lootTreasure = false;
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
                    BossMod_IPCSubscriber.DisablePresets();
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
        if (YesAlready_IPCSubscriber.IsEnabled && YesAlready_IPCSubscriber.IsEnabled)
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
        if (YesAlready_IPCSubscriber.IsEnabled && _settingsActive.HasFlag(SettingsActive.YesAlready))
        {
            Svc.Log.Debug($"Setting YesAlready Enabled: {on}");
            YesAlready_IPCSubscriber.SetState(on);
        }
    }

    internal void SetRotationPluginSettings(bool on, bool ignoreConfig = false, bool ignoreTimer = false)
    {
        // Only try to set the rotation state every few seconds
        if (on && (DateTime.Now - this._lastRotationSetTime).TotalSeconds < 5 && !ignoreTimer)
            return;
        
        if(on) 
            this._lastRotationSetTime = DateTime.Now;

        if (!ignoreConfig && !this.Configuration.AutoManageRotationPluginState)
            return;

        bool? EnableWrath(bool active)
        {
            if (Wrath_IPCSubscriber.IsEnabled)
            {
                bool wrathRotationReady = true;
                if (active)
                    wrathRotationReady = Wrath_IPCSubscriber.IsCurrentJobAutoRotationReady() ||
                                         ConfigurationMain.Instance.GetCurrentConfig.Wrath_AutoSetupJobs && Wrath_IPCSubscriber.SetJobAutoReady();

                if (!active || wrathRotationReady)
                {
                    Svc.Log.Debug("Wrath rotation:" + active);
                    Wrath_IPCSubscriber.SetAutoMode(active);

                    return true;
                }
                return false;
            }
            return null;
        }

        bool? EnableRSR(bool active)
        {
            if (RSR_IPCSubscriber.IsEnabled)
            {
                Svc.Log.Debug("RSR: " + active);
                if (active)
                    RSR_IPCSubscriber.RotationAuto();
                else
                    RSR_IPCSubscriber.RotationStop();
                return true;
            }
            return null;
        }

        bool? EnableBM(bool active, bool rotation)
        {
            if (BossMod_IPCSubscriber.IsEnabled)
            {
                if (active)
                {
                    BossMod_IPCSubscriber.SetRange(Plugin.Configuration.MaxDistanceToTargetFloat);
                    if (rotation)
                        BossMod_IPCSubscriber.SetPreset("AutoDuty", Resources.AutoDutyPreset);
                    else if (ConfigurationMain.Instance.GetCurrentConfig.AutoManageBossModAISettings)
                        BossMod_IPCSubscriber.SetPreset("AutoDuty Passive", Resources.AutoDutyPassivePreset);
                    return true;
                }
                else if (!rotation || ConfigurationMain.Instance.GetCurrentConfig.AutoManageBossModAISettings)
                {
                    BossMod_IPCSubscriber.DisablePresets();
                    return true;
                }
                return false;
            }
            return null;
        }

        bool act = on;

        

        bool wrathEnabled = this.Configuration.rotationPlugin is RotationPlugin.WrathCombo or RotationPlugin.All;
        bool? wrath        = EnableWrath(on && wrathEnabled);
        if (on && wrathEnabled && wrath.HasValue)
            act = !wrath.Value;
        
        bool rsrEnabled = this.Configuration.rotationPlugin is RotationPlugin.RotationSolverReborn or RotationPlugin.All;
        bool? rsr        = EnableRSR(act && on && rsrEnabled);
        if (on && rsrEnabled && rsr.HasValue) 
            act = !rsr.Value;

        EnableBM(on, act && this.Configuration.rotationPlugin is RotationPlugin.BossMod or RotationPlugin.All);
    }

    internal void SetBMSettings(bool defaults = false)
    {
        BMRoleChecks();

        if (defaults)
        {
            Configuration.MaxDistanceToTargetRoleBased = true;
            Configuration.PositionalRoleBased = true;
        }

        BossMod_IPCSubscriber.SetMovement(true);
        BossMod_IPCSubscriber.SetRange(Plugin.Configuration.MaxDistanceToTargetFloat);
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
        float maxDistanceToTarget = (Player.Object.ClassJob.Value.GetJobRole() is JobRole.Melee or JobRole.Tank ? 
                                         Plugin.Configuration.MaxDistanceToTargetRoleMelee : Plugin.Configuration.MaxDistanceToTargetRoleRanged);
        if (PlayerHelper.IsValid && Configuration.MaxDistanceToTargetRoleBased && Math.Abs(this.Configuration.MaxDistanceToTargetFloat - maxDistanceToTarget) > 0.01f)
        {
            Configuration.MaxDistanceToTargetFloat = maxDistanceToTarget;
            Configuration.Save();
        }

        //RoleBased MaxDistanceToTargetAoE
        float maxDistanceToTargetAoE = (Player.Object.ClassJob.Value!.GetJobRole() is JobRole.Melee or JobRole.Tank or JobRole.Ranged_Physical ?
                                            Plugin.Configuration.MaxDistanceToTargetRoleMelee : Plugin.Configuration.MaxDistanceToTargetRoleRanged);
        if (PlayerHelper.IsValid && Configuration.MaxDistanceToTargetRoleBased && Math.Abs(this.Configuration.MaxDistanceToTargetAoEFloat - maxDistanceToTargetAoE) > 0.01f)
        {
            Configuration.MaxDistanceToTargetAoEFloat = maxDistanceToTargetAoE;
            Configuration.Save();
        }
    }

    private unsafe void ActionInvoke()
    {
        if (PathAction == null) return;

        if (!TaskManager.IsBusy && !PathAction.Name.IsNullOrEmpty())
        {
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
                Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(this.LevelingModeEnum);
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
        if (AutoRetainerHelper.State == ActionState.Running || AutoRetainer_IPCSubscriber.IsBusy() || AM_IPCSubscriber.IsRunning() || Stage == Stage.Paused)
            return;

        if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            while(!AutoRetainerHelper.Instance.CloseAddons());
    }

    private void InteractablesCheck()
    {
        if (Interactables.Count == 0) return;

        var list = Svc.Objects.Where(x => Interactables.Contains(x.DataId));

        if (!list.Any()) return;

        var index = this.Actions.Select((Value, Index) => (Value, Index)).First(x => this.Interactables.Contains(x.Value.Arguments.Any(y => y.Any(z => z == ' ')) ? uint.Parse(x.Value.Arguments[0].Split(" ")[0]) : uint.Parse(x.Value.Arguments[0]))).Index;

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

        if (!Player.Available) 
            return;

        if (!InDungeon && CurrentTerritoryContent != null)
            GetJobAndLevelingCheck();

        if (!PlayerHelper.IsValid || !BossMod_IPCSubscriber.IsEnabled || !VNavmesh_IPCSubscriber.IsEnabled) 
            return;

        if (!RSR_IPCSubscriber.IsEnabled && !BossMod_IPCSubscriber.IsEnabled && !Configuration.UsingAlternativeRotationPlugin) 
            return;

        if (CurrentTerritoryType == 0 && Svc.ClientState.TerritoryType != 0 && InDungeon)
            ClientState_TerritoryChanged(Svc.ClientState.TerritoryType);

        if (this.States.HasFlag(PluginState.Navigating) && this.Configuration.LootTreasure && (!this.Configuration.LootBossTreasureOnly || (this.PathAction?.Name == "Boss" && this.Stage == Stage.Action)) &&
            (this.treasureCofferGameObject = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure)
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
            BossMod_IPCSubscriber.DisablePresets();

        SetGeneralSettings(true);
        if (Configuration.AutoManageRotationPluginState && !Configuration.UsingAlternativeRotationPlugin)
            SetRotationPluginSettings(false);
        if (Indexer > 0 && !MainListClicked)
            Indexer = -1;
        if (this.Configuration is { ShowOverlay: true, HideOverlayWhenStopped: true })
            Overlay.IsOpen = false;
        if (VNavmesh_IPCSubscriber.IsEnabled && VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
            VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
        FollowHelper.SetFollow(null);

        if (VNavmesh_IPCSubscriber.IsEnabled && VNavmesh_IPCSubscriber.Path_IsRunning())
            VNavmesh_IPCSubscriber.Path_Stop();

        if (MapHelper.State == ActionState.Running)
            MapHelper.StopMoveToMapMarker();

        if (DeathHelper.DeathState == PlayerLifeState.Revived)
            DeathHelper.Stop();

        foreach (IActiveHelper helper in ActiveHelper.activeHelpers) 
            helper.StopIfRunning();

        Wrath_IPCSubscriber.Release();
        Action = "";
    }

    public void Dispose()
    {
        GitHubHelper.Dispose();
        StopAndResetALL();
        ConfigurationMain.Instance.MultiBox =  false;
        Svc.Framework.Update                -= Framework_Update;
        Svc.Framework.Update                -= SchedulerHelper.ScheduleInvoker;
        FileHelper.FileSystemWatcher.Dispose();
        FileHelper.FileWatcher.Dispose();
        WindowSystem.RemoveAllWindows();
        ECommonsMain.Dispose();
        MainWindow.Dispose();
        OverrideCamera.Dispose();
        Svc.ClientState.TerritoryChanged -= ClientState_TerritoryChanged;
        Svc.Condition.ConditionChange    -= Condition_ConditionChange;
        PictoService.Dispose();
        PluginInterface.UiBuilder.Draw   -= UiBuilderOnDraw;
        Svc.Commands.RemoveHandler(CommandName);
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
