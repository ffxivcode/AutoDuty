using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
namespace AutoDuty.Helpers
{
    internal static class ExitDutyHelper
    {
        internal static void Invoke()
        {
            if (!ExitDutyRunning)
            {
                Svc.Log.Info("ExitDuty Started");
                ExitDutyRunning = true;
                AutoDuty.Plugin.Action = "Exiting Duty";
                _currentTerritoryType = Svc.ClientState.TerritoryType;
                Svc.Framework.Update += ExitDutyUpdate;

                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal unsafe static void Stop()
        {
            AutoDuty.Plugin.Action = "";
            _stop = true;

            if (GenericHelpers.TryGetAddonByName("ContentsFinderMenu", out AtkUnitBase* addonContentsFinderMenu))
                addonContentsFinderMenu->Close(true);

            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool ExitDutyRunning = false;

        private static bool _stop = false;
        private static uint _currentTerritoryType = 0;

        internal static unsafe void ExitDutyUpdate(IFramework framework)
        {
            if (_stop)
            {
                if (GenericHelpers.TryGetAddonByName("ContentsFinderMenu", out AtkUnitBase* addonContentsFinderMenu))
                    addonContentsFinderMenu->Close(true);
                else
                {
                    Svc.Log.Info("ExitDuty Finished");
                    _stop = false;
                    ExitDutyRunning = false;
                    _currentTerritoryType = 0;
                    Svc.Framework.Update -= ExitDutyUpdate;
                }
                return;
            }

            if (!ObjectHelper.IsReady || Player.Object.InCombat())
                return;

            if (Svc.ClientState.TerritoryType != _currentTerritoryType)
                Stop();

            Exit();
        }

        private unsafe static void Exit()
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
