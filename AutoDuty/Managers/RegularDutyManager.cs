using AutoDuty.Helpers;
using AutoDuty.IPC;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
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
            _taskManager.Enqueue(() => AgentContentsFinder.Instance()->OpenRegularDuty(content.ContentFinderCondition), "RegisterRegularDuty");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinder", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterRegularDuty");
            _taskManager.Enqueue(() => AgentContentsFinder.Instance()->SelectedDutyId == content.ContentFinderCondition, "RegisterRegularDuty");
            _taskManager.Enqueue(() => ((AddonContentsFinder*)addon)->DutyList->Items.Size() > 0, "RegisterRegularDuty");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 12, 1), "RegisterRegularDuty");
            _taskManager.Enqueue(() => ContentsFinder.Instance()->IsUnrestrictedParty = AutoDuty.Plugin.Configuration.Unsynced, "RegisterRegularDuty");
            _taskManager.Enqueue(() => FireUntilAddon(content, addon, "ContentsFinderConfirm"));
            _taskManager.DelayNext("RegisterRegularDuty", 50);
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterRegularDuty");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterRegularDuty");
            _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == content.TerritoryType, int.MaxValue, "RegisterRegularDuty");
            _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterRegularDuty");
            _taskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, int.MaxValue, "RegisterRegularDuty");
            _taskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "RegisterRegularDuty");
            _taskManager.Enqueue(() => AutoDuty.Plugin.StartNavigation(true), "RegisterRegularDuty");
        }

        internal unsafe nint FireUntilAddon(Content content, AtkUnitBase* addonContentsFider, string addonName)
        {
            if (!EzThrottler.Throttle("FireUntilAddon", 250))
                return 0;

            if (GenericHelpers.TryGetAddonByName(addonName, out AtkUnitBase* addon) && GenericHelpers.IsAddonReady(addon))
                return (nint)addon;

            AddonHelper.FireCallBack(addonContentsFider, true, 3, ((AddonContentsFinder*)addonContentsFider)->SelectedRow - (1 + (content.ExVersion * 2)));
            AddonHelper.FireCallBack(addonContentsFider, true, 12, 0);

            return 0;
        }
    }
}
