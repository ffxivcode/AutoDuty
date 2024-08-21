using System.Collections.Generic;
using System;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoDuty.Helpers
{
    internal static class SchedulerHelper
    {
        internal class Schedule
        {
            internal List<Action> Action { get; set; } = [() => { }];

            internal int TimeMS { get; set; } = 0;

            internal Func<bool>? Condition { get; set; } = null;

            internal bool RunOnce { get; set; } = true;
        }

        internal static Dictionary<string, Schedule> Schedules = [];

        internal static void ScheduleAction(string name, Action action, int timeMS, bool runOnce = true) => ScheduleAction(name, [action], null, timeMS, runOnce);

        internal static void ScheduleAction(string name, List<Action> action, int timeMS, bool runOnce = true) => ScheduleAction(name, action, null, timeMS, runOnce);

        internal static void ScheduleAction(string name, Action action, Func<bool> condition, bool runOnce = true) => ScheduleAction(name, [action], condition, 0, runOnce);

        internal static void ScheduleAction(string name, List<Action> action, Func<bool> condition, bool runOnce = true) => ScheduleAction(name, action, condition, 0, runOnce);

        private static void ScheduleAction(string name, List<Action> action, Func<bool>? condition, int timeMS, bool runOnce = true)
        {
            if (!Schedules.TryAdd(name, new Schedule() { Action = action, Condition = condition, TimeMS = Environment.TickCount + timeMS, RunOnce = runOnce }))
            {
                Svc.Log.Debug($"SchedulerHelper - {name} already exists in SchedulerQueue, updating");
                Schedules.Remove(name);
                Schedules.TryAdd(name, new Schedule() { Action = action, Condition = condition, TimeMS = Environment.TickCount + timeMS, RunOnce = runOnce });
            }
            else
                Svc.Log.Debug($"SchedulerHelper - {name} Added to queue");
        }

        internal static bool DescheduleAction(string name) => Schedules.Remove(name);

        internal static void ScheduleInvoker(IFramework _)
        {
            foreach (var schedule in Schedules)
            {
                if (schedule.Value.TimeMS != 0 ? Environment.TickCount >= schedule.Value.TimeMS : schedule.Value.Condition?.Invoke() ?? false)
                {
                    Svc.Log.Debug($"SchedulerHelper - Executing action {schedule.Key}");
                    schedule.Value.Action.ForEach(a => a.Invoke());
                    if (schedule.Value.RunOnce)
                        Schedules.Remove(schedule.Key);
                }
            }
        }
    }
}
