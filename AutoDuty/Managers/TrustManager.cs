using AutoDuty.Helpers;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Linq;

namespace AutoDuty.Managers
{
    using System.Collections.Generic;
    using Lumina.Excel;
    using Lumina.Excel.GeneratedSheets2;
    using static ContentHelper;
    using static ObjectHelper;

    internal partial class TrustManager(TaskManager _taskManager)
    {
        internal static readonly Dictionary<TrustMemberName, TrustMember> members = [];

        internal static void PopulateTrustMembers()
        {
            ExcelSheet<DawnMemberUIParam>?                     dawnSheet = Svc.Data.GetExcelSheet<DawnMemberUIParam>();
            ExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>? jobSheet  = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>();

            void AddMember(TrustMemberName name, uint index, TrustRole role, ClassJobType classJob, uint levelInit = 71, uint levelCap = 100) =>
                members.Add(name,
                            new TrustMember
                            {
                                Index    = index, 
                                Name = dawnSheet!.GetRow((uint)name)!.Unknown0.RawString, 
                                Role = role, 
                                Job = jobSheet.GetRow((uint)classJob), 
                                MemberName = name,
                                LevelInit = levelInit,
                                Level = levelInit,
                                LevelCap = levelCap
                            });

            AddMember(TrustMemberName.Alphinaud, 0, TrustRole.Healer,     ClassJobType.Sage);
            AddMember(TrustMemberName.Alisaie,   1, TrustRole.DPS,        ClassJobType.RedMage);
            AddMember(TrustMemberName.Thancred,  2, TrustRole.Tank,       ClassJobType.Gunbreaker);
            AddMember(TrustMemberName.Urianger,  3, TrustRole.Healer,     ClassJobType.Astrologian);
            AddMember(TrustMemberName.Yshtola,   4, TrustRole.DPS,        ClassJobType.BlackMage);
            AddMember(TrustMemberName.Ryne,      5, TrustRole.DPS,        ClassJobType.Rogue,       71, 80);
            AddMember(TrustMemberName.Estinien,  5, TrustRole.DPS,        ClassJobType.Dragoon,     81);
            AddMember(TrustMemberName.Graha,     6, TrustRole.AllRounder, ClassJobType.BlackMage,   81);
            AddMember(TrustMemberName.Zero,      7, TrustRole.DPS,        ClassJobType.Reaper,      90, 90);
            AddMember(TrustMemberName.Krile,     7, TrustRole.DPS,        ClassJobType.Pictomancer, 91);
        }


