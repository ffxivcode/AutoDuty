using AutoDuty.External;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Linq;
using System.Numerics;

namespace AutoDuty.Managers
{
    internal class RepairManager(TaskManager _taskManager)
    {
        bool _returnAfter = true;
        public unsafe void Repair(bool forceCity = false, bool returnAfter = true)
        {
            _returnAfter = returnAfter;
            if (AutoDuty.Plugin.Configuration.AutoRepair && InventoryHelper.LowestEquippedCondition() <= AutoDuty.Plugin.Configuration.AutoRepairPct)
            {
                AutoDuty.Plugin.Repairing = true;
                ExecSkipTalk.IsEnabled = true;
                if (AutoDuty.Plugin.Configuration.AutoRepairCity || forceCity)
                {
                    switch(UIState.Instance()->PlayerState.GrandCompany)
                    {
                        //Limsa=1,129, Gridania=2,132, Uldah=3,130 -- Goto Limsa if no GC
                        case 1:
                            RepairTasks(129, [new Vector3(17.715698f, 40.200005f, 3.9520264f)], "Leofrun", [new Vector3(15.42688f, 39.99999f, 12.466553f)], "Mytesyn", [new Vector3(98.00867f, 41.275635f, 62.790894f)], false, "The Aftcastle", 128);
                            break;
                        case 2:
                            RepairTasks(132, [new Vector3(34.23728f, -7.8000317f, 94.41259f), new Vector3(24.826416f, -8, 93.18677f)], "Erkenbaud", [new Vector3(23.697266f, -8.1026f, 100.053345f)], "Antoinaut", [new Vector3(-80.00789f, -0.5001702f, -6.6672616f)]);
                            break;
                        case 3:
                            RepairTasks(130, [new Vector3(32.85266f, 6.999999f, -81.31531f)], "Zuzutyro", [new Vector3(29.495605f, 7.4500003f, -78.324646f)], "Otopa Pottopa", [new Vector3(-153.30743f, 5.2338257f, -98.039246f)]);
                            break;
                        default:
                            RepairTasks(129, [new Vector3(17.715698f, 40.200005f, 3.9520264f)], "Leofrun", [new Vector3(15.42688f, 39.99999f, 12.466553f)], "Mytesyn", [new Vector3(98.00867f, 41.275635f, 62.790894f)], false, "The Aftcastle", 128);
                            break;
                    }
                }
                else if (AutoDuty.Plugin.Configuration.AutoRepairSelf && !forceCity)
                    RepairTasks(0, [Vector3.Zero], "", [Vector3.Zero], "", [Vector3.Zero], true);
            }
        }

