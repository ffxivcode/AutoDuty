using ECommons;
using ECommons.DalamudServices;
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
                        1145u, // Castrum Abania
                        837u,  // Holminster
                        823u,  // Qitana
                        822u,  // Mt. Gulg
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
            Svc.Log.Debug($"Leveling Mode: Searching for highest relevant leveling duty, Player Level: {lvl}");
            CombatRole combatRole = Player.Object.GetRole();
            if (trust)
            {
                if (TrustHelper.Members.All(tm => !tm.Value.LevelIsSet))
                {
                    Svc.Log.Debug($"Leveling Mode: All trust members levels are not set, returning");
                    return null;
                }

                TrustMember?[] memberTest = new TrustMember?[3];

                foreach ((TrustMemberName _, TrustMember member) in TrustHelper.Members)
                {
                    
                    if (member.Level < lvl && member.Level < member.LevelCap && member.LevelIsSet && memberTest.CanSelectMember(member, combatRole))
                        lvl = (short)member.Level;
                    Svc.Log.Debug($"Leveling Mode: Checking {member.Name} level which is {member.Level}, lowest level is now {lvl}");
                }
            }

            if ((lvl < 15 && !trust) || (trust && lvl < 71) || combatRole == CombatRole.NonCombat || lvl >= 100)
            {
                Svc.Log.Debug($"Leveling Mode: Lowest level is out of range (support<15 and trust<71) at {lvl} or we are not on a combat role {combatRole} or we (support) or we and all trust members are capped, returning");
                return null;
            }
            LevelingDuties.Each(x => Svc.Log.Debug($"Leveling Mode: Duties: {x.Name} CanRun: {x.CanRun(lvl)}{(trust ? $"CanTrustRun : {x.CanTrustRun()}" : "")}"));
            curContent = LevelingDuties.LastOrDefault(x => x.CanRun(lvl) && (!trust || x.CanTrustRun()));

            Svc.Log.Debug($"Leveling Mode: We found {curContent?.Name ?? "no duty"} to run");

            if (trust && curContent != null)
            {
                if (!TrustHelper.SetLevelingTrustMembers(curContent))
                {
                    Svc.Log.Debug($"Leveling Mode: We were unable to set our LevelingTrustMembers");
                    curContent = null;
                }
            }

            return curContent;
        }
    }
}
