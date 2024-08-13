using System;
using System.Linq;

namespace AutoDuty.Helpers
{
    using ECommons;
    using ECommons.DalamudServices;
    using ECommons.ExcelServices;
    using ECommons.GameFunctions;
    using ECommons.GameHelpers;
    using FFXIVClientStructs.FFXIV.Client.Game.UI;
    using FFXIVClientStructs.FFXIV.Component.GUI;
    using Managers;
    using static ContentHelper;

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


        internal static bool CanTrustRun(this Content content, bool checkTrustLevels = true)
        {
            if (!content.TrustContent)
                return false;
            
            if (!UIState.IsInstanceContentCompleted(content.Id))
                return false;

            return !checkTrustLevels || CanTrustRunMembers(content);
        }

        private static bool CanTrustRunMembers(Content content)
        {
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

        internal static bool SetLevelingTrustMembers(Content content)
        {
            AutoDuty.Plugin.Configuration.SelectedTrustMembers = new TrustMemberName?[3];

            TrustMember?[] trustMembers = new TrustMember?[3];

            Job        playerJob  = (AutoDuty.Plugin.Player?.GetJob() ?? AutoDuty.Plugin.JobLastKnown);
            CombatRole playerRole = playerJob.GetRole();

            ObjectHelper.JobRole playerJobRole = AutoDuty.Plugin.Player.ClassJob.GameData.GetJobRole();

            Svc.Log.Info("Leveling Trust Members set");
            Svc.Log.Info(content.TrustMembers.Count.ToString());

            int index = 0;

            try
            {
                TrustMember[] membersPossible = content.TrustMembers
                                                       .OrderBy(tm => tm.Level + (tm.Level < tm.LevelCap ? 0 : 100) + 
                                                                      (playerRole == CombatRole.DPS ? playerJobRole == tm.Job.GetJobRole() ? 0.5f : 0 : 0)).ToArray();
                foreach (TrustMember member in membersPossible)
                {
                    Svc.Log.Info("checking: " + member.Name);

                    if (trustMembers.CanSelectMember(member, playerRole))
                    {
                        Svc.Log.Info("check successful");
                        trustMembers[index++] = member;

                        if (index >= 3)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }

            if (trustMembers.All(tm => tm != null))
            {
                AutoDuty.Plugin.Configuration.SelectedTrustMembers = trustMembers.Select(tm => tm?.MemberName).ToArray();
                AutoDuty.Plugin.Configuration.Save();
                return true;
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
                TrustRole.AllRounder => true,
                _ => throw new ArgumentOutOfRangeException(member.Name, "member is of invalid role.. somehow")
            };
    }
}
