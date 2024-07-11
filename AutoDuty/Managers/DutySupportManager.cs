using AutoDuty.Helpers;
using AutoDuty.IPC;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Managers
{
    internal class DutySupportManager(TaskManager _taskManager)
    {
        internal unsafe void RegisterDutySupport(ContentHelper.Content content)
        {
            if (content.DawnIndex < 0)
                return;
            _taskManager.Enqueue(() => Svc.Log.Info($"Queueing Duty Support: {content.Name}"), "RegisterDutySupport");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Step: Queueing Duty Support: {content.Name}", "RegisterDutySupport");
            AtkUnitBase * addon = null;
            int indexModifier = 0;

            if (!ObjectHelper.IsValid)
            {
                _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterDutySupport");
                _taskManager.DelayNext("RegisterDutySupport", 2000);
            }

            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("DawnStory"), "RegisterDutySupport");
            _taskManager.Enqueue(() => { if (addon == null) OpenDawnStory(); }, "RegisterDutySupport");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("DawnStory", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterDutySupport");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 11, 3), "RegisterDutySupport");
            _taskManager.DelayNext("RegisterDutySupport", 50);
            _taskManager.Enqueue(() => indexModifier = DawnStoryCount((nint)addon) - 1, "RegisterDutySupport");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 11, 4), "RegisterDutySupport");
            _taskManager.DelayNext("RegisterDutySupport", 50);
            _taskManager.Enqueue(() => indexModifier += DawnStoryCount((nint)addon) - 1, "RegisterDutySupport");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 11, 5), "RegisterDutySupport");
            _taskManager.DelayNext("RegisterDutySupport", 50);
            _taskManager.Enqueue(() => indexModifier += DawnStoryCount((nint)addon) - 1, "RegisterDutySupport");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 11, content.ExVersion), "RegisterDutySupport");
            _taskManager.DelayNext("RegisterDutySupport", 250);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 12, DawnStoryIndex(content.DawnIndex, content.ExVersion, indexModifier)), "RegisterDutySupport");
            _taskManager.DelayNext("RegisterDutySupport", 250);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 14), "RegisterDutySupport");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterDutySupport");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterDutySupport");
            _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == content.TerritoryType, int.MaxValue, "RegisterDutySupport");
            _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterDutySupport");
            _taskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, int.MaxValue, "RegisterDutySupport");
            _taskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "RegisterDutySupport");
            _taskManager.Enqueue(() => AutoDuty.Plugin.StartNavigation(true), "RegisterDutySupport");
        }

        private unsafe void OpenDawnStory() => AgentModule.Instance()->GetAgentByInternalId(AgentId.DawnStory)->Show();



        private unsafe int DawnStoryCount(nint addon)
        {
            var addonBase = (AtkUnitBase*)addon;
            var atkComponentTreeListDungeons = (AtkComponentTreeList*)addonBase->UldManager.NodeList[7]->GetComponent();
            return (int)atkComponentTreeListDungeons->Items.Size();
        }

        private unsafe int DawnStoryIndex(int index, uint ex, int indexModifier)
        {
            return ex switch
            {
                0 or 1 or 2 => indexModifier + index,
                3 or 4 or 5 => index - 1,
                _ => -1,
            };
        }
    }
}
