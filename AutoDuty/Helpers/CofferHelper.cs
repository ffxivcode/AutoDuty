using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using System.Linq;
using ECommons.Throttlers;

namespace AutoDuty.Helpers
{
    using FFXIVClientStructs.FFXIV.Client.UI.Misc;
    using Lumina.Excel.Sheets;

    internal static class CofferHelper
    {
        private static List<InventoryItem> doneItems = [];
        private static int                 initialGearset;

        internal static unsafe void Invoke()
        {
            if (State != ActionState.Running)
            {
                Svc.Log.Info("Opening Coffers Started");
                State         =  ActionState.Running;
                Plugin.States |= PluginState.Other;

                initialGearset = RaptureGearsetModule.Instance()->CurrentGearsetIndex;

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
            Svc.Log.Info("Opening Coffers Done");
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

            if (PlayerHelper.IsCasting || !PlayerHelper.IsReadyFull || Player.IsBusy)
                return;

            Svc.Log.Debug("CofferHelper: Checking items");

            IEnumerable <InventoryItem> items = InventoryHelper.GetInventorySelection(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4)
                                                               .Where(iv =>
                                                                      {
                                                                          Item? excelItem = InventoryHelper.GetExcelItem(iv.ItemId);//                                                      Miscellany
                                                                          return !doneItems.Contains(iv) && (excelItem?.ItemAction.RowId ?? 0) is 1085 or 388 && excelItem?.ItemUICategory.RowId is 61;
                                                                      });


            RaptureGearsetModule* module = RaptureGearsetModule.Instance();
            
            if (items.Any())
            {
                Svc.Log.Debug("CofferHelper: item found");
                if (Plugin.Configuration.AutoOpenCoffersGearset != null && module->CurrentGearsetIndex != Plugin.Configuration.AutoOpenCoffersGearset)
                {
                    Svc.Log.Debug("CofferHelper: change gearset");
                    if (!module->IsValidGearset((int)Plugin.Configuration.AutoOpenCoffersGearset))
                    {
                        Svc.Log.Debug("CofferHelper: invalid gearset");
                        Plugin.Configuration.AutoOpenCoffersGearset = null;
                        Plugin.Configuration.Save();
                    } else
                    {
                        module->EquipGearset(Plugin.Configuration.AutoOpenCoffersGearset.Value);
                        return;
                    }
                }

                InventoryItem item = items.First();

                doneItems.Add(item);
                InventoryHelper.UseItem(item.ItemId);
            } else if (initialGearset != module->CurrentGearsetIndex)
            {
                if (!EzThrottler.Throttle("CofferChangeBack", 1000))
                    return;

                Svc.Log.Debug("CofferHelper: change back to original gearset");
                module->EquipGearset(initialGearset);
            }
            else
            {
                Svc.Log.Debug("CofferHelper: no items found");
                Stop();
            }
        }
    }
}