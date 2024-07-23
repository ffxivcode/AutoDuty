using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Numerics;
using System;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.ClientState.Conditions;
using AutoDuty.IPC;

//still need to test self repair

namespace AutoDuty.Helpers
{
    internal unsafe static class RepairHelper
    {
        internal static void Invoke()
        {
            if (!RepairRunning)
            {
                Svc.Log.Info($"Repair Started");
                RepairRunning = true;
                Svc.Framework.Update += RepairUpdate;
                YesAlready_IPCSubscriber.SetPluginEnabled(false);
            }
        }

        internal static void Stop() 
        {
            if (RepairRunning)
                Svc.Log.Info($"Repair Finished");
            Svc.Framework.Update -= RepairUpdate;
            RepairRunning = false;
            _seenAddon = false;
            AutoDuty.Plugin.Action = "";
            AgentModule.Instance()->GetAgentByInternalId(AgentId.Repair)->Hide();
            YesAlready_IPCSubscriber.SetPluginEnabled(true);
        }

        internal static bool RepairRunning = false;

        private static IGameObject? _repairVendorGameObject = null;
        private static Vector3 _repairVendorLocation => UIState.Instance()->PlayerState.GrandCompany == 1 ? new Vector3(17.715698f, 40.200005f, 3.9520264f) : (UIState.Instance()->PlayerState.GrandCompany == 2 ? new Vector3(24.826416f, -8, 93.18677f) : new Vector3(32.85266f, 6.999999f, -81.31531f));
        private static uint _repairVendorDataId => UIState.Instance()->PlayerState.GrandCompany == 1 ? 1003251u : (UIState.Instance()->PlayerState.GrandCompany == 2 ? 1000394u : 1004416u);
        private static bool _seenAddon = false;
        private static AtkUnitBase* addonRepair = null;
        private static AtkUnitBase* addonSelectYesno = null;

        internal static unsafe void RepairUpdate(IFramework framework)
        {
            if (!EzThrottler.Check("RepairBarracks"))
                return;

            EzThrottler.Throttle("RepairBarracks", 250);

            if (Svc.ClientState.LocalPlayer == null)
                return;

            if (GotoHelper.GotoRunning)
                return;

            if (AutoDuty.Plugin.Configuration.AutoRepairSelf)
            {
                if (!ObjectHelper.IsOccupied)
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

            if (Svc.ClientState.TerritoryType != ObjectHelper.GrandCompanyTerritoryType(UIState.Instance()->PlayerState.GrandCompany) || (_repairVendorGameObject = ObjectHelper.GetObjectByDataId(Convert.ToUInt32(_repairVendorDataId))) == null || Vector3.Distance(Svc.ClientState.LocalPlayer.Position, _repairVendorGameObject.Position) > 3f)
            {
                GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(UIState.Instance()->PlayerState.GrandCompany), [_repairVendorLocation], 0.25f, 3f);
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
