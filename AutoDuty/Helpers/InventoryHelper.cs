using ECommons.DalamudServices;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons.Throttlers;
using System.Net.NetworkInformation;

namespace AutoDuty.Helpers
{
    internal unsafe static class InventoryHelper
    {
        internal static uint SlotsFree => InventoryManager.Instance()->GetEmptySlotsInBag();
        internal static uint MySeals => InventoryManager.Instance()->GetCompanySeals(PlayerState.Instance()->GrandCompany);
        internal static uint MaxSeals => InventoryManager.Instance()->GetMaxCompanySeals(PlayerState.Instance()->GrandCompany);

        internal static int ItemCount(uint itemId) => InventoryManager.Instance()->GetInventoryItemCount(itemId);

        internal static void UseItem(uint itemId) => ActionManager.Instance()->UseAction(ActionType.Item, itemId, extraParam: 65535);

        internal static bool UseItemUntilStatus(uint itemId, uint statusId, bool allowHq = true)
        {
            if (!EzThrottler.Throttle("UseItemUntilStatus", 250) || !PlayerHelper.IsReady || Player.Character->IsCasting)
                return false;

            if (PlayerHelper.HasStatus(statusId))
                return true;

            UseItemIfAvailable(itemId, allowHq);
            return false;
        }

        internal static bool UseItemUntilAnimationLock(uint itemId, bool allowHq = true)
        {
            if (PlayerHelper.IsAnimationLocked)
                return true;

            if (!EzThrottler.Throttle("UseItemUntilStatus", 250) || !PlayerHelper.IsReady || PlayerHelper.IsCasting)
                return false;

            UseItemIfAvailable(itemId, allowHq);
            return false;
        }

        internal static void UseItemIfAvailable(uint itemId, bool allowHq = true)
        {
            if (allowHq && ItemCount(itemId + 1_000_000) >= 1)
            {
                Svc.Log.Debug($"Using Item: {itemId + 1_000_000}");
                UseItem(itemId + 1_000_000);
            }
            else if (ItemCount(itemId) >= 1)
            {
                UseItem(itemId);
                Svc.Log.Debug($"Using Item: {itemId}");
            }
        }

        internal static bool IsItemAvailable(uint itemId, bool allowHq = true) => (allowHq && ItemCount(itemId + 1_000_000) >= 1) || ItemCount(itemId) >= 1;

        internal static Item? GetExcelItem(uint itemId) => Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);

        internal static EquippedSlotIndex GetRingSlot()
        {
            InventoryContainer* equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);

            uint ring1SlotiLvl = GetExcelItem(equipped->Items[(int)EquippedSlotIndex.Ring1].ItemId)?.LevelItem.Value?.RowId ?? 0u;
            uint ring2SlotiLvl = GetExcelItem(equipped->Items[(int)EquippedSlotIndex.Ring2].ItemId)?.LevelItem.Value?.RowId ?? 0u;
            return ring1SlotiLvl < ring2SlotiLvl ? EquippedSlotIndex.Ring1 : EquippedSlotIndex.Ring2;
        }

        internal static EquippedSlotIndex GetEquippedSlot(Item itemData)
        {
            EquippedSlotIndex targetSlot = itemData!.EquipSlotCategory.Value switch
            {
                { MainHand: > 0 } => EquippedSlotIndex.MainHand,
                { OffHand: > 0 } => EquippedSlotIndex.Offhand,
                { Head: > 0 } => EquippedSlotIndex.Helm,
                { Body: > 0 } => EquippedSlotIndex.Body,
                { Gloves: > 0 } => EquippedSlotIndex.Hands,
                { Legs: > 0 } => EquippedSlotIndex.Legs,
                { Feet: > 0 } => EquippedSlotIndex.Feet,
                { Ears: > 0 } => EquippedSlotIndex.Earring,
                { Neck: > 0 } => EquippedSlotIndex.Neck,
                { Wrists: > 0 } => EquippedSlotIndex.Wrist,
                { FingerL: > 0 } => EquippedSlotIndex.Ring1,
                { FingerR: > 0 } => EquippedSlotIndex.Ring1,
                _ => throw new ArgumentOutOfRangeException("the heck is " + itemData.RowId)
            };

            return targetSlot;
        }

        internal static void EquipGear(Item item, InventoryType type, int slotIndex, EquippedSlotIndex targetSlot) => InventoryManager.Instance()->MoveItemSlot(type, (ushort)slotIndex, InventoryType.EquippedItems, (ushort)targetSlot, 1);

        internal static ushort CurrentItemLevel => *(ushort*)((nint)(AgentStatus.Instance()) + 48);

        /*internal unsafe static uint CurrentItemLevelUI()
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
        }*/
        

        /*internal static uint CurrentItemLevelCalc()
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
        }*/

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
        internal static bool CanRepair(uint percent) => (LowestEquippedItem().Condition / 300f) < percent;// && (!AutoDuty.Plugin.Configuration.AutoRepairSelf || CanRepairItem(LowestEquippedItem().GetItemId()));

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
