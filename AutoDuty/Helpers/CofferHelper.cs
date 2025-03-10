using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using System.Linq;
using ECommons.Throttlers;

namespace AutoDuty.Helpers
{
    internal static class CofferHelper
    {
        private static List<InventoryItem> doneItems = [];

        internal static void Invoke()
        {
            if (State != ActionState.Running)
            {
                Svc.Log.Info("Opening Coffers Started");
                State         =  ActionState.Running;
                Plugin.States |= PluginState.Other;

                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(false);
                doneItems.Clear();
                SchedulerHelper.ScheduleAction("CofferTimeOut", Stop, 300000);
                Plugin.Action        =  "Opening Coffers";
                Svc.Framework.Update += CofferOpenUpdate;
            }
        }

        internal unsafe static void Stop()
        {
            Plugin.States |= PluginState.Other;
            Plugin.Action =  "";

            SchedulerHelper.DescheduleAction("CofferTimeOut");
            Svc.Framework.Update += CofferOpenStopUpdate;
            Svc.Framework.Update -= CofferOpenUpdate;
        }

        internal static ActionState State = ActionState.None;

        internal static void CofferOpenStopUpdate(IFramework framework)
        {
            State         =  ActionState.None;
            Plugin.States &= ~PluginState.Other;
            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(true);
            Svc.Framework.Update -= CofferOpenStopUpdate;
        }


        internal static unsafe void CofferOpenUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating) || Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("CofferOpen", 250))
                return;

            if (Conditions.Instance()->Mounted)
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                return;
            }

            if (InventoryManager.Instance()->GetEmptySlotsInBag() < 1)
            {
                Stop();
                return;
            }

            if (PlayerHelper.IsOccupiedFull || PlayerHelper.IsCasting)
                return;

            IEnumerable<InventoryItem> items = InventoryHelper.GetInventorySelection(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4)
                                                              .Where(iv => !doneItems.Contains(iv) && (InventoryHelper.GetExcelItem(iv.ItemId)?.ItemAction.RowId ?? 0) is 1085 or 388);

            if (items.Any())
            {
                InventoryItem item = items.First();

                doneItems.Add(item);
                InventoryHelper.UseItem(item.ItemId);
            }
            else
                Stop();
        }
    }
}