        public unsafe void RepairTasks(uint territoryType, Vector3[] vendorPositions, string vendorName, Vector3[] innKeepPositions, string innKeepName, Vector3[] barracksDoorPositions, bool selfRepair=false, string aethernetName = "", uint aethernetToTerritoryType = 0)
        {
            AtkUnitBase* addon = null;
            GameObject? gameObject = null;

            if (selfRepair)
                _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6), "Repair");
            else
            {
                if (((Svc.ClientState.TerritoryType == 536 || Svc.ClientState.TerritoryType == 177) && territoryType == 129) || ((Svc.ClientState.TerritoryType == 534 || Svc.ClientState.TerritoryType == 179) && territoryType == 132) || ((Svc.ClientState.TerritoryType == 535 || Svc.ClientState.TerritoryType == 178) && territoryType == 130))
                {
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair");
                    if (Svc.ClientState.TerritoryType == 536 || Svc.ClientState.TerritoryType == 534 || Svc.ClientState.TerritoryType == 535)
                        _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Exit to Maelstrom Command")) != null, "Repair");
                    else
                        _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Heavy Oaken Door")) != null, "Repair");
                    _taskManager.Enqueue(() => MovementHelper.Move(gameObject?.Position ?? Vector3.Zero, 0.25f, 2f), int.MaxValue, "Repair");
                    _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), int.MaxValue, "Repair");
                    _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Repair");
                    _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Repair");
                    _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno") != null, "Repair");
                    _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), "Repair");
                    _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Repair");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair");
                }
                else
                {
                    if (Svc.ClientState.TerritoryType != territoryType && Svc.ClientState.TerritoryType != aethernetToTerritoryType)
                    {
                        _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair");
                        _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Repair");
                        _taskManager.Enqueue(() => TeleportHelper.TeleportGCCity(), int.MaxValue, "Repair");
                        _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting && !ObjectHelper.IsReady, "Repair");
                        _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair");
                    }
                    if (!aethernetName.IsNullOrEmpty())
                    {
                        _taskManager.Enqueue(() => TeleportHelper.MoveToClosestAetheryte(aethernetToTerritoryType));
                        _taskManager.Enqueue(() => !ObjectHelper.IsJumping, 500, "Repair");
                        _taskManager.Enqueue(() => TeleportHelper.TeleportAethernet(aethernetName, aethernetToTerritoryType), int.MaxValue, "Repair");
                        _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Repair");
                        _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair");
                    }
                }
                foreach (var v in vendorPositions.Select((Value, Index) => (Value, Index)))
                {
                    if ((v.Index + 1) == vendorPositions.Length)
                        _taskManager.Enqueue(() => MovementHelper.Move(v.Value, 0.25f, 7f), int.MaxValue, "Repair");
                    else
                        _taskManager.Enqueue(() => MovementHelper.Move(v.Value), int.MaxValue, "Repair");
                    _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), int.MaxValue, "Repair");
                }
                _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Repair");
                _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Repair");
                _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(ObjectHelper.GetObjectByName(vendorName), "Repair") != null, "Repair");
            }
            _taskManager.Enqueue(() => AddonHelper.ClickRepair(), "Repair");
            _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), "Repair");
            if (selfRepair)
                _taskManager.Enqueue(() => !ObjectHelper.IsOccupied, "Repair");
            _taskManager.Enqueue(() => AgentModule.Instance()->GetAgentByInternalID((uint)AgentId.Repair)->Hide(), "Repair");
            if (AutoDuty.Plugin.Configuration.AutoRepairReturnToInn && _returnAfter)
            {
                foreach (var v in innKeepPositions.Select((Value, Index) => (Value, Index)))
                {
                    if ((v.Index + 1) == innKeepPositions.Length)
                        _taskManager.Enqueue(() => MovementHelper.Move(v.Value, 0.25f, 7f), int.MaxValue, "Repair");
                    else
                        _taskManager.Enqueue(() => MovementHelper.Move(v.Value), int.MaxValue, "Repair");
                    _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), "Repair");
                }
                _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), "Repair");
                _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Repair");
                _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Repair");
                _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(innKeepName)) != null, "Repair");
                _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "Talk") != null, "Repair");
                _taskManager.Enqueue(() => AddonHelper.ClickSelectString(0), "Repair");
                _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Repair");
                _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair");
            }
            else if (AutoDuty.Plugin.Configuration.AutoRepairReturnToBarracks && _returnAfter)
            {
                foreach (var v in barracksDoorPositions.Select((Value, Index) => (Value, Index)))
                {
                    if ((v.Index + 1) == barracksDoorPositions.Length)
                        _taskManager.Enqueue(() => MovementHelper.Move(v.Value, 0.25f, 3f), int.MaxValue, "Repair");
                    else
                        _taskManager.Enqueue(() => MovementHelper.Move(v.Value), int.MaxValue, "Repair");
                    _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), "Repair");
                }
                _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Repair");
                _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Repair");
                _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Entrance to the Barracks")) != null, "Repair");
                _taskManager.DelayNext("Repair", 50);
                _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno") != null, "Repair");
                _taskManager.DelayNext("Repair", 50);
                _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), "Repair");
                _taskManager.DelayNext("Repair", 50);
                _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Repair");
                _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair");
            }
            _taskManager.Enqueue(() => { AutoDuty.Plugin.Repairing = false; }, "Repair");
            _taskManager.Enqueue(() => { ExecSkipTalk.IsEnabled = false; }, "Repair");
        }
    }
}
