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
    internal unsafe static class GCTurninHelper
    {
        internal static void Invoke()
        {
            Svc.Log.Debug("GCTurninHelper.Invoke");
            if (!Deliveroo_IPCSubscriber.IsEnabled)
                Svc.Log.Info("GC Turnin Requires Deliveroo plugin. Get @ https://git.carvel.li/liza/plugin-repo");
            else if (_gCTurnInState == GCTurnInState.GC_TURNIN_ENDED)
            {
                Svc.Log.Info("GCTurnin Started");
                _gCTurnInState = GCTurnInState.GC_TURNIN_STARTED;
                GCTurninRunning = true;
                Svc.Framework.Update += GCTurninUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }

        }

        internal static void Stop()
        {
            if (_gCTurnInState == GCTurnInState.GC_TURNIN_COMPLETE)
                Svc.Log.Info("GCTurnin Finished");
            else if (_gCTurnInState == GCTurnInState.GC_TURNIN_ERROR)
                Svc.Log.Info("GCTurnin Error");
            else
                Svc.Log.Info("GCTurnin Stopped");

            _gCTurnInState = GCTurnInState.GC_TURNIN_ENDED;
            GCTurninRunning = false;
            AutoDuty.Plugin.Action = "";
            Svc.Framework.Update -= GCTurninUpdate;
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static IGameObject? GetPersonnelOfficerObject(uint _grandCompany) => _grandCompany == 1 ? ObjectHelper.GetObjectByDataId(1002388u) : (_grandCompany == 2 ? ObjectHelper.GetObjectByDataId(1002394u) : ObjectHelper.GetObjectByDataId(1002391u));

        private enum GCTurnInState : uint
        {
            GC_TURNIN_ENDED = 0u,
            GC_TURNIN_STARTED,
            GC_TURNIN_GOTOINVOKED,
            GC_TURNIN_MOVINGTOOFFICER,
            GC_TURNIN_WAIT_MOVING_DONE,
            GC_TURNIN_DELIVEROO_COMMAND_DO,
            GC_TURNIN_DELIVEROO_COMMAND_DONE,
            GC_TURNIN_DELIVEROO_DO,
            GC_TURNIN_ERROR,
            GC_TURNIN_COMPLETE
        }

        internal static bool GCTurninRunning = false;
        internal unsafe static Vector3 GCSupplyLocation => UIState.Instance()->PlayerState.GrandCompany == 1 ? new Vector3(94.02183f, 40.27537f, 74.475525f) : (UIState.Instance()->PlayerState.GrandCompany == 2 ? new Vector3(-68.678566f, -0.5015295f, -8.470145f) : new Vector3(-142.82619f, 4.0999994f, -106.31349f));

        private static IGameObject? _personnelOfficer = null;
        private static IGameObject? _quartermaster = null;
        private static GCTurnInState _gCTurnInState = GCTurnInState.GC_TURNIN_ENDED;
        private static Chat _chat = new();

        internal static unsafe void GCTurninUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.Started)
            {
                Svc.Log.Debug("AutoDuty is Started, Stopping GCTurninHelper");
                Stop();
            }
            if ((_gCTurnInState == GCTurnInState.GC_TURNIN_DELIVEROO_COMMAND_DONE) && Deliveroo_IPCSubscriber.IsTurnInRunning())
            {
                Svc.Log.Info("Deliveroo has Started");
                _gCTurnInState = GCTurnInState.GC_TURNIN_DELIVEROO_DO;
                return;
            }
            else if ((_gCTurnInState == GCTurnInState.GC_TURNIN_DELIVEROO_DO) && !Deliveroo_IPCSubscriber.IsTurnInRunning())
            {
                Svc.Log.Debug("Deliveroo is Complete");
                _gCTurnInState = GCTurnInState.GC_TURNIN_COMPLETE;
                Stop();
                return;
            }

            if (!EzThrottler.Throttle("Turnin", 50))
                return;

            if ((_personnelOfficer = GetPersonnelOfficerObject(UIState.Instance()->PlayerState.GrandCompany)) == null)
            {
                Svc.Log.Debug("Personnel Officer Objerct Not Found");
                _gCTurnInState = GCTurnInState.GC_TURNIN_ERROR;
                Stop();
                return;
            }

            if (GotoHelper.GotoRunning && (_gCTurnInState == GCTurnInState.GC_TURNIN_GOTOINVOKED))
            {
                Svc.Log.Debug("Goto Running");
                _gCTurnInState = GCTurnInState.GC_TURNIN_MOVINGTOOFFICER;
                return;
            }
            AutoDuty.Plugin.Action = "GC Turning In";

            if (!GotoHelper.GotoRunning && (_gCTurnInState == GCTurnInState.GC_TURNIN_STARTED))
            {
                Svc.Log.Debug("Moving to GC Supply");
                GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(UIState.Instance()->PlayerState.GrandCompany), [GCSupplyLocation], 0.25f, 3f);
                _gCTurnInState = GCTurnInState.GC_TURNIN_GOTOINVOKED;
                return;
            }

            if (ObjectHelper.GetDistanceToPlayer(_personnelOfficer) > 5 && ObjectHelper.IsReady && VNavmesh_IPCSubscriber.Nav_IsReady() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0 && (_gCTurnInState == GCTurnInState.GC_TURNIN_MOVINGTOOFFICER))
            {
                Svc.Log.Debug("Setting Move to Personnel Officer");
                MovementHelper.Move(_personnelOfficer, 0.25f, 5);
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(_personnelOfficer) > 5 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0 && (_gCTurnInState == GCTurnInState.GC_TURNIN_MOVINGTOOFFICER))
            {
                Svc.Log.Debug("Moving to Personnel Officer");
                _gCTurnInState = GCTurnInState.GC_TURNIN_WAIT_MOVING_DONE;
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(_personnelOfficer) <= 5 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0 && (_gCTurnInState == GCTurnInState.GC_TURNIN_WAIT_MOVING_DONE))
            {
                Svc.Log.Debug("Stopping Path");
                VNavmesh_IPCSubscriber.Path_Stop();
                _gCTurnInState = GCTurnInState.GC_TURNIN_DELIVEROO_COMMAND_DO;
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(_personnelOfficer) <= 5 && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0 && (_gCTurnInState == GCTurnInState.GC_TURNIN_DELIVEROO_COMMAND_DO))
            {
                Svc.Log.Debug("Sending Chat Command /deliveroo e");
                _chat.SendMessage("/deliveroo e");
                _gCTurnInState = GCTurnInState.GC_TURNIN_DELIVEROO_COMMAND_DONE;
                return;
            }
            else
            {
                // no opertion
                return;
            }
        }
    }
}
