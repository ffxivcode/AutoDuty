using AutoDuty.External;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Linq;
using System.Numerics;

namespace AutoDuty.Managers
{
    internal class GotoManager(TaskManager _taskManager)
    {
        private IGameObject? gameObject = null;

        public unsafe void GotoGCSupply(Vector3[] gcSupplyPositions)
        {
            foreach (var v in gcSupplyPositions.Select((Value, Index) => (Value, Index)))
            {
                if ((v.Index + 1) == gcSupplyPositions.Length)
                    _taskManager.Enqueue(() => MovementHelper.Move(v.Value, 0.25f, 3f), int.MaxValue, "Goto");
                else
                    _taskManager.Enqueue(() => MovementHelper.Move(v.Value), int.MaxValue, "Goto");
                _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), "Goto");
            }
            _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Goto");
            _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Goto");
        }

        public unsafe void GotoBarracks(Vector3[] barracksDoorPositions)
        {
            foreach (var v in barracksDoorPositions.Select((Value, Index) => (Value, Index)))
            {
                if ((v.Index + 1) == barracksDoorPositions.Length)
                    _taskManager.Enqueue(() => MovementHelper.Move(v.Value, 0.25f, 3f), int.MaxValue, "Goto");
                else
                    _taskManager.Enqueue(() => MovementHelper.Move(v.Value), int.MaxValue, "Goto");
                _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), "Goto");
            }
            _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Goto");
            _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Goto");
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Entrance to the Barracks")) != null, "Goto");
            _taskManager.DelayNext("Goto", 50);
            _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno") != null, "Goto");
            _taskManager.DelayNext("Goto", 50);
            _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), "Goto");
            _taskManager.DelayNext("Goto", 50);
            _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Goto");
            _taskManager.Enqueue(() => ObjectHelper.IsReady, "Goto");
        }

        public unsafe void GotoInn(Vector3[] innKeepPositions, string innKeepName)
        {

            foreach (var v in innKeepPositions.Select((Value, Index) => (Value, Index)))
            {
                if ((v.Index + 1) == innKeepPositions.Length)
                    _taskManager.Enqueue(() => MovementHelper.Move(v.Value, 0.25f, 7f), int.MaxValue, "Goto");
                else
                    _taskManager.Enqueue(() => MovementHelper.Move(v.Value), int.MaxValue, "Goto");
                _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), "Goto");
            }
            _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), "Goto");
            _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Goto");
            _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Goto");
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(innKeepName)) != null, "Goto");
            _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectString") != null, "Goto");
            _taskManager.Enqueue(() => AddonHelper.ClickSelectString(0));
            _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Goto");
            _taskManager.Enqueue(() => ObjectHelper.IsReady, "Goto");
        }

        public unsafe void Goto(bool gotoBarracks, bool gotoInn, bool gotoGCSupply) 
        {
            if ((gotoBarracks && Svc.ClientState.TerritoryType != 536 && Svc.ClientState.TerritoryType != 534 && Svc.ClientState.TerritoryType != 535) || (gotoInn && Svc.ClientState.TerritoryType != 177 && Svc.ClientState.TerritoryType != 179 && Svc.ClientState.TerritoryType != 178) || gotoGCSupply)
            {
                AutoDuty.Plugin.Action = $"Going to: {(gotoBarracks ? "Barracks" : (gotoGCSupply ? "GCSupply" : "Inn"))}";
                AutoDuty.Plugin.Goto = true;
                ExecSkipTalk.IsEnabled = true;
                if (AutoDuty.Plugin.InDungeon)
                {
                    AutoDuty.Plugin.ExitDuty();
                    _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Goto");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, "Goto");
                }
                switch (UIState.Instance()->PlayerState.GrandCompany)
                {
                    //Limsa=1,129, Gridania=2,132, Uldah=3,130 -- Goto Limsa if no GC
                    case 1:
                        GotoTasks(129, [new Vector3(15.42688f, 39.99999f, 12.466553f)], "Mytesyn", [new Vector3(98.00867f, 41.275635f, 62.790894f)], [new Vector3(94.02183f, 40.27537f, 74.475525f)], gotoBarracks, gotoInn, gotoGCSupply, "The Aftcastle", 128);
                        break;
                    case 2:
                        GotoTasks(132, [new Vector3(23.697266f, -8.1026f, 100.053345f)], "Antoinaut", [new Vector3(-80.00789f, -0.5001702f, -6.6672616f)], [new Vector3(-68.678566f, -0.5015295f, -8.470145f)], gotoBarracks, gotoInn, gotoGCSupply);
                        break;
                    case 3:
                        GotoTasks(130, [new Vector3(29.495605f, 7.4500003f, -78.324646f)], "Otopa Pottopa", [new Vector3(-153.30743f, 5.2338257f, -98.039246f)], [new Vector3(-142.82619f, 4.0999994f, -106.31349f)], gotoBarracks, gotoInn, gotoGCSupply);
                        break;
                    default:
                        GotoTasks(130, [new Vector3(29.495605f, 7.4500003f, -78.324646f)], "Otopa Pottopa", [new Vector3(-153.30743f, 5.2338257f, -98.039246f)], [new Vector3(-142.82619f, 4.0999994f, -106.31349f)], gotoBarracks, gotoInn, gotoGCSupply);
                        break;
                }
            }
        }
        public unsafe void GotoTasks(uint territoryType, Vector3[] innKeepPositions, string innKeepName, Vector3[] barracksDoorPositions, Vector3[] gcSupplyPositions, bool gotoBarracks, bool gotoInn, bool gotoGCSupply, string aethernetName = "", uint aethernetToTerritoryType = 0)
        {
            AtkUnitBase* addon = null;

            if ((((Svc.ClientState.TerritoryType == 536 && (gotoInn || gotoGCSupply)) || (Svc.ClientState.TerritoryType == 177 && (gotoBarracks || gotoGCSupply))) && territoryType == 129) || (((Svc.ClientState.TerritoryType == 534 && (gotoInn || gotoGCSupply)) || (Svc.ClientState.TerritoryType == 179 && (gotoBarracks || gotoGCSupply))) && territoryType == 132) || (((Svc.ClientState.TerritoryType == 535 && (gotoInn || gotoGCSupply)) || (Svc.ClientState.TerritoryType == 178 && (gotoBarracks || gotoGCSupply))) && territoryType == 130))
            {
                _taskManager.Enqueue(() => ObjectHelper.IsReady, "Goto");
                if (Svc.ClientState.TerritoryType == 536 || Svc.ClientState.TerritoryType == 534 || Svc.ClientState.TerritoryType == 535)
                    _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Exit to Maelstrom Command")) != null, "Goto");
                else
                    _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Heavy Oaken Door")) != null, "Goto");
                _taskManager.Enqueue(() => MovementHelper.Move(gameObject?.Position ?? Vector3.Zero, 0.25f, 2f), int.MaxValue, "Goto");
                _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.Path_IsRunning(), int.MaxValue, "Goto");
                _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Goto");
                _taskManager.Enqueue(() => !ObjectHelper.IsJumping, "Goto");
                _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno") != null, "Goto");
                _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), "Goto");
                _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Goto");
                _taskManager.Enqueue(() => ObjectHelper.IsReady, "Goto");
            }
            else
            {
                if (Svc.ClientState.TerritoryType != territoryType && Svc.ClientState.TerritoryType != aethernetToTerritoryType)
                {
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, "Goto");
                    _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Goto");
                    _taskManager.Enqueue(() => TeleportHelper.TeleportGCCity(), int.MaxValue, "Goto");
                    _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting && !ObjectHelper.IsReady, "Goto");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, "Goto");
                }
                if (!aethernetName.IsNullOrEmpty())
                {
                    _taskManager.Enqueue(() => TeleportHelper.MoveToClosestAetheryte(aethernetToTerritoryType));
                    _taskManager.Enqueue(() => !ObjectHelper.IsJumping, 500, "Goto");
                    _taskManager.Enqueue(() => TeleportHelper.TeleportAethernet(aethernetName, aethernetToTerritoryType), int.MaxValue, "Goto");
                    _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "Goto");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, "Goto");
                }
            }
            if (gotoInn)
                GotoInn(innKeepPositions, innKeepName);
            else if (gotoBarracks)
                GotoBarracks(barracksDoorPositions);
            else if (gotoGCSupply)
                GotoGCSupply(gcSupplyPositions);

            _taskManager.Enqueue(() => { AutoDuty.Plugin.Goto = false; }, "Goto");
            _taskManager.Enqueue(() => { ExecSkipTalk.IsEnabled = false; }, "Goto");
        }
    }
}
