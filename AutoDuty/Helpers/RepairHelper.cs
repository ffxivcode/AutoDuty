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
    internal static class RepairHelper
    {
        internal static void Invoke()
        {
            if (!RepairRunning)
            {
                Svc.Log.Info($"Repair Started");
                RepairRunning = true;
                AutoDuty.Plugin.States |= State.Other;
                if (!AutoDuty.Plugin.States.HasFlag(State.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(false);
                _stop = false;
                if (AutoDuty.Plugin.Configuration.AutoRepairSelf)
                    SchedulerHelper.ScheduleAction("RepairTimeOut", Stop, 300000);
                else
                    SchedulerHelper.ScheduleAction("RepairTimeOut", Stop, 600000);
                Svc.Framework.Update += RepairUpdate;
            }
        }

        internal unsafe static void Stop() 
        {
            if (RepairRunning)
                Svc.Log.Info($"Repair Finished");
            SchedulerHelper.DescheduleAction("RepairTimeOut");
            _stop = true;
            _seenAddon = false;
            AutoDuty.Plugin.Action = "";
            AgentModule.Instance()->GetAgentByInternalId(AgentId.Repair)->Hide();
        }

        internal static bool RepairRunning = false;
            
        private static Vector3 _repairVendorLocation => _preferredRepairNpc != null ? _preferredRepairNpc.Position : (ObjectHelper.GrandCompany == 1 ? new Vector3(17.715698f, 40.200005f, 3.9520264f) : (ObjectHelper.GrandCompany == 2 ? new Vector3(24.826416f, -8, 93.18677f) : new Vector3(32.85266f, 6.999999f, -81.31531f)));
        private static uint _repairVendorDataId => _preferredRepairNpc != null ? _preferredRepairNpc.DataId : (ObjectHelper.GrandCompany == 1 ? 1003251u : (ObjectHelper.GrandCompany == 2 ? 1000394u : 1004416u));
        private static IGameObject? _repairVendorGameObject => ObjectHelper.GetObjectByDataId(_repairVendorDataId);
        private static uint _repairVendorTerritoryType => _preferredRepairNpc != null ? _preferredRepairNpc.TerritoryType : ObjectHelper.GrandCompanyTerritoryType(ObjectHelper.GrandCompany);
        private static bool _seenAddon = false;
        private static bool _stop = false;
        private unsafe static AtkUnitBase* addonRepair = null;
        private unsafe static AtkUnitBase* addonSelectYesno = null;
        private unsafe static AtkUnitBase* addonSelectIconString = null;
        private static RepairNPCHelper.RepairNpcData? _preferredRepairNpc => AutoDuty.Plugin.Configuration.PreferredRepairNPC;

        internal static unsafe void RepairUpdate(IFramework framework)
        {
            if (_stop)
            {
                if (!Svc.Condition[ConditionFlag.OccupiedInQuestEvent])
                {
                    _stop = false;
                    RepairRunning = false;
                    AutoDuty.Plugin.States &= ~State.Other;
                    if (!AutoDuty.Plugin.States.HasFlag(State.Looping))
                        AutoDuty.Plugin.SetGeneralSettings(true);
                    Svc.Framework.Update -= RepairUpdate;
                }
                else if (Svc.Targets.Target != null)
                    Svc.Targets.Target = null;
                else if (GenericHelpers.TryGetAddonByName("SelectIconString", out AtkUnitBase* addonSelectIconString))
                    addonSelectIconString->Close(true);
                else if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                    addonSelectYesno->Close(true);
                else if (GenericHelpers.TryGetAddonByName("Repair", out AtkUnitBase* addonRepair))
                    addonRepair->Close(true);
                return;
            }

            if (AutoDuty.Plugin.States.HasFlag(State.Navigating))
                Stop();

            if (Conditions.IsMounted && !GotoHelper.GotoRunning)
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

            if (GotoHelper.GotoRunning)
                return;

            AutoDuty.Plugin.Action = "Repairing";

            if (AutoDuty.Plugin.Configuration.AutoRepairSelf)
            {
                if (!ObjectHelper.IsOccupied || (EzThrottler.Throttle("GearCheck") && InventoryHelper.CanRepair()))
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
                return;
            }

            if (Svc.ClientState.TerritoryType != _repairVendorTerritoryType || _repairVendorGameObject == null || Vector3.Distance(Player.Position, _repairVendorGameObject.Position) > 3f)
            {
                Svc.Log.Debug("Going to RepairVendor");
                GotoHelper.Invoke(_repairVendorTerritoryType, [_repairVendorLocation], 0.25f, 3f);
            }
            else if (ObjectHelper.IsValid)
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
