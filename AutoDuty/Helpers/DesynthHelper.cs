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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Windows;
    using ECommons.ExcelServices;
    using ECommons.MathHelpers;
    using FFXIVClientStructs.FFXIV.Client.UI.Misc;

    internal class DesynthHelper : ActiveHelperBase<DesynthHelper>
    {
        protected override string Name        => nameof(DesynthHelper);
        protected override string DisplayName => "Desynthing";

        public override string[]? Commands { get; init; } = ["desynth"];
        public override string? CommandDescription { get; init; } = "Desynth's items in your inventory";

        protected override string[] AddonsToClose { get; } = ["Desynth", "SalvageResult", "SalvageDialog", "SalvageItemSelector"];

        internal override void Start()
        {
            _maxDesynthLevel = PlayerHelper.GetMaxDesynthLevel();
            if(this.NextCategory(true))
                base.Start();
        }

        private float _maxDesynthLevel = 1;

        private AgentSalvage.SalvageItemCategory curCategory;

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
                if (AgentSalvage.Instance()->SelectedCategory != this.curCategory)
                {
                    DebugLog("Switching Category to " + this.curCategory);
                    AgentSalvage.Instance()->SelectedCategory = this.curCategory;
                    return;
                }
                else if (addonSalvageItemSelector->ItemCount > 0)
                {
                    HashSet<uint>? gearsetItemIds = null;

                    var foundOne = false;
                    for (int i = 0; i < AgentSalvage.Instance()->ItemCount; i++)
                    {
                        var            item          = AgentSalvage.Instance()->ItemList[i];
                        InventoryItem* inventoryItem = InventoryManager.Instance()->GetInventorySlot(item.InventoryType, (int)item.InventorySlot);
                        var            itemId        = inventoryItem->ItemId;
                            
                        if (itemId == 10146) continue;

                        var itemSheetRow = Svc.Data.Excel.GetSheet<Item>()?.GetRow(itemId);
                        var itemLevel = itemSheetRow?.LevelItem.ValueNullable?.RowId;
                        var desynthLevel = PlayerHelper.GetDesynthLevel(item.ClassJob);

                        if (itemLevel == null || itemSheetRow == null || desynthLevel <= 0) continue;

                        if (!Plugin.Configuration.AutoDesynthSkillUp || (desynthLevel < itemLevel + Plugin.Configuration.AutoDesynthSkillUpLimit && desynthLevel < _maxDesynthLevel))
                        {
                            if (Plugin.Configuration.AutoDesynthNoGearset)
                            {
                                if (gearsetItemIds == null)
                                {
                                    gearsetItemIds = [];

                                    RaptureGearsetModule* gearsetModule = RaptureGearsetModule.Instance();
                                    byte                  num           = gearsetModule->NumGearsets;
                                    for (byte j = 0; j < num; j++)
                                    {
                                        foreach (RaptureGearsetModule.GearsetEntry entry in gearsetModule->Entries)
                                            foreach (RaptureGearsetModule.GearsetItem gearsetItem in entry.Items) 
                                                gearsetItemIds.Add(gearsetItem.ItemId);
                                    }
                                }

                                if (gearsetItemIds.Contains(itemId))
                                    continue;
                            }


                            DebugLog($"Salvaging Item({i}): {itemSheetRow.Value.Name.ToString()} with iLvl {itemLevel} because our desynth level is {desynthLevel}");
                            foundOne = true;
                            AddonHelper.FireCallBack((AtkUnitBase*)addonSalvageItemSelector, true, 12, i);
                            return;
                        }
                    }

                    if (!foundOne)
                    {
                        if (!this.NextCategory())
                        {
                            addonSalvageItemSelector->Close(true);
                            DebugLog("Desynth Finished");
                            Stop();
                        }
                    }
                }
                else
                {
                    if (!this.NextCategory())
                    {
                        addonSalvageItemSelector->Close(true);
                        DebugLog("Desynth Finished");
                        Stop();
                    }
                }
            }
        }

        public bool NextCategory(bool reset = false)
        {
            AgentSalvage.SalvageItemCategory[]? categories = Enum.GetValues<AgentSalvage.SalvageItemCategory>();
            for (int i = reset ? 0 : (int) this.curCategory + 1; i < categories.Length; i++)
            {
                if(Bitmask.IsBitSet(Plugin.Configuration.AutoDesynthCategories, i))
                {
                    this.curCategory = categories[i];
                    return true;
                }
            }

            return false;
        }
    }
}
