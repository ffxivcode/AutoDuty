using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.ClientState.Conditions;

//still need to test self repair

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
                Svc.Framework.Update += RepairUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal unsafe static void Stop() 
        {
            if (RepairRunning)
                Svc.Log.Info($"Repair Finished");
            Svc.Framework.Update -= RepairUpdate;
            RepairRunning = false;
            _seenAddon = false;
            AutoDuty.Plugin.Action = "";
            AgentModule.Instance()->GetAgentByInternalId(AgentId.Repair)->Hide();
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool RepairRunning = false;
            
        private static Vector3 _repairVendorLocation => ObjectHelper.GrandCompany == 1 ? new Vector3(17.715698f, 40.200005f, 3.9520264f) : (ObjectHelper.GrandCompany == 2 ? new Vector3(24.826416f, -8, 93.18677f) : new Vector3(32.85266f, 6.999999f, -81.31531f));
        private static uint _repairVendorDataId => ObjectHelper.GrandCompany == 1 ? 1003251u : (ObjectHelper.GrandCompany == 2 ? 1000394u : 1004416u);
        private static IGameObject? _repairVendorGameObject => ObjectHelper.GetObjectByDataId(_repairVendorDataId);
        private static bool _seenAddon = false;
        private unsafe static AtkUnitBase* addonRepair = null;
        private unsafe static AtkUnitBase* addonSelectYesno = null;

        internal static unsafe void RepairUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.Started)
                Stop();

            if (!EzThrottler.Check("RepairBarracks"))
                return;

            EzThrottler.Throttle("RepairBarracks", 250);

            if (Svc.ClientState.LocalPlayer == null)
                return;

            AutoDuty.Plugin.Action = "Repairing";

            if (GotoHelper.GotoRunning)
                return;

            if (AutoDuty.Plugin.Configuration.AutoRepairSelf)
            {
                if (!ObjectHelper.IsOccupied || (EzThrottler.Throttle("GearCheck") && InventoryHelper.LowestEquippedCondition() > AutoDuty.Plugin.Configuration.AutoRepairPct))
                {
                    if (Svc.Condition[ConditionFlag.Occupied39])
                        Stop();
                    if (!ECommons.GenericHelpers.TryGetAddonByName("Repair", out addonRepair) && !ECommons.GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno))
                    {
                        ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6);
                        return;
                    }
                    else if (!_seenAddon && (!ECommons.GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) || !ECommons.GenericHelpers.IsAddonReady(addonSelectYesno)))
                    {
                        AddonHelper.ClickRepair();
                        return;
                    }
                    else if (ECommons.GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) && ECommons.GenericHelpers.IsAddonReady(addonSelectYesno))
                    {
                        AddonHelper.ClickSelectYesno();
                        _seenAddon = true;
                    }
                    else if (_seenAddon && (!ECommons.GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) || !ECommons.GenericHelpers.IsAddonReady(addonSelectYesno)))
                    {
                        Stop();
                    }
                }
                else
                    Stop();
                
                return;
            }

            if (Svc.ClientState.TerritoryType != ObjectHelper.GrandCompanyTerritoryType(ObjectHelper.GrandCompany) || _repairVendorGameObject == null || Vector3.Distance(Svc.ClientState.LocalPlayer.Position, _repairVendorGameObject.Position) > 3f)
            {
                GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(ObjectHelper.GrandCompany), [_repairVendorLocation], 0.25f, 3f);
                return;
            }
            else if (ObjectHelper.IsValid)
            {
                if (!ECommons.GenericHelpers.TryGetAddonByName("Repair", out addonRepair) && !ECommons.GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno))
                {
                    ObjectHelper.InteractWithObjectUntilAddon(_repairVendorGameObject, "Repair");
                    return;
                }
                else if (!_seenAddon && (!ECommons.GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) || !ECommons.GenericHelpers.IsAddonReady(addonSelectYesno)))
                {
                    AddonHelper.ClickRepair();
                    return;
                }
                else if (ECommons.GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) && ECommons.GenericHelpers.IsAddonReady(addonSelectYesno))
                {
                    AddonHelper.ClickSelectYesno();
                    _seenAddon = true;
                }
                else if (_seenAddon && (!ECommons.GenericHelpers.TryGetAddonByName("SelectYesno", out addonSelectYesno) || !ECommons.GenericHelpers.IsAddonReady(addonSelectYesno)))
                {
                    Stop();
                }
            }
        }
    }
}
