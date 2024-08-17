using ECommons.DalamudServices;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using System.Linq;
using System;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Throttlers;

namespace AutoDuty.Helpers
{
    internal unsafe static class InventoryHelper
    {
        internal static uint SlotsFree => InventoryManager.Instance()->GetEmptySlotsInBag();
        internal static uint MySeals => InventoryManager.Instance()->GetCompanySeals(PlayerState.Instance()->GrandCompany);
        internal static uint MaxSeals => InventoryManager.Instance()->GetMaxCompanySeals(PlayerState.Instance()->GrandCompany);

        internal static uint GetHQItemId(uint itemId) => itemId + 1_000_000;

        internal static int ItemCount(uint itemId, bool hq = false) => InventoryManager.Instance()->GetInventoryItemCount(hq ? GetHQItemId(itemId) : itemId);
        internal static void UseItem(uint itemId, bool hq = false) => 
            ActionManager.Instance()->UseAction(ActionType.Item, hq ? GetHQItemId(itemId) : itemId, extraParam: 65535);

        internal static void UseItemIfAvailable(uint itemId, bool allowHq = true)
        {
            if(allowHq && ItemCount(itemId, true) >= 1)
                UseItem(itemId, true);
            else if (ItemCount(itemId) >= 1) 
                UseItem(itemId);
        }

        internal unsafe static uint CurrentItemLevel()
        {
            if (GenericHelpers.TryGetAddonByName("Character", out AddonCharacter* addonCharacter) && GenericHelpers.IsAddonReady((AtkUnitBase*)addonCharacter))
            {
                if (addonCharacter->GetTextNodeById(71)->GetAsAtkTextNode()->NodeText.ExtractText().IsNullOrEmpty())
                    return 0;
                var iLvl = Convert.ToUInt32(addonCharacter->GetTextNodeById(71)->GetAsAtkTextNode()->NodeText.ExtractText());
                addonCharacter->Close(true);
                return iLvl;
            }
            else
            {
                if (EzThrottler.Throttle("AgentStatus", 250))
                    AgentStatus.Instance()->Show();
                return 0;
            }
        }

        internal static uint CurrentItemLevelCalc()
        {
            var equipedItems = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            uint itemLevelTotal = 0;
            uint itemLevelOfMainHand = 0;
            bool offhandIsEquipped = false;

            for (int i = 0; i < 13; i++)
            {
                var slot = equipedItems->Items[i].Slot;
                var itemId = equipedItems->Items[i].ItemId;
                var item = Svc.Data.GetExcelSheet<Item>()?.FirstOrDefault(item => item.RowId == itemId);
                var itemLevel = item?.LevelItem.Value?.RowId ?? 0;
                var itemName = item?.Name.RawString ?? "";

                if (slot == 0)
                    itemLevelOfMainHand = itemLevel;

                if (slot == 1 && itemId > 0)
                    offhandIsEquipped = true;

                itemLevelTotal += itemLevel;
            }

            if (!offhandIsEquipped)
                itemLevelTotal += itemLevelOfMainHand;

            return itemLevelTotal / 12;
        }

        internal static InventoryItem LowestEquippedItem()
        {
            var equipedItems = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            uint itemLowestCondition = 60000;
            uint itemLowest = 0;

            for (uint i = 0; i < 13; i++)
            {
                if (itemLowestCondition > equipedItems->Items[i].Condition)
                {
                    itemLowest = i;
                    itemLowestCondition = equipedItems->Items[i].Condition;
                }
            }

            return equipedItems->Items[itemLowest];
        }

        internal static bool CanRepair() => (LowestEquippedItem().Condition / 300f) <= AutoDuty.Plugin.Configuration.AutoRepairPct;// && (!AutoDuty.Plugin.Configuration.AutoRepairSelf || CanRepairItem(LowestEquippedItem().GetItemId()));

        //artisan
        internal static bool CanRepairItem(uint itemID)
        {
            var item = Svc.Data.Excel.GetSheet<Item>()?.GetRow(itemID);

            if (item == null)
                return false;

            if (item.ClassJobRepair.Row > 0)
            {
                var actualJob = (Job)(item.ClassJobRepair.Row);
                var repairItem = item.ItemRepair.Value?.Item;

                if (repairItem == null)
                    return false;

                if (!HasDarkMatterOrBetter(repairItem.Row))
                    return false;

                var jobLevel = PlayerState.Instance()->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>()?.GetRow((uint)actualJob)?.ExpArrayIndex ?? 0];
                if (Math.Max(item.LevelEquip - 10, 1) <= jobLevel)
                    return true;
            }

            return false;
        }

        //artisan
        internal static bool HasDarkMatterOrBetter(uint darkMatterID)
        {
            var repairResources = Svc.Data.Excel.GetSheet<ItemRepairResource>();
            foreach (var dm in repairResources!)
            {
                if (dm.Item.Row < darkMatterID)
                    continue;

                if (InventoryManager.Instance()->GetInventoryItemCount(dm.Item.Row) > 0)
                    return true;
            }
            return false;
        }
    }
}
