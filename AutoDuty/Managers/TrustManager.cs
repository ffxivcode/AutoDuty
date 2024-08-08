using AutoDuty.Helpers;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;

namespace AutoDuty.Managers
{
    internal partial class TrustManager(TaskManager _taskManager)
    {
        internal unsafe void RegisterTrust(ContentHelper.Content content)
        {
            if (content.DawnIndex < 1)
                return;
            int indexModifier = 1;
            if (content.DawnIndex >= 17) //Skips Trials mistakenly present in the Trusts list because I (Vera) can't figure out how to parse them out in ContentHelper.cs
                indexModifier++;
            if (content.DawnIndex >= 26)
                indexModifier++;
            if (content.DawnIndex >= 30)
                indexModifier++;
            _taskManager.Enqueue(() => Svc.Log.Info($"Queueing Trust: {content.DisplayName}"), "RegisterTrust");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Queueing Trust: {content.DisplayName}", "RegisterTrust");
            AtkUnitBase* addon = null;

            if (!ObjectHelper.IsValid)
            {
                _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterTrust");
                _taskManager.DelayNext("RegisterTrust", 2000);
            }

            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("Dawn"), "RegisterTrust");
            _taskManager.Enqueue(() => { if (addon == null) OpenDawn(); }, "RegisterTrust");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("Dawn", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterTrust");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 20, (content.ExVersion)), "RegisterTrust");
            _taskManager.DelayNext("RegisterTrust", 50);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 15, content.DawnIndex - indexModifier), "RegisterTrust");
            _taskManager.Enqueue(() => TurnOffAllMembers());
            _taskManager.Enqueue(() => TurnOnConfigMembers());
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 14), "RegisterTrust");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterTrust");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterTrust");
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
                var playerRole = Player.Job.GetRole();
                var dps = AutoDuty.Plugin.Configuration.SelectedTrusts.Count(x => x is not null && x.Role is 0);
                var healers = AutoDuty.Plugin.Configuration.SelectedTrusts.Count(x => x is not null && x.Role is 1);
                var tanks = AutoDuty.Plugin.Configuration.SelectedTrusts.Count(x => x is not null && x.Role is 2);

                bool needsReset = playerRole switch
                {
                   CombatRole.DPS => dps == 2,
                   CombatRole.Healer => healers == 1,
                   CombatRole.Tank => tanks == 1,
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
                    var isEnabled = addon->AtkValues[i + 33].Bool;
                    if (isEnabled)
                        Callback.Fire(addon, true, 12, i);
                }
            }
        }
    }
}
