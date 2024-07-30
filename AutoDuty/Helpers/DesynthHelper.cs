using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using static Dalamud.Interface.Utility.Raii.ImRaii;

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
                SchedulerHelper.ScheduleAction("DesynthTimeOut", Stop, 300000);
                AutoDuty.Plugin.Action = "Desynthing";
                Svc.Framework.Update += DesynthUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal unsafe static void Stop()
        {
            DesynthRunning = false;
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("DesynthTimeOut");
            Svc.Framework.Update -= DesynthUpdate;
            if (GenericHelpers.TryGetAddonByName("Desynth", out AtkUnitBase* addonDesynth))
                addonDesynth->Close(true);
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool DesynthRunning = false;

        internal static unsafe void DesynthUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.Started || AutoDuty.Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("Desynth", 250))
                return;

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
