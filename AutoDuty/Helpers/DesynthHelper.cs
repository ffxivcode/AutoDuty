using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;

namespace AutoDuty.Helpers
{
    internal static class DesynthHelper
    {
        internal static void Invoke()
        {
            if (State != ActionState.Running)
            {
                Svc.Log.Info("Desynth Started");
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;
                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(false);
                SchedulerHelper.ScheduleAction("DesynthTimeOut", Stop, 300000);
                Plugin.Action = "Desynthing";
                Svc.Framework.Update += DesynthUpdate;
                _maxDesynthLevel = PlayerHelper.GetMaxDesynthLevel();
            }
        }

        internal unsafe static void Stop()
        {
            Plugin.Action = "";
            SchedulerHelper.DescheduleAction("DesynthTimeOut");
            Svc.Framework.Update += DesynthStopUpdate;
            Svc.Framework.Update -= DesynthUpdate;
            if (GenericHelpers.TryGetAddonByName("Desynth", out AtkUnitBase* addonDesynth))
                addonDesynth->Close(true);
        }

        internal static ActionState State = ActionState.None;

        private static float _maxDesynthLevel = 1;
        
        internal static unsafe void DesynthStopUpdate(IFramework framework)
        {
            if (GenericHelpers.TryGetAddonByName("SalvageResult", out AtkUnitBase* addonSalvageResultClose))
                addonSalvageResultClose->Close(true);
            else if (GenericHelpers.TryGetAddonByName("SalvageDialog", out AtkUnitBase* addonSalvageDialog))
                addonSalvageDialog->Close(true);
            else if (GenericHelpers.TryGetAddonByName("SalvageItemSelector", out AtkUnitBase* addonSalvageItemSelectorClose))
                addonSalvageItemSelectorClose->Close(true);
            else
            {
                State = ActionState.None;
                Plugin.States &= ~PluginState.Other;
                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(true);
                Svc.Framework.Update -= DesynthStopUpdate;
            }
            return;
        }

        internal static unsafe void DesynthUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating) || Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("Desynth", 250))
                return;

            if (Conditions.IsMounted)
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                return;
            }

            Plugin.Action = "Desynthing Inventory";

            if (InventoryManager.Instance()->GetEmptySlotsInBag() < 1)
            {
                Stop();
                return;
            }

            if (PlayerHelper.IsOccupied)
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
                    return;
                }
                else if (addonSalvageItemSelector->ItemCount > 0)
                {
                    var foundOne = false;
                    for (int i = 0; i < AgentSalvage.Instance()->ItemCount; i++)
                    {
                        var item = AgentSalvage.Instance()->ItemList[i];
                        var itemId = InventoryManager.Instance()->GetInventorySlot(item.InventoryType, (int)item.InventorySlot)->ItemId;

                        if (itemId == 10146) continue;

                        var itemSheetRow = Svc.Data.Excel.GetSheet<Item>()?.GetRow(itemId);
                        var itemLevel = itemSheetRow?.LevelItem.Value?.RowId;
                        var desynthLevel = PlayerHelper.GetDesynthLevel(item.ClassJob);

                        if (itemLevel == null || itemSheetRow == null) continue;

                        if (!Plugin.Configuration.AutoDesynthSkillUp || (desynthLevel < itemLevel + 50 && desynthLevel < _maxDesynthLevel))
                        {
                            Svc.Log.Debug($"Salvaging Item({i}): {itemSheetRow.Name.RawString} with iLvl {itemLevel} because our desynth level is {desynthLevel}");
                            foundOne = true;
                            AddonHelper.FireCallBack((AtkUnitBase*)addonSalvageItemSelector, true, 12, i);
                            return;
                        }
                    }

                    if (!foundOne)
                    {
                        addonSalvageItemSelector->Close(true);
                        Svc.Log.Info("Desynth Finished");
                        Stop();
                    }
                }
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
