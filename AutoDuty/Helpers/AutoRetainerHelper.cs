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
            DebugLog("AutoRetainerHelper.Invoke");
            if (!AutoRetainer_IPCSubscriber.IsEnabled)
                Svc.Log.Info("AutoRetainer requires a plugin, visit https://puni.sh/plugin/AutoRetainer for more info");
            else if (State != ActionState.Running) 
                base.Start();
        }

        internal override void Stop()
        {
            this._autoRetainerStarted = false;
            this._autoRetainerStopped = false;
            GotoInnHelper.ForceStop();

            base.Stop();

            if (AutoRetainer_IPCSubscriber.IsBusy())
                AutoRetainer_IPCSubscriber.AbortAllTasks();
            Plugin.Chat.ExecuteCommand("/autoretainer d");
        }

        private bool         _autoRetainerStarted = false;
        private bool         _autoRetainerStopped = false;
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
            if (!this.UpdateBase())
                return;

            if (!PlayerHelper.IsValid) return;

            if (this._autoRetainerStopped)
            {
                if (AutoRetainer_IPCSubscriber.IsBusy())
                {
                    this._autoRetainerStopped = false;
                }
                else
                {
                    this.Stop();
                    return;
                }
            }

            if (!this._autoRetainerStarted && AutoRetainer_IPCSubscriber.IsBusy())
            {
                DebugLog("AutoRetainer has Started");
                this._autoRetainerStarted = true;
                UpdateBaseThrottle        = 1000;
                return;
            }
            else if (this._autoRetainerStarted && !AutoRetainer_IPCSubscriber.IsBusy())
            {
                DebugLog("AutoRetainer is Complete");
                this._autoRetainerStopped = true;
                EzThrottler.Throttle(this.Name, 2000, true);
            }

            if (this.SummoningBellGameObject != null && !SummoningBellHelper.HousingZones.Contains(Player.Territory) && ObjectHelper.GetDistanceToPlayer(this.SummoningBellGameObject) > 4)
            {
                DebugLog("Moving Closer to Summoning Bell");
                MovementHelper.Move(this.SummoningBellGameObject, 0.25f, 4);
            }
            else if ((this.SummoningBellGameObject == null || SummoningBellHelper.HousingZones.Contains(Player.Territory)) && GotoHelper.State != ActionState.Running)
            {
                DebugLog("Moving to Summoning Bell Location");
                SummoningBellHelper.Invoke(Plugin.Configuration.PreferredSummoningBellEnum);
            }
            else if (this.SummoningBellGameObject != null && ObjectHelper.GetDistanceToPlayer(this.SummoningBellGameObject) <= 4 && !this._autoRetainerStarted && !GenericHelpers.TryGetAddonByName("RetainerList", out AtkUnitBase* _) && (ObjectHelper.InteractWithObjectUntilAddon(this.SummoningBellGameObject, "RetainerList") == null))
            {
                if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
                {
                    if (VNavmesh_IPCSubscriber.Path_IsRunning())
                        VNavmesh_IPCSubscriber.Path_Stop();
                    DebugLog("Waiting for AutoRetainer to Start");
                    Plugin.Chat.ExecuteCommand("/autoretainer e");
                }
                else
                    DebugLog("Interacting with SummoningBell");
                
            }
        }
    }
}
