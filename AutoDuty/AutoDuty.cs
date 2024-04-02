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

namespace AutoDuty;

// TODO: Need to add options to who they follow in combat.
// need to add shorcut checking on death
// Need to add 4-Box capability and add dungeons not in support
// Need to expand AutoRepair to include check for level and stuff to see if you are eligible for self repair. and check for dark matter
// Add auto GC turn in and Auto desynth
// make config saving per character
// Add auto treasure coffer based on distance and check closest point on the pathfind list and go get it from there.

public class AutoDuty : IDalamudPlugin
{
    public List<string> ListBoxPOSText { get; set; } = [];
    public int LoopTimes = 1;
    public int CurrentLoop = 0;
    public int CurrentTerritoryIndex;
    public string Name => "AutoDuty";
    public static AutoDuty Plugin { get; private set; }
    public bool StopForCombat = true;
    public DirectoryInfo PathsDirectory;
    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("AutoDuty");
    public int Stage = 0;
    public int Indexer = 0;
    public bool MainListClicked = false;
    public bool Started = false;
    public bool Running = false;
    public PlayerCharacter? Player = null;
    public OverrideCamera OverrideCamera;
    public bool Support = false;
    public bool Trust = false;
    public bool Squadron = false;
    public bool Regular = false;
    public bool Repairing = false;
    public bool Goto = false;
    public event EventHandler? DutyWiped;
    public event EventHandler? DutyRecommenced;

