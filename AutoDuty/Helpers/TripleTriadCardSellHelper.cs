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
    using Dalamud.Game.ClientState.Objects.Types;
    using System.Numerics;
    using ECommons.UIHelpers.AtkReaderImplementations;
    using FFXIVClientStructs.FFXIV.Client.Game.UI;
    using Lumina.Excel.Sheets;

    internal static class TripleTriadCardSellHelper
    {
        internal static unsafe void Invoke()
        {
            if (!QuestManager.IsQuestComplete(65970))
            {
                Svc.Log.Info("Gold Saucer requires having completed quest: It Could Happen To You");
            }
            else if(!InventoryHelper.GetInventorySelection(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4)
                                   .Any(iv =>
                                        {
                                            Item? excelItem = InventoryHelper.GetExcelItem(iv.ItemId);
                                            return excelItem is { ItemUICategory.RowId: 86 };
                                        }))
            {
                Svc.Log.Info("No TTT cards in inventory");
            }
            else if (State != ActionState.Running)
            {
                Svc.Log.Info("Gold Saucer started");
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;
                SchedulerHelper.ScheduleAction("GSTimeOut", Stop, 300000);

                Plugin.Action = "Gold Saucer";
                Svc.Framework.Update += GoldSaucerUpdate;
            }
        }

        internal unsafe static void Stop()
        {
            Plugin.States     |= PluginState.Other;
            Plugin.Action     =  "";
            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(false);

            SchedulerHelper.DescheduleAction("GSTimeOut");
            Svc.Framework.Update += GoldSaucerStopUpdate;
            Svc.Framework.Update -= GoldSaucerUpdate;

            if (GenericHelpers.TryGetAddonByName("SelectIconString", out AtkUnitBase* addonMaterializeDialog))
                addonMaterializeDialog->Close(true);
            if (GenericHelpers.TryGetAddonByName("TripleTriadCoinExchange", out AtkUnitBase* addonMaterialize))
                addonMaterialize->Close(true);
        }

        internal static        ActionState State                         = ActionState.None;
        public const           int         GoldSaucerTerritoryType       = 144;


        public static readonly Vector3     TripleTriadCardVendorLocation = new(-56.1f, 1.6f, 16.6f);
        private const uint tripleTriadVendorDataId = 1016294u;
        private static IGameObject? tripleTriadVendorGameObject => ObjectHelper.GetObjectByDataId(tripleTriadVendorDataId);

        private static unsafe AtkUnitBase*                   addonExchange         = null;
        private static unsafe ReaderTripleTriadCoinExchange? readerExchange        = null;
        private static unsafe AtkUnitBase*                   addonSelectIconString = null;

        private static unsafe void GoldSaucerStopUpdate(IFramework framework)
        {
            if (GenericHelpers.TryGetAddonByName("SelectIconString", out AtkUnitBase* addonSelectIconString))
                addonSelectIconString->Close(true);
            else if (GenericHelpers.TryGetAddonByName("TripleTriadCoinExchange", out AtkUnitBase* addonTTExchange))
                addonTTExchange->Close(true);
            else
            {
                State         =  ActionState.None;
                Plugin.States &= ~PluginState.Other;
                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(true);
                Svc.Framework.Update -= GoldSaucerStopUpdate;
            }

            return;
        }

        private static unsafe void GoldSaucerUpdate(IFramework framework)
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
    }
}
