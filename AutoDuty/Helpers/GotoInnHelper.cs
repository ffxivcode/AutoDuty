using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons;
using System.Collections.Generic;

namespace AutoDuty.Helpers
{
    internal class GotoInnHelper : ActiveHelperBase<GotoInnHelper>
    {

        protected override string Name        => nameof(GotoInnHelper);
        protected override string DisplayName => string.Empty;
        protected override int    TimeOut     { get; set; } = 600_000;

        protected override string[] AddonsToClose { get; } = ["SelectYesno", "SelectString", "Talk"];

        internal static void Invoke(uint whichGrandCompany = 0)
        {
            _whichGrandCompany = whichGrandCompany is 0 or > 3 ? 
                                     PlayerHelper.GetGrandCompany() : 
                                     whichGrandCompany;

            if (Svc.ClientState.TerritoryType != InnTerritoryType(_whichGrandCompany))
            {
                Instance.Start();
                Svc.Log.Info($"Goto Inn Started {_whichGrandCompany}");
            }
        }


        internal override void Stop() 
        {
            GotoHelper.ForceStop();
            _whichGrandCompany = 0;
            base.Stop();
        }

        internal static uint InnTerritoryType(uint _grandCompany) => _grandCompany switch
        {
            1 => 177u,
            2 => 179u,
            _ => 178u
        };
        internal static uint ExitInnDoorDataId(uint _grandCompany) => _grandCompany switch
        {
            1 => 2001010u,
            2 => 2000087u,
            _ => 2001011u
        };

        private static uint _whichGrandCompany = 0;
        private static List<Vector3> _innKeepLocation => _whichGrandCompany switch
        {
            1 => [new Vector3(15.42688f, 39.99999f, 12.466553f)],
            2 => [new Vector3(25.6627f,  -8f,       99.74237f)],
            _ => [new Vector3(28.85994f, 6.999999f, -80.12716f)]
        };
        private static uint _innKeepDataId => _whichGrandCompany switch
        {
            1 => 1000974u,
            2 => 1000102u,
            _ => 1001976u
        };
        private IGameObject? _innKeepGameObject => ObjectHelper.GetObjectByDataId(_innKeepDataId);

        protected override unsafe void HelperStopUpdate(IFramework framework)
        {
            if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInQuestEvent])
                base.HelperStopUpdate(framework);
            else if (Svc.Targets.Target != null)
                Svc.Targets.Target = null;
            else
                this.CloseAddons();
        }

        protected override void HelperUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating))
            {
                Svc.Log.Debug($"AutoDuty has Started, Stopping GotoInn");
                Stop();
            }

            if (!EzThrottler.Check("GotoInn"))
                return;

            EzThrottler.Throttle("GotoInn", 50);

            if (Svc.ClientState.LocalPlayer == null)
            {
                Svc.Log.Debug($"Our player is null");
                return;
            }

            if (GotoHelper.State == ActionState.Running)
                return;

            Plugin.Action = "Retiring to Inn";

            if (Svc.ClientState.TerritoryType == InnTerritoryType(_whichGrandCompany))
            {
                Svc.Log.Debug($"We are in the Inn, stopping GotoInn");
                Stop();
                return;
            }

            if (Svc.ClientState.TerritoryType != PlayerHelper.GetGrandCompanyTerritoryType(_whichGrandCompany) || _innKeepGameObject == null || Vector3.Distance(Svc.ClientState.LocalPlayer.Position, _innKeepGameObject.Position) > 7f)
            {
                Svc.Log.Debug($"We are not in the correct TT or our innkeepGO is null or out innkeepPosition is > 7f, moving there");
                GotoHelper.Invoke(PlayerHelper.GetGrandCompanyTerritoryType(_whichGrandCompany), _innKeepLocation, 0.25f, 5f, false);
                return;
            }
            else if (PlayerHelper.IsValid)
            {
                Svc.Log.Debug($"Interacting with GO and Addons");
                ObjectHelper.InteractWithObject(_innKeepGameObject);
                AddonHelper.ClickSelectString(0);
                AddonHelper.ClickSelectYesno();
                AddonHelper.ClickTalk();
            }
        }
    }
}