    private const string CommandName = "/autoduty";
    private MainWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private DirectoryInfo _configDirectory;
    private ActionsManager _actions;
    private Chat _chat;
    private TaskManager _taskManager;
    private RepairManager _repairManager;
    private ContentManager _contentManager;
    private GotoManager _gotoManager;
    private DutySupportManager _dutySupportManager;
    private TrustManager _trustManager;
    private SquadronManager _squadronManager;
    private OverrideAFK _overrideAFK;
    private OverrideMovement _overrideMovement;

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
            _chat = new();
            _contentManager = new();
            _overrideMovement = new();
            _contentManager.PopulateDuties();
            _repairManager = new(_taskManager);
            _gotoManager = new(_taskManager);
            _dutySupportManager = new(_taskManager);
            _trustManager = new(_taskManager);
            _squadronManager = new(_taskManager);
            _actions = new(this, _chat, _taskManager, _overrideMovement);
            MainWindow = new(this, _actions.ActionsList, _taskManager, _contentManager);
            ConfigWindow = new(this);
            OverrideCamera = new();

            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(ConfigWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "\n/autoduty->opens main window\n" +
                "/autoduty config or cfg->opens config window\n"
            });

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
            pluginInterface.UiBuilder.OpenMainUi += OpenMainUI;

            Svc.Framework.Update += Framework_Update;
            Svc.DutyState.DutyWiped += DutyState_DutyWiped;
            Svc.DutyState.DutyRecommenced += DutyState_DutyRecommenced;
            Svc.DutyState.DutyStarted += DutyState_DutyStarted;
            Svc.DutyState.DutyCompleted += DutyState_DutyCompleted;
            Svc.ClientState.TerritoryChanged += ClientState_TerritoryChanged;
            Svc.Condition.ConditionChange += Condition_ConditionChange;
        }
        catch (Exception e) { Svc.Log.Info($"Failed loading plugin\n{e}");
        }
    }

    private void DutyState_DutyCompleted(object? sender, ushort e)
    {
        Svc.Log.Info($"DutyCompleted {e}");
    }

    private void DutyState_DutyStarted(object? sender, ushort e)
    {
        Svc.Log.Info($"DutyDutyStarted {e}");
    }

    private void DutyState_DutyRecommenced(object? sender, ushort e)
    {
        try
        {
            Svc.Log.Info($"DutyRecommenced {e}");
            DutyRecommenced?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Unhandled exception when invoking DutyEventService.DutyRecommenced");
        }
    }

    private void DutyState_DutyWiped(object? sender, ushort e)
    {
        try
        {
            Svc.Log.Info($"DutyWiped {e}");
            DutyWiped?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Unhandled exception when invoking DutyEventService.DutyWiped");
        }
    }

    private void ClientState_TerritoryChanged(ushort t)
    {
        if (t == 0 || !Running || Repairing || Goto)
            return;

        if (t != _contentManager.ListContent[CurrentTerritoryIndex].TerritoryType)
        {
            if (CurrentLoop < LoopTimes)
            {
                _taskManager.Enqueue(() => Stage = 99, "Loop");
                _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Loop");
                _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "Loop");
                _taskManager.Enqueue(_repairManager.Repair, int.MaxValue, "Loop");
                if (Trust)
                    _taskManager.Enqueue(() => _trustManager.RegisterTrust(_contentManager.ListContent[CurrentTerritoryIndex]), int.MaxValue, "Loop");
                else if (Support)
                    _taskManager.Enqueue(() => _dutySupportManager.RegisterDutySupport(_contentManager.ListContent[CurrentTerritoryIndex]), int.MaxValue, "Loop");
                else if (Squadron)
                {
                    _gotoManager.Goto(true, false);
                    _taskManager.Enqueue(() => _squadronManager.RegisterSquadron(_contentManager.ListContent[CurrentTerritoryIndex]), int.MaxValue, "Loop");
                }
                _taskManager.Enqueue(() => CurrentLoop++, "Loop");
            }
            else
            {
                Running = false;
                CurrentLoop = 0;
                Stage = 0;
                MainWindow.SetWindowSize(425, 375);
            }
        }
    }

    private void Condition_ConditionChange(Dalamud.Game.ClientState.Conditions.ConditionFlag flag, bool value)
    {
        //Svc.Log.Debug($"{flag} : {value}");
        if (value && Started && (flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas || flag == Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51))
        {
            Stage = 1;
            VNavmesh_IPCSubscriber.Path_Stop();
        }
    }

    public void Run(int clickedDuty)
    {
        Stage = 99;
        Svc.Log.Info($"Running {_contentManager.ListContent[clickedDuty].Name} {LoopTimes} Times");
        Running = true;
        CurrentTerritoryIndex = clickedDuty;
        if (!Squadron)
            _gotoManager.Goto(Configuration.RetireToBarracksBeforeLoops, Configuration.RetireToInnBeforeLoops);
        _repairManager.Repair();
        if (Trust)
            _trustManager.RegisterTrust(_contentManager.ListContent[clickedDuty]);
        else if (Support)
            _dutySupportManager.RegisterDutySupport(_contentManager.ListContent[clickedDuty]);
        else if (Squadron)
        {
            _gotoManager.Goto(true, false);
            _squadronManager.RegisterSquadron(_contentManager.ListContent[clickedDuty]);
        }
        CurrentLoop = 1;
    }

    public void StartNavigation()
    {
        Stage = 1;
        Started = true;
        ExecSkipTalk.IsEnabled = true;
        _chat.ExecuteCommand($"/vbmai on");
        _chat.ExecuteCommand($"/rotation auto");
        Svc.Log.Info("Starting Navigation");
    }

    public void Framework_Update(IFramework framework)
    {
        if (EzThrottler.Throttle("OverrideAFK") && Started && ObjectHelper.IsValid)
            _overrideAFK.ResetTimers();

        if ((Player = Svc.ClientState.LocalPlayer) == null)
            return;

        if (!BossMod_IPCSubscriber.IsEnabled)
            return;

        if (!VNavmesh_IPCSubscriber.IsEnabled)
            return;

        if (!ObjectHelper.IsValid)
            return;

        //if (ExcelTerritoryHelper.Get(Svc.ClientState.TerritoryType).TerritoryIntendedUse != 3)
            //return;

        if (Indexer >= ListBoxPOSText.Count && ListBoxPOSText.Count > 0)
        {
            Stage = 0;
            Indexer = 0;
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
                    VNavmesh_IPCSubscriber.Path_Stop();
                }
                else if (Started)
                    Started = false;
                if (VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
                    VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
                if (_taskManager.IsBusy)
                    _taskManager.Abort();
                if (Indexer > 0 && !MainListClicked)
                    Indexer = 0;
                if (ExecSkipTalk.IsEnabled)
                    ExecSkipTalk.IsEnabled = false;
                break;
            //We are started lets call what we need to based off our index
            case 1:
                if (!ObjectHelper.IsReady)
                    return;
                //_taskManager.SetStepMode(false);
                if (ListBoxPOSText[Indexer].Contains('|'))
                {
                    Stage = 3;
                    var lst = ListBoxPOSText[Indexer].Split('|');
                    var action = lst[0];
                    var p = lst[1].Split(',');
                    _actions.InvokeAction(action, p);
                }
                else
                {
                    Stage = 2;
                    var destinationVector = new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0]), float.Parse(ListBoxPOSText[Indexer].Split(',')[1]), float.Parse(ListBoxPOSText[Indexer].Split(',')[2]));
                    if (!VNavmesh_IPCSubscriber.Path_GetMovementAllowed())
                        VNavmesh_IPCSubscriber.Path_SetMovementAllowed(true);
                    if (VNavmesh_IPCSubscriber.Path_GetTolerance() > 0.25F)
                        VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f);
                    VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destinationVector, false);
                }
                break;
            //Navigation
            case 2:
                if (!ObjectHelper.IsReady)
                    return;
                if (ObjectHelper.InCombat(Player))
                {
                    VNavmesh_IPCSubscriber.Path_Stop();
                    Stage = 4;
                    break;
                }

                if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
                {
                    Stage = 1;
                    Indexer++;
                }
                break;
            //Action
            case 3:
                if (!ObjectHelper.IsReady)
                    return;
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
                if (ObjectHelper.InCombat(Player))
                {
                    var range = ObjectHelper.JobRange;
                    if (Svc.Targets.Target != null && ObjectHelper.GetBattleDistanceToPlayer(Svc.Targets.Target) > range && BossMod_IPCSubscriber.ForbiddenZonesCount() == 0 && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress())
                    {
                        VNavmesh_IPCSubscriber.Path_SetTolerance(range);
                        VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(Svc.Targets.Target.Position, false);
                    }
                }
                else
                    Stage = 1;
                break;
            //Paused
            case 5:
                if (VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                    VNavmesh_IPCSubscriber.Path_Stop();
                break;
            case 98:
                if (!_taskManager.IsBusy)
                    Stage = 0;
                break;
            //Looping
            case 99:
                if (!_taskManager.IsBusy && ObjectHelper.IsValid)
                    Stage = 0;
                break;
            default:
                break;
        }
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        ECommonsMain.Dispose();
        ExecSkipTalk.Shutdown();
        MainWindow.Dispose();
        OverrideCamera.Dispose();
        _overrideMovement.Dispose();
        Svc.Framework.Update -= Framework_Update;
        Svc.DutyState.DutyWiped -= DutyState_DutyWiped;
        Svc.DutyState.DutyRecommenced -= DutyState_DutyRecommenced;
        Svc.DutyState.DutyStarted -= DutyState_DutyStarted;
        Svc.DutyState.DutyCompleted -= DutyState_DutyCompleted;
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
        if (ConfigWindow != null)
            ConfigWindow.IsOpen = true;
    }

    public void OpenMainUI()
    {
        if (MainWindow != null)
            MainWindow.IsOpen = true;
    }
}