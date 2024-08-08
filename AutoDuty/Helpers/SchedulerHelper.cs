using System.Collections.Generic;
using System.Collections;
using System;
using Dalamud.Plugin.Services;

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

        internal static bool ScheduleAction(string name, Action action, int timeMS, bool runOnce = true) => Schedules.TryAdd(name, new Schedule() { Action = [action], TimeMS = Environment.TickCount + timeMS, RunOnce = runOnce });

        internal static bool ScheduleAction(string name, List<Action> action, int timeMS, bool runOnce = true) => Schedules.TryAdd(name, new Schedule() { Action = action, TimeMS = Environment.TickCount + timeMS, RunOnce = runOnce });

        internal static bool ScheduleAction(string name, Action action, Func<bool> condition, bool runOnce = true) => Schedules.TryAdd(name, new Schedule() { Action = [action], Condition = condition, RunOnce = runOnce });

        internal static bool ScheduleAction(string name, List<Action> action, Func<bool> condition, bool runOnce = true) => Schedules.TryAdd(name, new Schedule() { Action = action, Condition = condition, RunOnce = runOnce });

        internal static bool DescheduleAction(string name) => Schedules.Remove(name);

        internal static void ScheduleInvoker(IFramework _)
        {
            foreach (var schedule in Schedules)
            {
                if (schedule.Value.TimeMS != 0 ? Environment.TickCount >= schedule.Value.TimeMS : schedule.Value.Condition?.Invoke() ?? false)
                {
                    schedule.Value.Action.ForEach(a => a.Invoke());
                    if (schedule.Value.RunOnce)
                        Schedules.Remove(schedule.Key);
                }
            }
        }
    }
}
