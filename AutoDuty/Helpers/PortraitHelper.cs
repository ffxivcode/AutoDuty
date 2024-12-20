namespace AutoDuty.Helpers
{
    using Dalamud.Plugin.Services;
    using ECommons;
    using ECommons.DalamudServices;
    using ECommons.Throttlers;
    using FFXIVClientStructs.FFXIV.Component.GUI;

    internal static class PortraitHelper
    {
        internal static ActionState State = ActionState.None;

        internal static void Invoke()
        {
            if (State != ActionState.Running && Svc.ClientState.TerritoryType != 0)
            {
                Svc.Log.Info("Portrait Started");
                State         =  ActionState.Running;
                Plugin.States |= PluginState.Other;
                
                SchedulerHelper.ScheduleAction("PortraitTimeOut", Stop, 10000);
                Plugin.Action         =  "Updating Portrait";
                Svc.Framework.Update  += PortraitUpdate;
            }
        }

        internal static void Stop()
        {
            Plugin.Action = "";
            SchedulerHelper.DescheduleAction("PortraitTimeOut");
            Svc.Framework.Update -= PortraitUpdate;
        }


        internal static unsafe void PortraitUpdate(IFramework framework)
        {
            if (!EzThrottler.Throttle("PortraitUpdate", 500))
                return;
            if (!GenericHelpers.TryGetAddonByName("BannerPreview", out AtkUnitBase* addonBanner) || !GenericHelpers.IsAddonReady(addonBanner))
                return;
            AddonHelper.FireCallBack(addonBanner, true, 0);

            Svc.Framework.Update -= PortraitUpdate;
        }

    }
}
