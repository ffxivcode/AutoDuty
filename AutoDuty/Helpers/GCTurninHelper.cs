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
    internal class GCTurninHelper : ActiveHelperBase<GCTurninHelper>
    {
        protected override string Name        { get; } = nameof(GCTurninHelper);
        protected override string DisplayName { get; } = "GC Turnin";

        public override string[]? Commands { get; init; } = ["turnin", "gcturnin"];
        public override string? CommandDescription { get; init; } = "Automatically turns in items into the Grand Company Supply";

        protected override string[] AddonsToClose { get; } = ["GrandCompanySupplyReward", "SelectYesno", "SelectString", "GrandCompanySupplyList"];

        protected override int TimeOut { get; set; } = 600_000;

        internal override void Start()
        {
            if (!AutoRetainer_IPCSubscriber.IsEnabled)
                Svc.Log.Info("GC Turnin Requires AutoRetainer plugin. Get @ https://love.puni.sh/ment.json");
            else if (PlayerHelper.GetGrandCompanyRank() <= 5)
                Svc.Log.Info("GC Turnin requires GC Rank 6 or Higher");
            else
                base.Start();
        }

        internal override void Stop() 
        {
            this._turninStarted = false;
            GotoHelper.ForceStop();
            base.Stop();
        }

        internal static Vector3 GCSupplyLocation => PlayerHelper.GetGrandCompany() == 1 ? new Vector3(94.02183f, 40.27537f, 74.475525f) : (PlayerHelper.GetGrandCompany() == 2 ? new Vector3(-68.678566f, -0.5015295f, -8.470145f) : new Vector3(-142.82619f, 4.0999994f, -106.31349f));

        private IGameObject? _personnelOfficerGameObject => ObjectHelper.GetObjectByDataId(_personnelOfficerDataId);
        private static uint _personnelOfficerDataId => PlayerHelper.GetGrandCompany() == 1 ? 1002388u : (PlayerHelper.GetGrandCompany() == 2 ? 1002394u : 1002391u);
        private static uint _aetheryteTicketId = PlayerHelper.GetGrandCompany() == 1 ? 21069u : (PlayerHelper.GetGrandCompany() == 2 ? 21070u : 21071u);
        private bool _turninStarted = false;

        protected override unsafe void HelperStopUpdate(IFramework framework)
        {
            if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInQuestEvent])
            {
                base.HelperStopUpdate(framework);
            }
            else
            {
                if (Svc.Targets.Target != null)
                    Svc.Targets.Target = null;
                this.CloseAddons();
            }
        }

        protected override unsafe void HelperUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating))
            {
                DebugLog("AutoDuty is Started, Stopping GCTurninHelper");
                Stop();
                return;
            }
            if (!this._turninStarted && AutoRetainer_IPCSubscriber.IsBusy())
            {
                InfoLog("TurnIn has Started");
                this._turninStarted = true;
                return;
            }
            else if (this._turninStarted && !AutoRetainer_IPCSubscriber.IsBusy())
            {
                DebugLog("TurnIn is Complete");
                Stop();
                return;
            }

            if (!EzThrottler.Throttle("Turnin", 250))
                return;

            if (GotoHelper.State == ActionState.Running)
            {
                //DebugLog("Goto Running");
                return;
            }
            Plugin.Action = "GC Turning In";

            if (GotoHelper.State != ActionState.Running && Svc.ClientState.TerritoryType != PlayerHelper.GetGrandCompanyTerritoryType(PlayerHelper.GetGrandCompany()))
            {
                DebugLog("Moving to GC Supply");
                if (Plugin.Configuration.AutoGCTurninUseTicket && InventoryHelper.ItemCount(_aetheryteTicketId) > 0)
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
                DebugLog("Setting Move to Personnel Officer");
                MovementHelper.Move(GCSupplyLocation, 0.25f, 4f);
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) > 4 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                DebugLog("Moving to Personnel Officer");
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) <= 4 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                DebugLog("Stopping Path");
                VNavmesh_IPCSubscriber.Path_Stop();
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(GCSupplyLocation) <= 4 && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0 && !this._turninStarted)
            {
                /*
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
                else*/
                {
                    DebugLog("Starting TurnIn proper");
                    AutoRetainer_IPCSubscriber.EnqueueGCInitiation();
                }
                return;
            }
        }
    }
}
