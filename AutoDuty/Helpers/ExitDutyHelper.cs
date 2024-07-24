using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal static class ExitDutyHelper
    {
        internal static void Invoke() 
        {
            Svc.Log.Debug("ExitDutyHelper.Invoke");
            if (!ExitDutyRunning)
            {
                Svc.Log.Info("ExitDuty Started");
                _currentTerritoryType = Svc.ClientState.TerritoryType;
                ExitDutyRunning = true;
                Svc.Framework.Update += ExitDutyUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal static void Stop() 
        {
            Svc.Log.Debug("ExitDutyHelper.Stop");
            if (ExitDutyRunning)
                Svc.Log.Info("ExitDuty Finished");
            ExitDutyRunning = false;
            _currentTerritoryType = 0;
            AutoDuty.Plugin.Action = "";
            Svc.Framework.Update -= ExitDutyUpdate;
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool ExitDutyRunning = false;

        private static uint _currentTerritoryType = 0;

        internal static unsafe void ExitDutyUpdate(IFramework framework)
        {
            if (!EzThrottler.Throttle("ExitDuty", 250))
                return;

            AutoDuty.Plugin.Action = "Exiting Duty";

            if (Svc.ClientState.TerritoryType != _currentTerritoryType)
                Stop();
            else if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno))
                AddonHelper.FireCallBack(addonSelectYesno, true, 0);
            else if (!GenericHelpers.TryGetAddonByName("ContentsFinderMenu", out AtkUnitBase* addonContentsFinderMenu))
                AgentModule.Instance()->GetAgentByInternalId(AgentId.ContentsFinderMenu)->Show();
            else
                AddonHelper.FireCallBack(addonContentsFinderMenu, true, 0);
        }
    }
}