        internal unsafe void RegisterTrust(Content content)
        {
            if (content.TrustIndex < 0)
                return;
            int queueIndex = content.TrustIndex;

            _taskManager.Enqueue(() => Svc.Log.Info($"Queueing Trust: {content.Name}"), "RegisterTrust");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Queueing Trust: {content.Name}", "RegisterTrust");
            AtkUnitBase* addon = null;

            if (!IsValid)
            {
                _taskManager.Enqueue(() => IsValid, int.MaxValue, "RegisterTrust");
                _taskManager.DelayNext("RegisterTrust", 2000);
            }

            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("Dawn"), "RegisterTrust");
            _taskManager.Enqueue(() => { if (addon == null) OpenDawn(); }, "RegisterTrust");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("Dawn", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterTrust");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 20, (content.ExVersion)), "RegisterTrust");
            _taskManager.DelayNext("RegisterTrust", 50);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 15, queueIndex), "RegisterTrust");
            _taskManager.Enqueue(TurnOffAllMembers);
            _taskManager.Enqueue(TurnOnConfigMembers);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 14), "RegisterTrust");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterTrust");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterTrust");
        }

        private static int QueueIndex(ContentHelper.Content content)
        {
            int indexModifier = 1;
            if (content.DawnIndex >= 17) //Skips Trials mistakenly present in the Trusts list because I (Vera) can't figure out how to parse them out in ContentHelper.cs
                indexModifier++;
            if (content.DawnIndex >= 26)
                indexModifier++;
            if (content.DawnIndex >= 30)
                indexModifier++;
            return content.DawnIndex - indexModifier;
        }

        private unsafe void TurnOnConfigMembers()
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("Dawn", out var addon))
            {

                if (AutoDuty.Plugin.Configuration.SelectedTrustMembers.Count(x => x is not null) == 3)
                {
                    foreach (TrustMemberName member in AutoDuty.Plugin.Configuration.SelectedTrustMembers.OrderBy(x => members[(TrustMemberName)x!].Role))
                        Callback.Fire(addon, true, 12, members[member].Index);
                }
            }
        }

        public static void ResetTrustIfInvalid()
        {
            if (AutoDuty.Plugin.Configuration.SelectedTrustMembers.Count(x => x is not null) == 3)
            {
                CombatRole playerRole = Player.Job.GetRole();

                TrustMember[] trustMembers = AutoDuty.Plugin.Configuration.SelectedTrustMembers.Select(name => members[(TrustMemberName)name!]).ToArray();

                int dps     = trustMembers.Count(x => x.Role is TrustRole.DPS);
                int healers = trustMembers.Count(x => x.Role is TrustRole.Healer);
                int tanks   = trustMembers.Count(x => x.Role is TrustRole.Tank);

                bool needsReset = playerRole switch
                {
                    CombatRole.DPS => dps == 2,
                    CombatRole.Healer => healers == 1,
                    CombatRole.Tank => tanks == 1,
                    _ => false
                } || trustMembers.Any(tm => tm.Level < AutoDuty.Plugin.CurrentTerritoryContent?.ClassJobLevelRequired);

                if (needsReset)
                {
                    AutoDuty.Plugin.Configuration.SelectedTrustMembers = new TrustMemberName?[3];
                    AutoDuty.Plugin.Configuration.Save();
                }
            }
        }

        private unsafe void OpenDawn() => AgentModule.Instance()->GetAgentByInternalId(AgentId.Dawn)->Show();

        private unsafe void TurnOffAllMembers()
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("Dawn", out var addon))
            {
                for (int i = 0; i <= 7; i++)
                {
                    bool isEnabled = addon->AtkValues[i + 33].Bool;
                    if (isEnabled)
                        Callback.Fire(addon, true, 12, i);
                }
            }
        }

        internal static void ClearCachedLevels()
        {
            foreach ((TrustMemberName _, TrustMember? member) in members) 
                member.ResetLevel();
        }

        internal static void ClearCachedLevels(Content content)
        {
            foreach (TrustMember member in content.TrustMembers)
                member.ResetLevel();
        }

        internal bool GetLevelsCheck() => 
            !currentlyGettingLevels;

        private bool currentlyGettingLevels;

        internal unsafe void GetLevels(Content? content)
        {
            if (currentlyGettingLevels)
                return;

            content ??= AutoDuty.Plugin.CurrentTerritoryContent;
            if (content?.DawnIndex < 1)
                return;

            if (content.TrustMembers.TrueForAll(tm => tm.LevelIsSet))
                return;
            
            if (!content.CanTrustRun(false))
                return;
            

            currentlyGettingLevels = true;

            AtkUnitBase* addon = null;
            bool wasOpen = false;

            int queueIndex = content.TrustIndex;

            if (!IsValid)
            {
                _taskManager.Enqueue(() => IsValid, int.MaxValue, "TrustLevelCheck1");
                _taskManager.DelayNext("TrustLevelCheck2", 2000);
            }

            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("Dawn"), "TrustLevelCheck3");
            _taskManager.Enqueue(() =>
            {
                if (addon == null || !GenericHelpers.IsAddonReady(addon)) OpenDawn();
                else wasOpen = true;
            }, "TrustLevelCheck4");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("Dawn", out addon) && GenericHelpers.IsAddonReady(addon), "TrustLevelCheck5");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 20, content.ExVersion), "TrustLevelCheck6");
            _taskManager.DelayNext("TrustLevelCheck7", 50);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 15, queueIndex), "TrustLevelCheck8");
            _taskManager.Enqueue(() =>
            {
                for (int id = 0; id < content.TrustMembers.Count; id++)
                {
                    int index = id;

                    if (!content.TrustMembers[index].LevelIsSet)
                    {
                        _taskManager.EnqueueImmediate(() => Callback.Fire(addon, true, 16, index));
                        _taskManager.EnqueueImmediate(() => content.TrustMembers[index].SetLevel(TrustHelper.GetLevelFromTrustWindow(addon)));
                    }
                }
                _taskManager.EnqueueImmediate(() =>
                {
                    if (!wasOpen)
                        AgentModule.Instance()->GetAgentByInternalId(AgentId.Dawn)->Hide();
                    else
                        currentlyGettingLevels = false;
                }, "TrustLevelCheck10");
                if (!wasOpen)
                {
                    _taskManager.EnqueueImmediate(() => !GenericHelpers.IsAddonReady(addon), "TrustLevelCheck11");
                    _taskManager.EnqueueImmediate(() => !(currentlyGettingLevels = false), "TrustLevelCheck12");
                }
            }, "TrustLevelCheck9");
        }
    }
}