using AutoDuty.IPC;
using ClickLib.Clicks;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Numerics;

namespace AutoDuty.Managers
{
    internal class RepairManager(TaskManager _taskManager, VNavmesh_IPCSubscriber _vnavIPC, ActionsManager _actions)
    {
        public unsafe static float LowestEquippedCondition()
        {
            var equipedItems = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            uint itemLowestCondition = 60000;
            for (int i = 0; i < 13; i++)
            {
                Svc.Log.Info($"{equipedItems->Items[i].Condition}");
                if (itemLowestCondition > equipedItems->Items[i].Condition)
                    itemLowestCondition = equipedItems->Items[i].Condition;
            }

            return itemLowestCondition / 300f;
        }

        public unsafe void Repair()
        {
            if (AutoDuty.Plugin.Configuration.AutoRepair && LowestEquippedCondition() <= AutoDuty.Plugin.Configuration.AutoRepairPct)
            {
                if (AutoDuty.Plugin.Configuration.AutoRepairCity)
                {
                    if (AutoDuty.Plugin.Configuration.AutoRepairLimsa)
                        RepairTasks(129, [new Vector3(-247.06625f, 16.2f, 50.961113f)], "Alistair", false);
                    else if (AutoDuty.Plugin.Configuration.AutoRepairUldah)
                        RepairTasks(130, [new Vector3(-112.40048f, 3.9999998f, -104.43906f), new Vector3(-155.58505f, 12, -24.212015f)], "Hehezofu", false);
                    else if (AutoDuty.Plugin.Configuration.AutoRepairGridania)
                        RepairTasks(132, [new Vector3(34.23728f, -7.8000317f, 94.41259f), new Vector3(25.467928f, -8.000013f, 93.7083f)], "Erkenbaud", false);
                }
                else if (AutoDuty.Plugin.Configuration.AutoRepairSelf)
                    RepairTasks(0, [Vector3.Zero], "", true);
            }
        }

        public unsafe void RepairTasks(uint territoryType, Vector3[] vendorPosition, string vendorName, bool selfRepair)
        {
            nint addon = 0;
            if (selfRepair)
                _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6));
            else
            {
                GameObject? gameObject = null;
                _taskManager.Enqueue(() => ObjectManager.IsReady, "Repair");
                _taskManager.Enqueue(() => { if (Svc.ClientState.TerritoryType != territoryType) TeleportManager.TeleportCity(territoryType); }, "Repair");
                _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == territoryType, int.MaxValue, "Repair");
                _taskManager.Enqueue(() => ObjectManager.IsReady, int.MaxValue, "Repair");
                _taskManager.Enqueue(() => _vnavIPC.Nav_IsReady(), int.MaxValue, "Repair");
                foreach (var position in vendorPosition)
                {
                    _taskManager.Enqueue(() => _vnavIPC.SimpleMove_PathfindAndMoveTo(position, false), "Repair");
                    _taskManager.Enqueue(() => (!_vnavIPC.SimpleMove_PathfindInProgress() && _vnavIPC.Path_NumWaypoints() == 0), int.MaxValue, "Repair");
                }
                _taskManager.Enqueue(() => (gameObject = ObjectManager.GetObjectByNameAndRadius(vendorName)) != null, "Repair");
                _taskManager.Enqueue(() => { if (gameObject != null) ObjectManager.InteractWithObject(gameObject); });
            }
            _taskManager.Enqueue(() => (addon = Svc.GameGui.GetAddonByName("Repair", 1)) > 0 && _actions.IsAddonReady(addon), "Repair");
            _taskManager.DelayNext("Repair", 250);
            _taskManager.Enqueue(() => { new ClickRepair(addon).RepairAll(); }, "Repair");
            _taskManager.DelayNext("Repair", 250);
            _taskManager.Enqueue(() => (addon = Svc.GameGui.GetAddonByName("SelectYesno", 1)) > 0 && _actions.IsAddonReady(addon), "Repair");
            _taskManager.Enqueue(() => ClickSelectYesNo.Using(addon).Yes(), "Repair");
            _taskManager.DelayNext("Repair", 250);
            _taskManager.Enqueue(() => !ObjectManager.IsOccupied);
            _taskManager.Enqueue(() => AgentModule.Instance()->GetAgentByInternalID(41)->Hide(), "Repair");
            
        }
    }
}
