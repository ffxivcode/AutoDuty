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
    using FFXIVClientStructs.FFXIV.Client.UI.Misc;
    using Lumina.Excel.GeneratedSheets;

    public static class LevelingHelper
    {
        internal static unsafe ContentHelper.Content? SelectHighestLevelingRelevantDuty(out int index)
        {
            ContentHelper.Content? curContent = null;

            PlayerState* playerState = PlayerState.Instance();

            short level = playerState->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>()?.GetRow((uint)AutoDuty.Plugin.Player.GetJob())?.ExpArrayIndex ?? 0];

            int curIndex = index = 0;
            if (level < 15 || AutoDuty.Plugin.Player!.GetRole() == CombatRole.NonCombat || level >= 100)
                return null;


            RaptureGearsetModule* gearsetModule = RaptureGearsetModule.Instance();
            int                   gearsetId     = gearsetModule->CurrentGearsetIndex;
            gearsetModule->UpdateGearset(gearsetId);
            short ilvl = gearsetModule->GetGearset(gearsetId)->ItemLevel;
        

            uint? dungeonId = level switch
            {
                >= 16 and < 24 => 1037u, // TamTara Deepcroft
                < 32 => 1039u, // The Thousand Maws of Toto-Rak
                < 41 => 1041u, // Brayflox's Longstop
                < 53 => 1042u, // Stone Vigil
                < 61 => 1064u, // Sohm Al
                < 67 => 1142u, // Sirensong Sea
                < 71 => 1144u, // Doma Castle
                < 75 => 837u, // Holminster
                < 81 => 823u, // Qitana
                < 87 => 952u, // Tower of Zot
                < 91 => 974u, // Ktisis Hyperboreia
                _ => null
            };

            if (dungeonId != null)
            {
                if (ContentHelper.DictionaryContent.TryGetValue(dungeonId.Value, out ContentHelper.Content? content)) 
                    return content;
            }
            foreach ((uint id, ContentHelper.Content? content) in ContentHelper.DictionaryContent)
            {
                if (content.DawnContent)
                {
                    if (curContent == null || curContent.ClassJobLevelRequired < content.ClassJobLevelRequired)
                    {
                        if (FileHelper.DictionaryPathFiles.ContainsKey(id) &&
                            content.ClassJobLevelRequired % 10 != 0        &&
                            content.ClassJobLevelRequired      <= level    &&
                            content.ItemLevelRequired          <= ilvl)
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
