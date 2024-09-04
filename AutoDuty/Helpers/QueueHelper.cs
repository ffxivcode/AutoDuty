using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;


namespace AutoDuty.Helpers
{
    internal unsafe static class QueueHelper
    {
        internal static void Invoke(Content? content, DutyMode dutyMode)
        {
            if (State != ActionState.Running && content != null && dutyMode != DutyMode.None)
            {
                _dutyMode = dutyMode;
                _content = content;
                Svc.Log.Info($"Queueing: {dutyMode}: {content.Name}");
                AutoDuty.Plugin.Action = $"Queueing {_dutyMode}: {content.Name}";
                State = ActionState.Running;
                AutoDuty.Plugin.States |= PluginState.Other;
                Svc.Framework.Update += QueueUpdate;
            }
        }

        internal static void Stop()
        {
            Svc.Framework.Update -= QueueUpdate;
            if (State == ActionState.Running)
                Svc.Log.Info($"Done Queueing: {_dutyMode}: {_content?.Name}");
            _content = null;
            State = ActionState.None;
            AutoDuty.Plugin.States &= ~PluginState.Other;
            _allConditionsMetToJoin = false; 
            _dutyMode = DutyMode.None;
        }

        internal static ActionState State = ActionState.None;

        private static Content? _content = null;
        private static DutyMode _dutyMode = DutyMode.None;
        private static AddonContentsFinder* _addonContentsFinder = null;
        private static bool _allConditionsMetToJoin = false;

        private static bool ContentsFinderConfirm()
        {
            if (GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out AtkUnitBase* addonContentsFinderConfirm) && GenericHelpers.IsAddonReady(addonContentsFinderConfirm))
            {
                Svc.Log.Debug("Queue Helper - Confirming DutyPop");
                AddonHelper.FireCallBack(addonContentsFinderConfirm, true, 8);
                return true;
            }
            return false;
        }

        internal static void QueueSupport()
        {
            if (!GenericHelpers.TryGetAddonByName("DawnStory", out AtkUnitBase* addonDawnStory) || !GenericHelpers.IsAddonReady(addonDawnStory) && EzThrottler.Throttle("OpenDawnSupport", 5000))
            {
                Svc.Log.Debug("Queue Helper - Opening Dawn Support");
                AgentModule.Instance()->GetAgentByInternalId(AgentId.DawnStory)->Show();
                return;
            }

            if (addonDawnStory->AtkValues[8].UInt < _content!.ExVersion)
            {
                Svc.Log.Debug($"Queue Helper - You do not have expansion: {_content.ExVersion} unlocked stopping");
                Stop();
                return;
            }

            if (addonDawnStory->AtkValues[343].UInt != _content!.ExVersion)
            {
                Svc.Log.Debug($"Queue Helper - Opening Expansion: {_content.ExVersion}");
                AddonHelper.FireCallBack(addonDawnStory, true, 11, _content.ExVersion);
                return;
            }

            else if (addonDawnStory->AtkValues[21].UInt != _content.DawnIndex + 1)
            {
                Svc.Log.Debug($"Queue Helper - Clicking: {_content.EnglishName} at index: {_content.DawnIndex + addonDawnStory->AtkValues[27].UInt} {addonDawnStory->AtkValues[27].UInt}");
                AddonHelper.FireCallBack(addonDawnStory, true, 12, (uint)_content.DawnIndex + addonDawnStory->AtkValues[27].UInt);
            }
            else
            {
                Svc.Log.Debug($"Queue Helper - Clicking: Register For Duty");
                AddonHelper.FireCallBack(addonDawnStory, true, 14);
            }
        }

