using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoDuty.Helpers
{
    using Data;
    using ECommons.ExcelServices;

    public static class PathSelectionHelper
    {
        public static void AddPathSelectionEntry(uint territoryId)
        {
            if (!Plugin.Configuration.PathSelectionsByPath.ContainsKey(territoryId))
            {
                Dictionary<string, JobWithRole> jobs = [];
                Plugin.Configuration.PathSelectionsByPath.Add(territoryId, jobs);
                if (ContentPathsManager.DictionaryPaths.TryGetValue(territoryId, out ContentPathsManager.ContentPathContainer? container))
                    foreach (Job job in Enum.GetValues<Job>())
                    {
                        string path = container.SelectPath(out _, job)!.FileName;
                        jobs.TryAdd(path, JobWithRole.None);
                        jobs[path] |= job.JobToJobWithRole();
                    }

                Plugin.Configuration.Save();
            }
        }

        public static void RebuildDefaultPaths(uint territoryId)
        {
            ContentPathsManager.ContentPathContainer container = ContentPathsManager.DictionaryPaths[territoryId];

            Dictionary<string, JobWithRole>? pathJobConfigs = Plugin.Configuration.PathSelectionsByPath[territoryId];

            JobWithRole jwr = JobWithRole.All;

            foreach (string key in pathJobConfigs.Keys)
                jwr &= ~pathJobConfigs[key];

            foreach (Job job in jwr.ContainedJobs())
            {
                string path = container.SelectPath(out _, job)!.FileName;
                pathJobConfigs.TryAdd(path, JobWithRole.None);
                pathJobConfigs[path] |= job.JobToJobWithRole();
            }
            Plugin.Configuration.Save();
        }
    }
}
