using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.DalamudServices;
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
            var addonChecker = AddonChecker("SelectString", out AtkUnitBase* addon, out bool seenAddon);

            if (!addonChecker && seenAddon)
                new AddonMaster.SelectString(addon).Entries[index].Select();

            if (addonChecker && seenAddon)
                return true;

            return false;
        }

        internal static bool ClickSelectIconString(int index)
        {
            var addonChecker = AddonChecker("SelectIconString", out AtkUnitBase* addon, out bool seenAddon);

            if (!addonChecker && seenAddon)
                new AddonMaster.SelectIconString(addon).Entries[index].Select();

            if (addonChecker && seenAddon)
                return true;

            return false;
        }

        internal static bool ClickSelectYesno(bool yes = true)
        {
            var addonChecker = AddonChecker("SelectYesno", out AtkUnitBase* addon, out bool seenAddon);

            if (!addonChecker && seenAddon)
            {
                if (yes)
                    new AddonMaster.SelectYesno(addon).Yes();
                else
                    new AddonMaster.SelectYesno(addon).No();
                return false;
            }

            if (addonChecker && seenAddon)
                return true;

            return false;
        }

        internal static bool ClickRepair()
        {
            var addonChecker = AddonChecker("Repair", out AtkUnitBase* addon, out bool seenAddon);

            if (!addonChecker && seenAddon)
                new AddonMaster.Repair(addon).RepairAll();

            if (addonChecker && seenAddon)
                return true;

            return false;
        }

        internal static bool ClickTalk()
        {
            var addonChecker = AddonChecker("Talk", out AtkUnitBase* addon, out bool seenAddon);

            if (!addonChecker && seenAddon)
                new AddonMaster.Talk(addon).Click();
            
            if (addonChecker && seenAddon)
                return true;
                    
            return false;
        }

        private static bool AddonChecker(string addonName, out AtkUnitBase* outAddon, out bool outSeenAddon)
        {
            outSeenAddon = false;
            
            var gotAddon = GenericHelpers.TryGetAddonByName(addonName, out outAddon);
            var addonReady = gotAddon && GenericHelpers.IsAddonReady(outAddon);

            if (gotAddon && addonReady)
            {
                outSeenAddon = true;
                SeenAddon = true;
                return false;
            }

            if (SeenAddon && (!gotAddon || !addonReady))
            {
                outSeenAddon = true;
                SeenAddon = false;
                return true;
            }
            return false;
        }

        public static void ClickCheckboxButton(this AtkComponentCheckBox target, AtkComponentBase* addon, uint which, EventType type = EventType.CHANGE)
        => ClickHelper.ClickAddonComponent(addon, target.OwnerNode, which, type);
    }
}
