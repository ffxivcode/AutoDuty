using AutoDuty.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System.Numerics;

namespace AutoDuty.Helpers
{
    internal static class GCTurninHelper
    {
        internal static void Invoke() 
        {
            if (!Deliveroo_IPCSubscriber.IsEnabled)
                Svc.Log.Info("GC Turnin Requires Deliveroo plugin. Get @ https://git.carvel.li/liza/plugin-repo");
            else if (!GCTurninRunning)
            {
                Svc.Log.Info("GCTurnin Started");
                GCTurninRunning = true;
                Svc.Framework.Update += GCTurninUpdate;
            }
        }

        internal static void Stop() 
        {
            if (GCTurninRunning)
                Svc.Log.Info("GCTurnin Finished");
            deliverooStarted = false;
            GCTurninRunning = false;
            AutoDuty.Plugin.Action = "";
            Svc.Framework.Update -= GCTurninUpdate;
        }

        internal static bool GCTurninRunning = false;
        internal unsafe static Vector3 GCSupplyLocation => UIState.Instance()->PlayerState.GrandCompany == 1 ? new Vector3(94.02183f, 40.27537f, 74.475525f) : (UIState.Instance()->PlayerState.GrandCompany == 2 ? new Vector3(-68.678566f, -0.5015295f, -8.470145f) : new Vector3(-142.82619f, 4.0999994f, -106.31349f));
        
        private static IGameObject? personnelOfficer = null;
        private static IGameObject? quartermaster = null;
        private static bool deliverooStarted = false;
        private static Chat chat = new();

        internal static unsafe void GCTurninUpdate(IFramework framework)
        {
            if (!EzThrottler.Throttle("Turnin", 50))
                return;

            if (GotoHelper.GotoRunning)
                return;

            AutoDuty.Plugin.Action = "GC Turning In";

            if (!GotoHelper.GotoRunning && (personnelOfficer = ObjectHelper.GetObjectByPartialName("Personnel Officer")) == null)
            {
                GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(UIState.Instance()->PlayerState.GrandCompany), [GCSupplyLocation], 0.25f, 3f);
                return;
            }

            if ((quartermaster = ObjectHelper.GetObjectByPartialName("Quartermaster")) == null)
                return;

            if (ObjectHelper.GetDistanceToPlayer(personnelOfficer) > 5 && ObjectHelper.IsReady && VNavmesh_IPCSubscriber.Nav_IsReady() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
            {
                MovementHelper.Move(personnelOfficer, 0.25f, 5);
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(personnelOfficer) > 5 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                return;
            else if (ObjectHelper.GetDistanceToPlayer(personnelOfficer) <= 5 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(personnelOfficer) <= 5 && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0 && !Deliveroo_IPCSubscriber.IsTurnInRunning() && !deliverooStarted)
            {
                chat.SendMessage("/deliveroo e");
                deliverooStarted = true;
                return;
            }
            else if (!Deliveroo_IPCSubscriber.IsTurnInRunning() && deliverooStarted)
                Stop();
        }
    }
}
