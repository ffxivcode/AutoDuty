using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using AutoDuty.IPC;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using System;

namespace AutoDuty.Helpers
{
    using Windows;

    internal unsafe class AutoEquipHelper : ActiveHelperBase<AutoEquipHelper>
    {
        internal override void Start()
        {
            if (Plugin.Configuration.AutoEquipRecommendedGearGearsetter && Gearsetter_IPCSubscriber.IsEnabled)
            {
                this.TimeOut    = 5000;
                this.gearsetter = true;
            }
            else
            {
                this.TimeOut    = 2000;
                this.gearsetter = false;
            }
            base.Start();
        }

        private bool gearsetter;

        protected override string Name        => nameof(AutoEquipHelper);
        protected override string DisplayName => "Auto Equip";

        protected override int TimeOut { get; set; }


        protected override void     HelperUpdate(IFramework framework)
        {
            if(this.gearsetter)
                this.AutoEquipGearSetterUpdate(framework);
            else
                this.AutoEquipUpdate(framework);
        }

        internal override void Stop()
        {
            base.Stop();

            RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex);
            this._statesExecuted = AutoEquipState.None;
            this._index          = 0;
            this._gearset        = null;
            PortraitHelper.Invoke();
        }

        [Flags]
        enum AutoEquipState : int
        {
            None                                  = 0,
            Setting_Up                            = 1 << 0,
            Equipping                             = 1 << 1,
            Updating_Gearset                      = 1 << 2,
            Getting_Recommended_Gear              = 1 << 3,
            Recommended_Gear_Need_Second_Pass     = 1 << 4,
            Updating_Gearset_Second_Pass          = 1 << 5,
            Getting_Recommended_Gear_Second_Pass  = 1 << 6,
        }

        private AutoEquipState _statesExecuted = AutoEquipState.None;

        private void AutoEquipUpdate(IFramework framework)
        {
            if (!EzThrottler.Throttle(this.Name, 250))
                return;

            if (RecommendEquipModule.Instance()->IsUpdating)
                    return;

            if (!this._statesExecuted.HasFlag(AutoEquipState.Setting_Up))
            {
                DebugLog($"RecommendEquipModule - SetupForClassJob");
                RecommendEquipModule.Instance()->SetupForClassJob((byte)Svc.ClientState.LocalPlayer!.ClassJob.RowId);
                this._statesExecuted |= AutoEquipState.Setting_Up;
            }
            else if (!this._statesExecuted.HasFlag(AutoEquipState.Equipping))
            {
                DebugLog($"RecommendEquipModule - EquipRecommendedGear");
                RecommendEquipModule.Instance()->EquipRecommendedGear();
                this._statesExecuted |= AutoEquipState.Equipping;
            }
            else
            {
                DebugLog($"Stop");
                this.Stop();
            }
        }

        private List<(uint ItemId, InventoryType? SourceInventory, byte? SourceInventorySlot, RaptureGearsetModule.GearsetItemIndex TargetSlot)>? _gearset           = null;
        private int                                                                                                                               _index             = 0;

