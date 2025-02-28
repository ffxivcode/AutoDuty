using System;
using System.Linq;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace AutoDuty.Helpers
{
    using static Data.Classes;
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
            if (!content.DutyModes.HasFlag(DutyMode.Trust))
                return false;

            if (!UIState.IsInstanceContentCompleted(content.Id) && !QuestManager.IsQuestComplete(content.UnlockQuest))
                return false;

            return !checkTrustLevels || CanTrustRunMembers(content);
        }

        private static bool CanTrustRunMembers(Content content)
        {
            if (content.TrustMembers.Any(tm => tm is { LevelIsSet: false, Available: true }))
                GetLevels(content);

            TrustMember?[] members = new TrustMember?[3];

            int index = 0;
            foreach (TrustMember member in content.TrustMembers)
            {
                if (member.Level >= content.ClassJobLevelRequired && members.CanSelectMember(member, (Player.Available ? Player.Object.GetRole() : CombatRole.NonCombat)))
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
            Job        playerJob  = PlayerHelper.GetJob();
            CombatRole playerRole = playerJob.GetCombatRole();

            if (!Members.Any(tm => tm.Value.Level < tm.Value.LevelCap) && Plugin.Configuration.SelectedTrustMembers.Any(tmn => tmn.HasValue))
            {
                bool test = true;

                for (int i = 0; i < 3 && test; i++)
                {
                    TrustMember?[] curMembers = Plugin.Configuration.SelectedTrustMembers.Select(tmn => Members[tmn!.Value]).ToArray();
                    TrustMember    testMember = curMembers[i]!;
                    curMembers[i] = null;
                    test &= curMembers.CanSelectMember(testMember, playerRole);
                }

                if (test)
                {
                    Svc.Log.Info("Leveling Trust Members retained from previous selection");
                    return true;
                }
            }

            Plugin.Configuration.SelectedTrustMembers = new TrustMemberName?[3];

            TrustMember?[] trustMembers = new TrustMember?[3];

            JobRole playerJobRole = Player.Available ? Player.Object.ClassJob.ValueNullable?.GetJobRole() ?? JobRole.None : JobRole.None;

            Svc.Log.Info("Leveling Trust Members set");
            Svc.Log.Info(content.TrustMembers.Count.ToString());

            int index = 0;

            try
            {
                TrustMember[] membersPossible = [.. content.TrustMembers
                              .OrderBy(tm => tm.Level + (tm.Level < tm.LevelCap ? 0 : 100) +
                                                                      (playerRole == CombatRole.DPS ? playerJobRole == tm.Job?.GetJobRole() ? 0.5f : 0 : 0))];
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
                Plugin.Configuration.SelectedTrustMembers = trustMembers.Select(tm => tm?.MemberName).ToArray();
                Plugin.Configuration.Save();
                return true;
            }

            return false;
        }


        public static bool CanSelectMember(this TrustMember?[] trustMembers, TrustMember member, CombatRole playerRole) =>
            playerRole != CombatRole.NonCombat &&
            member.Available &&
            member.Role switch
            {
                TrustRole.DPS => playerRole == CombatRole.DPS && !trustMembers.Where(x => x != null).Any(x => x.Role is TrustRole.DPS) ||
                                 playerRole != CombatRole.DPS && trustMembers.Where(x => x  != null).Count(x => x.Role is TrustRole.DPS) < 2,
                TrustRole.Healer => playerRole != CombatRole.Healer && !trustMembers.Where(x => x != null).Any(x => x.Role is TrustRole.Healer),
                TrustRole.Tank => playerRole   != CombatRole.Tank   && !trustMembers.Where(x => x != null).Any(x => x.Role is TrustRole.Tank),
                TrustRole.AllRounder => true,
                _ => throw new ArgumentOutOfRangeException(member.Name, "member is of invalid role.. somehow")
            };
        internal static readonly Dictionary<TrustMemberName, TrustMember> Members = [];

        internal static void PopulateTrustMembers()
        {
            var dawnSheet = Svc.Data.GetExcelSheet<DawnMemberUIParam>();
            var jobSheet = Svc.Data.GetExcelSheet<ClassJob>();

            if (dawnSheet == null || jobSheet == null) return;

            void AddMember(TrustMemberName name, uint index, TrustRole role, ClassJobType classJob, uint levelInit = 71, uint levelCap = 100, uint unlockQuest = 0) => Members.Add(name, new TrustMember
                         {
                             Index       = index,
                             Name        = dawnSheet.GetRow((uint)name)!.Unknown0.ToString(),
                             Role        = role,
                             Job         = jobSheet.GetRow((uint)classJob)!,
                             MemberName  = name,
                             LevelInit   = levelInit,
                             Level       = levelInit,
                             LevelCap    = levelCap,
                             LevelIsSet  = levelInit == levelCap,
                             UnlockQuest = unlockQuest
                         });

            AddMember(TrustMemberName.Alphinaud, 0, TrustRole.Healer,     ClassJobType.Sage);
            AddMember(TrustMemberName.Alisaie,   1, TrustRole.DPS,        ClassJobType.RedMage);
            AddMember(TrustMemberName.Thancred,  2, TrustRole.Tank,       ClassJobType.Gunbreaker);
            AddMember(TrustMemberName.Urianger,  3, TrustRole.Healer,     ClassJobType.Astrologian);
            AddMember(TrustMemberName.Yshtola,   4, TrustRole.DPS,        ClassJobType.Black_Mage);
            AddMember(TrustMemberName.Ryne,      5, TrustRole.DPS,        ClassJobType.Rogue,       71, 80);
            AddMember(TrustMemberName.Estinien,  5, TrustRole.DPS,        ClassJobType.Dragoon,     81);
            AddMember(TrustMemberName.Graha,     6, TrustRole.AllRounder, ClassJobType.Black_Mage,  81, unlockQuest: 69318);
            AddMember(TrustMemberName.Zero,      7, TrustRole.DPS,        ClassJobType.Reaper,      90, 90);
            AddMember(TrustMemberName.Krile,     7, TrustRole.DPS,        ClassJobType.Pictomancer, 91);
        }

        public static void ResetTrustIfInvalid()
        {
            if (!PlayerHelper.IsValid) return;

            if (Plugin.Configuration.SelectedTrustMembers.Count(x => x is not null) == 3)
            {
                CombatRole playerRole = Player.Job.GetCombatRole();

                TrustMember[] trustMembers = Plugin.Configuration.SelectedTrustMembers.Select(name => Members[(TrustMemberName)name!]).ToArray();

                int dps = trustMembers.Count(x => x.Role is TrustRole.DPS);
                int healers = trustMembers.Count(x => x.Role is TrustRole.Healer);
                int tanks = trustMembers.Count(x => x.Role is TrustRole.Tank);

                bool needsReset = playerRole switch
                {
                    CombatRole.DPS => dps == 2,
                    CombatRole.Healer => healers == 1,
                    CombatRole.Tank => tanks == 1,
                    _ => false
                } || trustMembers.Any(tm => tm.Level < Plugin.CurrentTerritoryContent?.ClassJobLevelRequired);

                if (needsReset)
                {
                    Plugin.Configuration.SelectedTrustMembers = new TrustMemberName?[3];
                    Plugin.Configuration.Save();
                }
            }
        }

        internal static void ClearCachedLevels() => Members.Each(x => x.Value.ResetLevel());

        internal static void ClearCachedLevels(Content content) => content?.TrustMembers.Each(x => { if (x.Level < x.LevelCap) x.ResetLevel(); });

    internal static void GetLevels(Content? content)
        {
            if (State == ActionState.Running) return;
                    
            _getLevelsContent = content;

            _getLevelsContent ??= Plugin.CurrentTerritoryContent;

            if (_getLevelsContent == null)
            {
                Svc.Log.Debug($"Get Trust Levels: our content was null, returning");
                return;
            }
            if (_getLevelsContent.DawnIndex < 0)
            {
                Svc.Log.Debug($"Get Trust Levels: our content was not dawn content, returning");
                return;
            }
            if (_getLevelsContent.TrustMembers.TrueForAll(tm => tm.LevelIsSet || !tm.Available))
            {
                Svc.Log.Debug($"Get Trust Levels: we already have all our trust levels, returning");
                return;
            }

            if (!_getLevelsContent.CanTrustRun(false))
            {
                Svc.Log.Debug($"Get Trust Levels: this content CanTrustRun is false, returning");
                return;
            }
            Svc.Log.Info($"TrustHelper - Getting trust levels for expansion {_getLevelsContent.ExVersion} from {_getLevelsContent.EnglishName}");
            Svc.Log.Info("Get Trust Levels: Level not set for: " + string.Join(" | ", _getLevelsContent.TrustMembers.Where(tm => tm is { LevelIsSet: false, Available: true }).Select(tm => tm.Name)));

            State = ActionState.Running;
            Svc.Framework.Update += GetLevelsUpdate;
            SchedulerHelper.ScheduleAction("CheckTrustLevelTimeout", () => Stop(), 2500);
        }

        private static unsafe void Stop(bool forceHide = false)
        {
            if (forceHide || (_getLevelsContent?.TrustMembers.TrueForAll(tm => tm.LevelIsSet || !tm.Available) ?? false))
                AgentModule.Instance()->GetAgentByInternalId(AgentId.Dawn)->Hide();
            Svc.Framework.Update -= GetLevelsUpdate;
            State = ActionState.None; 
            Svc.Log.Info($"TrustHelper - Done getting trust levels for expansion {_getLevelsContent?.ExVersion}");
            _getLevelsContent = null;
            SchedulerHelper.DescheduleAction("CheckTrustLevelTimeout");
        }

        internal static ActionState State = ActionState.None;

        private static Content? _getLevelsContent = null;
        internal static unsafe void GetLevelsUpdate(IFramework framework)
        {
            if (_getLevelsContent == null || Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("GetLevelsUpdate", 5) || !PlayerHelper.IsValid) return;

            if (!GenericHelpers.TryGetAddonByName("Dawn", out AtkUnitBase* addonDawn) || !GenericHelpers.IsAddonReady(addonDawn))
            {
                if (EzThrottler.Throttle("OpenDawn", 5000))
                {
                    Svc.Log.Debug("TrustHelper - Opening Dawn");
                    AgentModule.Instance()->GetAgentByInternalId(AgentId.Dawn)->Show();
                }
                return;
            }
            else
                EzThrottler.Throttle("OpenDawn", 5, true);

            if (addonDawn->AtkValues[241].UInt < (_getLevelsContent!.ExVersion - 2))
            {
                Svc.Log.Debug($"TrustHelper - You do not have expansion: {_getLevelsContent.ExVersion} unlocked stopping");
                Stop(true);
                return;
            }

            if (addonDawn->AtkValues[242].UInt != (_getLevelsContent!.ExVersion - 3))
            {
                Svc.Log.Debug($"TrustHelper - Opening Expansion: {_getLevelsContent.ExVersion}");
                AddonHelper.FireCallBack(addonDawn, true, 20, (_getLevelsContent!.ExVersion - 3));
            }
            else if (addonDawn->AtkValues[151].UInt != _getLevelsContent.DawnIndex)
            {
                Svc.Log.Debug($"TrustHelper - Clicking: {_getLevelsContent.EnglishName} at index: {_getLevelsContent.DawnIndex}");
                AddonHelper.FireCallBack(addonDawn, true, 15, _getLevelsContent.DawnIndex);
            }
            else
            {
                for (int id = 0; id < _getLevelsContent.TrustMembers.Count; id++)
                {
                    TrustMember trustMember = _getLevelsContent.TrustMembers[id];
                    if (trustMember is { LevelIsSet: false, Available: true })
                    {
                        
                        AddonHelper.FireCallBack(addonDawn, true, 16, id);
                        uint lvl = GetLevelFromTrustWindow(addonDawn);
                        Svc.Log.Debug($"TrustHelper - Setting {trustMember.MemberName} level to {lvl}");
                        trustMember.SetLevel(lvl);
                    }
                }
                Stop();
            }
        }
    }
}
