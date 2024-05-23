using AutoDuty.IPC;
using AutoDuty.Managers;
using AutoDuty.Windows;
using ClickLib.Clicks;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.Schedulers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentSatisfactionSupply;

namespace AutoDuty.Helpers
{
    internal static class GCTurninHelper
    {
        internal static void Invoke() => Svc.Framework.Update += GCTurninUpdate;

        internal static void Stop() => Svc.Framework.Update -= GCTurninUpdate;

        private static GameObject? personnelOfficer = null;

        private static GameObject? quartermaster = null;

        private static readonly TaskManager taskManager =  new();

        internal static void InvokeTest(uint itemid) 
        {
            ITEMID = itemid;
            Svc.Framework.Update += GCTest;
        } 
        private static uint ITEMID = 0;

        internal static (uint, uint, uint) GCItemIndex(uint itemId)
        {
            var gcScripShopItemSheet = Svc.Data.GetExcelSheet<GCScripShopItem>();
            var gcScripShopCategorySheet = Svc.Data.GetExcelSheet<GCScripShopCategory>();
            uint itemRowId = 0;
            uint gcSealsCost = 0;
            uint tier = 0;
            uint subcategory = 0;

            if ((itemRowId = gcScripShopItemSheet?.FirstOrDefault(i => i.Item.Value?.RowId == itemId)?.RowId ?? 0) == 0 || (gcSealsCost = gcScripShopItemSheet?.FirstOrDefault(i => i.Item.Value?.RowId == itemId)?.CostGCSeals ?? 0) == 0)
                return (0, 0, 0);

            if ((tier = (uint)gcScripShopCategorySheet?.FirstOrDefault(i => i.RowId == itemRowId).Tier) == 0 || (subcategory = (uint)gcScripShopCategorySheet?.FirstOrDefault(i => i.RowId == itemRowId).SubCategory) == 0)
                return (0, 0, 0);

            return (tier, subcategory, gcSealsCost);
        }

        /*internal unsafe static void BuyGCItem(uint itemId)
        {
            if (GenericHelpers.TryGetAddonByName("GrandCompanyExchange", out AtkUnitBase* addonGrandCompanyExchangeAtkUnitBase))
            {
                (uint, uint, uint) indices;
                if ((indices = GCItemIndex(itemId)) == (0, 0, 0)) 
                {
                    Svc.Log.Error($"We were unable to determine the indices of item id {itemId}");
                    return;
                }
                Svc.Log.Info($"{indices}");
                AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 1, indices.Item1 - 1);
                AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 2, indices.Item2);
                AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 0, indices.Item3, 1);
            }
        }*/

        internal unsafe static void SelectTier(uint itemId)
        {

        }

        private unsafe static void GCTurninComplete()
        {
            if (GenericHelpers.TryGetAddonByName("GrandCompanySupplyList", out AtkUnitBase* addonGrandCompanySupplyListAtkUnitBase))
            addonGrandCompanySupplyListAtkUnitBase->Hide2();
            _ = new TickScheduler(delegate
            {
                if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                    addonSelectString->Hide2();
            }, 250);
            _ = new TickScheduler(delegate
            {
                if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                    addonSelectString->Hide2();
            }, 250);
            Svc.Framework.Update -= GCTurninUpdate;
            AutoDuty.Plugin.GCTurninComplete = true;
        }

