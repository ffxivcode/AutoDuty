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
    using System;
    using System.Collections.Generic;
    using Lumina.Excel;
    using Lumina.Excel.GeneratedSheets2;
    using static ContentHelper;

    internal partial class TrustManager(TaskManager _taskManager)
    {
        internal static readonly Dictionary<TrustMemberName, TrustMember> members = [];

        internal static void PopulateTrustMembers()
        {
            ExcelSheet<DawnMemberUIParam>? dawnSheet = Svc.Data.GetExcelSheet<DawnMemberUIParam>();
            members.Add(TrustMemberName.AlisaieBlue, new TrustMember { Index = 0, Name = dawnSheet!.GetRow(1)!.Unknown0.RawString, Role = TrustRole.Healer }); // Blue Alisaie
            members.Add(TrustMemberName.Alisaie, new TrustMember { Index = 1, Name = dawnSheet.GetRow(2)!.Unknown0.RawString, Role = TrustRole.DPS });    // Alisaie
            members.Add(TrustMemberName.Thancred, new TrustMember { Index = 2, Name = dawnSheet.GetRow(3)!.Unknown0.RawString, Role = TrustRole.Tank });   // Thancred
            members.Add(TrustMemberName.Urianger, new TrustMember { Index = 3, Name = dawnSheet.GetRow(5)!.Unknown0.RawString, Role = TrustRole.Healer }); // Urianger
            members.Add(TrustMemberName.BestCatGirl, new TrustMember { Index = 4, Name = dawnSheet.GetRow(6)!.Unknown0.RawString, Role = TrustRole.DPS });    // Best cat girl
            members.Add(TrustMemberName.Ryne, new TrustMember { Index = 5, Name = dawnSheet.GetRow(7)!.Unknown0.RawString, Role = TrustRole.DPS });    //Ryne
            members.Add(TrustMemberName.Estinien, new TrustMember { Index = 5, Name = dawnSheet.GetRow(12)!.Unknown0.RawString, Role = TrustRole.DPS });    //Estinien
            members.Add(TrustMemberName.Graha, new TrustMember { Index = 6, Name = dawnSheet.GetRow(10)!.Unknown0.RawString, Role = TrustRole.Graha });  //Take a guess
            members.Add(TrustMemberName.Zero, new TrustMember { Index = 7, Name = dawnSheet.GetRow(41)!.Unknown0.RawString, Role = TrustRole.DPS });    // Zero.. bit random
            members.Add(TrustMemberName.Krile, new TrustMember { Index = 7, Name = dawnSheet.GetRow(60)!.Unknown0.RawString, Role = TrustRole.DPS });    // Krile
        }


        internal unsafe void RegisterTrust(Content content)
        {
            if (content.DawnIndex < 1)
                return;
            int queueIndex = QueueIndex(content);

            _taskManager.Enqueue(() => Svc.Log.Info($"Queueing Trust: {content.DisplayName}"), "RegisterTrust");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Queueing Trust: {content.DisplayName}", "RegisterTrust");
            AtkUnitBase* addon = null;

            if (!ObjectHelper.IsValid)
            {
                _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterTrust");
                _taskManager.DelayNext("RegisterTrust", 2000);
            }

            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("Dawn"), "RegisterTrust");
            _taskManager.Enqueue(() => { if (addon == null) this.OpenDawn(); }, "RegisterTrust");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("Dawn", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterTrust");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 20, (content.ExVersion)), "RegisterTrust");
            _taskManager.DelayNext("RegisterTrust", 50);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 15, queueIndex), "RegisterTrust");
            _taskManager.Enqueue(this.TurnOffAllMembers);
            _taskManager.Enqueue(this.TurnOnConfigMembers);
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
                if (AutoDuty.Plugin.Configuration.SelectedTrusts.Count(x => x is not null) == 3)
                {
                    foreach (var member in AutoDuty.Plugin.Configuration.SelectedTrusts.OrderBy(x => x.Role))
                        Callback.Fire(addon, true, 12, member.Index);
                }
            }
        }

        public static void ResetTrustIfInvalid()
        {
            if (AutoDuty.Plugin.Configuration.SelectedTrusts.Count(x => x is not null) == 3)
            {
                CombatRole playerRole = Player.Job.GetRole();

                int dps = AutoDuty.Plugin.Configuration.SelectedTrusts.Count(x => x?.Role is TrustRole.DPS);
                int healers = AutoDuty.Plugin.Configuration.SelectedTrusts.Count(x => x?.Role is TrustRole.Healer);
                int tanks = AutoDuty.Plugin.Configuration.SelectedTrusts.Count(x => x?.Role is TrustRole.Tank);

                bool needsReset = playerRole switch
                {
                    CombatRole.DPS => dps == 2,
                    CombatRole.Healer => healers == 1,
                    CombatRole.Tank => tanks == 1,
                    _ => false
                };

                if (needsReset)
                {
                    AutoDuty.Plugin.Configuration.SelectedTrusts = new TrustMember[3];
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

        private bool currentlyGettingLevels;
        internal unsafe void GetLevels(Content? content)
        {
            if (this.currentlyGettingLevels)
                return;

            content ??= AutoDuty.Plugin.CurrentTerritoryContent;
            if (content?.DawnIndex < 1)
                return;

            if (content.TrustMembers.TrueForAll(tm => tm.Level > 0))
                return;

            this.currentlyGettingLevels = true;

            AtkUnitBase* addon = null;
            bool wasOpen = false;

            int queueIndex = QueueIndex(content);

            if (!ObjectHelper.IsValid)
            {
                _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "TrustLevelCheck1");
                _taskManager.DelayNext("TrustLevelCheck2", 2000);
            }

            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("Dawn"), "TrustLevelCheck3");
            _taskManager.Enqueue(() =>
            {
                if (addon == null || !GenericHelpers.IsAddonReady(addon)) this.OpenDawn();
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

                    if (content.TrustMembers[index].Level <= 0)
                    {
                        _taskManager.Enqueue(() => Callback.Fire(addon, true, 16, index));
                        _taskManager.Enqueue(() => content.TrustMembers[index].Level = TrustHelper.GetLevelFromTrustWindow(addon));
                    }
                }
                _taskManager.Enqueue(() =>
                {
                    if (!wasOpen)
                        AgentModule.Instance()->GetAgentByInternalId(AgentId.Dawn)->Hide();
                    else
                        this.currentlyGettingLevels = false;
                }, "TrustLevelCheck10");
                if (!wasOpen)
                {
                    _taskManager.Enqueue(() => !GenericHelpers.IsAddonReady(addon), "TrustLevelCheck11");
                    _taskManager.Enqueue(() => !(this.currentlyGettingLevels = false), "TrustLevelCheck12");
                }
            }, "TrustLevelCheck9");
        }
    }
}