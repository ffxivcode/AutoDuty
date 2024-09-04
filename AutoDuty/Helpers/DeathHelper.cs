using AutoDuty.IPC;
using AutoDuty.Windows;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.ComponentModel.Design;
using System.Linq;

namespace AutoDuty.Helpers
{
    internal static class DeathHelper
    {
        private static PlayerState _deathState = PlayerState.Alive;
        internal static PlayerState DeathState
        {
            get
            {
                if (!AutoDuty.Plugin.Configuration.DutyModeEnum.EqualsAny(DutyMode.Regular, DutyMode.Trial, DutyMode.Raid) || AutoDuty.Plugin.Configuration.Unsynced)
                    return _deathState;
                else if (Player.Object.CurrentHp == 0 && _deathState != PlayerState.Dead)
                {
                    OnDeath();
                    return _deathState = PlayerState.Dead;
                }
                else if (Player.Object.CurrentHp > 0 && _deathState != PlayerState.Revived)
                {
                    Svc.Framework.Update += OnRevive;
                    return _deathState = PlayerState.Revived;
                }
                else
                    return _deathState;
            }
        }

        private static unsafe void OnDeath()
        {
            if (AutoDuty.Plugin.Configuration.DutyModeEnum.EqualsAny(DutyMode.Regular, DutyMode.Trial, DutyMode.Raid) && !AutoDuty.Plugin.Configuration.Unsynced)
                return;

            AutoDuty.Plugin.StopForCombat = true;
            AutoDuty.Plugin.SkipTreasureCoffer = true;

            if (VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.Path_Stop();

            if (AutoDuty.Plugin.TaskManager.IsBusy)
                AutoDuty.Plugin.TaskManager.Abort();
           
            if (AutoDuty.Plugin.Configuration.DutyModeEnum.EqualsAny(DutyMode.Regular, DutyMode.Trial, DutyMode.Raid))
            {
                if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno))
                    AddonHelper.ClickSelectYesno();
                else
                    SchedulerHelper.ScheduleAction("OnDeath", () => OnDeath(), 500);
            }
        }

        private static unsafe void OnRevive(IFramework _)
        {
            if (AutoDuty.Plugin.Configuration.DutyModeEnum.EqualsAny(DutyMode.Regular, DutyMode.Trial, DutyMode.Raid) && !AutoDuty.Plugin.Configuration.Unsynced)
            {
                Svc.Framework.Update -= OnRevive;
            }

            TaskManager.DelayNext(5000);
            TaskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting);
            TaskManager.Enqueue(() => BossMod_IPCSubscriber.Presets_ClearActive());
            IGameObject? gameObject = ObjectHelper.GetObjectByDataId(2000700);
            if (gameObject == null || !gameObject.IsTargetable)
            {
                TaskManager.Enqueue(() => { Stage = Stage.Reading_Path; });
                return;
            }

