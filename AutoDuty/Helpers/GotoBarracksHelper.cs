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
            if (State != ActionState.Running && Svc.ClientState.TerritoryType != BarracksTerritoryType(PlayerHelper.GetGrandCompany()))
            {
                Svc.Log.Info($"Goto Barracks Started");
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;
                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(false);
                SchedulerHelper.ScheduleAction("GotoBarracksTimeOut", Stop, 600000);
                Svc.Framework.Update += GotoBarracksUpdate;
            }
        }

        internal unsafe static void Stop() 
        {
            if (State == ActionState.Running)
                Svc.Log.Info($"Goto Barracks Finished");
            SchedulerHelper.DescheduleAction("GotoBarracksTimeOut");
            Svc.Framework.Update -= GotoBarracksUpdate;
            GotoHelper.Stop();
            State = ActionState.None;
            Plugin.States &= ~PluginState.Other;
            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(true);
            if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                addonSelectYesno->Close(true);
            Plugin.Action = "";
        }

        internal static ActionState State = ActionState.None;
        internal static uint BarracksTerritoryType(uint _grandCompany) => _grandCompany == 1 ? 536u : (_grandCompany == 2 ? 534u : 535u);
        internal static uint ExitBarracksDoorDataId(uint _grandCompany) => _grandCompany == 1 ? 2007528u : (_grandCompany == 2 ? 2006963u : 2007530u);

        private static Vector3 _barracksDoorLocation => PlayerHelper.GetGrandCompany() == 1 ? new Vector3(98.00867f, 41.275635f, 62.790894f) : (PlayerHelper.GetGrandCompany() == 2 ? new Vector3(-80.216736f, 0.47296143f, -7.0039062f) : new Vector3(-153.30743f, 5.2338257f, -98.039246f));
        private static uint _barracksDoorDataId => PlayerHelper.GetGrandCompany() == 1 ? 2007527u : (PlayerHelper.GetGrandCompany() == 2 ? 2006962u : 2007529u);
        private static IGameObject? _barracksDoorGameObject => ObjectHelper.GetObjectByDataId(_barracksDoorDataId);

        internal static void GotoBarracksUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating))
                Stop();

            if (!EzThrottler.Check("GotoBarracks"))
                return;

            EzThrottler.Throttle("GotoBarracks", 50);

            if (Svc.ClientState.LocalPlayer == null)
                return;

            if (GotoHelper.State == ActionState.Running)
                return;

            Plugin.Action = "Retiring to Barracks";

            if (Svc.ClientState.TerritoryType == BarracksTerritoryType(PlayerHelper.GetGrandCompany()))
            {
                Stop();
                return;
            }

            if (Svc.ClientState.TerritoryType != PlayerHelper.GetGrandCompanyTerritoryType(PlayerHelper.GetGrandCompany()) || _barracksDoorGameObject == null || Vector3.Distance(Svc.ClientState.LocalPlayer.Position, _barracksDoorGameObject.Position) > 2f)
            {
                GotoHelper.Invoke(PlayerHelper.GetGrandCompanyTerritoryType(PlayerHelper.GetGrandCompany()), _barracksDoorLocation, 0.25f, 2f, false);
                return;
            }
            else if (PlayerHelper.IsValid)
            {
                ObjectHelper.InteractWithObject(_barracksDoorGameObject);
                AddonHelper.ClickSelectYesno();
            }
        }
    }
}
