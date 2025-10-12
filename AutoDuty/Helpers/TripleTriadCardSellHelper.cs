using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Linq;
using AutoDuty.IPC;
using ECommons.Throttlers;

namespace AutoDuty.Helpers
{
    using System.Collections.Generic;
    using Dalamud.Game.ClientState.Objects.Types;
    using System.Numerics;
    using ECommons.UIHelpers.AtkReaderImplementations;
    using Lumina.Excel.Sheets;

    internal class TripleTriadCardSellHelper : ActiveHelperBase<TripleTriadCardSellHelper>
    {
        protected override string Name        { get; } = nameof(TripleTriadCardSellHelper);
        protected override string DisplayName { get; } = "Selling TTT Cards";

        public override string[]? Commands           { get; init; } = ["ttsell", "sellcards"];
        public override string?   CommandDescription { get; init; } = "Sells Triple Triad cards to the vendor in Gold Saucer";

        protected override string[] AddonsToClose { get; } = ["SelectYesno", "SelectIconString", "TripleTriadCoinExchange", "ShopCardDialog"];

        internal override void Start()
        {
            if (!QuestManager.IsQuestComplete(65970))
            {
                Svc.Log.Info("Gold Saucer requires having completed quest: It Could Happen To You");
            }
            else if(!EnoughCardsInInventory())
            {
                Svc.Log.Info("Not enough TTT cards in inventory");
            }
            else if (State != ActionState.Running)
            {
                base.Start();
            }
        }

        public const           int         GoldSaucerTerritoryType       = 144;

        public static readonly Vector3     TripleTriadCardVendorLocation = new(-56.1f, 1.6f, 16.6f);
        private const uint tripleTriadVendorDataId = 1016294u;
        private static IGameObject? tripleTriadVendorGameObject => ObjectHelper.GetObjectByDataId(tripleTriadVendorDataId);

        private static unsafe AtkUnitBase*                   addonExchange         = null;
        private static unsafe ReaderTripleTriadCoinExchange? readerExchange        = null;
        private static unsafe AtkUnitBase*                   addonSelectIconString = null;

        protected override unsafe void HelperUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating) || Plugin.InDungeon)
            {
                Stop();
                return;
            }

            if (!EzThrottler.Throttle("TTT", 250))
                return;

            if (GotoHelper.State == ActionState.Running)
            {
                //Svc.Log.Debug("Goto Running");
                return;
            }

            if (Svc.ClientState.TerritoryType != GoldSaucerTerritoryType)
            {
                Svc.Log.Debug("Moving to Gold Saucer");
                GotoHelper.Invoke(GoldSaucerTerritoryType, [TripleTriadCardVendorLocation], 0.25f, 2f, false);

                return;
            }

            if (ObjectHelper.GetDistanceToPlayer(TripleTriadCardVendorLocation) > 4 && PlayerHelper.IsReady && VNavmesh_IPCSubscriber.Nav_IsReady() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() &&
                VNavmesh_IPCSubscriber.Path_NumWaypoints()         == 0)
            {
                Svc.Log.Debug("Setting Move to Triple Triad Card Trader");
                MovementHelper.Move(TripleTriadCardVendorLocation, 0.25f, 4f);
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(TripleTriadCardVendorLocation) > 4 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                Svc.Log.Debug("Moving to Triple Triad Card Trader");
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(TripleTriadCardVendorLocation) <= 4 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                Svc.Log.Debug("Stopping Path");
                VNavmesh_IPCSubscriber.Path_Stop();
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(TripleTriadCardVendorLocation) <= 4 && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
            {
                if (addonExchange == null || !GenericHelpers.IsAddonReady(addonExchange))
                {
                    readerExchange = null;
                    if (GenericHelpers.TryGetAddonByName("SelectIconString", out addonSelectIconString) && GenericHelpers.IsAddonReady(addonSelectIconString))
                    {
                        Svc.Log.Debug($"Clicking SelectIconString");
                        AddonHelper.ClickSelectIconString(1);
                    }
                    else if (!GenericHelpers.TryGetAddonByName("TripleTriadCoinExchange", out addonExchange) && tripleTriadVendorGameObject != null)
                    {
                        Svc.Log.Debug("Interacting with TTT");
                        ObjectHelper.InteractWithObject(tripleTriadVendorGameObject);
                    }
                }
                else
                {
                    readerExchange ??= new ReaderTripleTriadCoinExchange(addonExchange);

                    if (readerExchange.Entries.Count <= 0)
                    {
                        Stop();
                        return;
                    }

                    if (GenericHelpers.TryGetAddonByName("ShopCardDialog", out AtkUnitBase* shopCardDialog) && GenericHelpers.IsAddonReady(shopCardDialog))
                    {
                        AddonHelper.FireCallBack(shopCardDialog, true, 0, readerExchange.Entries.First().Count);
                        return;
                    }
                    AddonHelper.FireCallBack(addonExchange, true, 0, 0u);
                }
            }
        }

        private static bool EnoughCardsInInventory()
        {
            IEnumerable<InventoryItem> items      = InventoryHelper.GetInventorySelection(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4);

            int cardCount = 0;

            int slotCount = items.Count(iv =>
                                    {
                                        Item? excelItem = InventoryHelper.GetExcelItem(iv.ItemId);
                                        bool  isCard    = excelItem is { ItemUICategory.RowId: 86 };

                                        if (isCard)
                                            cardCount += iv.Quantity;

                                        return isCard;
                                    });
            
            return cardCount >= Plugin.Configuration.TripleTriadSellMinItemCount && slotCount >= Plugin.Configuration.TripleTriadSellMinSlotCount;
        }
    }
}