            var oldindex = Indexer;
            Indexer = FindWaypoint();
            TaskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2));
            TaskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting);
            TaskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno"), int.MaxValue);
            TaskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), int.MaxValue);
            TaskManager.Enqueue(() => !ObjectHelper.IsValid, 500);
            TaskManager.Enqueue(() => ObjectHelper.IsValid);
            TaskManager.Enqueue(() => { if (Indexer == 0) Indexer = FindWaypoint(); });
            TaskManager.Enqueue(() => Stage = Stage.Reading_Path);
        }

        internal static void Invoke() 
        {
            Svc.Log.Debug("AMHelper.Invoke");
            if (!AM_IPCSubscriber.IsEnabled)
            {
                Svc.Log.Info("AM requires a plugin, visit https://discord.gg/JzSxThjKnd for more info");
                Svc.Log.Info("DO NOT ask in Puni.sh discord about this option");
            }
            else if (State != ActionState.Running)
            {
                Svc.Log.Info("AM Started");
                State = ActionState.Running;
                AutoDuty.Plugin.States |= PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(false);
                SchedulerHelper.ScheduleAction("AMTimeOut", Stop, 600000);
                Svc.Framework.Update += AMUpdate;
            }
        }

        

        internal static void Stop() 
        {
            Svc.Log.Debug("AMHelper.Stop");
            if (State == ActionState.Running)
                Svc.Log.Info("AM Finished");
            GotoInnHelper.Stop();
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("AMTimeOut");
            _aMStarted = false;
            if (AM_IPCSubscriber.IsRunning())
                AM_IPCSubscriber.Stop();
            Svc.Framework.Update += AMStopUpdate;
            Svc.Framework.Update -= AMUpdate;
        }

        internal static ActionState State = ActionState.None;

        private static bool _aMStarted = false;
        private static IGameObject? SummoningBellGameObject => Svc.Objects.FirstOrDefault(x => x.DataId == SummoningBellHelper.SummoningBellDataIds((uint)AutoDuty.Plugin.Configuration.PreferredSummoningBellEnum));

        internal static unsafe void AMStopUpdate(IFramework framework)
        {
            if (!Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                State = ActionState.None;
                AutoDuty.Plugin.States &= ~PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(true);
                Svc.Framework.Update -= AMStopUpdate;
            }
            else if (Svc.Targets.Target != null)
                Svc.Targets.Target = null;
            else if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                addonSelectYesno->Close(true);
            else if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                addonSelectString->Close(true);
            else if (GenericHelpers.TryGetAddonByName("RetainerList", out AtkUnitBase* addonRetainerList))
                addonRetainerList->Close(true);
            else if (GenericHelpers.TryGetAddonByName("RetainerSellList", out AtkUnitBase* addonRetainerSellList))
                addonRetainerSellList->Close(true);
            else if (GenericHelpers.TryGetAddonByName("RetainerSell", out AtkUnitBase* addonRetainerSell))
                addonRetainerSell->Close(true);
            else if (GenericHelpers.TryGetAddonByName("ItemSearchResult", out AtkUnitBase* addonItemSearchResult))
                addonItemSearchResult->Close(true);
            return;
        }

        internal static unsafe void AMUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.States.HasFlag(PluginState.Paused))
                return;

            if (AutoDuty.Plugin.States.HasFlag(PluginState.Navigating))
            {
                Svc.Log.Debug("AutoDuty is Started, Stopping AMHelper");
                Stop();
            }
            if (!_aMStarted && AM_IPCSubscriber.IsRunning())
            {
                Svc.Log.Info("AM has Started");
                _aMStarted = true;
                return;
            }
            else if (_aMStarted && !AM_IPCSubscriber.IsRunning())
            {
                Svc.Log.Debug("AM is Complete");
                Stop();
                return;
            }

            if (!EzThrottler.Throttle("AM", 250))
                return;

            if (!ObjectHelper.IsValid) return;

            if (GotoHelper.State == ActionState.Running)
            {
                Svc.Log.Debug("Goto Running");
                return;
            }
            AutoDuty.Plugin.Action = "AM Running";

            if (SummoningBellGameObject != null && ObjectHelper.GetDistanceToPlayer(SummoningBellGameObject) > 4)
            {
                Svc.Log.Debug("Moving Closer to Summoning Bell");
                MovementHelper.Move(SummoningBellGameObject, 0.25f, 4);
            }
            else if (SummoningBellGameObject == null && GotoHelper.State != ActionState.Running)
            {
                Svc.Log.Debug("Moving to Summoning Bell Location");
                SummoningBellHelper.Invoke(AutoDuty.Plugin.Configuration.PreferredSummoningBellEnum);
            }
            else if (SummoningBellGameObject != null && ObjectHelper.GetDistanceToPlayer(SummoningBellGameObject) <= 4 && !_aMStarted && !GenericHelpers.TryGetAddonByName("RetainerList", out AtkUnitBase* addonRetainerList) && (ObjectHelper.InteractWithObjectUntilAddon(SummoningBellGameObject, "RetainerList") == null))
            {
                if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
                {
                    Svc.Log.Debug("Starting AM");
                    AM_IPCSubscriber.Start();
                }
                else
                    Svc.Log.Debug("Interacting with SummoningBell");
            }
        }
    }
}
