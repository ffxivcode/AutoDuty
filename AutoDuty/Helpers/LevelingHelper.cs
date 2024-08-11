using ECommons.GameFunctions;

namespace AutoDuty.Helpers
{
    using System.Collections.Generic;
    using System.Linq;
    using Managers;

    public static class LevelingHelper
    {
        private static ContentHelper.Content[] levelingDuties = [];

        private static ContentHelper.Content[] LevelingDuties
        {
            get
            {
                if (levelingDuties.Length <= 0)
                {
                    uint[] ids =
                    [
                        1037u, // TamTara Deepcroft
                        1039u, // The Thousand Maws of Toto-Rak
                        1041u, // Brayflox's Longstop
                        1042u, // Stone Vigil
                        1064u, // Sohm Al
                        1142u, // Sirensong Sea
                        1144u, // Doma Castle
                        837u,  // Holminster
                        823u,  // Qitana
                        952u,  // Tower of Zot
                        974u,  // Ktisis Hyperboreia
                    ];
                    levelingDuties = ids.Select(id => ContentHelper.DictionaryContent.GetValueOrDefault(id)).Where(c => c != null).Cast<ContentHelper.Content>().Reverse().ToArray();
                }
                return levelingDuties;
            }
        }

        internal static ContentHelper.Content? SelectHighestLevelingRelevantDuty(bool trust = false)
        {
            ContentHelper.Content? curContent = null;

            short lvl = PlayerHelper.GetCurrentLevelFromSheet();

            if (lvl < 15 || AutoDuty.Plugin.Player!.GetRole() == CombatRole.NonCombat || lvl >= 100)
                return null;

            if (trust)
            {
                if (TrustManager.members.Any(tm => tm.Value.Level <= 0)) 
                    return null;


                foreach ((TrustMemberName _, TrustMember member) in TrustManager.members)
                {
                    if (member.Level < lvl && member.Level < member.LevelCap)
                        lvl = (short) member.Level;
                }
            }


            short ilvl = PlayerHelper.GetCurrentItemLevelFromGearSet();
            
            if(lvl is >= 16 and < 91)
                foreach (ContentHelper.Content duty in LevelingDuties)
                    if (duty.CanRun(lvl, ilvl) && (!trust || duty.CanTrustRun()))
                    {
                        curContent = duty;
                        break;
                    }

            if (curContent == null)
            {
                foreach ((uint _, ContentHelper.Content? content) in ContentHelper.DictionaryContent)
                {
                    if (content.DawnContent)
                    {
                        if (curContent == null || curContent.ClassJobLevelRequired < content.ClassJobLevelRequired)
                        {
                            if (content.CanRun(lvl, ilvl) && (!trust || content.CanTrustRun()) && (content.ClassJobLevelRequired < 50 || content.ClassJobLevelRequired % 10 != 0))
                            {
                                curContent = content;
                            }
                        }
                    }
                }
            }
            if (trust && curContent != null)
                if (!TrustHelper.SetLowestTrustMembers(curContent))
                    curContent = null;

            return curContent ?? null;
        }
    }
}
