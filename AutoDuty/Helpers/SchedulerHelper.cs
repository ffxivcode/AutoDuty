using System.Collections.Generic;
using System;
using Dalamud.Plugin.Services;

namespace AutoDuty.Helpers
{
    internal static class SchedulerHelper
    {
        internal class Schedule
        {
            internal string Name { get; set; } = string.Empty;

            internal Action Action { get; set; } = () => { };

            internal int TimeMS { get; set; }

            internal bool RunOnce { get; set; } = true;
        }

        internal static HashSet<Schedule> schedules = [];

        internal static bool ScheduleAction(string name, Action action, int timeMS, bool runOnce = true) => schedules.Add(new Schedule() { Name = name, Action = action, TimeMS = Environment.TickCount + timeMS, RunOnce = runOnce });

        internal static int DescheduleAction(string name) => schedules.RemoveWhere(s => s.Name == name);

        internal static void ScheduleInvoker(IFramework _)
        {
            foreach (var schedule in schedules)
            {
                if (Environment.TickCount >= schedule.TimeMS)
                {
                    schedule.Action();
                    schedules.Remove(schedule);
                }
            }
        }
    }
}
