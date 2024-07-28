using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using ECommons.ExcelServices;
    using ECommons.GameFunctions;
    using ECommons.GameHelpers;

    internal static class MultiPathHelper
    {
        public static int BestPathIndex()
        {
            uint territoryType = AutoDuty.Plugin.CurrentTerritoryContent.TerritoryType;
            if (FileHelper.DictionaryPathFiles.TryGetValue(territoryType, out List<string> curPaths) && curPaths.Count > 1)
            {
                if (AutoDuty.Plugin.Configuration.PathSelections.TryGetValue(territoryType, out Dictionary<Job, int>? jobConfig))
                {
                    if (jobConfig.TryGetValue(AutoDuty.Plugin.Player.GetJob(), out int pathId))
                    {
                        return pathId;
                    }
                }

                if (AutoDuty.Plugin.Player?.GetRole() == CombatRole.Tank)
                {
                    for (int index = 0; index < curPaths.Count; index++)
                    {
                        string curPath = curPaths[index];
                        if (curPath.Contains(PathIdentifiers.W2W))
                        {
                            return index;
                        }
                    }
                }

                return 0;
            }

            return 0;
        }
    }
}
