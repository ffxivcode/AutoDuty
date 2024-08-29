using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using AutoDuty.IPC;
using System.Collections.Generic;

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

                int extraDelay = 0;

                if (AutoDuty.Plugin.Configuration.AutoEquipRecommendedGearGearsetter && Gearsetter_IPCSubscriber.IsEnabled)
                {
                    extraDelay = 500;
                    RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex);
                    SchedulerHelper.ScheduleAction("AutoEquip_Gearsetter",
                        () =>
                        {
                            List<(uint ItemId, InventoryType? SourceInventory, byte? SourceInventorySlot)> gearset = Gearsetter_IPCSubscriber.GetRecommendationsForGearset((byte)RaptureGearsetModule.Instance()->CurrentGearsetIndex);

                            if (gearset != null)
                            {
                                for (int i = 0; i < gearset.Count; i++)
                                {
                                    (uint itemId, InventoryType? inventoryType, byte? sourceInventorySlot) = gearset[i];
                                    //Svc.Log.Info($"Recommendation: Equip item {itemId} from {inventoryType} (slot {sourceInventorySlot})");

                                    if (inventoryType != null && sourceInventorySlot != null)
                                    {
                                        SchedulerHelper.ScheduleAction($"AutoEquip_{inventoryType}_{sourceInventorySlot}", () => InventoryHelper.EquipGear((InventoryType)inventoryType, (int)sourceInventorySlot), 200 * (i + 1));
                                        extraDelay += 201;
                                    }
                                }
                            }
                        }, 500);
                }
                else
                {
                    RecommendEquipModule.Instance()->SetupForClassJob((byte)Svc.ClientState.LocalPlayer!.ClassJob.Id);
                    SchedulerHelper.ScheduleAction("AutoEquip_EquipRecommendedGear", () => RecommendEquipModule.Instance()->EquipRecommendedGear(), () => !RecommendEquipModule.Instance()->IsUpdating);
                }
                
                SchedulerHelper.ScheduleAction("AutoEquip_Finish", 
                    () =>
                    {
                        SchedulerHelper.ScheduleAction("AutoEquip_UpdateGearsetFinish", () => RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex), 50);
                        SchedulerHelper.ScheduleAction("AutoEquip_FinishedLog", () => Svc.Log.Info($"AutoEquip - Finished"), 500);
                        SchedulerHelper.ScheduleAction("AutoEquip_SetRunningFalse", () => State = ActionState.None,        500);
                        SchedulerHelper.ScheduleAction("AutoEquip_SetPreviousStage", () => AutoDuty.Plugin.States &= ~PluginState.Other, 500);
                        SchedulerHelper.ScheduleAction("AutoEquip_SetActionBlank", () => AutoDuty.Plugin.Action =  "", 500);
                    }, 650 + extraDelay);
            }
        }
    }
}
