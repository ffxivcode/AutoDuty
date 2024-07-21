using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using ECommons.GameFunctions;

    internal static class MultiPathHelper
    {
        public static int BestPathIndex()
        {
            Svc.Log.Info("Best Path check");
            if (FileHelper.DictionaryPathFiles.TryGetValue(Svc.ClientState.TerritoryType, out List<string> curPaths) && curPaths.Count > 1)
            {
                Svc.Log.Info("paths found");
                if (Svc.ClientState.LocalPlayer?.GetRole() == CombatRole.Tank)
                {
                    Svc.Log.Info("tank");
                    for (int index = 0; index < curPaths.Count; index++)
                    {
                        string curPath = curPaths[index];
                        if (curPath.Contains(PathIdentifiers.W2W))
                        {
                            Svc.Log.Info("Best Path: " + index);
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
