using ECommons.DalamudServices;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons.Throttlers;

namespace AutoDuty.Helpers
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using FFXIVClientStructs.FFXIV.Client.UI.Misc;
    using Lumina.Excel.Sheets;

    internal unsafe static class InventoryHelper
    {
        internal static InventoryType[] Bag       => [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];
        internal static uint            SlotsFree => InventoryManager.Instance()->GetEmptySlotsInBag();
        internal static uint            MySeals   => InventoryManager.Instance()->GetCompanySeals(PlayerState.Instance()->GrandCompany);
        internal static uint            MaxSeals  => InventoryManager.Instance()->GetMaxCompanySeals(PlayerState.Instance()->GrandCompany);

        internal static int ItemCount(uint itemId) => InventoryManager.Instance()->GetInventoryItemCount(itemId);

        internal static void UseItem(uint itemId) => ActionManager.Instance()->UseAction(ActionType.Item, itemId, extraParam: 65535);

        internal static bool UseItemUntilStatus(uint itemId, uint statusId, float minTime = 0, bool allowHq = true)
        {
            if (!EzThrottler.Throttle("UseItemUntilStatus", 250) || !PlayerHelper.IsReadyFull || Player.Character->IsCasting)
                return false;

            if (PlayerHelper.HasStatus(statusId, minTime))
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

        internal static Item? GetExcelItem(uint itemId) => Svc.Data.GetExcelSheet<Item>()?.GetRowOrDefault(itemId);

        internal static RaptureGearsetModule.GearsetItemIndex GetEquippedSlot(Item itemData)
        {
            RaptureGearsetModule.GearsetItemIndex targetSlot = itemData!.EquipSlotCategory.Value switch
            {
                { MainHand: > 0 } => RaptureGearsetModule.GearsetItemIndex.MainHand,
                { OffHand: > 0 } => RaptureGearsetModule.GearsetItemIndex.OffHand,
                { Head: > 0 } => RaptureGearsetModule.GearsetItemIndex.Head,
                { Body: > 0 } => RaptureGearsetModule.GearsetItemIndex.Body,
                { Gloves: > 0 } => RaptureGearsetModule.GearsetItemIndex.Hands,
                { Legs: > 0 } => RaptureGearsetModule.GearsetItemIndex.Legs,
                { Feet: > 0 } => RaptureGearsetModule.GearsetItemIndex.Feet,
                { Ears: > 0 } => RaptureGearsetModule.GearsetItemIndex.Ears,
                { Neck: > 0 } => RaptureGearsetModule.GearsetItemIndex.Neck,
                { Wrists: > 0 } => RaptureGearsetModule.GearsetItemIndex.Wrists,
                { FingerL: > 0 } => RaptureGearsetModule.GearsetItemIndex.RingLeft,
                { FingerR: > 0 } => RaptureGearsetModule.GearsetItemIndex.RingRight,
                _ => throw new ArgumentOutOfRangeException("the heck is " + itemData.RowId)
            };

            return targetSlot;
        }

        internal static void EquipGear(Item item, InventoryType type, int slotIndex, RaptureGearsetModule.GearsetItemIndex targetSlot) => 
            InventoryManager.Instance()->MoveItemSlot(type, (ushort)slotIndex, InventoryType.EquippedItems, (ushort)targetSlot, true);

        internal static (InventoryType, ushort) GetFirstAvailableSlot(params InventoryType[] types)
        {
            foreach (InventoryType type in types)
            {
                ushort slot = GetFirstAvailableSlot(type);
                if(slot > 0)
                    return (type, slot);
            }

            return (InventoryType.Invalid, 0);
        }

        internal static ushort GetFirstAvailableSlot(InventoryType container)
        {
            InventoryContainer* cont = InventoryManager.Instance()->GetInventoryContainer(container);
            for (int i = 0; i < cont->Size; i++)
            {
                if (cont->Items[i].ItemId == 0)
                    return (ushort)i;
            }
            return 0;
        }

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

            Svc.Log.Verbose("Lowest Equipped Item checks:");

            for (uint i = 0; i < 13; i++)
            {
                InventoryItem item = equipedItems->Items[i];
                Svc.Log.Verbose($"{i}: {item.ItemId} {item.Condition}");
                if (itemLowestCondition > item.Condition)
                {
                    Svc.Log.Verbose($"lower");
                    itemLowest = i;
                    itemLowestCondition = item.Condition;
                }
            }

            Svc.Log.Verbose($"lowest Index {itemLowest}");

            return equipedItems->Items[itemLowest];
        }

        public static IEnumerable<InventoryItem> GetInventorySelection(params InventoryType[] types)
        {
            IEnumerable<InventoryItem> items = [];
            foreach (InventoryType type in types)
            {
                InventoryContainer container = *InventoryManager.Instance()->GetInventoryContainer(type);
                if(container.IsLoaded)
                {
                    for (uint i = 0; i < container.Size; i++) 
                        items = items.Append(container.Items[i]);
                }
            }
            
            return items.Where(item => item.ItemId > 0);
        }

        internal static bool CanRepair() => CanRepair(Plugin.Configuration.AutoRepairPct);// && (!Plugin.Configuration.AutoRepairSelf || CanRepairItem(LowestEquippedItem().GetItemId()));
        internal static bool CanRepair(uint percent) => (LowestEquippedItem().Condition / 300f) <= percent;// && (!Plugin.Configuration.AutoRepairSelf || CanRepairItem(LowestEquippedItem().GetItemId()));

        //artisan
        internal static bool CanRepairItem(uint itemID)
        {
            var item = Svc.Data.Excel.GetSheet<Item>()?.GetRow(itemID);

            if (item == null)
                return false;

            if (item.Value.ClassJobRepair.RowId > 0)
            {
                var actualJob = (Job)(item.Value.ClassJobRepair.RowId);
                var repairItem = item.Value.ItemRepair.ValueNullable?.Item;

                if (repairItem == null)
                    return false;

                if (!HasDarkMatterOrBetter(repairItem.Value.RowId))
                    return false;

                var jobLevel = PlayerHelper.GetCurrentLevelFromSheet(actualJob);
                if (Math.Max(item.Value.LevelEquip - 10, 1) <= jobLevel)
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
                if (dm.Item.RowId < darkMatterID)
                    continue;

                if (InventoryManager.Instance()->GetInventoryItemCount(dm.Item.RowId) > 0)
                    return true;
            }
            return false;
        }
    }
}
