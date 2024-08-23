using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal static class DesynthHelper
    {
        internal static void Invoke()
        {
            if (!DesynthRunning)
            {
                Svc.Log.Info("Desynth Started");
                DesynthRunning = true;
                AutoDuty.Plugin.States |= State.Other;
                if (!AutoDuty.Plugin.States.HasFlag(State.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(false);
                SchedulerHelper.ScheduleAction("DesynthTimeOut", Stop, 300000);
                AutoDuty.Plugin.Action = "Desynthing";
                Svc.Framework.Update += DesynthUpdate;
            }
        }

        internal unsafe static void Stop()
        {
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("DesynthTimeOut");
            _stop = true;
            if (GenericHelpers.TryGetAddonByName("Desynth", out AtkUnitBase* addonDesynth))
                addonDesynth->Close(true);
        }

        internal static bool DesynthRunning = false;
        private static bool _stop = false;

        internal static unsafe void DesynthUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.States.HasFlag(State.Navigating) || AutoDuty.Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("Desynth", 250))
                return;

            if (_stop)
            {
                if (GenericHelpers.TryGetAddonByName("SalvageResult", out AtkUnitBase* addonSalvageResultClose))
                    addonSalvageResultClose->Close(true);
                else if (GenericHelpers.TryGetAddonByName("SalvageDialog", out AtkUnitBase* addonSalvageDialog))
                    addonSalvageDialog->Close(true);
                else if (GenericHelpers.TryGetAddonByName("SalvageItemSelector", out AtkUnitBase* addonSalvageItemSelectorClose))
                    addonSalvageItemSelectorClose->Close(true);
                else
                {
                    _stop = false;
                    DesynthRunning = false;
                    AutoDuty.Plugin.States &= ~State.Other;
                    if (!AutoDuty.Plugin.States.HasFlag(State.Looping))
                        AutoDuty.Plugin.SetGeneralSettings(true);
                    Svc.Framework.Update -= DesynthUpdate;
                }
                return;
            }

            if (Conditions.IsMounted)
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                return;
            }

            AutoDuty.Plugin.Action = "Desynthing Inventory";

            if (InventoryManager.Instance()->GetEmptySlotsInBag() < 1)
            {
                Stop();
                return;
            }

            if (ObjectHelper.IsOccupied)
                return;

            if (GenericHelpers.TryGetAddonByName("SalvageResult", out AtkUnitBase* addonSalvageResult) && GenericHelpers.IsAddonReady(addonSalvageResult))
            {
                Svc.Log.Info("Closing SalvageResult");
                addonSalvageResult->Close(true);
                return;
            }
            else if (GenericHelpers.TryGetAddonByName("SalvageDialog", out AtkUnitBase* addonSalvageDialog) && GenericHelpers.IsAddonReady(addonSalvageDialog))
            {
                Svc.Log.Info("Confirming SalvageDialog");
                AddonHelper.FireCallBack(addonSalvageDialog, true, 0, false);
                return;
            }

            if (!GenericHelpers.TryGetAddonByName<AddonSalvageItemSelector>("SalvageItemSelector", out var addonSalvageItemSelector))
                AgentSalvage.Instance()->AgentInterface.Show();
            else if (GenericHelpers.IsAddonReady((AtkUnitBase*)addonSalvageItemSelector))
            {
                AgentSalvage.Instance()->ItemListRefresh();
                if (AgentSalvage.Instance()->SelectedCategory != AgentSalvage.SalvageItemCategory.InventoryEquipment)
                {
                    Svc.Log.Info("Switching Category");
                    AddonHelper.FireCallBack((AtkUnitBase*)addonSalvageItemSelector, true, 11, 0);
                }
                else if (addonSalvageItemSelector->ItemCount > 0)
                    AddonHelper.FireCallBack((AtkUnitBase*)addonSalvageItemSelector, true, 12, 0);
                else
                {
                    addonSalvageItemSelector->Close(true);
                    Svc.Log.Info("Desynth Finished");
                    Stop();
                }
            }
        }
    }
}