        private void AutoEquipGearSetterUpdate(IFramework framework)
        {
            if (!EzThrottler.Check("AutoEquipGearSetter"))
                return;

            EzThrottler.Throttle("AutoEquipGearSetter", 50);

            if (!this._statesExecuted.HasFlag(AutoEquipState.Updating_Gearset))
            {
                DebugLog($"RaptureGearsetModule - UpdateGearset");
                RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex);
                this._statesExecuted |= AutoEquipState.Updating_Gearset;
                EzThrottler.Throttle("AutoEquipGearSetter", 500, true);
            }
            else if (!this._statesExecuted.HasFlag(AutoEquipState.Getting_Recommended_Gear))
            {
                DebugLog($"Gearsetter_IPCSubscriber - GetRecommendationsForGearset");
                this._gearset     =  Gearsetter_IPCSubscriber.GetRecommendationsForGearset((byte)RaptureGearsetModule.Instance()->CurrentGearsetIndex);
                this._statesExecuted |= AutoEquipState.Getting_Recommended_Gear;
            }
            else if (this._gearset != null && this._index < this._gearset.Count)
            {
                (uint itemId, InventoryType? inventoryType, byte? sourceInventorySlot, RaptureGearsetModule.GearsetItemIndex targetSlot) = this._gearset[this._index];
                DebugLog($"Equip item {itemId} in {targetSlot} from {inventoryType} (slot {sourceInventorySlot})");

                if (inventoryType != null && sourceInventorySlot != null)
                {
                    var itemData = InventoryHelper.GetExcelItem(itemId);
                    if (itemData == null) return;
                    var equipSlotIndex = targetSlot;// InventoryHelper.GetEquippedSlot(itemData.Value);

                    if (InventoryManager.Instance()->GetInventoryContainer(inventoryType.Value)->Items[(int)sourceInventorySlot].ItemId != itemId)
                    {
                        DebugLog($"Item in slot does not match expected item");
                        this._statesExecuted |= AutoEquipState.Recommended_Gear_Need_Second_Pass;
                        this._index++;
                        return;
                    }

                    if (Plugin.Configuration.AutoEquipRecommendedGearGearsetterOldToInventory && equipSlotIndex is not RaptureGearsetModule.GearsetItemIndex.MainHand and not RaptureGearsetModule.GearsetItemIndex.OffHand &&
                        !InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[(int)equipSlotIndex].IsEmpty())
                    {
                        if (InventoryManager.Instance()->GetEmptySlotsInBag() < 1)
                        {
                            DebugLog("Moving to inventory ignored because no empty inventory slot");
                        }
                        else
                        {
                            (InventoryType inv, ushort slot) = InventoryHelper.GetFirstAvailableSlot(InventoryHelper.Bag);

                            if (slot <= 0)
                            {
                                DebugLog("Moving to inventory ignored because no empty inventory slot found.. somehow");
                            }
                            else
                            {
                                InventoryManager.Instance()->MoveItemSlot(InventoryType.EquippedItems, (ushort)equipSlotIndex, inv, slot, true);
                                DebugLog("Moving old item to inventory");
                                return;
                            }
                        }
                    }



                    DebugLog("Actually equipping");
                    InventoryHelper.EquipGear(itemData.Value, (InventoryType)inventoryType, (int)sourceInventorySlot, equipSlotIndex);
                    if (InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[(int)equipSlotIndex].ItemId == itemId)
                    {
                        DebugLog($"Successfully Equipped {itemData.Value.Name} to {equipSlotIndex.ToCustomString()}");
                        this._index++;
                    }
                }
                else
                    this._index++;
            }
            else if (this._statesExecuted.HasFlag(AutoEquipState.Recommended_Gear_Need_Second_Pass) && !this._statesExecuted.HasFlag(AutoEquipState.Updating_Gearset_Second_Pass))
            {
                // Gearsetter returns the same ring slot for both hands if two instances of the same ring should be used. This allows equiping one of them and the other one.
                DebugLog($"RaptureGearsetModule - UpdateGearsetSecondPass");
                RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex);
                this._statesExecuted |= AutoEquipState.Updating_Gearset_Second_Pass;
                EzThrottler.Throttle("AutoEquipGearSetter", 500, true);
            }
            else if (this._statesExecuted.HasFlag(AutoEquipState.Recommended_Gear_Need_Second_Pass) && !this._statesExecuted.HasFlag(AutoEquipState.Getting_Recommended_Gear_Second_Pass))
            {
                DebugLog($"Gearsetter_IPCSubscriber - GetRecommendationsForGearset");
                this._gearset     =  Gearsetter_IPCSubscriber.GetRecommendationsForGearset((byte)RaptureGearsetModule.Instance()->CurrentGearsetIndex);
                this._index       = 0;
                this._statesExecuted |= AutoEquipState.Getting_Recommended_Gear_Second_Pass;
            }
            else
            {
                DebugLog($"Gearsetter doesn't recommend any more");
                this.Stop();
            }
        }
    }
}