using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons;
using System.Collections.Generic;

namespace AutoDuty.Helpers
{
    internal static class GotoInnHelper
    {
        internal static void Invoke(uint whichGrandCompany = 0)
        {
            if (whichGrandCompany == 0 || whichGrandCompany > 3)
                _whichGrandCompany = PlayerHelper.GetGrandCompany();
            else
                _whichGrandCompany = whichGrandCompany;

            if (State != ActionState.Running && Svc.ClientState.TerritoryType != InnTerritoryType(_whichGrandCompany))
            {
                Svc.Log.Info($"Goto Inn Started {_whichGrandCompany}");
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;
                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(false);
                SchedulerHelper.ScheduleAction("GotoInnTimeOut", Stop, 600000);
                Svc.Framework.Update += GotoInnUpdate;
            }
        }

        internal static void Stop() 
        {
            if (State == ActionState.Running)
                Svc.Log.Info($"Goto Inn Finished");
            SchedulerHelper.DescheduleAction("GotoInnTimeOut");
            GotoHelper.Stop();
            Svc.Framework.Update += GotoInnStopUpdate;
            Svc.Framework.Update -= GotoInnUpdate;
            _whichGrandCompany = 0;
            Plugin.Action = "";
        }

        internal static ActionState State = ActionState.None;
        internal static uint InnTerritoryType(uint _grandCompany) => _grandCompany == 1 ? 177u : (_grandCompany == 2 ? 179u : 178u);
        internal static uint ExitInnDoorDataId(uint _grandCompany) => _grandCompany == 1 ? 2001010u : (_grandCompany == 2 ? 2000087u : 2001011u);

        private static uint _whichGrandCompany = 0;
        private static List<Vector3> _innKeepLocation => _whichGrandCompany == 1 ? [new Vector3(15.42688f, 39.99999f, 12.466553f)] : (_whichGrandCompany == 2 ? [new Vector3(25.6627f, -8f, 99.74237f)] : [new Vector3(28.85994f, 6.999999f, -80.12716f)]);
        private static uint _innKeepDataId => _whichGrandCompany == 1 ? 1000974u : (_whichGrandCompany == 2 ? 1000102u : 1001976u);
        private static IGameObject? _innKeepGameObject => ObjectHelper.GetObjectByDataId(_innKeepDataId);

        internal unsafe static void GotoInnStopUpdate(IFramework framework)
        {
            if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInQuestEvent])
            {
                Svc.Log.Debug("Stopping GotoInn");
                State = ActionState.None;
                Plugin.States &= ~PluginState.Other;
                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(true);
                Svc.Framework.Update -= GotoInnStopUpdate;
            }
            else if (Svc.Targets.Target != null)
                Svc.Targets.Target = null;
            else if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                addonSelectYesno->Close(true);
            else if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                addonSelectString->Close(true);
            else if (GenericHelpers.TryGetAddonByName("Talk", out AtkUnitBase* addonTalk))
                addonTalk->Close(true);
            return;
        }

        internal unsafe static void GotoInnUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating))
            {
                Svc.Log.Debug($"AutoDuty has Started, Stopping GotoInn");
                Stop();
            }

            if (!EzThrottler.Check("GotoInn"))
                return;

            EzThrottler.Throttle("GotoInn", 50);

            if (Svc.ClientState.LocalPlayer == null)
            {
                Svc.Log.Debug($"Our player is null");
                return;
            }

            if (GotoHelper.State == ActionState.Running)
                return;

            Plugin.Action = "Retiring to Inn";

            if (Svc.ClientState.TerritoryType == InnTerritoryType(_whichGrandCompany))
            {
                Svc.Log.Debug($"We are in the Inn, stopping GotoInn");
                Stop();
                return;
            }

            if (Svc.ClientState.TerritoryType != PlayerHelper.GetGrandCompanyTerritoryType(_whichGrandCompany) || _innKeepGameObject == null || Vector3.Distance(Svc.ClientState.LocalPlayer.Position, _innKeepGameObject.Position) > 7f)
            {
                Svc.Log.Debug($"We are not in the correct TT or our innkeepGO is null or out innkeepPosition is > 7f, moving there");
                GotoHelper.Invoke(PlayerHelper.GetGrandCompanyTerritoryType(_whichGrandCompany), _innKeepLocation, 0.25f, 5f, false);
                return;
            }
            else if (PlayerHelper.IsValid)
            {
                Svc.Log.Debug($"Interacting with GO and Addons");
                ObjectHelper.InteractWithObject(_innKeepGameObject);
                AddonHelper.ClickSelectString(0);
                AddonHelper.ClickSelectYesno();
                AddonHelper.ClickTalk();
            }
        }
    }
}
