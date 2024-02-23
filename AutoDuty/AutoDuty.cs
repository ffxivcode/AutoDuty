using System;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using Dalamud.Common;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.GameHelpers;
using ECommons.Reflection;

namespace AutoDuty;

// TODO: 

public class AutoDuty : IDalamudPlugin
{
    public string txt = "";
    public List<string> ListBoxPOSText { get; set; } = [];
    public string Name => "AutoDuty";
    public static AutoDuty Plugin { get; private set; }
    public bool StopForCombat = true;
    private const string CommandName = "/autoduty";
    private MainWindow MainWindow { get; init; }
    private DirectoryInfo _configDirectory;
    private ActionsManager _actions;
    public DirectoryInfo PathsDirectory;
    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("AutoDuty");
    public int Stage = 0;
    public int Indexer = 0;
    private Task? _task = null;

    public AutoDuty(DalamudPluginInterface pluginInterface)
    {
        try
        {
            Plugin = this;
            ECommonsMain.Init(pluginInterface, this, ECommons.Module.All);

            Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(pluginInterface);
            

            _configDirectory = pluginInterface.ConfigDirectory;
            PathsDirectory = new DirectoryInfo(_configDirectory.FullName + "/paths");

            if (!_configDirectory.Exists)
                _configDirectory.Create();
            if (!PathsDirectory.Exists)
                PathsDirectory.Create();

            _actions = new ActionsManager();

            MainWindow = new MainWindow(this, _actions.ActionsList);

            WindowSystem.AddWindow(MainWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open main window"
            });

            pluginInterface.UiBuilder.Draw += DrawUI;

            Svc.Framework.Update += Framework_Update;

            MainWindow.IsOpen = true;

            SetToken();
        }
        catch (Exception e) { Svc.Log.Info($"Failed loading plugin\n{e}");
        }
    }

    private void SetToken()
    {
        _actions.TokenSource = new();
        _actions.Token = _actions.TokenSource.Token;
    }

    public void Framework_Update(IFramework framework)
    {
        if (!IPCManager.BossMod_IsEnabled)
            return;

        if (!IPCManager.VNavmesh_IsEnabled)
            return;

        if (ExcelTerritoryHelper.Get(Svc.ClientState.TerritoryType).TerritoryIntendedUse != 3)
            return;

        PlayerCharacter? _player;
        if ((_player = Svc.ClientState.LocalPlayer) is null)
            return;

        if (Indexer >= ListBoxPOSText.Count)
        {
            Stage = 0;
            Indexer = 0;
        }

        switch (Stage)
        {
            //AutoDuty is stopped or has not started
            case 0:
                if (IPCManager.VNavmesh_WaypointsCount > 0)
                {
                    Svc.Log.Info($"Stopping Navigation");
                    IPCManager.VNavmesh_Stop();
                }
                if (IPCManager.VNavmesh_Tolerance > 0.5F)
                    IPCManager.VNavmesh_SetTolerance(0.5f);
                if (_task is not null)
                {
                    Svc.Log.Info($"Clearing Task: {_task.Status}");
                    if ((_task.Status != TaskStatus.Running || _task.Status != TaskStatus.WaitingForActivation) && !_actions.Token.IsCancellationRequested)
                    {
                        Svc.Log.Info($"Setting Cancellation Token");
                        _actions.TokenSource.Cancel();
                    }
                    else if (_task.Status != TaskStatus.Running && _task.Status != TaskStatus.WaitingForActivation)
                    {
                        Svc.Log.Info($"Cancellation Succesful");
                        //_task.Dispose();
                        _task = null;
                        SetToken();
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
                    Svc.Log.Info($"Invoking Action: {action} with params: {p}");
                    Svc.Log.Info($"Task is null: {_task is null}");
                    _task = Task.Run(() => _actions.InvokeAction(action, p));
                    Svc.Log.Info($"Waiting for Action");
                }
                else
                {
                    Stage = 2;
                    var destinationVector = new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0]), float.Parse(ListBoxPOSText[Indexer].Split(',')[1]), float.Parse(ListBoxPOSText[Indexer].Split(',')[2]));
                    Svc.Log.Info($"Navigating To: {destinationVector}");
                    if (!IPCManager.VNavmesh_MovementAllowed)
                        IPCManager.VNavmesh_SetMovementAllowed(true);
                    IPCManager.VNavmesh_MoveTo(destinationVector);
                    Svc.Log.Info($"Waiting for Navigation");
                }
                break;
            //We are navigating
            case 2:
                if ((ObjectManager.InCombat(_player) || IPCManager.BossMod_IsMoving || IPCManager.BossMod_ForbiddenZonesCount > 0) && IPCManager.VNavmesh_MovementAllowed)
                    IPCManager.VNavmesh_SetMovementAllowed(false);
                else if (!IPCManager.VNavmesh_MovementAllowed && !ObjectManager.InCombat(_player) && !IPCManager.BossMod_IsMoving && IPCManager.BossMod_ForbiddenZonesCount == 0)
                    IPCManager.VNavmesh_SetMovementAllowed(true);

                if (IPCManager.VNavmesh_WaypointsCount == 0)
                {
                    Svc.Log.Info($"Done Waiting for Navigation");
                    Stage = 1;
                    Indexer++;
                }
                break;
            case 3:
                if (_task is not null)
                {
                    if (_task.IsCompleted)
                    {
                        Svc.Log.Info($"Done Waiting for Action");
                        Stage = 1;
                        _task = null;
                        Indexer++;
                    }
                }
                else
                    Svc.Log.Info("Task has not started");
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
}