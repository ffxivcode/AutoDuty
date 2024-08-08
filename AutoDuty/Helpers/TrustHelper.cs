using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using ECommons;
    using ECommons.Automation;
    using ECommons.DalamudServices;
    using ECommons.GameFunctions;
    using ECommons.Throttlers;
    using FFXIVClientStructs.FFXIV.Component.GUI;
    using Managers;
    using static Dalamud.Interface.Utility.Raii.ImRaii;
    using static global::AutoDuty.Helpers.ContentHelper;

    internal static class TrustHelper
    {
        internal static unsafe uint GetLevelFromTrustWindow(AtkUnitBase* addon)
        {
            if (addon == null)
                return 0;

            AtkComponentNode* atkResNode       = addon->GetComponentNodeById(88);
            AtkResNode*       resNode          = atkResNode->Component->UldManager.NodeList[5];
            AtkResNode*       resNodeChildNode = resNode->GetComponent()->UldManager.NodeList[0];
            return Convert.ToUInt32(resNodeChildNode->GetAsAtkCounterNode()->NodeText.ExtractText());
        }


        internal static bool CanTrustRun(this Content content)
        {
            if (!content.TrustContent)
                return false;

            if (content.TrustMembers.Any(tm => tm.Level <= 0))
            {
                AutoDuty.Plugin._trustManager.GetLevels(content);
                return false;
            }

            TrustMember?[] members = new TrustMember?[3];

            int index = 0;
            foreach (TrustMember member in content.TrustMembers)
            {
                if (member.Level >= content.ClassJobLevelRequired && members.CanSelectMember(member, AutoDuty.Plugin.Player?.GetRole() ?? CombatRole.NonCombat))
                {
                    members[index++] = member;
                    if (index >= 3)
                        return true;
                }
            }

            return false;
        }


        public static bool CanSelectMember(this TrustMember?[] trustMembers, TrustMember member, CombatRole playerRole) =>
            playerRole != CombatRole.NonCombat &&
            member.Role switch
            {
                TrustRole.DPS => playerRole == CombatRole.DPS && !trustMembers.Where(x => x != null).Any(x => x.Role is TrustRole.DPS) ||
                                 playerRole != CombatRole.DPS && trustMembers.Where(x => x  != null).Count(x => x.Role is TrustRole.DPS) < 2,
                TrustRole.Healer => playerRole != CombatRole.Healer && !trustMembers.Where(x => x != null).Any(x => x.Role is TrustRole.Healer),
                TrustRole.Tank => playerRole   != CombatRole.Tank   && !trustMembers.Where(x => x != null).Any(x => x.Role is TrustRole.Tank),
                TrustRole.Graha => true,
                _ => throw new ArgumentOutOfRangeException()
            };
    }
}
