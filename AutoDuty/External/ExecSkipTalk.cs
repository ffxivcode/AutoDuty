using ClickLib.Clicks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using static ECommons.GenericHelpers;

namespace AutoDuty.External;
//From TextAdvance
internal unsafe static class ExecSkipTalk
{
    internal static bool IsEnabled = false;

    internal static void Init()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Talk", Click);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "Talk", Click);
    }

    internal static void Shutdown()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Talk", Click);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "Talk", Click);
    }

    private static void Click(AddonEvent type, AddonArgs args)
    {
        if (IsEnabled)
        {
            ClickTalk.Using(args.Addon).Click();
        }
    }

    internal static void Tick()
    {
        var addon = Svc.GameGui.GetAddonByName("Talk", 1);
        if (addon == IntPtr.Zero) return;
        var talkAddon = (AtkUnitBase*)addon;
        if (!IsAddonReady(talkAddon)) return;
        ClickTalk.Using(addon).Click();
    }
}
