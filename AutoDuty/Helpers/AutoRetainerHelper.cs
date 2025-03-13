using AutoDuty.IPC;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Linq;

namespace AutoDuty.Helpers
{
    internal static class AutoRetainerHelper
    {
        internal static void Invoke()
        {
            if (!AutoRetainer_IPCSubscriber.IsEnabled || !AutoRetainer_IPCSubscriber.AreAnyRetainersAvailableForCurrentChara())
                return;
            Svc.Log.Debug("AutoRetainerHelper.Invoke");
            if (!AutoRetainer_IPCSubscriber.IsEnabled)
            {
                Svc.Log.Info("AutoRetainer requires a plugin, visit https://puni.sh/plugin/AutoRetainer for more info");
            }
            else if (State != ActionState.Running)
            {
                Svc.Log.Info("AutoRetainer Started");
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;
                SchedulerHelper.ScheduleAction("AutoRetainerTimeOut", Stop, 600000);
                Svc.Framework.Update += AutoRetainerUpdate;
            }
        }

        internal static void Stop()
        {
            Svc.Log.Debug("AutoRetainerHelper.Stop");
            if (State == ActionState.Running)
                Svc.Log.Info("AutoRetainer Finished");
            GotoInnHelper.Stop();
            Plugin.Action = "";
            SchedulerHelper.DescheduleAction("AutoRetainerTimeOut");
            _autoRetainerStarted = false;
            Svc.Framework.Update -= AutoRetainerUpdate;
            Svc.Framework.Update += AutoRetainerStopUpdate;
            if (AutoRetainer_IPCSubscriber.IsBusy())
                AutoRetainer_IPCSubscriber.AbortAllTasks();
        }

        internal static ActionState State = ActionState.None;
        private static bool _autoRetainerStarted = false;
        private static IGameObject? SummoningBellGameObject => Svc.Objects.FirstOrDefault(x => x.DataId == SummoningBellHelper.SummoningBellDataIds((uint)Plugin.Configuration.PreferredSummoningBellEnum));

        internal static unsafe void CloseRetainerWindows()
        {
            if (Svc.Targets.Target != null)
                Svc.Targets.Target = null;
            else if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                addonSelectYesno->Close(true);
            else if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                addonSelectString->Close(true);
            else if (GenericHelpers.TryGetAddonByName("RetainerList", out AtkUnitBase* addonRetainerList))
                addonRetainerList->Close(true);
            else if (GenericHelpers.TryGetAddonByName("RetainerTaskAsk", out AtkUnitBase* addonRetainerSell))
                addonRetainerSell->Close(true);
        }

        internal static unsafe void AutoRetainerStopUpdate(IFramework framework)
        {

            if (!Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                State = ActionState.None;
                Plugin.States &= ~PluginState.Other;
                Svc.Framework.Update -= AutoRetainerStopUpdate;
            }
            else
                CloseRetainerWindows();
        }

        internal static unsafe void AutoRetainerUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating))
            {
                Svc.Log.Debug("AutoDuty is Started, Stopping AutoRetainerHelper");
                Stop();
            }

            if (!_autoRetainerStarted && AutoRetainer_IPCSubscriber.IsBusy())
            {
                Svc.Log.Info("AutoRetainer has Started");
                _autoRetainerStarted = true;
                return;
            }
            else if (_autoRetainerStarted && !AutoRetainer_IPCSubscriber.IsBusy())
            {
                Svc.Log.Debug("AutoRetainer is Complete");
                Stop();
                return;
            }

            if (!EzThrottler.Throttle("AM", 250))
                return;

            if (!PlayerHelper.IsValid) return;

            if (GotoHelper.State == ActionState.Running)
            {
                Svc.Log.Debug("Goto Running");
                return;
            }
            Plugin.Action = "AutoRetainer Running";

            if (SummoningBellGameObject != null && !SummoningBellHelper.HousingZones.Contains(Player.Territory) && ObjectHelper.GetDistanceToPlayer(SummoningBellGameObject) > 4)
            {
                Svc.Log.Debug("Moving Closer to Summoning Bell");
                MovementHelper.Move(SummoningBellGameObject, 0.25f, 4);
            }
            else if ((SummoningBellGameObject == null || SummoningBellHelper.HousingZones.Contains(Player.Territory)) && GotoHelper.State != ActionState.Running)
            {
                Svc.Log.Debug("Moving to Summoning Bell Location");
                SummoningBellHelper.Invoke(Plugin.Configuration.PreferredSummoningBellEnum);
            }
            else if (SummoningBellGameObject != null && ObjectHelper.GetDistanceToPlayer(SummoningBellGameObject) <= 4 && !_autoRetainerStarted && !GenericHelpers.TryGetAddonByName("RetainerList", out AtkUnitBase* addonRetainerList) && (ObjectHelper.InteractWithObjectUntilAddon(SummoningBellGameObject, "RetainerList") == null))
            {
                if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
                {
                    if (VNavmesh_IPCSubscriber.Path_IsRunning())
                        VNavmesh_IPCSubscriber.Path_Stop();
                    Svc.Log.Debug("Waiting for AutoRetainer to Start");
                    new ECommons.Automation.Chat().ExecuteCommand("/autoretainer e");
                }
                else
                    Svc.Log.Debug("Interacting with SummoningBell");
                
            }
        }
    }
}
