using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ECommons.Automation.LegacyTaskManager;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;

namespace AutoDuty.Helpers
{
    internal unsafe class AutoEquipHelper
    {

        private static TaskManager _taskManager;

        internal static void Invoke(TaskManager taskManager)
        {
            if (!AutoEquipRunning)
            {
                Svc.Log.Info($"Equipping Started");
                AutoEquipRunning = true;
                _taskManager = taskManager;
                SchedulerHelper.ScheduleAction("AutoEquipTimeout", Stop, AutoDuty.Plugin.Configuration.AutoEquipRecommendedGear ? 300000 : 600000);
                Svc.Framework.Update += AutoEquipRecommendedGear;
            }
        }

        internal static void Stop()
        {
            if (AutoEquipRunning)
                Svc.Log.Info($"AutoEquip Finished");
            SchedulerHelper.DescheduleAction("AutoEquipTimeout");
            Svc.Framework.Update -= AutoEquipRecommendedGear;
            AutoEquipRunning = false;
            AutoDuty.Plugin.Action = "";
        }

        internal static bool AutoEquipRunning = false;

        internal static unsafe void AutoEquipRecommendedGear(IFramework framework)
        {
            _taskManager.Insert(() =>
            {
                if (Svc.Condition[ConditionFlag.InCombat] ||
                    Svc.Condition[ConditionFlag.BetweenAreas])
                {
                    Svc.Log.Debug("Cannot equip gear: player is in combat or between areas.");
                    AutoEquipRunning = false;
                    return false;
                }
                Stop();
                return true;
            }, "CheckConditions");

            RecommendEquipModule* equipModule = RecommendEquipModule.Instance();

            _taskManager.EnqueueImmediate(() =>
            {
                Svc.Log.Debug("Set up recommended gear for current class/job.");
                equipModule->SetupForClassJob((byte)Svc.ClientState.LocalPlayer!.ClassJob.Id);
                equipModule->EquipRecommendedGear();
            });

            _taskManager.EnqueueImmediate(() =>
            {
                Svc.Log.Info("Attempted to equip recommended gear.");
                equipModule->EquipRecommendedGear();
            });


            _taskManager.EnqueueImmediate(() =>
            {
                equipModule->EquipRecommendedGear();
                int id = RaptureGearsetModule.Instance()->CurrentGearsetIndex;
                RaptureGearsetModule.Instance()->UpdateGearset(id);
                Svc.Log.Info($"Attempted to update gearset {id}.");
                AutoEquipRunning = false;
                Stop();

            }, "UpdateGearset");

            // Force prevention of additional calls per queue
            Svc.Framework.Update -= AutoEquipRecommendedGear;
        }
    }
}