        private static void QueueRegular()
        {
            if (ContentsFinder.Instance()->IsUnrestrictedParty != AutoDuty.Plugin.Configuration.Unsynced)
            {
                Svc.Log.Debug("Queue Helper - Setting UnrestrictedParty");
                ContentsFinder.Instance()->IsUnrestrictedParty = AutoDuty.Plugin.Configuration.Unsynced;
                return;
            }

            if (!_allConditionsMetToJoin && (!GenericHelpers.TryGetAddonByName("ContentsFinder", out _addonContentsFinder) || !GenericHelpers.IsAddonReady((AtkUnitBase*)_addonContentsFinder)))
            {
                Svc.Log.Debug($"Queue Helper - Opening ContentsFinder to {_content!.Name}");
                AgentContentsFinder.Instance()->OpenRegularDuty(_content.ContentFinderCondition);
                return;
            }

            if (_addonContentsFinder->DutyList->Items.LongCount == 0)
                return;

            var vectorDutyListItems = _addonContentsFinder->DutyList->Items;
            List<AtkComponentTreeListItem> listAtkComponentTreeListItems = [];
            if (vectorDutyListItems.Count == 0)
                return;
            vectorDutyListItems.ForEach(pointAtkComponentTreeListItem => listAtkComponentTreeListItems.Add(*(pointAtkComponentTreeListItem.Value)));

            if (!_allConditionsMetToJoin && AgentContentsFinder.Instance()->SelectedDutyId != _content!.ContentFinderCondition)
            {
                Svc.Log.Debug($"Queue Helper - Opening ContentsFinder to {_content.Name} because we have the wrong selection of {listAtkComponentTreeListItems[(int)_addonContentsFinder->SelectedRow].Renderer->GetTextNodeById(5)->GetAsAtkTextNode()->NodeText.ToString().Replace("...", "")}");
                AgentContentsFinder.Instance()->OpenRegularDuty(_content.ContentFinderCondition);
                EzThrottler.Throttle("QueueHelper", 500, true);
                return;
            }

            var selectedDutyName = _addonContentsFinder->AtkValues[18].GetValueAsString();
            if (selectedDutyName != _content!.Name && !string.IsNullOrEmpty(selectedDutyName))
            {
                Svc.Log.Debug($"Queue Helper - We have {_addonContentsFinder->SelectedDutyTextNode[0].Value->NodeText.ToString().Replace("...", "")} selected, not {_content.Name}, Clearing");
                AddonHelper.FireCallBack((AtkUnitBase*)_addonContentsFinder, true, 12, 1);
                return;
            }

            if (string.IsNullOrEmpty(selectedDutyName))
            {
                Svc.Log.Debug("Queue Helper - Checking Duty");
                SelectDuty(_addonContentsFinder);
                return;
            }

            if (selectedDutyName == _content.Name)
            {
                _allConditionsMetToJoin = true;
                Svc.Log.Debug("Queue Helper - All Conditions Met, Clicking Join");
                AddonHelper.FireCallBack((AtkUnitBase*)_addonContentsFinder, true, 12, 0);
                return;
            }
        }

        internal static void QueueUpdate(IFramework _)
        {
            if (_content == null || AutoDuty.Plugin.InDungeon || Svc.ClientState.TerritoryType == _content?.TerritoryType)
                Stop();

            if (!EzThrottler.Throttle("QueueHelper", 250)|| !ObjectHelper.IsValid || ContentsFinderConfirm() || Conditions.IsInDutyQueue) return;

            switch (_dutyMode)
            {
                case DutyMode.Regular:
                case DutyMode.Trial:
                case DutyMode.Raid:
                    QueueRegular();
                    break;
                case DutyMode.Support:
                    QueueSupport();
                    break;
            }
        }

        private static uint HeadersCount(uint before, List<AtkComponentTreeListItem> list)
        {
            uint count = 0;
            for (int i = 0; i < before; i++)
            {
                if (list[i].UIntValues[0] == 4 || list[i].UIntValues[0] == 2)
                    count++;
            }
            return count;
        }

        private static void SelectDuty(AddonContentsFinder* addonContentsFinder)
        {
            if (addonContentsFinder == null) return;

            var vectorDutyListItems = addonContentsFinder->DutyList->Items;
            List<AtkComponentTreeListItem> listAtkComponentTreeListItems = [];
            vectorDutyListItems.ForEach(pointAtkComponentTreeListItem => listAtkComponentTreeListItems.Add(*(pointAtkComponentTreeListItem.Value)));

            AddonHelper.FireCallBack((AtkUnitBase*)addonContentsFinder, true, 3, addonContentsFinder->SelectedRow - (HeadersCount(addonContentsFinder->SelectedRow, listAtkComponentTreeListItems) - 1));
        }
    }
}
