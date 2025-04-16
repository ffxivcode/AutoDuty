namespace AutoDuty.Helpers
{
    using Dalamud.Plugin.Services;
    using ECommons;
    using ECommons.DalamudServices;
    using ECommons.Throttlers;
    using FFXIVClientStructs.FFXIV.Component.GUI;

    internal class PortraitHelper : ActiveHelperBase<PortraitHelper>
    {
        protected override string Name        { get; } = nameof(PortraitHelper);
        protected override string DisplayName { get; } = "Updating Portrait";
        protected override int    TimeOut     { get; set; } = 10_000;

        internal override void Start()
        {
            if (Svc.ClientState.TerritoryType != 0) 
                base.Start();
        }

        protected override unsafe void HelperUpdate(IFramework framework)
        {
            if (!EzThrottler.Throttle("PortraitUpdate", 500))
                return;
            if (!GenericHelpers.TryGetAddonByName("BannerPreview", out AtkUnitBase* addonBanner) || !GenericHelpers.IsAddonReady(addonBanner))
                return;
            AddonHelper.FireCallBack(addonBanner, true, 0);

            this.Stop();
        }

    }
}
