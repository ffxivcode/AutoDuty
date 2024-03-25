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
using Lumina.Excel.GeneratedSheets;
using System.Linq;
using AutoDuty.IPC;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Automation;

namespace AutoDuty;

// TODO: Need to add options to who they follow in combat. need to add shorcut checking on death and auto revive on death. Need to add 4-Box capability and add dungeons not in support and squadron queueing.

public class AutoDuty : IDalamudPlugin
{
    public List<string> ListBoxPOSText { get; set; } = [];
    public List<(string, uint, uint)> ListBoxDutyText { get; set; } = [];
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
    public bool Started = false;
    public bool Running = false;
    public PlayerCharacter? Player =null;

    private const string CommandName = "/autoduty";
    private MainWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private DirectoryInfo _configDirectory;
    private ActionsManager _actions;
    private MBT_IPCSubscriber _mbtIPC;
    private BossMod_IPCSubscriber _vbmIPC;
    private VNavmesh_IPCSubscriber _vnavIPC;
    private Chat _chat;
    private TaskManager _taskManager;
    private RepairManager _repairManager;

    public AutoDuty(DalamudPluginInterface pluginInterface)
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(pluginInterface, this, Module.All);

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
            _vbmIPC = new();
            _mbtIPC = new();
            _vnavIPC = new();
            _actions = new(this, _vnavIPC, _vbmIPC, _mbtIPC, _chat, _taskManager); 
            _repairManager = new(_taskManager, _vnavIPC, _actions);
            MainWindow = new(this, _actions.ActionsList, _vnavIPC, _vbmIPC, _mbtIPC, _taskManager);
            ConfigWindow = new(this);

            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(ConfigWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "\n/mbt->opens main window\n" +
                "/mbt config or cfg->opens config window\n"
            });
             
            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
            pluginInterface.UiBuilder.OpenMainUi += OpenMainUI;

            Svc.Framework.Update += Framework_Update;

            Svc.ClientState.TerritoryChanged += ClientState_TerritoryChanged;
            Svc.Condition.ConditionChange += Condition_ConditionChange;

            PopulateDuties();
        }
        catch (Exception e) { Svc.Log.Info($"Failed loading plugin\n{e}");
        }
    }
    
    private void ClientState_TerritoryChanged(ushort t)
    {
        if (t == 0 || !Running)
            return;

        if (t != ListBoxDutyText[CurrentTerritoryIndex].Item2)
        {
            if (CurrentLoop < LoopTimes)
            {
                _taskManager.Enqueue(() => Stage = 99, "Loop");
                _taskManager.Enqueue(() => !ObjectManager.IsReady, 500, "Loop");
                _taskManager.Enqueue(() => ObjectManager.IsReady, int.MaxValue, "Loop");
                _taskManager.Enqueue(_repairManager.Repair, int.MaxValue, "Loop");
                _taskManager.Enqueue(() => RegisterDutySupport(CurrentTerritoryIndex, ListBoxDutyText[CurrentTerritoryIndex].Item2, ListBoxDutyText[CurrentTerritoryIndex].Item3), int.MaxValue, "Loop");
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
        Svc.Log.Debug($"{flag} : {value}");
    }

    public void Run(int clickedDuty)
    {
        Stage = 99;
        Svc.Log.Info($"Running {ListBoxDutyText[clickedDuty]} {LoopTimes} Times");
        
        CurrentTerritoryIndex = clickedDuty;
        Running = true;
        _repairManager.Repair();
        RegisterDutySupport(clickedDuty, ListBoxDutyText[clickedDuty].Item2, ListBoxDutyText[clickedDuty].Item3);
        CurrentLoop = 1;
    }
    private void PopulateDuties()
    {
        var list = Svc.Data.GameData.GetExcelSheet<DawnContent>();
        if (list == null) return;

        foreach (var e in list.Select((Value, Index) => (Value, Index)))
        {
            if (e.Value is null || e.Index == 0)
                continue;
            ListBoxDutyText.Add((e.Value.Content.Value.Name.ToString()[..3].Equals("the") ? e.Value.Content.Value.Name.ToString().ReplaceFirst("the", "The") : e.Value.Content.Value.Name.ToString(), e.Value.Content.Value.TerritoryType.Value?.RowId ?? 0, e.Value.Content.Value.TerritoryType.Value?.ExVersion.Value?.RowId ?? 0));
        }
        ListBoxDutyText = [.. ListBoxDutyText.OrderBy(e => e.Item3)];
    }
    private void PopulateDutiesTT()
    {
        var list = Svc.Data.GameData.GetExcelSheet<TerritoryType>();
        if (list == null) return;

        foreach (var e in list)
        {
            ContentFinderCondition? contentFinderCondition;
            if (e.TerritoryIntendedUse == 3 && (contentFinderCondition = e.ContentFinderCondition.Value) != null && !contentFinderCondition.Name.ToString().IsNullOrEmpty())
                ListBoxDutyText.Add((contentFinderCondition.Name.ToString()[..3].Equals("the") ? contentFinderCondition.Name.ToString().ReplaceFirst("the", "The") : contentFinderCondition.Name.ToString(), e.RowId, 0));
        }
    }

    public void StartNavigation()
    {
        Stage = 1;
        Started = true;
        _chat.ExecuteCommand($"/vbmai on");
        _chat.ExecuteCommand($"/rotation auto");
        Svc.Log.Info("Starting Navigation");
    }

    public void Framework_Update(IFramework framework)
    {
        if ((Player = Svc.ClientState.LocalPlayer) == null)
            return;

        if (!_vbmIPC.IsEnabled)
            return;

        if (!_vnavIPC.IsEnabled)
            return;

        if (!ObjectManager.IsValid)
            return;

        //if (ExcelTerritoryHelper.Get(Svc.ClientState.TerritoryType).TerritoryIntendedUse != 3 && !Running)
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
                if (_vnavIPC.Path_NumWaypoints() > 0 && Started)
                {
                    Started = false;
                    _vnavIPC.Path_Stop();
                }
                else if (Started)
                    Started = false;
                if (_vnavIPC.Path_GetTolerance() > 0.25F)
                    _vnavIPC.Path_SetTolerance(0.25f);
                if (_taskManager.IsBusy)
                    _taskManager.Abort();
                if (Indexer > 0)
                    Indexer = 0;
                break;
            //We are started lets call what we need to based off our index
            case 1:
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
                    if (!_vnavIPC.Path_GetMovementAllowed())
                        _vnavIPC.Path_SetMovementAllowed(true);
                    if (_vnavIPC.Path_GetTolerance() > 0.25F)
                        _vnavIPC.Path_SetTolerance(0.25f);
                    _vnavIPC.SimpleMove_PathfindAndMoveTo(destinationVector, false);
                }
                break;
            //Navigation
            case 2:
                if (ObjectManager.InCombat(Player))
                {
                    _vnavIPC.Path_Stop();
                    Stage = 4;
                    break;
                }

                if (!_vnavIPC.SimpleMove_PathfindInProgress() && _vnavIPC.Path_NumWaypoints() == 0)
                {
                    Stage = 1;
                    Indexer++;
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
                if (ObjectManager.InCombat(Player))
                {
                    var range = ObjectManager.JobRange;
                    if (Svc.Targets.Target != null && ObjectManager.GetBattleDistanceToPlayer(Svc.Targets.Target) > range && _vbmIPC.ForbiddenZonesCount() == 0 && !_vnavIPC.SimpleMove_PathfindInProgress())
                    {
                        _vnavIPC.Path_SetTolerance(range);
                        _vnavIPC.SimpleMove_PathfindAndMoveTo(Svc.Targets.Target.Position, false);
                    }
                }
                else
                    Stage = 1;
                break;
            //Paused
            case 5:
                if (_vnavIPC.Path_NumWaypoints() > 0)
                    _vnavIPC.Path_Stop();
                //Looping
                break;
            case 99:
                if (!_taskManager.IsBusy)
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
        MainWindow.Dispose();
        Svc.Framework.Update -= Framework_Update;
        Svc.ClientState.TerritoryChanged -= ClientState_TerritoryChanged;
        Svc.Condition.ConditionChange -= Condition_ConditionChange;
        Svc.Commands.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command
        switch (args)
        {
            case "config" or "cfg":
                OpenConfigUI(); break;
            default:
                OpenMainUI(); break;
        }
        OpenMainUI();
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

    public void RegisterDutySupport(int index, uint territoryType, uint exp)
    {
        if (index < 0)
            return;

        nint addon = 0;
        int indexModifier = 0;

        if (!ObjectManager.IsValid)
        {
            _taskManager.Enqueue(() => ObjectManager.IsValid, int.MaxValue, "RegisterDutySupport");
            _taskManager.DelayNext("RegisterDutySupport", 2000);
        }

        _taskManager.Enqueue(() => addon = Svc.GameGui.GetAddonByName("DawnStory"), "RegisterDutySupport");
        _taskManager.Enqueue(() => { if (addon == 0) OpenDawnStory(); }, "RegisterDutySupport");
        _taskManager.Enqueue(() => (addon = Svc.GameGui.GetAddonByName("DawnStory", 1)) > 0 && _actions.IsAddonReady(addon), "RegisterDutySupport");
        _taskManager.Enqueue(() => (addon = Svc.GameGui.GetAddonByName("DawnStory")) > 0, "RegisterDutySupport");
        _taskManager.Enqueue(() => FireCallBack(addon, true, 11, 3), "RegisterDutySupport");
        _taskManager.DelayNext("RegisterDutySupport", 50);
        _taskManager.Enqueue(() => indexModifier = DawnStoryCount(addon) - 1, "RegisterDutySupport");
        _taskManager.Enqueue(() => FireCallBack(addon, true, 11, exp), "RegisterDutySupport");
        _taskManager.DelayNext("RegisterDutySupport", 250);
        _taskManager.Enqueue(() => FireCallBack(addon, true, 12, DawnStoryIndex(index, exp, indexModifier)), "RegisterDutySupport");
        _taskManager.DelayNext("RegisterDutySupport", 250);
        _taskManager.Enqueue(() => FireCallBack(addon, true, 14), "RegisterDutySupport");
        _taskManager.DelayNext("RegisterDutySupport", 1000);
        _taskManager.Enqueue(() => FireCallBack(Svc.GameGui.GetAddonByName("ContentsFinderConfirm", 1), true, 8), "RegisterDutySupport");
        _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == territoryType, int.MaxValue, "RegisterDutySupport");
        _taskManager.Enqueue(() => ObjectManager.IsValid, int.MaxValue, "RegisterDutySupport");
        _taskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, int.MaxValue, "RegisterDutySupport");
        _taskManager.Enqueue(() => _vnavIPC.Nav_IsReady(), int.MaxValue, "RegisterDutySupport");
        _taskManager.Enqueue(StartNavigation, "RegisterDutySupport");
    }

    private unsafe void OpenDawnStory() => AgentModule.Instance()->GetAgentByInternalID(341)->Show();

    private static unsafe void FireCallBack(nint addon, bool boolValue, params object[] args)
    {
        var addonPtr = (AtkUnitBase*)addon;
        if (addon == 0 || addonPtr is null) return;
        Callback.Fire(addonPtr, boolValue, args);
    }

    private unsafe int DawnStoryCount(nint addon)
    {
        var addonBase = (AtkUnitBase*)addon;
        var atkComponentTreeListDungeons = (AtkComponentTreeList*)addonBase->UldManager.NodeList[7]->GetComponent();
        return (int)atkComponentTreeListDungeons->Items.Size();
    }
    
    private unsafe int DawnStoryIndex(int index, uint ex, int indexModifier)
    {
        return ex switch
        {
            0 or 1 or 2 => indexModifier + index,
            3 or 4 => index - (43 - indexModifier),
            _ => -1,
        };
    }
}