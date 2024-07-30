using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace AutoDuty.Helpers
{
    internal unsafe static class AddonHelper
    {
        internal static bool SeenAddon = false;

        internal static unsafe void FireCallBack(AtkUnitBase* addon, bool boolValue, params object[] args)
        {
            var addonPtr = addon;
            if (addon == null || addonPtr is null) return;
            try
            {
                Callback.Fire(addonPtr, boolValue, args);
            }
            catch (Exception ex) 
            { 
                Svc.Log.Error($"{ex}");
            }
        }

        internal static bool ClickSelectString(int index)
        {
            if ((!GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addon) || !GenericHelpers.IsAddonReady(addon)) && !SeenAddon)
            {
                return false;
            }
            if (SeenAddon && (!GenericHelpers.TryGetAddonByName("SelectString", out addon) || !GenericHelpers.IsAddonReady(addon)))
            {
                SeenAddon = false;
                return true;
            }
            if (EzThrottler.Throttle("ClickSelectString", 50))
                new AddonMaster.SelectString(addon).Entries[index].Select();
                //FireCallBack(addon, true, index);
            SeenAddon = true;
            return false;
        }

        internal static bool ClickSelectYesno(bool yes = true)
        {
            if ((!GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addon) || !GenericHelpers.IsAddonReady(addon)) && !SeenAddon)
            {
                return false;
            }
            if (SeenAddon && (!GenericHelpers.TryGetAddonByName("SelectYesno", out addon) || !GenericHelpers.IsAddonReady(addon)))
            {
                SeenAddon = false;
                return true;
            }

            if (EzThrottler.Throttle("ClickYesno", 50))
            {
                if (yes)
                    new AddonMaster.SelectYesno(addon).Yes();
                    //FireCallBack(addon, true, 0);
                else
                    new AddonMaster.SelectYesno(addon).No();
                    //FireCallBack(addon, true, 1);
            }
            SeenAddon = true;
            return false;
        }

        internal static bool ClickRepair()
        {
            if ((!GenericHelpers.TryGetAddonByName("Repair", out AtkUnitBase* addon) || !GenericHelpers.IsAddonReady(addon)) && !SeenAddon)
                return false;
            if (SeenAddon && (!GenericHelpers.TryGetAddonByName("Repair", out addon) || !GenericHelpers.IsAddonReady(addon)))
            {
                SeenAddon = false;
                return true;
            }
            if (EzThrottler.Throttle("Repair", 50))
                //new AddonMaster.Repair(addon).RepairAll();
                FireCallBack(addon, true, 0);
            SeenAddon = true;
            return false;
        }

        internal static bool ClickTalk()
        {
            if ((!GenericHelpers.TryGetAddonByName("Talk", out AtkUnitBase* addon) || !GenericHelpers.IsAddonReady(addon)) && !SeenAddon)
                return false;
            if (SeenAddon && (!GenericHelpers.TryGetAddonByName("Talk", out addon) || !GenericHelpers.IsAddonReady(addon)))
            {
                SeenAddon = false;
                return true;
            }
            if (EzThrottler.Throttle("ClickTalk", 50))
                new AddonMaster.Talk(addon).Click();
                //FireCallBack(addon, true);
            SeenAddon = true;
            return false;
        }

        public static void ClickCheckboxButton(this AtkComponentCheckBox target, AtkComponentBase* addon, uint which, EventType type = EventType.CHANGE)
        => ClickHelper.ClickAddonComponent(addon, target.OwnerNode, which, type);
    }
}
