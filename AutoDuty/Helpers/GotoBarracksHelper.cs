using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    using FFXIVClientStructs;

    internal class GotoBarracksHelper : ActiveHelperBase<GotoBarracksHelper>
    {
        protected override string Name        => nameof(GotoBarracksHelper);
        protected override string DisplayName => string.Empty;

        protected override string[] AddonsToClose { get; } = ["SelectYesno"];

        internal override void Start()
        {
            if (Svc.ClientState.TerritoryType != BarracksTerritoryType(PlayerHelper.GetGrandCompany())) 
                base.Start();
        }

        internal override void Stop() 
        {
            GotoHelper.ForceStop();
            base.Stop();
        }

        internal static uint BarracksTerritoryType(uint _grandCompany) => _grandCompany == 1 ? 536u : (_grandCompany == 2 ? 534u : 535u);
        internal static uint ExitBarracksDoorDataId(uint _grandCompany) => _grandCompany == 1 ? 2007528u : (_grandCompany == 2 ? 2006963u : 2007530u);

        private static Vector3 _barracksDoorLocation => PlayerHelper.GetGrandCompany() == 1 ? new Vector3(98.00867f, 41.275635f, 62.790894f) : (PlayerHelper.GetGrandCompany() == 2 ? new Vector3(-80.216736f, 0.47296143f, -7.0039062f) : new Vector3(-153.30743f, 5.2338257f, -98.039246f));
        private static uint _barracksDoorDataId => PlayerHelper.GetGrandCompany() == 1 ? 2007527u : (PlayerHelper.GetGrandCompany() == 2 ? 2006962u : 2007529u);
        private IGameObject? _barracksDoorGameObject => ObjectHelper.GetObjectByDataId(_barracksDoorDataId);

        protected override void HelperUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating))
                Stop();

            if (!EzThrottler.Check("GotoBarracks"))
                return;

            EzThrottler.Throttle("GotoBarracks", 50);

            if (Svc.ClientState.LocalPlayer == null)
                return;

            if (GotoHelper.State == ActionState.Running)
                return;

            Plugin.Action = "Retiring to Barracks";

            if (Svc.ClientState.TerritoryType == BarracksTerritoryType(PlayerHelper.GetGrandCompany()))
            {
                Stop();
                return;
            }

            if (Svc.ClientState.TerritoryType != PlayerHelper.GetGrandCompanyTerritoryType(PlayerHelper.GetGrandCompany()) || _barracksDoorGameObject == null || Vector3.Distance(Svc.ClientState.LocalPlayer.Position, _barracksDoorGameObject.Position) > 2f)
            {
                GotoHelper.Invoke(PlayerHelper.GetGrandCompanyTerritoryType(PlayerHelper.GetGrandCompany()), _barracksDoorLocation, 0.25f, 2f, false);
                return;
            }
            else if (PlayerHelper.IsValid)
            {
                ObjectHelper.InteractWithObject(_barracksDoorGameObject);
                AddonHelper.ClickSelectYesno();
            }
        }
    }
}
