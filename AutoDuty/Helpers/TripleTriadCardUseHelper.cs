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

    internal class TripleTriadCardUseHelper : ActiveHelperBase<TripleTriadCardUseHelper>
    {
        protected override string Name        { get; } = nameof(TripleTriadCardUseHelper);
        protected override string DisplayName { get; } = "Registering Cards";
        
        protected override unsafe void HelperUpdate(IFramework framework)
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

            DebugLog("Checking items");

            IEnumerable<InventoryItem> items = InventoryHelper.GetInventorySelection(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4)
                                                               .Where(iv =>
                                                               {
                                                                   Item? excelItem = InventoryHelper.GetExcelItem(iv.ItemId);
                                                                   return excelItem is { ItemUICategory.RowId: 86 } && !UIState.Instance()->IsTripleTriadCardUnlocked((ushort) excelItem.Value.AdditionalData.RowId);
                                                               });


            RaptureGearsetModule* module = RaptureGearsetModule.Instance();

            if (items.Any())
            {
                DebugLog("item found");

                InventoryItem item = items.First();

                InventoryHelper.UseItem(item.ItemId);

                if (!PlayerHelper.IsCasting)
                {
                    DebugLog("failed to use item");
                    return;
                }

                DebugLog("item used");
            }
            else
            {
                DebugLog("no items found");
                Stop();
            }
        }
    }
}
