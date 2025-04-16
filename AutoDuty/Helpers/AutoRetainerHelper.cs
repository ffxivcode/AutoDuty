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
    internal class AutoRetainerHelper : ActiveHelperBase<AutoRetainerHelper>
    {
        protected override string Name        { get; } = nameof(AutoRetainerHelper);
        protected override string DisplayName { get; } = "AutoRetainer";

        protected override int TimeOut { get; set; } = 600_000;

        protected override string[] AddonsToClose { get; } = ["RetainerList", "SelectYesno", "SelectString", "RetainerTaskAsk"];

        internal override void Start()
        {
            if (!AutoRetainer_IPCSubscriber.IsEnabled || !AutoRetainer_IPCSubscriber.AreAnyRetainersAvailableForCurrentChara())
                return;
            Svc.Log.Debug("AutoRetainerHelper.Invoke");
            if (!AutoRetainer_IPCSubscriber.IsEnabled)
                Svc.Log.Info("AutoRetainer requires a plugin, visit https://puni.sh/plugin/AutoRetainer for more info");
            else if (State != ActionState.Running) 
                base.Start();
        }

        internal override void Stop()
        {
            this._autoRetainerStarted = false;
            GotoInnHelper.ForceStop();

            base.Stop();

            if (AutoRetainer_IPCSubscriber.IsBusy())
                AutoRetainer_IPCSubscriber.AbortAllTasks();
        }

        private bool _autoRetainerStarted = false;
        private IGameObject? SummoningBellGameObject => Svc.Objects.FirstOrDefault(x => x.DataId == SummoningBellHelper.SummoningBellDataIds((uint)Plugin.Configuration.PreferredSummoningBellEnum));

        protected override unsafe void HelperStopUpdate(IFramework framework)
        {
            if (!Svc.Condition[ConditionFlag.OccupiedSummoningBell])
                base.HelperStopUpdate(framework);
            else if (Svc.Targets.Target != null)
                Svc.Targets.Target = null;
            else
                this.CloseAddons();
        }

        protected override unsafe void HelperUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating))
            {
                Svc.Log.Debug("AutoDuty is Started, Stopping AutoRetainerHelper");
                this.Stop();
            }

            if (!this._autoRetainerStarted && AutoRetainer_IPCSubscriber.IsBusy())
            {
                Svc.Log.Info("AutoRetainer has Started");
                this._autoRetainerStarted = true;
                return;
            }
            else if (this._autoRetainerStarted && !AutoRetainer_IPCSubscriber.IsBusy())
            {
                Svc.Log.Debug("AutoRetainer is Complete");
                this.Stop();
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

            if (this.SummoningBellGameObject != null && !SummoningBellHelper.HousingZones.Contains(Player.Territory) && ObjectHelper.GetDistanceToPlayer(this.SummoningBellGameObject) > 4)
            {
                Svc.Log.Debug("Moving Closer to Summoning Bell");
                MovementHelper.Move(this.SummoningBellGameObject, 0.25f, 4);
            }
            else if ((this.SummoningBellGameObject == null || SummoningBellHelper.HousingZones.Contains(Player.Territory)) && GotoHelper.State != ActionState.Running)
            {
                Svc.Log.Debug("Moving to Summoning Bell Location");
                SummoningBellHelper.Invoke(Plugin.Configuration.PreferredSummoningBellEnum);
            }
            else if (this.SummoningBellGameObject != null && ObjectHelper.GetDistanceToPlayer(this.SummoningBellGameObject) <= 4 && !this._autoRetainerStarted && !GenericHelpers.TryGetAddonByName("RetainerList", out AtkUnitBase* _) && (ObjectHelper.InteractWithObjectUntilAddon(this.SummoningBellGameObject, "RetainerList") == null))
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
