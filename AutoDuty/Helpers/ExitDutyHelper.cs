using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoDuty.Helpers
{
    internal static class ExitDutyHelper
    {
        internal static void Invoke()
        {
            if (State != ActionState.Running && Svc.ClientState.TerritoryType != 0)
            {
                Svc.Log.Info("ExitDuty Started");
                State = ActionState.Running;
                AutoDuty.Plugin.States |= PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(false);
                SchedulerHelper.ScheduleAction("ExitDutyTimeOut", Stop, 60000);
                AutoDuty.Plugin.Action = "Exiting Duty";
                _currentTerritoryType = Svc.ClientState.TerritoryType;
                Svc.Framework.Update += ExitDutyUpdate;
            }
        }

        internal unsafe static void Stop()
        {
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("ExitDutyTimeOut");
            Svc.Framework.Update += ExitDutyStopUpdate;
            Svc.Framework.Update -= ExitDutyUpdate;

            if (GenericHelpers.TryGetAddonByName("ContentsFinderMenu", out AtkUnitBase* addonContentsFinderMenu))
                addonContentsFinderMenu->Close(true);
        }

        internal static ActionState State = ActionState.None;

        private static uint _currentTerritoryType = 0;

        internal static unsafe void ExitDutyStopUpdate(IFramework framework)
        {
            if (GenericHelpers.TryGetAddonByName("ContentsFinderMenu", out AtkUnitBase* addonContentsFinderMenu))
                addonContentsFinderMenu->Close(true);
            else
            {
                Svc.Log.Info("ExitDuty Finished");
                State = ActionState.None;
                AutoDuty.Plugin.States &= ~PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(true);
                _currentTerritoryType = 0;
                Svc.Framework.Update -= ExitDutyStopUpdate;
            }
            return;
        }

        internal static unsafe void ExitDutyUpdate(IFramework framework)
        {
            if (!ObjectHelper.IsReady || Player.Object.InCombat())
                return;

            if (Svc.ClientState.TerritoryType != _currentTerritoryType || !AutoDuty.Plugin.InDungeon || Svc.ClientState.TerritoryType == 0)
            {
                Stop();
                return;
            }

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
