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
    internal unsafe class AutoEquipHelper
    {
        internal static ActionState State = ActionState.None;

        internal static void Invoke()
        {
            if (State != ActionState.Running)
            {
                Svc.Log.Info("AutoEquip - Started");
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;
                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(false);
                
                if (Plugin.Configuration.AutoEquipRecommendedGearGearsetter && Gearsetter_IPCSubscriber.IsEnabled)
                {
                    SchedulerHelper.ScheduleAction("AutoEquipTimeOut", Stop, 5000);
                    Svc.Framework.Update += AutoEquipGearSetterUpdate;
                }
                else
                {
                    Svc.Framework.Update += AutoEquipUpdate;
                    SchedulerHelper.ScheduleAction("AutoEquipTimeOut", Stop, 2000);
                }
            }
        }

        internal static void Stop()
        {
            Svc.Log.Debug("AutoEquipHelper.Stop");
            RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex);
            if (State == ActionState.Running)
                Svc.Log.Info("AutoEquip Finished");
            Plugin.Action = "";
            SchedulerHelper.DescheduleAction("AutoEquipTimeOut");
            Svc.Framework.Update -= AutoEquipUpdate;
            Svc.Framework.Update -= AutoEquipGearSetterUpdate;
            State                =  ActionState.None;
            Plugin.States        &= ~PluginState.Other;
            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(true);
            _statesExecuted = AutoEquipState.None;
            _index = 0;
            _gearset = null;
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

        private static AutoEquipState _statesExecuted = AutoEquipState.None;

        internal static void AutoEquipUpdate(IFramework framework)
        {
            if (RecommendEquipModule.Instance()->IsUpdating)
                    return;

            if (!_statesExecuted.HasFlag(AutoEquipState.Setting_Up))
            {
                Svc.Log.Debug($"AutoEquipHelper - RecommendEquipModule - SetupForClassJob");
                RecommendEquipModule.Instance()->SetupForClassJob((byte)Svc.ClientState.LocalPlayer!.ClassJob.RowId);
                _statesExecuted |= AutoEquipState.Setting_Up;
            }
            else if (!_statesExecuted.HasFlag(AutoEquipState.Equipping))
            {
                Svc.Log.Debug($"AutoEquipHelper - RecommendEquipModule - EquipRecommendedGear");
                RecommendEquipModule.Instance()->EquipRecommendedGear();
                _statesExecuted |= AutoEquipState.Equipping;
            }
            else
            {
                Svc.Log.Debug($"AutoEquipHelper - Stop");
                Stop();
            }
        }

        private static List<(uint ItemId, InventoryType? SourceInventory, byte? SourceInventorySlot, RaptureGearsetModule.GearsetItemIndex TargetSlot)>? _gearset           = null;
        private static int                                                                                                                               _index             = 0;
        internal static void AutoEquipGearSetterUpdate(IFramework framework)
        {
            if (!EzThrottler.Check("AutoEquipGearSetter"))
                return;

            EzThrottler.Throttle("AutoEquipGearSetter", 50);

            if (!_statesExecuted.HasFlag(AutoEquipState.Updating_Gearset))
            {
                Svc.Log.Debug($"AutoEquipHelper - RaptureGearsetModule - UpdateGearset");
                RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex);
                _statesExecuted |= AutoEquipState.Updating_Gearset;
                EzThrottler.Throttle("AutoEquipGearSetter", 500, true);
            }
            else if (!_statesExecuted.HasFlag(AutoEquipState.Getting_Recommended_Gear))
            {
                Svc.Log.Debug($"AutoEquipHelper - Gearsetter_IPCSubscriber - GetRecommendationsForGearset");
                _gearset = Gearsetter_IPCSubscriber.GetRecommendationsForGearset((byte)RaptureGearsetModule.Instance()->CurrentGearsetIndex);
                _statesExecuted |= AutoEquipState.Getting_Recommended_Gear;
            }
            else if (_gearset != null && _index < _gearset.Count)
            {
                (uint itemId, InventoryType? inventoryType, byte? sourceInventorySlot, RaptureGearsetModule.GearsetItemIndex targetSlot) = _gearset[_index];
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
                        _index++;
                    }
                }
                else
                    _index++;
            }
            else
            {
                Svc.Log.Debug($"AutoEquipHelper - Stop");
                Stop();
            }
        }
    }
}
