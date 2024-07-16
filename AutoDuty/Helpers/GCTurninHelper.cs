using AutoDuty.IPC;
using AutoDuty.Managers;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoDuty.Helpers
{
    internal static class GCTurninHelper
    {
        internal static void Invoke() => Svc.Framework.Update += GCTurninUpdate;

        internal static void Stop() => Svc.Framework.Update -= GCTurninUpdate;

        internal static bool GCTurninRunning = false;

        private static IGameObject? personnelOfficer = null;

        private static IGameObject? quartermaster = null;

        private static readonly TaskManager taskManager =  new();

        private static bool deliverooStarted = false;

        private static Chat chat = new();

        internal static unsafe void GCTurninUpdate(IFramework framework)
        {
            if (!AutoDuty.Plugin.Configuration.AutoGCTurnin || !Deliveroo_IPCSubscriber.IsEnabled)
            {
                Svc.Log.Info("GCTurnin or Deliveroo Not Enabled");
                Stop();
                return;
            }
            else if (!GCTurninRunning)
            {
                Svc.Log.Info("GCTurnin Started");
                GCTurninRunning = true;
            }
            if (!EzThrottler.Throttle("Turnin", 50))
                return;

            if (AutoDuty.Plugin.Goto)
                return;

            if (!AutoDuty.Plugin.Goto && (personnelOfficer = ObjectHelper.GetObjectByPartialName("Personnel Officer")) == null)
            {
                //UIState.Instance()->PlayerState.GrandCompany)
                //Limsa=1,129, Gridania=2,132, Uldah=3,130
                AutoDuty.Plugin.Goto = true;
                if ((UIState.Instance()->PlayerState.GrandCompany == 1 && Svc.ClientState.TerritoryType != 128) || (UIState.Instance()->PlayerState.GrandCompany == 2 && Svc.ClientState.TerritoryType != 132) || (UIState.Instance()->PlayerState.GrandCompany == 3 && Svc.ClientState.TerritoryType != 130))
                {
                    //Goto GCSupply
                    var g = new GotoManager(taskManager);
                    g.Goto(false, false, true);
                }
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
            {
                deliverooStarted = false;
                GCTurninRunning = false;
                Svc.Log.Info("GCTurnin Finished");
                Stop();
                return;
            }
        }
    }
}
