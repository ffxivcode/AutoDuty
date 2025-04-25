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
    using Lumina.Excel.Sheets;

    internal class DesynthHelper : ActiveHelperBase<DesynthHelper>
    {
        protected override string Name        => nameof(DesynthHelper);
        protected override string DisplayName => "Desynthing";

        protected override string[] AddonsToClose { get; } = ["Desynth", "SalvageResult", "SalvageDialog", "SalvageItemSelector"];

        internal override void Start()
        {
            _maxDesynthLevel = PlayerHelper.GetMaxDesynthLevel();
            base.Start();
        }

        private float _maxDesynthLevel = 1;

        protected override unsafe void HelperUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating) || Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("Desynth", 250))
                return;

            if (Conditions.Instance()->Mounted)
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
                DebugLog("Closing SalvageResult");
                addonSalvageResult->Close(true);
                return;
            }
            else if (GenericHelpers.TryGetAddonByName("SalvageDialog", out AtkUnitBase* addonSalvageDialog) && GenericHelpers.IsAddonReady(addonSalvageDialog))
            {
                DebugLog("Confirming SalvageDialog");
                AddonHelper.FireCallBack(addonSalvageDialog, true, 0, false);
                return;
            }
            
            if (!GenericHelpers.TryGetAddonByName<AddonSalvageItemSelector>("SalvageItemSelector", out var addonSalvageItemSelector))
            {
                AgentSalvage.Instance()->AgentInterface.Show();
                EzThrottler.Throttle("Desynth", 2000, true);
                return;
            }
            else if (GenericHelpers.IsAddonReady((AtkUnitBase*)addonSalvageItemSelector) && addonSalvageItemSelector->IsReady)
            {
                AgentSalvage.Instance()->ItemListRefresh(true);
                if (AgentSalvage.Instance()->SelectedCategory != AgentSalvage.SalvageItemCategory.InventoryEquipment)
                {
                    DebugLog("Switching Category");
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
                        var itemLevel = itemSheetRow?.LevelItem.ValueNullable?.RowId;
                        var desynthLevel = PlayerHelper.GetDesynthLevel(item.ClassJob);

                        if (itemLevel == null || itemSheetRow == null) continue;

                        if (!Plugin.Configuration.AutoDesynthSkillUp || (desynthLevel < itemLevel + 50 && desynthLevel < _maxDesynthLevel))
                        {
                            DebugLog($"Salvaging Item({i}): {itemSheetRow.Value.Name.ToString()} with iLvl {itemLevel} because our desynth level is {desynthLevel}");
                            foundOne = true;
                            AddonHelper.FireCallBack((AtkUnitBase*)addonSalvageItemSelector, true, 12, i);
                            return;
                        }
                    }

                    if (!foundOne)
                    {
                        addonSalvageItemSelector->Close(true);
                        DebugLog("Desynth Finished");
                        Stop();
                    }
                }
                else
                {
                    addonSalvageItemSelector->Close(true);
                    DebugLog("Desynth Finished");
                    Stop();
                }
            }
        }
    }
}
