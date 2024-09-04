using ECommons.GameFunctions;
using System.Collections.Generic;
using System.Linq;

namespace AutoDuty.Helpers
{
    internal static class LevelingHelper
    {
        private static Content[] levelingDuties = [];

        internal static Content[] LevelingDuties
        {
            get
            {
                if (levelingDuties.Length <= 0)
                {
                    uint[] ids =
                    [
                        1036u, // Sastasha
                        1037u, // TamTara Deepcroft
                        1039u, // The Thousand Maws of Toto-Rak
                        1041u, // Brayflox's Longstop
                        1042u, // Stone Vigil
                        1064u, // Sohm Al
                        1065u, // The Aery
                        1066u, // The Vault
                        1109u, // The Great Gubal Library
                        1142u, // Sirensong Sea
                        1144u, // Doma Castle
                        837u,  // Holminster
                        823u,  // Qitana
                        952u,  // Tower of Zot
                        974u,  // Ktisis Hyperboreia
                        1167u, // Ihuykatumu
                        1193u, // Worqor Zormor
                        1194u, // The Skydeep Cenote
                        1198u, // Vanguard
                        1208u, // Origenics
                    ];
                    levelingDuties = [.. ids.Select(id => ContentHelper.DictionaryContent.GetValueOrDefault(id)).Where(c => c != null).Cast<Content>().OrderBy(x => x.ClassJobLevelRequired).ThenBy(x => x.ItemLevelRequired).ThenBy(x => x.ExVersion).ThenBy(x => x.DawnIndex)];
                }
                return levelingDuties;
            }
        }

        internal static Content? SelectHighestLevelingRelevantDuty(bool trust = false)
        {
            Content? curContent = null;

            var lvl = PlayerHelper.GetCurrentLevelFromSheet();
            CombatRole combatRole = Player.Object.GetRole();
            if (trust)
            {
                if (TrustHelper.Members.All(tm => !tm.Value.LevelIsSet)) 
                    return null;

                TrustMember?[] memberTest = new TrustMember?[3];

                foreach ((TrustMemberName _, TrustMember member) in TrustHelper.Members)
                    if (member.Level < lvl && member.Level < member.LevelCap && member.LevelIsSet && memberTest.CanSelectMember(member, combatRole))
                        lvl = (short)member.Level;
            }

            if (lvl < 15 || combatRole == CombatRole.NonCombat || lvl >= 100)
                return null;

            if (lvl >= 15)
                curContent = levelingDuties.LastOrDefault(x => x.CanRun(lvl) && (!trust || x.CanTrustRun()));

            if (curContent == null)
            {
                foreach ((uint _, Content? content) in ContentHelper.DictionaryContent)
                {
                    if (content.DawnContent)
                    {
                        if (curContent == null || curContent.ClassJobLevelRequired < content.ClassJobLevelRequired)
                        {
                            if (content.CanRun(lvl) && (!trust || content.CanTrustRun()) && (content.ClassJobLevelRequired < 50 || content.ClassJobLevelRequired % 10 != 0))
                            {
                                curContent = content;
                            }
                        }
                    }
                }
            }

            if (trust && curContent != null)
                if (!TrustHelper.SetLevelingTrustMembers(curContent))
                    curContent = null;

            return curContent;
        }
    }
}
