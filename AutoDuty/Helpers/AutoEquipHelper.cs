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
                AutoDuty.Plugin.States |= PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(false);
                
                if (AutoDuty.Plugin.Configuration.AutoEquipRecommendedGearGearsetter && Gearsetter_IPCSubscriber.IsEnabled)
                {
                    SchedulerHelper.ScheduleAction("AutoEquipTimeOut", Stop, 60000);
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
            if (State == ActionState.Running)
                Svc.Log.Info("AutoEquip Finished");
            GotoInnHelper.Stop();
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("AutoEquipTimeOut");
            Svc.Framework.Update -= AutoEquipUpdate;
            Svc.Framework.Update -= AutoEquipGearSetterUpdate;
            State = ActionState.None;
            AutoDuty.Plugin.States &= ~PluginState.Other;
            if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                AutoDuty.Plugin.SetGeneralSettings(true);
            _statesExecuted = AutoEquipState.None;
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
                RecommendEquipModule.Instance()->SetupForClassJob((byte)Svc.ClientState.LocalPlayer!.ClassJob.Id);
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

        private static List<(uint ItemId, InventoryType? SourceInventory, byte? SourceInventorySlot)>? _gearset = null;
        private static int _index = 0;

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
                (uint itemId, InventoryType? inventoryType, byte? sourceInventorySlot) = _gearset[_index];
                Svc.Log.Info($"AutoEquipGearSetter: Equip item {itemId} from {inventoryType} (slot {sourceInventorySlot})");

                if (inventoryType != null && sourceInventorySlot != null)
                {
                    InventoryHelper.EquipGear((InventoryType)inventoryType, (int)sourceInventorySlot);
                    EzThrottler.Throttle("AutoEquipGearSetter", 200, true);
                }
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
