using AutoDuty.Helpers;
using AutoDuty.IPC;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Managers
{
    internal class TrustManager(TaskManager _taskManager)
    {
        internal unsafe void RegisterTrust(ContentHelper.Content content)
        {
            if (content.DawnIndex < 1)
                return;
            _taskManager.Enqueue(() => Svc.Log.Info($"Queueing Trust: {content.Name}"), "RegisterTrust");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Step: Queueing Trust: {content.Name}", "RegisterTrust");
            AtkUnitBase* addon = null;

            if (!ObjectHelper.IsValid)
            {
                _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterTrust");
                _taskManager.DelayNext("RegisterTrust", 2000);
            }

            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("Dawn"), "RegisterTrust");
            _taskManager.Enqueue(() => { if (addon == null) OpenDawn(); }, "RegisterTrust");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("Dawn", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterTrust");
            //_taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 11, (content.ExVersion - 4)), "RegisterTrust");
            _taskManager.DelayNext("RegisterTrust", 500);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 15, content.DawnIndex - 1), "RegisterTrust");
            _taskManager.DelayNext("RegisterTrust", 500);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 14), "RegisterTrust");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterTrust");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterTrust");
            _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == content.TerritoryType, int.MaxValue, "RegisterTrust");
            _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterTrust");
            _taskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, int.MaxValue, "RegisterTrust");
            _taskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "RegisterTrust");
            _taskManager.Enqueue(() => AutoDuty.Plugin.StartNavigation(true), "RegisterTrust");
        }

        private unsafe void OpenDawn() => AgentModule.Instance()->GetAgentByInternalID(340)->Show();

    }
}
