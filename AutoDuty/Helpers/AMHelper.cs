using AutoDuty.IPC;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal static class AMHelper
    {
        internal static void Invoke() 
        {
            Svc.Log.Debug("AMHelper.Invoke");
            if (!AM_IPCSubscriber.IsEnabled)
            {
                Svc.Log.Info("AM requires a plugin, visit https://discord.gg/JzSxThjKnd for more info");
                Svc.Log.Info("DO NOT ask in Puni.sh discord about this option");
            }
            else if (!AMRunning)
            {
                Svc.Log.Info("AM Started");
                AMRunning = true;
                SchedulerHelper.ScheduleAction("AMTimeOut", Stop, 600000);
                Svc.Framework.Update += AMUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal static void Stop() 
        {
            Svc.Log.Debug("AMHelper.Stop");
            if (AMRunning)
                Svc.Log.Info("AM Finished");
            GotoInnHelper.Stop();
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("AMTimeOut");
            _aMStarted = false;
            if (AM_IPCSubscriber.IsRunning())
                AM_IPCSubscriber.Stop();
            _stop = true;
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool AMRunning = false;
        private static bool _aMStarted = false;
        private static bool _stop = false;

        internal static unsafe void AMUpdate(IFramework framework)
        {
            if (_stop)
            {
                if (!Svc.Condition[ConditionFlag.OccupiedSummoningBell])
                {
                    _stop = false;
                    AMRunning = false;
                    Svc.Framework.Update -= AMUpdate;
                }
                else if (Svc.Targets.Target != null)
                    Svc.Targets.Target = null;
                else if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                    addonSelectYesno->Close(true);
                else if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                    addonSelectString->Close(true);
                else if (GenericHelpers.TryGetAddonByName("RetainerList", out AtkUnitBase* addonRetainerList))
                    addonRetainerList->Close(true);
                else if (GenericHelpers.TryGetAddonByName("RetainerSellList", out AtkUnitBase* addonRetainerSellList))
                    addonRetainerSellList->Close(true);
                else if (GenericHelpers.TryGetAddonByName("RetainerSell", out AtkUnitBase* addonRetainerSell))
                    addonRetainerSell->Close(true);
                else if (GenericHelpers.TryGetAddonByName("ItemSearchResult", out AtkUnitBase* addonItemSearchResult))
                    addonItemSearchResult->Close(true);
                return;
            }

            if (AutoDuty.Plugin.Started)
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

            if (!ObjectHelper.IsValid) return;

            if (GotoHelper.GotoRunning)
            {
                Svc.Log.Debug("Goto Running");
                return;
            }
            AutoDuty.Plugin.Action = "AM Running";

            if (!GotoHelper.GotoRunning && Svc.ClientState.TerritoryType != GotoInnHelper.InnTerritoryType(ObjectHelper.GrandCompany))
            {
                Svc.Log.Debug("Moving to Inn");
                GotoInnHelper.Invoke();
            }
            else if (!_aMStarted)
            {
                Svc.Log.Debug("Starting AM");
                AM_IPCSubscriber.Start();
            }
        }
    }
}
