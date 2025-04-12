using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using FFXIVClientStructs.FFXIV.Client.Game.UI;
    using Lumina.Excel.Sheets;

    internal class TripleTriadCardUseHelper
    {
        internal static unsafe void Invoke()
        {
            if (State != ActionState.Running)
            {
                Svc.Log.Info("Registering Triple Triad Cards");
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;


                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(false);

                SchedulerHelper.ScheduleAction("TTCTimeOut", Stop, 300000);
                Plugin.Action = "Registering Cards";
                Svc.Framework.Update += CardsOpenUpdate;
            }
        }

        internal unsafe static void Stop()
        {
            Svc.Log.Info("Registering Triple Triad Cards Done");
            Plugin.States |= PluginState.Other;
            Plugin.Action = "";

            SchedulerHelper.DescheduleAction("TTCTimeOut");
            Svc.Framework.Update += CardsOpenStopUpdate;
            Svc.Framework.Update -= CardsOpenUpdate;
        }

        internal static ActionState State = ActionState.None;

        internal static void CardsOpenStopUpdate(IFramework framework)
        {
            State = ActionState.None;
            Plugin.States &= ~PluginState.Other;
            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(true);
            Svc.Framework.Update -= CardsOpenStopUpdate;
        }


        internal static unsafe void CardsOpenUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating) || Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("CardsRegister", 250))
                return;
            
            if (Conditions.Instance()->Mounted)
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                return;
            }

            if (PlayerHelper.IsCasting || !PlayerHelper.IsReadyFull || Player.IsBusy)
                return;

            Svc.Log.Debug("TripleTriadCardUseHelper: Checking items");

            IEnumerable<InventoryItem> items = InventoryHelper.GetInventorySelection(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4)
                                                               .Where(iv =>
                                                               {
                                                                   Item? excelItem = InventoryHelper.GetExcelItem(iv.ItemId);
                                                                   return excelItem is { ItemUICategory.RowId: 86 } && !UIState.Instance()->IsTripleTriadCardUnlocked((ushort) excelItem.Value.AdditionalData.RowId);
                                                               });


            RaptureGearsetModule* module = RaptureGearsetModule.Instance();

            if (items.Any())
            {
                Svc.Log.Debug("TripleTriadCardUseHelper: item found");

                InventoryItem item = items.First();

                InventoryHelper.UseItem(item.ItemId);

                if (!PlayerHelper.IsCasting)
                {
                    Svc.Log.Debug("TripleTriadCardUseHelper: failed to use item");
                    return;
                }

                Svc.Log.Debug("TripleTriadCardUseHelper: item used");
            }
            else
            {
                Svc.Log.Debug("TripleTriadCardUseHelper: no items found");
                Stop();
            }
        }
    }
}
