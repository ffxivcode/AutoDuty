using AutoDuty.Helpers;
using ECommons;
using ECommons.Automation.UIInput;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static AutoDuty.Helpers.ContentHelper;

namespace AutoDuty.Managers
{
    internal class RegularDutyManager(TaskManager _taskManager)
    {
        internal unsafe void RegisterRegularDuty(Content content)
        {
            _taskManager.Enqueue(() => Svc.Log.Info($"Queueing Duty: {content.Name}"), "RegisterRegularDuty");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Step: Queueing Duty: {content.Name}", "RegisterRegularDuty");
            AtkUnitBase* addon = null;

            if (!ObjectHelper.IsValid)
            {
                _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterRegularDuty");
                _taskManager.DelayNext("RegisterRegularDuty", 2000);
            }
            _taskManager.Enqueue(() => ContentsFinder.Instance()->IsUnrestrictedParty = AutoDuty.Plugin.Configuration.Unsynced, "RegisterRegularDuty");
            _taskManager.DelayNext("RegisterRegularDuty", 50);
            _taskManager.Enqueue(() => AgentContentsFinder.Instance()->OpenRegularDuty(content.ContentFinderCondition), "RegisterRegularDuty");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinder", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterRegularDuty");
            _taskManager.Enqueue(() => ((AddonContentsFinder*)addon)->DutyList->Items.LongCount > 0, "RegisterRegularDuty");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 12, 1), "RegisterRegularDuty");
            _taskManager.DelayNext("RegisterRegularDuty", 50);
            _taskManager.Enqueue(() => SelectDuty(content, (AddonContentsFinder*)addon), "RegisterRegularDuty");
            _taskManager.DelayNext("RegisterRegularDuty", 50);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 12, 0), "RegisterRegularDuty");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterRegularDuty");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterRegularDuty");
            _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == content.TerritoryType, int.MaxValue, "RegisterRegularDuty");
        }

        private unsafe bool SelectDuty(Content content, AddonContentsFinder* addon)
        {
            if (GenericHelpers.IsAddonReady(&addon->AtkUnitBase))
            {
                var list = addon->AtkUnitBase.GetNodeById(52)->GetAsAtkComponentList();
                for (var i = 3; i < 19; i++)
                {
                    var componentNode = list->UldManager.NodeList[i]->GetComponent();
                    if (componentNode is null) continue;
                    var textNode = componentNode->GetTextNodeById(5)->GetAsAtkTextNode();
                    var buttonNode = componentNode->UldManager.NodeList[16]->GetAsAtkComponentCheckBox();
                    if (textNode is null || buttonNode is null) continue;
                    if (textNode->NodeText.ToString() == content.Name)
                        buttonNode->ClickCheckboxButton(componentNode, 0);
                }
                return true;
            }
            return false;
        }
    }
}
