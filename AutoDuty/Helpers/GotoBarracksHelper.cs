using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal static class GotoBarracksHelper
    {
        internal static void Invoke()
        {
            if (!GotoBarracksRunning && Svc.ClientState.TerritoryType != BarracksTerritoryType(ObjectHelper.GrandCompany))
            {
                Svc.Log.Info($"Goto Barracks Started");
                GotoBarracksRunning = true;
                AutoDuty.Plugin.States |= State.Other;
                SchedulerHelper.ScheduleAction("GotoBarracksTimeOut", Stop, 600000);
                Svc.Framework.Update += GotoBarracksUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal unsafe static void Stop() 
        {
            if (GotoBarracksRunning)
                Svc.Log.Info($"Goto Barracks Finished");
            SchedulerHelper.DescheduleAction("GotoBarracksTimeOut");
            Svc.Framework.Update -= GotoBarracksUpdate;
            GotoHelper.Stop();
            GotoBarracksRunning = false;
            AutoDuty.Plugin.States &= ~State.Other;
            if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                addonSelectYesno->Close(true);
            AutoDuty.Plugin.Action = "";
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool GotoBarracksRunning = false;
        internal static uint BarracksTerritoryType(uint _grandCompany) => _grandCompany == 1 ? 536u : (_grandCompany == 2 ? 534u : 535u);
        internal static uint ExitBarracksDoorDataId(uint _grandCompany) => _grandCompany == 1 ? 2007528u : (_grandCompany == 2 ? 2006963u : 2007530u);

        private static Vector3 _barracksDoorLocation => ObjectHelper.GrandCompany == 1 ? new Vector3(98.00867f, 41.275635f, 62.790894f) : (ObjectHelper.GrandCompany == 2 ? new Vector3(-80.216736f, 0.47296143f, -7.0039062f) : new Vector3(-153.30743f, 5.2338257f, -98.039246f));
        private static uint _barracksDoorDataId => ObjectHelper.GrandCompany == 1 ? 2007527u : (ObjectHelper.GrandCompany == 2 ? 2006962u : 2007529u);
        private static IGameObject? _barracksDoorGameObject => ObjectHelper.GetObjectByDataId(_barracksDoorDataId);

        internal static void GotoBarracksUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.States.HasFlag(State.Navigating))
                Stop();

            if (!EzThrottler.Check("GotoBarracks"))
                return;

            EzThrottler.Throttle("GotoBarracks", 50);

            if (Svc.ClientState.LocalPlayer == null)
                return;

            if (GotoHelper.GotoRunning)
                return;

            AutoDuty.Plugin.Action = "Retiring to Barracks";

            if (Svc.ClientState.TerritoryType == BarracksTerritoryType(ObjectHelper.GrandCompany))
            {
                Stop();
                return;
            }

            if (Svc.ClientState.TerritoryType != ObjectHelper.GrandCompanyTerritoryType(ObjectHelper.GrandCompany) || _barracksDoorGameObject == null || Vector3.Distance(Svc.ClientState.LocalPlayer.Position, _barracksDoorGameObject.Position) > 2f)
            {
                GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(ObjectHelper.GrandCompany), _barracksDoorLocation, 0.25f, 2f, false);
                return;
            }
            else if (ObjectHelper.IsValid)
            {
                ObjectHelper.InteractWithObject(_barracksDoorGameObject);
                AddonHelper.ClickSelectYesno();
            }
        }
    }
}