        internal static unsafe void GCTest(IFramework framework)
        {
            if (GenericHelpers.TryGetAddonByName("GrandCompanyExchange", out AtkUnitBase* addonGrandCompanyExchangeAtkUnitBase) && GenericHelpers.IsAddonReady(addonGrandCompanyExchangeAtkUnitBase))
            {
                (uint, uint, uint) indices;
                int index = -1;
                if ((indices = GCItemIndex(ITEMID)) == (0, 0, 0))
                {
                    Svc.Log.Error($"We were unable to determine the indices of item id {ITEMID}");
                    return;
                }
                Svc.Log.Info($"{indices}");
                switch (indices.Item1)
                {
                    case 1:
                        if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[23]->GetAsAtkComponentRadioButton()->IsSelected)
                        {
                            AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 1, 0);
                            return;
                        }
                        break;
                    case 2:
                        if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[22]->GetAsAtkComponentRadioButton()->IsSelected)
                        {
                            AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 1, 1);
                            return;
                        }
                        break;
                    case 3:
                        if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[21]->GetAsAtkComponentRadioButton()->IsSelected)
                        {
                            AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 1, 2);
                            return;
                        }
                        break;
                }
                
                if ((index = ListItemExists(addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[2]->GetAsAtkComponentList(), Svc.Data.GetExcelSheet<Item>()?.GetRow(ITEMID)?.Name.ExtractText() ?? "")) < 0)
                {
                    AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 2, indices.Item2);
                    return;
                }

                if (!GenericHelpers.TryGetAddonByName("ShopExchangeCurrencyDialog", out AtkUnitBase* addonShopExchangeCurrencyDialogAtkUnitBase) && !GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                {
                    AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 0, index, 1);
                    return;
                }
                Svc.Log.Info($"DONE NIGGA {index}");
            }
            
            Svc.Framework.Update -= GCTest;
        }

        internal unsafe static int ListItemExists(AtkComponentList* atkComponentList, string itemName)
        {
            Svc.Log.Info($"{itemName}");
            try
            {
                for (int i = 0; i < atkComponentList->ListLength; i++)
                {
                    if (atkComponentList->GetItemRenderer(i)->AtkComponentButton.ButtonTextNode->NodeText.ToString().Contains(itemName))
                        return i;
                }
                return -1;
            }
            catch (Exception e) { Svc.Log.Info($"{e}"); return -1; }
        }
        internal unsafe static void SetItemQty(AtkComponentList* atkComponentList, AtkUnitBase* addon, int index)
        {
            try
            {
                atkComponentList->GetItemRenderer(index)->
                //atkComponentList->GetItemRenderer(index)->AtkComponentButton.ClickAddonButton(addon);
            }
            catch (Exception e) { Svc.Log.Info($"{e}"); }
        }
        internal static unsafe void GCTurninUpdate(IFramework framework)
        {
            if (!EzThrottler.Throttle("Turnin", 50) || taskManager.IsBusy)
                return;

            if ((personnelOfficer = ObjectHelper.GetObjectByPartialName("Personnel Officer")) == null)
            {
                //UIState.Instance()->PlayerState.GrandCompany)
                //Limsa=1,129, Gridania=2,132, Uldah=3,130
                if ((UIState.Instance()->PlayerState.GrandCompany == 1 && Svc.ClientState.TerritoryType != 128) || (UIState.Instance()->PlayerState.GrandCompany == 2 && Svc.ClientState.TerritoryType != 132) || (UIState.Instance()->PlayerState.GrandCompany == 3 && Svc.ClientState.TerritoryType != 130))
                {
                    //Goto GCSupply
                    var g = new GotoManager(taskManager);
                    g.Goto(false, false, true);
                }
                return;
            }

            if ((quartermaster = ObjectHelper.GetObjectByPartialName("Quartermaster")) == null)
                return;

            if (ObjectHelper.GetDistanceToPlayer(personnelOfficer) > 6 && ObjectHelper.IsReady && VNavmesh_IPCSubscriber.Nav_IsReady() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
            {
                MovementHelper.Move(personnelOfficer, 0.25f, 6);
                return;
            }
            else if (ObjectHelper.GetDistanceToPlayer(personnelOfficer) > 6 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                return;
            else if (ObjectHelper.GetDistanceToPlayer(personnelOfficer) <= 6 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                return;
            }

            if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString) && GenericHelpers.IsAddonReady(addonSelectString))
                ClickSelectString.Using((nint)addonSelectString).SelectItem1();
            else if (GenericHelpers.TryGetAddonByName("GrandCompanySupplyReward", out AtkUnitBase* addonGrandCompanySupplyRewardAtkUnitBase) && GenericHelpers.IsAddonReady(addonGrandCompanySupplyRewardAtkUnitBase))
                AddonHelper.FireCallBack(addonGrandCompanySupplyRewardAtkUnitBase, true, 0);
            else if (GenericHelpers.TryGetAddonByName("GrandCompanySupplyList", out AtkUnitBase* addonGrandCompanySupplyListAtkUnitBase) && GenericHelpers.IsAddonReady(addonGrandCompanySupplyListAtkUnitBase))
            {
                var addonGrandCompanySupplyList = ((AddonGrandCompanySupplyList*)addonGrandCompanySupplyListAtkUnitBase);
                if (addonGrandCompanySupplyList->SelectedTab != 2)
                    AddonHelper.FireCallBack(addonGrandCompanySupplyListAtkUnitBase, true, 0, 2);

                if (addonGrandCompanySupplyList->SelectedFilter != 2)
                {
                    MainWindow.ShowPopup("Error", "Filter must be set to Hide Armory Chest Items");
                    Svc.Log.Error("Filter must be set to Hide Armory Chest Items");
                    GCTurninComplete();
                    return;
                }

                if (addonGrandCompanySupplyList->ListEmptyTextNode->AtkResNode.IsVisible || addonGrandCompanySupplyList->ExpertDeliveryList->ListLength == 0)
                {
                    Svc.Log.Info("GCTurnin Complete");
                    GCTurninComplete();
                    return;
                }

                if (InventoryHelper.MySeals + MemoryHelper.ReadSeString(&addonGrandCompanySupplyListAtkUnitBase->UldManager.NodeList[5]->GetAsAtkComponentNode()->Component->UldManager.NodeList[2]->GetAsAtkComponentNode()->Component->UldManager.NodeList[4]->GetAsAtkTextNode()->NodeText).ExtractText().ParseInt() > InventoryHelper.MaxSeals)
                {
                    if (!uint.TryParse(AutoDuty.Plugin.Configuration.AutoGCTurninItemToBuyId, out uint itemId))
                        itemId = 0;
                    if (itemId == 0)
                    {
                        MainWindow.ShowPopup("Error", "Please spend your seals or set an Item for AutoDuty to Buy");
                        Svc.Log.Error("Please spend your seals or set an Item for AutoDuty to Buy");
                        GCTurninComplete();
                    }
                    else
                    {
                        if (ObjectHelper.GetDistanceToPlayer(quartermaster) > 6 && ObjectHelper.IsReady && VNavmesh_IPCSubscriber.Nav_IsReady() && !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
                        {
                            MovementHelper.Move(quartermaster, 0.25f, 6);
                            return;
                        }
                        else if (ObjectHelper.GetDistanceToPlayer(quartermaster) > 6 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                            return;
                        else if (ObjectHelper.GetDistanceToPlayer(quartermaster) <= 6 && VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                        {
                            VNavmesh_IPCSubscriber.Path_Stop();
                            return;
                        }

                        //MainWindow.ShowPopup("Info", "Purchasing items from GC Vendor is Coming soon, Please spend your seals and reinvoke method");
                        //Svc.Log.Info("Purchasing items from GC Vendor is Coming soon, Please spend your seals and reinvoke method");

                        if (GenericHelpers.TryGetAddonByName("GrandCompanyExchange", out AtkUnitBase* addonGrandCompanyExchangeAtkUnitBase) && GenericHelpers.IsAddonReady(addonGrandCompanyExchangeAtkUnitBase))
                        {
                            (uint, uint, uint) indices;
                            if ((indices = GCItemIndex(itemId)) == (0, 0, 0))
                            {
                                Svc.Log.Error("We were unable to determine the indices of item id {itemId}");
                                return;
                            }
                            switch (indices.Item1)
                            {
                                case 1:
                                    if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[21]->GetAsAtkComponentRadioButton()->IsSelected)
                                    {
                                        AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 0, 0);
                                        return;
                                    }
                                    break;
                                case 2:
                                    if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[22]->GetAsAtkComponentRadioButton()->IsSelected)
                                    {
                                        AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 0, 1);
                                        return;
                                    }
                                    break; 
                                case 3:
                                    if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[23]->GetAsAtkComponentRadioButton()->IsSelected)
                                    {
                                        AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 0, 2);
                                        return;
                                    }
                                    break;
                            }
                            switch (indices.Item1)
                            {
                                case 1:
                                    if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[21]->GetAsAtkComponentRadioButton()->IsSelected)
                                    {
                                        AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 0, 14);
                                        return;
                                    }
                                    break;
                                case 2:
                                    if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[22]->GetAsAtkComponentRadioButton()->IsSelected)
                                    {
                                        AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 0, 16);
                                        return;
                                    }
                                    break;
                                case 3:
                                    if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[23]->GetAsAtkComponentRadioButton()->IsSelected)
                                    {
                                        AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 0, 15);
                                        return;
                                    }
                                    break;
                                case 4:
                                    if (!addonGrandCompanyExchangeAtkUnitBase->UldManager.NodeList[23]->GetAsAtkComponentRadioButton()->IsSelected)
                                    {
                                        AddonHelper.FireCallBack(addonGrandCompanyExchangeAtkUnitBase, true, 0, 13);
                                        return;
                                    }
                                    break;
                            }
                        }
                        GCTurninComplete();
                    }
                    return;
                }

                AddonHelper.FireCallBack(addonGrandCompanySupplyListAtkUnitBase, true, 1, 0);
            }
            else if(ObjectHelper.GetDistanceToPlayer(personnelOfficer) <= 6)
                ObjectHelper.InteractWithObject(personnelOfficer);
        }
    }
}
