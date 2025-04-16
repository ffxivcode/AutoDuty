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
    internal class AMHelper : ActiveHelperBase<AMHelper>
    {
        protected override string Name        { get; }      = nameof(AMHelper);
        protected override string DisplayName { get; }      = "AM";
        protected override int    TimeOut     { get; set; } = 600_000;
        protected override string[] AddonsToClose { get; } = 
            ["SelectYesno", "SelectString", "Talk", "RetainerList", "RetainerSellList", "RetainerSell", "ItemSearchResult"];

        internal override void Start()
        {
            if (!AM_IPCSubscriber.IsEnabled)
            {
                Svc.Log.Info("AM requires a plugin, visit https://discord.gg/JzSxThjKnd for more info");
                Svc.Log.Info("DO NOT ask in Puni.sh discord about this option");
            }
            else if (State != ActionState.Running)
            {
                base.Start();
            }
        }

        internal override void Stop() 
        {
            base.Stop();
            
            _aMStarted = false;
            if (AM_IPCSubscriber.IsRunning())
                AM_IPCSubscriber.Stop();
        }
        private static bool _aMStarted = false;
        private static IGameObject? SummoningBellGameObject => Svc.Objects.FirstOrDefault(x => x.DataId == SummoningBellHelper.SummoningBellDataIds((uint)Plugin.Configuration.PreferredSummoningBellEnum));

        protected override unsafe void HelperStopUpdate(IFramework framework)
        {
            if (!Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                base.HelperStopUpdate(framework);
            }
            else if (Svc.Targets.Target != null)
                Svc.Targets.Target = null;
            else 
                this.CloseAddons();
        }

        protected override unsafe void HelperUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Paused))
                return;

            if (Plugin.States.HasFlag(PluginState.Navigating))
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

            if (!PlayerHelper.IsValid) return;

            if (GotoHelper.State == ActionState.Running)
            {
                Svc.Log.Debug("Goto Running");
                return;
            }
            Plugin.Action = "AM Running";

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
