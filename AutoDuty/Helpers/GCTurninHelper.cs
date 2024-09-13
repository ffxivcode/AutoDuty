using AutoDuty.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
            else if (State != ActionState.Running)
            {
                Svc.Log.Info("GCTurnin Started");
                State = ActionState.Running;
                AutoDuty.Plugin.States |= PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(false);
                SchedulerHelper.ScheduleAction("GCTurninTimeOut", Stop, 600000);
                Svc.Framework.Update += GCTurninUpdate;
            }
        }

        internal static void Stop() 
        {
            Svc.Log.Debug("GCTurninHelper.Stop");
            if (State == ActionState.Running)
                Svc.Log.Info("GCTurnin Finished");
            _deliverooStarted = false;
            GotoHelper.Stop();
            AutoDuty.Plugin.Action = "";
            Svc.Framework.Update += GCTurninStopUpdate;
            Svc.Framework.Update -= GCTurninUpdate;
            SchedulerHelper.DescheduleAction("GCTurninTimeOut");
        }

        internal static ActionState State = ActionState.None;
        internal static Vector3 GCSupplyLocation => PlayerHelper.GetGrandCompany() == 1 ? new Vector3(94.02183f, 40.27537f, 74.475525f) : (PlayerHelper.GetGrandCompany() == 2 ? new Vector3(-68.678566f, -0.5015295f, -8.470145f) : new Vector3(-142.82619f, 4.0999994f, -106.31349f));

        private static IGameObject? _personnelOfficerGameObject => ObjectHelper.GetObjectByDataId(_personnelOfficerDataId);
        private static uint _personnelOfficerDataId => PlayerHelper.GetGrandCompany() == 1 ? 1002388u : (PlayerHelper.GetGrandCompany() == 2 ? 1002394u : 1002391u);
        private static uint _aetheryteTicketId = PlayerHelper.GetGrandCompany() == 1 ? 21069u : (PlayerHelper.GetGrandCompany() == 2 ? 21070u : 21071u);
        private static bool _deliverooStarted = false;
        private static Chat _chat = new();

        internal static unsafe void GCTurninStopUpdate(IFramework framework)
        {
            if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInQuestEvent])
            {
                State = ActionState.None;
                AutoDuty.Plugin.States &= ~PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(true);
                Svc.Framework.Update -= GCTurninStopUpdate;
            }
            else if (Svc.Targets.Target != null)
                Svc.Targets.Target = null;
            else if (GenericHelpers.TryGetAddonByName("GrandCompanySupplyReward", out AtkUnitBase* addonGrandCompanySupplyReward))
                addonGrandCompanySupplyReward->Close(true);
            else if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                addonSelectYesno->Close(true);
            else if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                addonSelectString->Close(true);
            else if (GenericHelpers.TryGetAddonByName("GrandCompanySupplyList", out AtkUnitBase* addonGrandCompanySupplyList))
                addonGrandCompanySupplyList->Close(true);
            return;
        }

        internal static unsafe void GCTurninUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.States.HasFlag(PluginState.Navigating))
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

            if (!EzThrottler.Throttle("Turnin", 250))
                return;

            if (GotoHelper.State == ActionState.Running)
            {
                //Svc.Log.Debug("Goto Running");
                return;
            }
            AutoDuty.Plugin.Action = "GC Turning In";

            if (GotoHelper.State != ActionState.Running && Svc.ClientState.TerritoryType != PlayerHelper.GetGrandCompanyTerritoryType(PlayerHelper.GetGrandCompany()))
            {
                Svc.Log.Debug("Moving to GC Supply");
                if (AutoDuty.Plugin.Configuration.AutoGCTurninUseTicket && InventoryHelper.ItemCount(_aetheryteTicketId) > 0)
                {
                    if (!PlayerHelper.IsCasting)
                        InventoryHelper.UseItem(_aetheryteTicketId);
                }
                else
                    GotoHelper.Invoke(PlayerHelper.GetGrandCompanyTerritoryType(PlayerHelper.GetGrandCompany()), [GCSupplyLocation], 0.25f, 2f, false);
                return;
            }

            if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) > 4 && PlayerHelper.IsReady && VNavmesh_IPCSubscriber.Nav_IsReady() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
            {
                Svc.Log.Debug("Setting Move to Personnel Officer");
                MovementHelper.Move(GCSupplyLocation, 0.25f, 4f);
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) > 4 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                Svc.Log.Debug("Moving to Personnel Officer");
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) <= 4 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                Svc.Log.Debug("Stopping Path");
                VNavmesh_IPCSubscriber.Path_Stop();
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) <= 4 && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0 && !_deliverooStarted)
            {
                if (_personnelOfficerGameObject == null)
                    return;
                if (Svc.Targets.Target?.DataId != _personnelOfficerGameObject.DataId)
                {
                    Svc.Log.Debug($"Targeting {_personnelOfficerGameObject.Name}({_personnelOfficerGameObject.DataId}) CurrentTarget={Svc.Targets.Target}({Svc.Targets.Target?.DataId})");
                    Svc.Targets.Target = _personnelOfficerGameObject;
                }
                else if (!GenericHelpers.TryGetAddonByName("GrandCompanySupplyList", out AtkUnitBase* addonGrandCompanySupplyList) || !GenericHelpers.IsAddonReady(addonGrandCompanySupplyList))
                {
                    if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString) && GenericHelpers.IsAddonReady(addonSelectString))
                    {
                        Svc.Log.Debug($"Clicking SelectString");
                        AddonHelper.ClickSelectString(0);
                    }
                    else
                    {
                        Svc.Log.Debug($"Interacting with {_personnelOfficerGameObject.Name}");
                        ObjectHelper.InteractWithObjectUntilAddon(_personnelOfficerGameObject, "SelectString");
                    }
                }
                else
                {
                    Svc.Log.Debug("Sending Chat Command /deliveroo e");
                    _chat.SendMessage("/deliveroo e");
                }
                return;
            }
        }
    }
}
