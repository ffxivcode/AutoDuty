using System;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.GameHelpers;
using AutoDuty.Managers;
using AutoDuty.Windows;
using Lumina.Excel.GeneratedSheets;
using System.Linq;
using AutoDuty.IPC;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Automation;
using System.Runtime.CompilerServices;

namespace AutoDuty;

// TODO: Need to add options to who they follow in combat. need to add shorcut checking on death and auto revive on death. Need to get Treelist callback from TaurenKey, Need to add 4-Box capability and add dungeons not in support. Add Pause and Resume (re pathfind on resume)

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
    private Task? _task = null;
    private Task? _loopTask = null;
    private const string CommandName = "/autoduty";
    private MainWindow MainWindow { get; init; }
    private DirectoryInfo _configDirectory;
    private ActionsManager _actions;
    private MBT_IPCSubscriber _mbtIPC;
    private BossMod_IPCSubscriber _vbmIPC;
    private VNavmesh_IPCSubscriber _vnavIPC;
    private Chat _chat;

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

            _chat = new();
            _vbmIPC = new();
            _mbtIPC = new();
            _vnavIPC = new();
            _actions = new(this, _vnavIPC, _vbmIPC, _mbtIPC, _chat);
            MainWindow = new(this, _actions.ActionsList, _vnavIPC, _vbmIPC, _mbtIPC);

            WindowSystem.AddWindow(MainWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open main window"
            });

            pluginInterface.UiBuilder.Draw += DrawUI;

            Svc.Framework.Update += Framework_Update;

            Svc.ClientState.TerritoryChanged += ClientState_TerritoryChanged;

            //Svc.Condition.ConditionChange += Condition_ConditionChange;

            SetToken();

            PopulateDuties();
            _mbtIPC.SetFollowStatus(false);
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
                Stage = 99;
                _loopTask = Task.Run(() =>RegisterDutySupport(CurrentTerritoryIndex, ListBoxDutyText[CurrentTerritoryIndex].Item2, ListBoxDutyText[CurrentTerritoryIndex].Item3));
                CurrentLoop++;
            }
            else
                Running = false;
        }
    }

    private void Condition_ConditionChange(Dalamud.Game.ClientState.Conditions.ConditionFlag flag, bool value)
    {
        Svc.Log.Info($"{flag} : {value}");
    }

    public void Run(int clickedDuty)
    {
        Stage = 99;
        Svc.Log.Info($"Running {ListBoxDutyText[clickedDuty]} {LoopTimes} Times");
        SetToken();
        CurrentTerritoryIndex = clickedDuty;
        Running = true;
        //Svc.Log.Info($"{DawnStoryIndex(clickedDuty, ListBoxDutyText[clickedDuty].Item3)}");
        _loopTask = Task.Run(() => RegisterDutySupport(clickedDuty, ListBoxDutyText[clickedDuty].Item2, ListBoxDutyText[clickedDuty].Item3));
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
        SetToken();
        _chat.ExecuteCommand($"/vbmai on");
        _chat.ExecuteCommand($"/rotation auto");
        Svc.Log.Info("Starting Navigation");
    }

    public void SetToken()
    {
        _actions.TokenSource = new();
        _actions.Token = _actions.TokenSource.Token;
    }

    public void Framework_Update(IFramework framework)
    {
        if (!_vbmIPC.IsEnabled)
            return;

        if (!_vnavIPC.IsEnabled)
            return;

        if (!ObjectManager.IsValid)
            return;

        PlayerCharacter? _player;
        if ((_player = Svc.ClientState.LocalPlayer) is null)
            return;

        if (ExcelTerritoryHelper.Get(Svc.ClientState.TerritoryType).TerritoryIntendedUse != 3 && !Running)
            return;

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
                if (_vnavIPC.Path_GetTolerance() > 0.25F)
                    _vnavIPC.Path_SetTolerance(0.25f);
                if (_task is not null)
                {
                    if (_actions.TokenSource != null && (_task.Status != TaskStatus.Running || _task.Status != TaskStatus.WaitingForActivation) && !_actions.Token.IsCancellationRequested)
                        _actions.TokenSource.Cancel();
                    else if (_task.Status != TaskStatus.Running && _task.Status != TaskStatus.WaitingForActivation)
                    {
                        _task = null;
                        //SetToken();
                    }
                }
                if (_loopTask is not null)
                {
                    if (_actions.TokenSource != null && (_loopTask.Status != TaskStatus.Running || _loopTask.Status != TaskStatus.WaitingForActivation) && !_actions.Token.IsCancellationRequested)
                        _actions.TokenSource.Cancel();
                    else if (_loopTask.Status != TaskStatus.Running && _loopTask.Status != TaskStatus.WaitingForActivation)
                    {
                        _loopTask = null;
                        //SetToken();
                    }
                }
                if (Indexer > 0)
                    Indexer = 0;
                break;
            //We are started lets call what we need to based off our index
            case 1:
                if (ListBoxPOSText[Indexer].Contains('|'))
                {
                    Stage = 3;
                    var lst = ListBoxPOSText[Indexer].Split('|');
                    var action = lst[0];
                    var p = lst[1].Split(',');
                    _task = null;
                    _task = Task.Run(() => _actions.InvokeAction(action, p));
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
                if (ObjectManager.InCombat(_player))
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
                if (_task is not null)
                {
                    if (_task.IsCompleted)
                    {
                        Stage = 1;
                        _task = null;
                        Indexer++;
                    }
                }
                break;
            //InCombat
            case 4:
                if (ObjectManager.InCombat(_player))
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
                if (_task is not null)
                {
                    if (_actions.TokenSource != null && (_task.Status != TaskStatus.Running || _task.Status != TaskStatus.WaitingForActivation) && !_actions.Token.IsCancellationRequested)
                        _actions.TokenSource.Cancel();
                    else if (_task.Status != TaskStatus.Running && _task.Status != TaskStatus.WaitingForActivation)
                    {
                        _task = null;
                        //SetToken();
                    }
                }
                if (_loopTask is not null)
                {
                    if (_actions.TokenSource != null && (_loopTask.Status != TaskStatus.Running || _loopTask.Status != TaskStatus.WaitingForActivation) && !_actions.Token.IsCancellationRequested)
                        _actions.TokenSource.Cancel();
                    else if (_loopTask.Status != TaskStatus.Running && _loopTask.Status != TaskStatus.WaitingForActivation)
                    {
                        _loopTask = null;
                        //SetToken();
                    }
                }
                break;
            //Looping
            case 99:
                if (_loopTask is not null)
                {
                    if (_loopTask.IsCompleted)
                    {
                        Stage = 0;
                        _loopTask = null;
                    }
                }
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
        //Svc.Condition.ConditionChange -= Condition_ConditionChange;
        Svc.Commands.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.IsOpen = true;
    }

    private void DrawUI()
    {
        if (!Player.Available)
            return;

        WindowSystem.Draw();
    }

    public async Task RegisterDutySupport(int index, uint territoryType, uint exp)
    {
        try
        {
            if (index < 0)
                return;
            nint addon;

            if (!ObjectManager.IsValid)
            {
                while (!ObjectManager.IsValid && !_actions.Token.IsCancellationRequested)
                    await Task.Delay(50, _actions.Token);
                await Task.Delay(2000, _actions.Token);
            }
            if (_actions.Token.IsCancellationRequested)
                return;
            if ((addon = Svc.GameGui.GetAddonByName("DawnStory")) == 0)
            {
                OpenDawnStory();
                await Task.Delay(1000, _actions.Token);
                addon = Svc.GameGui.GetAddonByName("DawnStory");
            }
            if (_actions.Token.IsCancellationRequested)
                return;
            FireCallBack(addon, true, 11, 3);
            await Task.Delay(50, _actions.Token);
            int indexModifier = (DawnStoryCount(addon) - 1);
            if (_actions.Token.IsCancellationRequested)
                return;
            if (indexModifier < 0)
                return;

            FireCallBack(addon, true, 11, exp);
            await Task.Delay(250, _actions.Token);
            if (_actions.Token.IsCancellationRequested)
                return;
            FireCallBack(addon, true, 12, DawnStoryIndex(index, exp, indexModifier));
            await Task.Delay(250, _actions.Token);
            if (_actions.Token.IsCancellationRequested)
                return;
            FireCallBack(addon, true, 14);
            await Task.Delay(1000, _actions.Token);
            if (_actions.Token.IsCancellationRequested)
                return;
            FireCallBack(Svc.GameGui.GetAddonByName("ContentsFinderConfirm", 1), true, 8);
            while (Svc.ClientState.TerritoryType != territoryType && !_actions.Token.IsCancellationRequested)
                await Task.Delay(50, _actions.Token);
            while (ObjectManager.IsValid && !_actions.Token.IsCancellationRequested)
                await Task.Delay(50, _actions.Token);
            await Task.Delay(5000, _actions.Token);
            if (_actions.Token.IsCancellationRequested)
                return;
            StartNavigation();
        }
        catch (Exception e)
        {
            //Svc.Log.Error(e.ToString());
            //throw;
        }
    }

    private unsafe void OpenDawnStory() => AgentModule.Instance()->GetAgentByInternalID(341)->Show();

    private static unsafe void FireCallBack(nint addon, bool boolValue, params object[] args) => Callback.Fire((AtkUnitBase*)addon, boolValue, args);
        
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
            3 => index - (43 - indexModifier),
            4 => index,
            _ => -1,
        };
    }
}