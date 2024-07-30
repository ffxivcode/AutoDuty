using AutoDuty.IPC;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal static class AutoMarketHelper
    {
        internal static void Invoke() 
        {
            Svc.Log.Debug("AutoMarketHelper.Invoke");
            if (!AutoMarket_IPCSubscriber.IsEnabled)
                Svc.Log.Info("GC Turnin Requires a plugin. Get @ https://github.com/ffxivcode/DalamudPlugins");
            else if (!AutoMarketRunning)
            {
                Svc.Log.Info("AutoMarket Started");
                AutoMarketRunning = true;
                SchedulerHelper.ScheduleAction("AutoMarketTimeOut", Stop, 600000);
                Svc.Framework.Update += AutoMarketUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal static void Stop() 
        {
            Svc.Log.Debug("AutoMarketHelper.Stop");
            if (AutoMarketRunning)
                Svc.Log.Info("AutoMarket Finished");
            GotoInnHelper.Stop();
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("AutoMarketTimeOut");
            _autoMarketStarted = false;
            if (AutoMarket_IPCSubscriber.IsRunning())
                AutoMarket_IPCSubscriber.Stop();
            _stop = true;
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool AutoMarketRunning = false;
        private static bool _autoMarketStarted = false;
        private static bool _stop = false;

        internal static unsafe void AutoMarketUpdate(IFramework framework)
        {
            if (_stop)
            {
                if (!Svc.Condition[ConditionFlag.OccupiedSummoningBell])
                {
                    _stop = false;
                    AutoMarketRunning = false;
                    Svc.Framework.Update -= AutoMarketUpdate;
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
                Svc.Log.Debug("AutoDuty is Started, Stopping AutoMarketHelper");
                Stop();
            }
            if (!_autoMarketStarted && AutoMarket_IPCSubscriber.IsRunning())
            {
                Svc.Log.Info("AutoMarket has Started");
                _autoMarketStarted = true;
                return;
            }
            else if (_autoMarketStarted && !AutoMarket_IPCSubscriber.IsRunning())
            {
                Svc.Log.Debug("AutoMarket is Complete");
                Stop();
                return;
            }

            if (!EzThrottler.Throttle("AutoMarket", 250))
                return;

            if (!ObjectHelper.IsValid) return;

            if (GotoHelper.GotoRunning)
            {
                Svc.Log.Debug("Goto Running");
                return;
            }
            AutoDuty.Plugin.Action = "AutoMarket Running";

            if (!GotoHelper.GotoRunning && Svc.ClientState.TerritoryType != GotoInnHelper.InnTerritoryType(ObjectHelper.GrandCompany))
            {
                Svc.Log.Debug("Moving to Inn");
                GotoInnHelper.Invoke();
            }
            else if (!_autoMarketStarted)
            {
                Svc.Log.Debug("Starting AutoMarket");
                AutoMarket_IPCSubscriber.Start();
            }
        }
    }
}
