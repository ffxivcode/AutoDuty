using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.ClientState.Conditions;
using ECommons;

namespace AutoDuty.Helpers
{
    internal class RepairHelper : ActiveHelperBase<RepairHelper>
    {
        protected override string   Name          { get; } = nameof(RepairHelper);
        protected override string   DisplayName   { get; } = string.Empty;

        public override string[]? Commands { get; init; } = ["repair"];
        public override string? CommandDescription { get; init; } = "Repairs your gear";

        protected override int      TimeOut       => Plugin.Configuration.AutoRepairSelf ? 300000 : 600000;
        protected override string[] AddonsToClose { get; } = ["SelectYesno", "SelectIconString", "Repair", "SelectString"];

        internal override void Start()
        {
            if (!InventoryHelper.CanRepair(100))
                return;
            base.Start();
        }

        internal override unsafe void Stop() 
        {
            base.Stop();
            _seenAddon           =  false;
            AgentModule.Instance()->GetAgentByInternalId(AgentId.Repair)->Hide();
        }

        private static Vector3 _repairVendorLocation => _preferredRepairNpc?.Position ?? (PlayerHelper.GetGrandCompany() == 1 ? new Vector3(17.715698f, 40.200005f, 3.9520264f) : (PlayerHelper.GetGrandCompany() == 2 ? new Vector3(24.826416f, -8, 93.18677f) : new Vector3(32.85266f, 6.999999f, -81.31531f)));
        private static uint _repairVendorDataId => _preferredRepairNpc?.DataId ?? (PlayerHelper.GetGrandCompany() == 1 ? 1003251u : (PlayerHelper.GetGrandCompany() == 2 ? 1000394u : 1004416u));
        private IGameObject? _repairVendorGameObject => ObjectHelper.GetObjectByDataId(_repairVendorDataId);
        private static uint _repairVendorTerritoryType => _preferredRepairNpc?.TerritoryType ?? PlayerHelper.GetGrandCompanyTerritoryType(PlayerHelper.GetGrandCompany());

        private bool _seenAddon = false;

        private static unsafe AtkUnitBase* addonRepair = null;
        private static unsafe AtkUnitBase* addonSelectYesno = null;
        private static unsafe AtkUnitBase* addonSelectIconString = null;
        private static RepairNPCHelper.RepairNpcData? _preferredRepairNpc => Plugin.Configuration.PreferredRepairNPC;

        protected override unsafe void HelperStopUpdate(IFramework framework)
        {
            if (!Svc.Condition[ConditionFlag.OccupiedInQuestEvent])
                base.HelperStopUpdate(framework);
            else if (Svc.Targets.Target != null)
                Svc.Targets.Target = null;
            else
                this.CloseAddons();
        }

        protected override unsafe void HelperUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating))
                Stop();

            if (Conditions.Instance()->Mounted && GotoHelper.State != ActionState.Running)
            {
                Svc.Log.Debug("Dismounting");
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                return;
            }

            if (!EzThrottler.Check("Repair"))
                return;

            EzThrottler.Throttle("Repair", 250);

            if (Svc.ClientState.LocalPlayer == null)
                return;

            if (GotoHelper.State == ActionState.Running)
                return;

            Plugin.Action = "Repairing";

            if (Plugin.Configuration.AutoRepairSelf)
            {
                if (EzThrottler.Throttle("GearCheck"))
                {
                    if (!PlayerHelper.IsOccupied && InventoryHelper.CanRepair())
                    {
                        if (Svc.Condition[ConditionFlag.Occupied39])
                        {
                            Svc.Log.Debug("Done Repairing");
                            Stop();
                        }
                        if (!GenericHelpers.TryGetAddonByName("Repair", out addonRepair) && !GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno))
                        {
                            Svc.Log.Debug("Using Repair Action");
                            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6);
                            return;
                        }
                        else if (!_seenAddon && (!GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) || !GenericHelpers.IsAddonReady(addonSelectYesno)))
                        {
                            Svc.Log.Debug("Clicking Repair");
                            AddonHelper.ClickRepair();
                            return;
                        }
                        else if (GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno))
                        {
                            Svc.Log.Debug("Clicking SelectYesno");
                            AddonHelper.ClickSelectYesno();
                            _seenAddon = true;
                        }
                        else if (_seenAddon && (!GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) || !GenericHelpers.IsAddonReady(addonSelectYesno)))
                        {
                            Svc.Log.Debug("Stopping-SelfRepair");
                            Stop();
                        }
                    }
                    else
                    {
                        Svc.Log.Debug("Stopping-SelfRepair");
                        Stop();
                    }
                }
                return;
            }

            if (Svc.ClientState.TerritoryType != _repairVendorTerritoryType && ContentHelper.DictionaryContent.ContainsKey(Svc.ClientState.TerritoryType) && Conditions.Instance()->BoundByDuty)
                Stop();

            if (Svc.ClientState.TerritoryType != _repairVendorTerritoryType || _repairVendorGameObject == null || Vector3.Distance(Player.Position, _repairVendorGameObject.Position) > 3f)
            {
                Svc.Log.Debug("Going to RepairVendor");
                GotoHelper.Invoke(_repairVendorTerritoryType, [_repairVendorLocation], 0.25f, 3f);
            }
            else if (PlayerHelper.IsValid)
            {
                if (GenericHelpers.TryGetAddonByName("SelectIconString", out addonSelectIconString) && GenericHelpers.IsAddonReady(addonSelectIconString))
                {
                    Svc.Log.Debug($"Clicking SelectIconString({_preferredRepairNpc?.RepairIndex})");
                    AddonHelper.ClickSelectIconString(_preferredRepairNpc?.RepairIndex ?? 0);
                }
                else if (!GenericHelpers.TryGetAddonByName("Repair", out addonRepair) && !GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno))
                {
                    Svc.Log.Debug("Interacting with RepairVendor");
                    ObjectHelper.InteractWithObject(_repairVendorGameObject);
                }
                else if (!_seenAddon && (!GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) || !GenericHelpers.IsAddonReady(addonSelectYesno)))
                {
                    Svc.Log.Debug("Clicking Repair");
                    AddonHelper.ClickRepair();
                }
                else if (GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno))
                {
                    Svc.Log.Debug("Clicking SelectYesno");
                    AddonHelper.ClickSelectYesno();
                    _seenAddon = true;
                }
                else if (_seenAddon && (!GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) || !GenericHelpers.IsAddonReady(addonSelectYesno)))
                {
                    Svc.Log.Debug("Stopping-RepairCity");
                    Stop();
                }
            }
        }
    }
}
