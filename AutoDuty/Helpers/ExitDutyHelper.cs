using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoDuty.Helpers
{
    internal class ExitDutyHelper : ActiveHelperBase<ExitDutyHelper>
    {
        protected override string Name        => nameof(ExitDutyHelper);
        protected override string DisplayName => "Exiting Duty";

        public override string[]? Commands           { get; init; } = ["exitduty"];
        public override string?   CommandDescription { get; init; } = "Exits the current duty if you are not in combat";

        protected override int TimeOut { get; set; } = 60_000;

        protected override string[] AddonsToClose { get; } = ["ContentsFinderMenu"];

        internal override void Start()
        {
            base.Start();

            if (Svc.ClientState.TerritoryType != 0)
            {
                _currentTerritoryType = Svc.ClientState.TerritoryType;
                base.Start();
            }
        }

        private uint _currentTerritoryType = 0;

        protected override void HelperStopUpdate(IFramework framework)
        {
            base.HelperStopUpdate(framework);
            this._currentTerritoryType = 0;
        }

        protected override void HelperUpdate(IFramework framework)
        {
            if (!PlayerHelper.IsReady || PlayerHelper.InCombat)
                return;

            if (Svc.ClientState.TerritoryType != _currentTerritoryType || !Plugin.InDungeon || Svc.ClientState.TerritoryType == 0)
            {
                Stop();
                return;
            }

            Exit();
        }

        private static unsafe void Exit()
        {
            AgentModule.Instance()->GetAgentByInternalId(AgentId.ContentsFinderMenu)->Show();
            if (GenericHelpers.TryGetAddonByName("ContentsFinderMenu", out AtkUnitBase* addonContentsFinderMenu))
            {
                AddonHelper.FireCallBack(addonContentsFinderMenu, true, 0);
                AddonHelper.FireCallBack(addonContentsFinderMenu, false, -2);
                GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno);
                AddonHelper.FireCallBack(addonSelectYesno, true, 0);
            }
        }
    }
}
