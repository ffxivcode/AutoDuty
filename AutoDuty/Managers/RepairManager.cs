using AutoDuty.External;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
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
                            RepairTasks(129, [new Vector3(17.715698f, 40.200005f, 3.9520264f)], "Leofrun", false, "The Aftcastle", 128);
                            break;
                        case 2:
                            RepairTasks(132, [new Vector3(34.23728f, -7.8000317f, 94.41259f), new Vector3(24.826416f, -8, 93.18677f)], "Erkenbaud");
                            break;
                        case 3:
                            RepairTasks(130, [new Vector3(32.85266f, 6.999999f, -81.31531f)], "Zuzutyro");
                            break;
                        default:
                            RepairTasks(130, [new Vector3(32.85266f, 6.999999f, -81.31531f)], "Zuzutyro");
                            break;
                    }
                }
                else if (AutoDuty.Plugin.Configuration.AutoRepairSelf && !forceCity)
                    RepairTasks(0, [Vector3.Zero], "", true);
            }
        }

        public unsafe void RepairTasks(uint territoryType, Vector3[] vendorPositions, string vendorName, bool selfRepair=false, string aethernetName = "", uint aethernetToTerritoryType = 0)
        {
            AtkUnitBase* addon = null;
            IGameObject? gameObject = null;

            if (selfRepair)
                _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6), "Repair-SelfRepair");
            else
            {
                if (((Svc.ClientState.TerritoryType == 536 || Svc.ClientState.TerritoryType == 177) && territoryType == 129) || ((Svc.ClientState.TerritoryType == 534 || Svc.ClientState.TerritoryType == 179) && territoryType == 132) || ((Svc.ClientState.TerritoryType == 535 || Svc.ClientState.TerritoryType == 178) && territoryType == 130))
                {
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair-WaitPlayerIsReady");
                    if (Svc.ClientState.TerritoryType == 536 || Svc.ClientState.TerritoryType == 534 || Svc.ClientState.TerritoryType == 535)
                        _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Exit to")) != null, "Repair-GetBarracksDoorGameObject");
                    else
                        _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Heavy Oaken Door")) != null, "Repair-GetInnDoorGameObject");
                    _taskManager.Enqueue(() => MovementHelper.Move(gameObject?.Position ?? Vector3.Zero, 0.25f, 2f), int.MaxValue, "Repair-Move");
                    _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), int.MaxValue, "Repair-WaitPathNotRunning");
                    _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Repair-WaitPlayerNotCasting");
                    _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Repair-WaitPlayerNotJumping");
                    _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno") != null, "Repair-InteractGameObject");
                    _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), "Repair-ClickSelectYesNo");
                    _taskManager.Enqueue(() => !ObjectHelper.IsReady, "Repair-WaitPlayerNotReady");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, "RepairWaitPlayerReady");
                }
                else
                {
                    if (Svc.ClientState.TerritoryType != territoryType && Svc.ClientState.TerritoryType != aethernetToTerritoryType)
                    {
                        _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair-WaitPlayerReady");
                        _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Repair-WaitPlayNotCasting");
                        _taskManager.Enqueue(() => TeleportHelper.TeleportGCCity(), int.MaxValue, "Repair-TeleportGCCity");
                        _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting && !ObjectHelper.IsReady, "Repair-WaitPlayerNotCastingAndPlayerNotReady");
                        _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair-WaitPlayerReady");
                    }
                    if (!aethernetName.IsNullOrEmpty())
                    {
                        _taskManager.Enqueue(() => TeleportHelper.MoveToClosestAetheryte(aethernetToTerritoryType), "Repair-MoveClosestAethernet");
                        _taskManager.Enqueue(() => !ObjectHelper.IsJumping, 500, "Repair-WaitPlayerNotJumping");
                        _taskManager.Enqueue(() => TeleportHelper.TeleportAethernet(aethernetName, aethernetToTerritoryType), int.MaxValue, "Repair-TeleportAethernet");
                        _taskManager.Enqueue(() => !ObjectHelper.IsReady, "Repair-WaitPlayerNotReady");
                        _taskManager.Enqueue(() => ObjectHelper.IsReady, "Repair-WaitPlayerReady");
                    }
                }
                foreach (var v in vendorPositions.Select((Value, Index) => (Value, Index)))
                {
                    if ((v.Index + 1) == vendorPositions.Length)
                        _taskManager.Enqueue(() => MovementHelper.Move(v.Value, 0.25f, 7f), int.MaxValue, "Repair-Move");
                    else
                        _taskManager.Enqueue(() => MovementHelper.Move(v.Value), int.MaxValue, "Repair-Move");
                    _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), int.MaxValue, "Repair-WaitPathNotRunning");
                }
                _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Repair-WaitPlayerNotCasting");
                _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Repair-WaitPlayerNotJumping");
                _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(ObjectHelper.GetObjectByName(vendorName), "Repair") != null, "Repair-GetRepairVendorGameObject");
            }
            _taskManager.Enqueue(() => AddonHelper.ClickRepair(), "Repair-ClickRepair");
            _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), "Repair-ClickSelectYesno");
            if (selfRepair)
                _taskManager.Enqueue(() => !ObjectHelper.IsOccupied, "Repair-WaitPlayerNotOccupied");
            _taskManager.Enqueue(() => AgentModule.Instance()->GetAgentByInternalId(AgentId.Repair)->Hide(), "Repair-CloseRepairAddon");
            _taskManager.Enqueue(() => { AutoDuty.Plugin.Repairing = false; }, "Repair-SetRepairingFalse");
            _taskManager.Enqueue(() => { ExecSkipTalk.IsEnabled = false; }, "Repair-SetSkipTalkFalse");
        }
    }
}
