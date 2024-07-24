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
            Svc.Log.Debug("GCTurninHelper.Invoke");
            if (!Deliveroo_IPCSubscriber.IsEnabled)
                Svc.Log.Info("GC Turnin Requires Deliveroo plugin. Get @ https://git.carvel.li/liza/plugin-repo");
            else if (!GCTurninRunning)
            {
                Svc.Log.Info("GCTurnin Started");
                GCTurninRunning = true;
                Svc.Framework.Update += GCTurninUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal static void Stop() 
        {
            Svc.Log.Debug("GCTurninHelper.Stop");
            if (GCTurninRunning)
                Svc.Log.Info("GCTurnin Finished");
            _deliverooStarted = false;
            GCTurninRunning = false;
            AutoDuty.Plugin.Action = "";
            Svc.Framework.Update -= GCTurninUpdate;
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool GCTurninRunning = false;
        internal unsafe static Vector3 GCSupplyLocation => UIState.Instance()->PlayerState.GrandCompany == 1 ? new Vector3(94.02183f, 40.27537f, 74.475525f) : (UIState.Instance()->PlayerState.GrandCompany == 2 ? new Vector3(-68.678566f, -0.5015295f, -8.470145f) : new Vector3(-142.82619f, 4.0999994f, -106.31349f));

        private static bool _deliverooStarted = false;
        private static Chat _chat = new();

        internal static unsafe void GCTurninUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.Started)
            {
                Svc.Log.Debug("AutoDuty is Started, Stopping GCTurninHelper");
                Stop();
            }
            if (!_deliverooStarted && Deliveroo_IPCSubscriber.IsTurnInRunning())
            {
                Svc.Log.Info("Deliveroo has Started");
                _deliverooStarted = true;
                return;
            }
            else if (_deliverooStarted && !Deliveroo_IPCSubscriber.IsTurnInRunning())
            {
                Svc.Log.Debug("Deliveroo is Complete");
                Stop();
                return;
            }

            if (!EzThrottler.Throttle("Turnin", 50))
                return;

            if (GotoHelper.GotoRunning)
            {
                Svc.Log.Debug("Goto Running");
                return;
            }
            AutoDuty.Plugin.Action = "GC Turning In";

            if (!GotoHelper.GotoRunning && Svc.ClientState.TerritoryType != ObjectHelper.GrandCompanyTerritoryType(UIState.Instance()->PlayerState.GrandCompany))
            {
                Svc.Log.Debug("Moving to GC Supply");
                GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(UIState.Instance()->PlayerState.GrandCompany), [GCSupplyLocation], 0.25f, 3f);
                return;
            }

            if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) > 5 && ObjectHelper.IsReady && VNavmesh_IPCSubscriber.Nav_IsReady() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
            {
                Svc.Log.Debug("Setting Move to Personnel Officer");
                MovementHelper.Move(GCSupplyLocation, 0.25f, 5);
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) > 5 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                Svc.Log.Debug("Moving to Personnel Officer");
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) <= 5 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                Svc.Log.Debug("Stopping Path");
                VNavmesh_IPCSubscriber.Path_Stop();
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) <= 5 && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
            {
                Svc.Log.Debug("Sending Chat Command /deliveroo e");
                _chat.SendMessage("/deliveroo e");
                return;
            }
        }
    }
}
