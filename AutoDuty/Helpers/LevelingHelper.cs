using ECommons.GameFunctions;

namespace AutoDuty.Helpers
{
    public static class LevelingHelper
    {
        internal static unsafe ContentHelper.Content? SelectHighestLevelingRelevantDuty(out int index)
        {
            ContentHelper.Content? curContent = null;

            short level = PlayerHelper.GetCurrentLevelFromSheet();

            int curIndex = index = 0;
            if (level < 15 || AutoDuty.Plugin.Player!.GetRole() == CombatRole.NonCombat || level >= 100)
                return null;

            short ilvl = PlayerHelper.GetCurrentItemLevelFromGearSet();
        

            uint? dungeonId = level switch
            {
                >= 16 and < 24 => 1037u, // TamTara Deepcroft
                < 32 => 1039u, // The Thousand Maws of Toto-Rak
                < 41 => 1041u,                                    // Brayflox's Longstop
                < 53 => 1042u,                                    // Stone Vigil
                < 61 => 1064u,                                    // Sohm Al
                < 67 => 1142u,                                    // Sirensong Sea
                < 71 => 1144u,                                    // Doma Castle
                < 75 => 837u,                                     // Holminster
                < 81 => 823u,                                     // Qitana
                < 87 => 952u,                                     // Tower of Zot
                < 91 => 974u,                                     // Ktisis Hyperboreia
                _ => null
            };

            if (dungeonId != null)
            {
                if (ContentHelper.DictionaryContent.TryGetValue(dungeonId.Value, out ContentHelper.Content? content)) 
                    return content;
            }
            foreach ((uint _, ContentHelper.Content? content) in ContentHelper.DictionaryContent)
            {
                if (content.DawnContent)
                {
                    if (curContent == null || curContent.ClassJobLevelRequired < content.ClassJobLevelRequired)
                    {
                        if (content.CanRun(level, ilvl) && (content.ClassJobLevelRequired < 50 || content.ClassJobLevelRequired % 10 != 0))
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
