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
using System.Linq;

namespace AutoDuty.Helpers
{
    using System;
    using static Data.Classes;

    internal static unsafe class QueueHelper
    {
        internal static void Invoke(Content? content, DutyMode dutyMode)
        {
            if (State != ActionState.Running && content != null && dutyMode != DutyMode.None)
            {
                _dutyMode = dutyMode;
                _content = content;
                Svc.Log.Info($"Queueing: {dutyMode}: {content.Name}");
                Plugin.Action = $"Queueing {_dutyMode}: {content.Name}";
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;
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
            Plugin.States &= ~PluginState.Other;
            _allConditionsMetToJoin = false;
            _turnedOffTrustMembers = false;
            _turnedOnConfigMembers = false;
            _dutyMode = DutyMode.None;
        }

        internal static ActionState State = ActionState.None;

        private static Content? _content = null;
        private static DutyMode _dutyMode = DutyMode.None;
        private static AddonContentsFinder* _addonContentsFinder = null;
        private static bool _allConditionsMetToJoin = false;
        private static bool _turnedOffTrustMembers = false;
        private static bool _turnedOnConfigMembers = false;

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

        internal static void QueueTrust()
        {
            if (TrustHelper.State == ActionState.Running) return;

            AgentDawn* agentDawn = AgentDawn.Instance();
            if (!agentDawn->IsAddonReady())
            {
                if (!EzThrottler.Throttle("OpenDawn", 5000)) return;

                Svc.Log.Debug("Queue Helper - Opening Dawn");
                RaptureAtkModule.Instance()->OpenDawn(_content.RowId);
                return;
            }

            if (agentDawn->Data->ContentData.ExpansionCount < (_content!.ExVersion - 2))
            {
                Svc.Log.Debug($"Queue Helper - You do not have expansion: {_content.ExVersion} unlocked stopping");
                Stop();
                return;
            }

            if (agentDawn->SelectedDawnContentId != _content.DawnRowId)
            {
                Svc.Log.Debug($"Queue Helper - Clicking: {_content.EnglishName} at index: {_content.TrustIndex}");
                RaptureAtkModule.Instance()->OpenDawn(_content.RowId);
            }
            else if (!_turnedOffTrustMembers)
            {
                if (EzThrottler.Throttle("_turnedOffTrustMembers", 500))
                {
                    agentDawn->Data->PartyData.ClearParty();
                    agentDawn->UpdateAddon();
                    SchedulerHelper.ScheduleAction("_turnedOffTrustMembers", () => _turnedOffTrustMembers = true, 250);
                }
            }
            else if (!_turnedOnConfigMembers)
            {
                if (EzThrottler.Throttle("_turnedOnConfigMembers", 500))
                {
                    AgentDawnInterface.DawnMemberEntry* curMembers = agentDawn->Data->MemberData.GetMembers(agentDawn->Data->MemberData.CurrentMembersIndex);
                    var                                 members    = Plugin.Configuration.SelectedTrustMembers;
                    if (members.Count(x => x is not null) == 3)
                        members.OrderBy(x => TrustHelper.Members[(TrustMemberName)x!].Role)
                               .Each(member =>
                                     {
                                         if (member != null)
                                         {
                                             byte                               index       = TrustHelper.Members[(TrustMemberName)member].Index;
                                             AgentDawnInterface.DawnMemberEntry memberEntry = curMembers[index];

                                             agentDawn->Data->PartyData.AddMember(index, &memberEntry);
                                         }
                                     });
                    agentDawn->UpdateAddon();
                    SchedulerHelper.ScheduleAction("_turnedOnConfigMembers", () => _turnedOnConfigMembers = true, 250);
                }
            }
            else if(EzThrottler.Throttle("ClickRegisterButton", 10000))
            {
                Svc.Log.Debug($"Queue Helper - Clicking: Register For Duty");
                agentDawn->RegisterForDuty();
            }
        }

        internal static void QueueSupport()
        {
            AgentDawnStory* agentDawnStory = AgentDawnStory.Instance();
            if (!agentDawnStory->IsAddonReady())
            {
                if (!EzThrottler.Throttle("OpenDawnStory", 5000)) return;
                
                Svc.Log.Debug("Queue Helper - Opening DawnStory");
                RaptureAtkModule.Instance()->OpenDawnStory(_content.Id);
                return;
            }

            if (agentDawnStory->Data->ContentData.ExpansionCount <= _content!.ExVersion)
            {
                Svc.Log.Debug($"Queue Helper - You do not have expansion: {_content.ExVersion} unlocked. stopping");
                Stop();
                return;
            }

            if (agentDawnStory->Data->ContentData.ContentEntries[agentDawnStory->Data->ContentData.SelectedContentEntry].ContentFinderConditionId != _content.RowId)
            {
                Svc.Log.Debug($"Queue Helper - Clicking: {_content.EnglishName} {_content.RowId}");// instead of {agentDawnStory->Data->ContentData.ContentEntries[agentDawnStory->Data->ContentData.SelectedContentEntry].ContentFinderConditionId}");

                RaptureAtkModule.Instance()->OpenDawnStory(_content.RowId);
            }
            else if(EzThrottler.Throttle("ClickRegisterButton", 10000))
            {
                Svc.Log.Debug($"Queue Helper - Clicking: Register For Duty");
                AgentDawnStory.Instance()->RegisterForDuty();
            }
        }

        private static void QueueRegular()
        {
            if (ContentsFinder.Instance()->IsUnrestrictedParty != Plugin.Configuration.Unsynced)
            {
                Svc.Log.Debug("Queue Helper - Setting UnrestrictedParty");
                ContentsFinder.Instance()->IsUnrestrictedParty = Plugin.Configuration.Unsynced;
                return;
            }

            GenericHelpers.TryGetAddonByName("ContentsFinder", out _addonContentsFinder);
            if (!_allConditionsMetToJoin && (_addonContentsFinder == null || !GenericHelpers.IsAddonReady((AtkUnitBase*)_addonContentsFinder)))
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
                Svc.Log.Debug($"Queue Helper - Opening ContentsFinder to {_content.Name} because we have the wrong selection of {listAtkComponentTreeListItems[(int)_addonContentsFinder->DutyList->SelectedItemIndex].Renderer->GetTextNodeById(5)->GetAsAtkTextNode()->NodeText.ToString().Replace("...", "")}");
                AgentContentsFinder.Instance()->OpenRegularDuty(_content.ContentFinderCondition);
                EzThrottler.Throttle("QueueHelper", 500, true);
                return;
            }

            var selectedDutyName = _addonContentsFinder->AtkValues[18].GetValueAsString().Replace("\u0002\u001a\u0002\u0002\u0003", string.Empty).Replace("\u0002\u001a\u0002\u0001\u0003", string.Empty).Replace("\u0002\u001f\u0001\u0003", "\u2013");
            if (selectedDutyName != _content!.Name && !string.IsNullOrEmpty(selectedDutyName))
            {
                Svc.Log.Debug($"Queue Helper - We have {selectedDutyName} selected, not {_content.Name}, Clearing.");
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
            Svc.Log.Debug("end");
        }

        internal static void QueueUpdate(IFramework _)
        {
            if (_content == null || Plugin.InDungeon || Svc.ClientState.TerritoryType == _content?.TerritoryType)
                Stop();

            if (!EzThrottler.Throttle("QueueHelper", 250)|| !PlayerHelper.IsReadyFull || ContentsFinderConfirm() || Conditions.Instance()->InDutyQueue) return;

            switch (_dutyMode)
            {
                case DutyMode.Regular:
                case DutyMode.Trial:
                case DutyMode.Raid:
                    try
                    {
                        QueueRegular();
                    }
                    catch (Exception ex)
                    {
                        Svc.Log.Error(ex.ToString());
                    }

                    break;
                case DutyMode.Support:
                    QueueSupport();
                    break;
                case DutyMode.Trust:
                    QueueTrust();
                    break;
            }
        }

        private static uint HeadersCount(int before, List<AtkComponentTreeListItem> list)
        {
            uint count = 0;
            try
            {
                for (int i = 0; i < before; i++)
                {
                    if (list[i].UIntValues[0] == 0 || list[i].UIntValues[0] == 1)
                        count++;
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }

            return count;
        }

        private static void SelectDuty(AddonContentsFinder* addonContentsFinder)
        {
            if (addonContentsFinder == null) return;
            
            var vectorDutyListItems = addonContentsFinder->DutyList->Items;
            List<AtkComponentTreeListItem> listAtkComponentTreeListItems = [];
            vectorDutyListItems.ForEach(pointAtkComponentTreeListItem => listAtkComponentTreeListItems.Add(*(pointAtkComponentTreeListItem.Value)));
            AddonHelper.FireCallBack((AtkUnitBase*)addonContentsFinder, true, 3, HeadersCount(addonContentsFinder->DutyList->SelectedItemIndex, listAtkComponentTreeListItems) + 1); // - (HeadersCount(addonContentsFinder->DutyList->SelectedItemIndex, listAtkComponentTreeListItems) + 1));
        }
    }
}
