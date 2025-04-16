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
            None = 0,
            Setting_Up = 1,
            Equipping = 2,
            Updating_Gearset = 4,
            Getting_Recommended_Gear = 8
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
                Svc.Log.Debug($"AutoEquipHelper - RecommendEquipModule - SetupForClassJob");
                RecommendEquipModule.Instance()->SetupForClassJob((byte)Svc.ClientState.LocalPlayer!.ClassJob.RowId);
                this._statesExecuted |= AutoEquipState.Setting_Up;
            }
            else if (!this._statesExecuted.HasFlag(AutoEquipState.Equipping))
            {
                Svc.Log.Debug($"AutoEquipHelper - RecommendEquipModule - EquipRecommendedGear");
                RecommendEquipModule.Instance()->EquipRecommendedGear();
                this._statesExecuted |= AutoEquipState.Equipping;
            }
            else
            {
                Svc.Log.Debug($"AutoEquipHelper - Stop");
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
                Svc.Log.Debug($"AutoEquipHelper - RaptureGearsetModule - UpdateGearset");
                RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex);
                this._statesExecuted |= AutoEquipState.Updating_Gearset;
                EzThrottler.Throttle("AutoEquipGearSetter", 500, true);
            }
            else if (!this._statesExecuted.HasFlag(AutoEquipState.Getting_Recommended_Gear))
            {
                Svc.Log.Debug($"AutoEquipHelper - Gearsetter_IPCSubscriber - GetRecommendationsForGearset");
                this._gearset     =  Gearsetter_IPCSubscriber.GetRecommendationsForGearset((byte)RaptureGearsetModule.Instance()->CurrentGearsetIndex);
                this._statesExecuted |= AutoEquipState.Getting_Recommended_Gear;
            }
            else if (this._gearset != null && this._index < this._gearset.Count)
            {
                (uint itemId, InventoryType? inventoryType, byte? sourceInventorySlot, RaptureGearsetModule.GearsetItemIndex targetSlot) = this._gearset[this._index];
                Svc.Log.Debug($"AutoEquipGearSetter: Equip item {itemId} in {targetSlot} from {inventoryType} (slot {sourceInventorySlot})");

                if (inventoryType != null && sourceInventorySlot != null)
                {
                    var itemData = InventoryHelper.GetExcelItem(itemId);
                    if (itemData == null) return;
                    var equipSlotIndex = targetSlot;// InventoryHelper.GetEquippedSlot(itemData.Value);
                    
                    InventoryHelper.EquipGear(itemData.Value, (InventoryType)inventoryType, (int)sourceInventorySlot, equipSlotIndex);
                    if (InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[(int)equipSlotIndex].ItemId == itemId)
                    {
                        Svc.Log.Debug($"AutoEquipGearSetter: Successfully Equipped {itemData.Value.Name} to {equipSlotIndex.ToCustomString()}");
                        this._index++;
                    }
                }
                else
                    this._index++;
            }
            else
            {
                Svc.Log.Debug($"AutoEquipHelper - Stop");
                this.Stop();
            }
        }
    }
}