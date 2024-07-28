using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using ECommons.DalamudServices;
    using ECommons.GameFunctions;
    using ECommons.GameHelpers;
    using FFXIVClientStructs.FFXIV.Client.Game.UI;
    using Lumina.Excel.GeneratedSheets;

    public static class LevelingHelper
    {
        internal static unsafe ContentHelper.Content? SelectHighestLevelingRelevantDuty(out int index)
        {
            ContentHelper.Content? curContent = null;

            PlayerState* playerState = PlayerState.Instance();

            short        level       = playerState->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>()?.GetRow((uint)AutoDuty.Plugin.Player.GetJob())?.ExpArrayIndex ?? 0];

            int curIndex = index = 0;
            if (level < 15 || AutoDuty.Plugin.Player!.GetRole() == CombatRole.NonCombat || level >= 100)
                return null;

            foreach ((uint id, ContentHelper.Content? content) in ContentHelper.DictionaryContent)
            {
                if (content.DawnContent)
                {
                    if (curContent == null || curContent.ClassJobLevelRequired < content.ClassJobLevelRequired)
                    {
                        if (FileHelper.DictionaryPathFiles.ContainsKey(id) && 
                            content.ClassJobLevelRequired % 10 != 0 && 
                            content.ClassJobLevelRequired <= level)
                        {
                            curContent = content;
                            index      = curIndex;
                        }
                    }
                    curIndex++;
                }
            }

            return curContent ?? null;
        }
    }
}
