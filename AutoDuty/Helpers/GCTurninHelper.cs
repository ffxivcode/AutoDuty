using AutoDuty.IPC;
using AutoDuty.Managers;
using AutoDuty.Windows;
using ClickLib.Clicks;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.Schedulers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal static class GCTurninHelper
    {
        internal static void Invoke() => Svc.Framework.Update += GCTurninUpdate;

        internal static void Stop() => Svc.Framework.Update -= GCTurninUpdate;

        private static GameObject? personnelOfficer = null;

        private static GameObject? quartermaster = null;

        private static readonly TaskManager taskManager =  new();

        private unsafe static void GCTurninComplete()
        {
            if (GenericHelpers.TryGetAddonByName("GrandCompanySupplyList", out AtkUnitBase* addonGrandCompanySupplyListAtkUnitBase))
            addonGrandCompanySupplyListAtkUnitBase->Hide2();
            _ = new TickScheduler(delegate
            {
                if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                    addonSelectString->Hide2();
            }, 250);
            Svc.Framework.Update -= GCTurninUpdate;
            AutoDuty.Plugin.GCTurninComplete = true;
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
                    if (!uint.TryParse(AutoDuty.Plugin.Configuration.AutoGCTurninItemToBuyId, out uint itemid))
                        itemid = 0;
                    if (itemid == 0)
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

                        MainWindow.ShowPopup("Info", "Purchasing items from GC Vendor is Coming soon, Please spend your seals and reinvoke method");
                        Svc.Log.Info("Purchasing items from GC Vendor is Coming soon, Please spend your seals and reinvoke method");
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
