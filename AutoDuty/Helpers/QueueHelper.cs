using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using static AutoDuty.Helpers.ContentHelper;

namespace AutoDuty.Helpers
{
    internal unsafe static class QueueHelper
    {
        internal static void Invoke(Content? content)
        {
            if (!QueueRunning && content != null)
            {
                _content = content;
                Svc.Log.Info($"Queueing: {content.Name}");
                QueueRunning = true;
                Svc.Framework.Update += QueueUpdate;
            }
        }

        internal static void Stop()
        {
            if (QueueRunning)
                Svc.Log.Info($"Done Queueing: {_content?.Name}");
            _content = null;
            QueueRunning = false;
            _allConditionsMetToJoin = false;
            Svc.Framework.Update -= QueueUpdate;
        }

        internal static bool QueueRunning = false;

        private static Content? _content = null;
        private static AddonContentsFinder* _addonContentsFinder = null;
        private static bool _allConditionsMetToJoin = false;

        internal static void QueueUpdate(IFramework _)
        {
            if (AutoDuty.Plugin.InDungeon || _content == null || Svc.ClientState.TerritoryType == _content.TerritoryType)
            {
                Stop();
                return;
            }

            if (!EzThrottler.Throttle("QueueHelper", 250))
                return;

            AutoDuty.Plugin.Action = $"Step: Queueing Duty: {_content.Name}";

            if (!ObjectHelper.IsValid)
                return;

            if (GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out AtkUnitBase* addonContentsFinderConfirm) && GenericHelpers.IsAddonReady(addonContentsFinderConfirm))
            {
                Svc.Log.Debug("Queue Helper - Confirming DutyPop");
                AddonHelper.FireCallBack(addonContentsFinderConfirm, true, 8);
                return;
            }

            if (ContentsFinder.Instance()->IsUnrestrictedParty != AutoDuty.Plugin.Configuration.Unsynced)
            {
                Svc.Log.Debug("Queue Helper - Setting UnrestrictedParty");
                ContentsFinder.Instance()->IsUnrestrictedParty = AutoDuty.Plugin.Configuration.Unsynced;
                return;
            }

            if (!_allConditionsMetToJoin && (!GenericHelpers.TryGetAddonByName("ContentsFinder", out _addonContentsFinder) || !GenericHelpers.IsAddonReady((AtkUnitBase*)_addonContentsFinder)))
            {
                Svc.Log.Debug($"Queue Helper - Opening ContentsFinder to {_content.Name}");
                AgentContentsFinder.Instance()->OpenRegularDuty(_content.ContentFinderCondition);
                return;
            }

            if (_addonContentsFinder->DutyList->Items.LongCount == 0)
                return;

            var vectorDutyListItems = _addonContentsFinder->DutyList->Items;
            List<AtkComponentTreeListItem> listAtkComponentTreeListItems = [];
            vectorDutyListItems.ForEach(pointAtkComponentTreeListItem => listAtkComponentTreeListItems.Add(*(pointAtkComponentTreeListItem.Value)));

            if (!_allConditionsMetToJoin && (_addonContentsFinder->SelectedRow == 0 || !_content.Name!.Contains(listAtkComponentTreeListItems[(int)_addonContentsFinder->SelectedRow].Renderer->GetTextNodeById(5)->GetAsAtkTextNode()->NodeText.ToString().Replace("...", ""), System.StringComparison.InvariantCultureIgnoreCase)))
            {
                Svc.Log.Debug($"Queue Helper - Opening ContentsFinder to {_content.Name} because we have the wrong selection");
                AgentContentsFinder.Instance()->OpenRegularDuty(_content.ContentFinderCondition);
                return;
            }

            if ((!_addonContentsFinder->NumberSelectedTextNode->NodeText.ToString().Equals("1/1 Selected") && !_addonContentsFinder->NumberSelectedTextNode->NodeText.ToString().Equals("0/5 Selected")) || (_addonContentsFinder->NumberSelectedTextNode->NodeText.ToString().Equals("1/1 Selected") && !_content.Name!.Contains(_addonContentsFinder->SelectedDutyTextNode[0].Value->NodeText.ToString().Replace("...", ""), System.StringComparison.InvariantCultureIgnoreCase)))
            {
                Svc.Log.Debug($"Queue Helper - We have duties that are not {_content.Name} Selected, Clearing");
                AddonHelper.FireCallBack((AtkUnitBase*)_addonContentsFinder, true, 12, 1);
                return;
            }

            if (_content.Name!.Contains(listAtkComponentTreeListItems[(int)_addonContentsFinder->SelectedRow].Renderer->GetTextNodeById(5)->GetAsAtkTextNode()->NodeText.ToString().Replace("...", ""), System.StringComparison.InvariantCultureIgnoreCase) && _addonContentsFinder->NumberSelectedTextNode->NodeText.ToString().Equals("0/5 Selected"))
            {
                Svc.Log.Debug("Queue Helper - Checking Duty");
                SelectDuty(_addonContentsFinder);
                return;
            }

            if (_content.Name!.Contains(listAtkComponentTreeListItems[(int)_addonContentsFinder->SelectedRow].Renderer->GetTextNodeById(5)->GetAsAtkTextNode()->NodeText.ToString().Replace("...", ""), System.StringComparison.InvariantCultureIgnoreCase) && _addonContentsFinder->NumberSelectedTextNode->NodeText.ToString().Equals("1/1 Selected") && _content.Name.Contains(_addonContentsFinder->SelectedDutyTextNode[0].Value->NodeText.ToString().Replace("...", ""), System.StringComparison.InvariantCultureIgnoreCase))
            {
                _allConditionsMetToJoin = true;
                Svc.Log.Debug("Queue Helper - All Conditions Met, Clicking Join");
                AddonHelper.FireCallBack((AtkUnitBase*)_addonContentsFinder, true, 12, 0);
                return;
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
