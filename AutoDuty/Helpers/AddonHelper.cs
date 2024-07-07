using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
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

        internal static bool ClickSelectString(ushort index)
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
                ClickLib.Clicks.ClickSelectString.Using((nint)addon).SelectItem(index);
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
                    ClickLib.Clicks.ClickSelectYesNo.Using((nint)addon).Yes();
                else
                    ClickLib.Clicks.ClickSelectYesNo.Using((nint)addon).No();
            }
            SeenAddon = true;
            return false;
        }

        internal static bool ClickRepair()
        {
            if ((!GenericHelpers.TryGetAddonByName("Repair", out AtkUnitBase* addon) || !GenericHelpers.IsAddonReady(addon)) && !SeenAddon)
                return false;
            if (SeenAddon && (!GenericHelpers.TryGetAddonByName("Repair", out addon) || !GenericHelpers.IsAddonReady(addon)) || GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* _))
            {
                SeenAddon = false;
                return true;
            }
            if (EzThrottler.Throttle("Repair", 50))
                new ClickLib.Clicks.ClickRepair((nint)addon).RepairAll();
            SeenAddon = true;
            return false;
        }
    }
}
