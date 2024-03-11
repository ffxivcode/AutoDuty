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
using Lumina.Excel.GeneratedSheets2;
using System.Linq;
using AutoDuty.IPC;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Automation;

namespace AutoDuty;

// TODO: Need to add options to who they follow in combat, need to find the bug that is destroying the path when vbm is jousting, need to add MBT to req plugins message

public class AutoDuty : IDalamudPlugin
{
    public List<string> ListBoxPOSText { get; set; } = [];
    public List<(string, uint, uint)> ListBoxDutyText { get; set; } = [];
    public int LoopTimes = 1;
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
    private const string CommandName = "/autoduty";
    private MainWindow MainWindow { get; init; }
    private DirectoryInfo _configDirectory;
    private ActionsManager _actions;
    private MBT_IPCSubscriber _mbtIPC;
    private BossMod_IPCSubscriber _vbmIPC;
    private VNavmesh_IPCSubscriber _vnavIPC;
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

            _vbmIPC = new();
            _mbtIPC = new();
            _vnavIPC = new();
            _actions = new(_vnavIPC, _vbmIPC, _mbtIPC);
            MainWindow = new(this, _actions.ActionsList, _vnavIPC, _vbmIPC, _mbtIPC);

            WindowSystem.AddWindow(MainWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open main window"
            });

            pluginInterface.UiBuilder.Draw += DrawUI;

            Svc.Framework.Update += Framework_Update;

            SetToken();

            PopulateDuties();
            _mbtIPC.SetFollowStatus(false);
            Svc.Log.Info(_mbtIPC.GetFollowStatus().ToString());
        }
        catch (Exception e) { Svc.Log.Info($"Failed loading plugin\n{e}");
        }
    }
    
    public void Run(int clickedDuty)
    {
        Svc.Log.Info($"Running {ListBoxDutyText[clickedDuty]} {LoopTimes} Times");
        Running = true;

    }
    private void PopulateDuties()
    {
        var list = Svc.Data.GameData.GetExcelSheet<DawnContent>();
        //Svc.Log.Info($"{list.RowCount}");
        foreach (var e in list.Select((Value, Index) => (Value, Index)))
        {
            Svc.Log.Information($"{e.Index}");
            if (e.Value is null || e.Index == 0)
                continue;
            ListBoxDutyText.Add((e.Value.Content.Value.Name.ToString()[..3].Equals("the") ? e.Value.Content.Value.Name.ToString().ReplaceFirst("the", "The") : e.Value.Content.Value.Name.ToString(), e.Value.Content.Value.TerritoryType.Value.RowId, e.Value.Content.Value.TerritoryType.Value.ExVersion.Value.RowId));
        }
        ListBoxDutyText = [.. ListBoxDutyText.OrderBy(e => e.Item3)];
    }
    private void PopulateDutiesTT()
    {
        var list = Svc.Data.GameData.GetExcelSheet<TerritoryType>();
        foreach ( var e in list )
        {
            ContentFinderCondition? contentFinderCondition;
            if (e.TerritoryIntendedUse == 3 && (contentFinderCondition = e.ContentFinderCondition.Value) != null && !contentFinderCondition.Name.RawString.IsNullOrEmpty())
                ListBoxDutyText.Add((contentFinderCondition.Name.ToString()[..3].Equals("the") ? contentFinderCondition.Name.ToString().ReplaceFirst("the", "The") : contentFinderCondition.Name.ToString(),e.RowId,0));
        }
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
                if (_vnavIPC.Path_NumWaypoints() > 0 && Started)
                {
                    //Svc.Log.Info($"Stopping Navigation");
                    Started = false;
                    _vnavIPC.Path_Stop();
                }
                if (_vnavIPC.Path_GetTolerance() > 0.5F)
                    _vnavIPC.Path_SetTolerance(0.5f);
                if (_task is not null)
                {
                    //Svc.Log.Info($"Clearing Task: {_task.Status}");
                    if ((_task.Status != TaskStatus.Running || _task.Status != TaskStatus.WaitingForActivation) && !_actions.Token.IsCancellationRequested)
                    {
                        //Svc.Log.Info($"Setting Cancellation Token");
                        _actions.TokenSource.Cancel();
                    }
                    else if (_task.Status != TaskStatus.Running && _task.Status != TaskStatus.WaitingForActivation)
                    {
                        //Svc.Log.Info($"Cancellation Succesful");
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
                    //Svc.Log.Info($"Invoking Action: {action} with params: {p}");
                   // Svc.Log.Info($"Task is null: {_task is null}");
                    _task = Task.Run(() => _actions.InvokeAction(action, p));
                    //Svc.Log.Info($"Waiting for Action");
                }
                else
                {
                    Stage = 2;
                    var destinationVector = new Vector3(float.Parse(ListBoxPOSText[Indexer].Split(',')[0]), float.Parse(ListBoxPOSText[Indexer].Split(',')[1]), float.Parse(ListBoxPOSText[Indexer].Split(',')[2]));
                    Svc.Log.Info($"Navigating To: {destinationVector}");
                    if (!_vnavIPC.Path_GetMovementAllowed() )
                        _vnavIPC.Path_SetMovementAllowed(true);
                    _vnavIPC.SimpleMove_PathfindAndMoveTo(destinationVector, false);
                    Svc.Log.Info($"Waiting for Navigation");
                }
                break;
            //We are navigating
            case 2:
                if (ObjectManager.InCombat(_player) && _vnavIPC.Path_GetMovementAllowed())
                    _vnavIPC.Path_SetMovementAllowed(false);
                else if (!ObjectManager.InCombat(_player) && !_vnavIPC.Path_GetMovementAllowed())
                    _vnavIPC.Path_SetMovementAllowed(true);

                if (!_vnavIPC.SimpleMove_PathfindInProgress() && _vnavIPC.Path_NumWaypoints() == 0)
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
                        //Svc.Log.Info($"Done Waiting for Action");
                        Stage = 1;
                        _task = null;
                        Indexer++;
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

    public unsafe void Test()
    {
        //Just a Test function
        try
        {
            //Run through Vault


            //var player = ClientState.LocalPlayer;
            //SetPos.SetPosPos(player.Position + new System.Numerics.Vector3(0, 1,
            //0)) ;
            //Dalamud.Logging.Log.Log(DalamudAPI.PartyList[0].Name.ToString());
            //DalamudAPI.Targets.SetTarget(gameObject);
            //Set the Games MovementMove 0=Standard, 1=Legacy
            //DalamudAPI.GameConfig.UiControl.Set("MoveMode", 1);
            /*if (DalamudAPI.GameConfig.UiControl.GetUInt("MoveMode") == 0)
                textFollow1 = "Standard";
            else if (DalamudAPI.GameConfig.UiControl.GetUInt("MoveMode") == 1)
                textFollow1 = "Legacy";
            //ClickSelectYesNo.Using(default).Yes();
            *if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon))
            {
                Log.Information("got addon");
            }
            else
            {
                Log.Information("no addon found");
            }
            */
            //var addon = GameGui.GetAddonByName("SelectYesno", 1);
            // var addon = (AtkUnitBase*)GameGui.GetAddonByName("SelectYesno", 1);
            // addonPTR->SendClick(addon, EventType.CHANGE, 0, ((AddonSelectYesno*)addon)->YesButton->AtkComponentBase.OwnerNode);
            //ClickSelectYesNo.Using(addon).Yes();
            //addon.
            // Log.Information(addon->AtkValues[0].ToString());
            //textTest = addon->AtkValues[0].ToString();
            //InteractWithObject("Red Coral Formation");
            //var go = GetGroupMemberObjectByRole(4);
            //Log.Info("Our Healer is: " + go.Name.ToString());
            //go = GetGroupMemberObjectByRole(1);
            //Log.Info("Our Tank is: " + go.Name.ToString());
            //Log.Info("Boss: " + IsBossFromIcon((BattleChara)Targets.Target));;

            /*var objs = GetObjectInRadius(Objects, 30);
            foreach (var obj in objs) 
            {
                Log.Info("Name: " + obj.Name.ToString() + " Distance: " + DistanceToPlayer(obj));            
            }*/
            /*var objs = GetObjectInRadius(Objects, 30);
            var battleCharaObjs = objs.OfType<BattleChara>();
            GameObject bossObject = default;
            foreach (var obj in battleCharaObjs)
            {
                Log.Info("Checking: " + obj.Name.ToString());
                if (IsBossFromIcon(obj))
                    bossObject = obj;
            }
            if (bossObject)
                Log.Info("Boss: " + bossObject.Name.ToString());*/
            //Log.Info(DistanceToPlayer(Targets.Target).ToString());
            /*var v3o = new RcVec3f(-113.888f, 150, 210.794f);
             var path = meshesDirectory + "/" + ClientState.TerritoryType.ToString() + ".navmesh";
             var fileStream = File.Open(path, FileMode.Open);
             var nmd = new NavMeshDetour();
             var point = nmd.FindNearestPolyPoint(v3o, new RcVec3f(0, 200, 0), fileStream);
             Log.Info(point.ToString());
             Navigate(new RcVec3f(ClientState.LocalPlayer.Position.X, ClientState.LocalPlayer.Position.Y, ClientState.LocalPlayer.Position.Z), point);*/
            /*var i = Objects.OrderBy(o => DistanceToPlayer(o)).Where(p => p.Name.ToString().ToUpper().Equals("MINERAL DEPOSIT"));

            foreach (var o in i) 
            { 
                Log.Info(o.Name.ToString() + " - " + DistanceToPlayer(o).ToString() + " IsTargetable" + o.IsTargetable);
            }*/
            //if (ECommons.Reflection.DalamudReflector.TryGetDalamudPlugin("vnavmesh", out var _))
            //{
            //PluginInterface.GetIpcSubscriber<bool, object>("vnavmesh.SetMovementAllowed").InvokeAction(true);

            //}
            /*
            //This clicks the Item in the Gathering Window by Index 
            var addon = (AddonGathering*)GameGui.GetAddonByName("Gathering", 1);
            if (addon != null)
            {
                var ids = new List<uint>()
                {
                    addon->GatheredItemId1,
                    addon->GatheredItemId2,
                    addon->GatheredItemId3,
                    addon->GatheredItemId4,
                    addon->GatheredItemId5,
                    addon->GatheredItemId6,
                    addon->GatheredItemId7,
                    addon->GatheredItemId8
                };
                ids.ForEach(p => Log.Info(p.ToString()));
            }
            //var addon2 = (AtkUnitBase*)GameGui.GetAddonByName("Gathering");
            var receiveEventAddress = new nint(addon->AtkUnitBase.AtkEventListener.vfunc[2]);
            var eventDelegate = Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>(receiveEventAddress)!;

            var target = AtkStage.GetSingleton();
            var eventData = EventData.ForNormalTarget(target, &addon->AtkUnitBase);
            var inputData = InputData.Empty();

            eventDelegate.Invoke(&addon->AtkUnitBase.AtkEventListener, ClickLib.Enums.EventType.CHANGE, (uint)2, eventData.Data, inputData.Data);*/

            //var addon = GameGui.GetAddonByName("SelectYesno", 1);
            //var add = (AddonSelectYesno*)addon;
            //Log.Info(Vector3.DistanceSquared(Svc.ClientState.LocalPlayer.Position,new Vector3(44.4254f,-9, 112.402f)).ToString());
            // var Agent = AgentContentsFinder.Instance();
            //Agent->OpenRegularDuty(10);
            var taskManager = new TaskManager();
            //s2->UiModule->
            //taskManager.Enqueue(() => OpenDawnStory());

            //Callback.Fire((AtkUnitBase*)GameGui.GetAddonByName("DawnStory", 1), true, 0, 84);

            //taskManager.DelayNext(2000);
            // taskManager.Enqueue(() => OpenDawnStory());
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("DawnStory", 1);
            /*taskManager.Enqueue(() => OpenDawnStory());
            taskManager.DelayNext(2000);
            taskManager.Enqueue(() => addon = (AtkUnitBase*)GameGui.GetAddonByName("DawnStory", 1));
            taskManager.Enqueue(() => SelectExpansionInDawnStory(addon, 1));
            taskManager.DelayNext(2000);*/
            taskManager.Enqueue(() => SelectDutyInDawnStory(addon, "The Vault"));
            /*taskManager.DelayNext(2000);
            taskManager.Enqueue(() => addon->Draw());
            taskManager.DelayNext(2000);
            taskManager.Enqueue(() => ClickDawnStoryRegisterForDuty(addon));*/
        }
        catch (Exception e)
        {
            Svc.Log.Error(e.ToString());
            //throw;
        }

    }
    private unsafe static void SelectExpansionInDawnStory(AtkUnitBase* addon, int expansion)
    {
        int expRadioButton;
        switch (expansion)
        {
            case 0:
                expRadioButton = 39;
                break;
            case 1:
                expRadioButton = 38;
                break;
            case 2:
                expRadioButton = 37;
                break;
            case 3:
                expRadioButton = 36;
                break;
            case 4:
                expRadioButton = 35;
                break;
            default:
                expRadioButton = -1;
                break;
        }

        ClickAddonRadioButtonByNode(addon, expRadioButton);
    }
    public unsafe static void SelectDutyInDawnStory(AtkUnitBase* addon, string dutyName)
    {
        try
        {
            var atkComponentTreeListDungeons = (AtkComponentTreeList*)addon->UldManager.NodeList[7]->GetComponent();
            for (ulong i = 0; i < atkComponentTreeListDungeons->Items.Size(); i++)
            {
                var a3 = atkComponentTreeListDungeons->Items.Get(i).Value->Renderer->AtkComponentButton.ButtonTextNode->NodeText;
                if (a3.ToString().Equals(dutyName))
                {
                    var item = atkComponentTreeListDungeons->GetItem((uint)i);
                    if (item != null)
                    {
                        atkComponentTreeListDungeons->SelectItem((uint)i, true);
                        atkComponentTreeListDungeons->AtkComponentList.SelectItem((int)i, true);
                        atkComponentTreeListDungeons->AtkComponentList.DispatchItemEvent((int)i, AtkEventType.MouseClick);
                        var atkEvent = item->Renderer->AtkComponentButton.AtkComponentBase.AtkResNode->AtkEventManager.Event;
                        var atkEventType = atkEvent->Type;
                        var atkEventParam = (int)atkEvent->Param;

                        addon->ReceiveEvent(AtkEventType.MouseClick, 3, atkEvent);
                        return;
                    }
                }
            }
        }
        catch (Exception e) { Svc.Log.Info($"{e}"); }
    }
    public unsafe static void OpenDawnStory() => AgentModule.Instance()->GetAgentByInternalID(341)->Show();

    public unsafe static void ClickDawnStoryRegisterForDuty(AtkUnitBase* addon) => ClickAddonButtonByNode(addon, 84);

    public unsafe static void ClickAddonButtonByNode(AtkUnitBase* addon, int node)
    {
        if (node == -1)
            return;

        addon->ReceiveEvent(((AtkComponentButton*)addon->UldManager.NodeList[node]->GetComponent())->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event->Type, (int)(AtkComponentButton*)addon->UldManager.NodeList[node]->GetComponent()->OwnerNode->AtkResNode.AtkEventManager.Event->Param, (AtkEvent*)((AtkComponentButton*)addon->UldManager.NodeList[node]->GetComponent()->OwnerNode->AtkResNode.AtkEventManager.Event));
    }
    public unsafe static void ClickAddonRadioButtonByNode(AtkUnitBase* addon, int node)
    {
        if (node == -1)
            return;

        addon->ReceiveEvent(((AtkComponentRadioButton*)addon->UldManager.NodeList[node]->GetComponent())->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event->Type, (int)(AtkComponentRadioButton*)addon->UldManager.NodeList[node]->GetComponent()->OwnerNode->AtkResNode.AtkEventManager.Event->Param, (AtkEvent*)((AtkComponentRadioButton*)addon->UldManager.NodeList[node]->GetComponent()->OwnerNode->AtkResNode.AtkEventManager.Event));
    }
}