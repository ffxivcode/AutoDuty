using AutoDuty.External;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Managers
{
    internal class SquadronManager(TaskManager _taskManager)
    {
        internal unsafe void RegisterSquadron(ContentHelper.Content content)
        {
            if (content.GCArmyIndex < 0)
                return;
            _taskManager.Enqueue(() => Svc.Log.Info($"Queueing Squadron: {content.Name}"), "RegisterSquadron");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Step: Queueing Squadron: {content.Name}", "RegisterSquadron");

            AtkUnitBase* addon = null;
            _taskManager.Enqueue(() => { ExecSkipTalk.IsEnabled = true; }, "RegisterSquadron");
            if (!ObjectHelper.IsValid)
            {
                _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterSquadron");
                _taskManager.DelayNext("RegisterSquadron", 2000);
            }
            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("GcArmyCapture"), "RegisterSquadron");
            _taskManager.Enqueue(() => OpenSquadron(addon), "RegisterSquadron");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("GcArmyCapture", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterSquadron");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 11, content.GCArmyIndex), "RegisterSquadron");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 13), "RegisterSquadron");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterSquadron");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterSquadron");
            _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == content.TerritoryType, int.MaxValue, "RegisterSquadron");
            _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterSquadron");
            _taskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, int.MaxValue, "RegisterSquadron");
            _taskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "RegisterSquadron");
            _taskManager.Enqueue(() => { ExecSkipTalk.IsEnabled = false; }, "RegisterSquadron");
            _taskManager.Enqueue(() => AutoDuty.Plugin.StartNavigation(true), "RegisterSquadron");
        }
        internal bool SeenAddon = false;
        internal unsafe bool OpenSquadron(AtkUnitBase* aub)
        {
            GameObject? gameObject;
            if (aub != null)
                return true;

            if ((gameObject = ObjectHelper.GetObjectByPartialName("Squadron Sergeant")) == null || !MovementHelper.Move(gameObject, 0.25f, 6f))
                return false;

            if (SeenAddon && AddonHelper.ClickSelectString(0))
            {
                SeenAddon = false;
                return true;
            }
            
            if (!SeenAddon && ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectString") == null)
                return false;
            else
                SeenAddon = true;

            return false;
        }

    }
}
