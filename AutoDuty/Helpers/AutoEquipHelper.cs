using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace AutoDuty.Helpers
{
    internal unsafe class AutoEquipHelper
    {
        internal static bool AutoEquipRunning = false;
        internal static void Invoke()
        {
            if (!AutoEquipRunning)
            {
                Svc.Log.Info("AutoEquip - Started");
                AutoEquipRunning = true;
                SchedulerHelper.ScheduleAction("AutoEquipTimeout", Stop, 600000);
                RecommendEquipModule.Instance()->SetupForClassJob((byte)Svc.ClientState.LocalPlayer!.ClassJob.Id);
                SchedulerHelper.ScheduleAction("EquipRecommendedGear", () => RecommendEquipModule.Instance()->EquipRecommendedGear(), () => !RecommendEquipModule.Instance()->IsUpdating);
                Svc.Log.Info($"AutoEquip - Equipped Recommended Gear");
                SchedulerHelper.ScheduleAction("UpdateGearset", () => RaptureGearsetModule.Instance()->UpdateGearset(RaptureGearsetModule.Instance()->CurrentGearsetIndex), 1000);
                Stop();
            }
        }

        internal static void Stop()
        {
            if (AutoEquipRunning)
                Svc.Log.Info($"AutoEquip - Finished");
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("AutoEquipTimeout");
            AutoEquipRunning = false;
        }
    }
}
