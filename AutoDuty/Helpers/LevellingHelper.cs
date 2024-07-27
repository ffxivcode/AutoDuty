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

    public static class LevellingHelper
    {
        internal static unsafe ContentHelper.Content? SelectHighestLevellingRelevantDuty(out int index)
        {
            ContentHelper.Content? curContent = null;

            PlayerState* playerState = PlayerState.Instance();
            short        level       = playerState->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>()?.GetRow((uint)AutoDuty.Plugin.Player.GetJob())?.ExpArrayIndex ?? 0];

            int curIndex = index = 0;
            foreach ((uint id, ContentHelper.Content? content) in ContentHelper.DictionaryContent)
            {
                if (content.DawnContent)
                {
                    if (content.ClassJobLevelRequired % 10 != 0 && content.ClassJobLevelRequired <= level && FileHelper.DictionaryPathFiles.ContainsKey(id))
                    {
                        if (curContent == null || curContent.ClassJobLevelRequired < content.ClassJobLevelRequired)
                        {
                            curContent = content;
                            index = curIndex;
                        }
                    }
                    curIndex++;
                }
            }

            return curContent ?? null;
        }
    }
}
