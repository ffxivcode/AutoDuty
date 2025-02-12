using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    public static class PathSelectionHelper
    {
        public static void AddPathSelectionEntry(uint territoryId)
        {
            if (!Plugin.Configuration.PathSelectionsByPath.ContainsKey(territoryId))
            {
                Plugin.Configuration.PathSelectionsByPath.Add(territoryId, []);
                if (ContentPathsManager.DictionaryPaths.TryGetValue(territoryId, out ContentPathsManager.ContentPathContainer? container))
                    Plugin.Configuration.PathSelectionsByPath[territoryId]!.Add(container.Paths[0].FileName, JobWithRole.All);
            }
        }

        public static void RebuildFirstPath(uint territoryId)
        {
            ContentPathsManager.ContentPathContainer container = ContentPathsManager.DictionaryPaths[territoryId];

            string firstFileName = container.Paths.First().FileName;

            Dictionary<string, JobWithRole>? pathJobConfigs = Plugin.Configuration.PathSelectionsByPath[territoryId];

            pathJobConfigs[firstFileName] = JobWithRole.None;
            JobWithRole jwr = JobWithRole.All;

            foreach (string key in pathJobConfigs.Keys)
                jwr &= ~pathJobConfigs[key];

            pathJobConfigs[firstFileName] = jwr;
        }
    }
}
