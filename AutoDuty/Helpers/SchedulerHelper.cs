using System.Collections.Generic;
using System;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons;

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
            if (!Schedules.TryAdd(name, new Schedule() { Action = action, Condition = condition, TimeMS = timeMS > 0 ?Environment.TickCount + timeMS : 0, RunOnce = runOnce }))
            {
                Svc.Log.Debug($"SchedulerHelper - {name} already exists in Scheduler, updating");
                Schedules[name] = new Schedule() { Action = action, Condition = condition, TimeMS = timeMS > 0 ? Environment.TickCount + timeMS : 0, RunOnce = runOnce };
            }
            else
                Svc.Log.Debug($"SchedulerHelper - {name} Added to Scheduler");
        }

        internal static bool DescheduleAction(string name) => Schedules.Remove(name);

        private static readonly Queue<string> _schedulesToRemove = [];

        internal static void ScheduleInvoker(IFramework _)
        {
            foreach (var schedule in Schedules)
            {
                if (schedule.Value.TimeMS != 0 ? Environment.TickCount >= schedule.Value.TimeMS : schedule.Value.Condition?.Invoke() ?? false)
                {
                    Svc.Log.Debug($"SchedulerHelper - Executing action {schedule.Key}");
                    schedule.Value.Action.ForEach(a => a.Invoke());
                    if (schedule.Value.RunOnce || schedule.Value.Condition != null)
                        _schedulesToRemove.Enqueue(schedule.Key);
                    else
                        schedule.Value.TimeMS += Environment.TickCount;
                }
            }

            while (_schedulesToRemove.Count !=0)
            {
                var schedule = _schedulesToRemove.Dequeue();
                if (schedule.IsNullOrEmpty())
                    return;
                Svc.Log.Debug($"SchedulerHelper - {schedule} Removed from Scheduler");
                Schedules.Remove(schedule);
            }
        }
    }
}
