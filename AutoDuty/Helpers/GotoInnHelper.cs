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
                _whichGrandCompany = ObjectHelper.GrandCompany;
            else
                _whichGrandCompany = whichGrandCompany;

            if (!GotoInnRunning && Svc.ClientState.TerritoryType != InnTerritoryType(_whichGrandCompany))
            {
                Svc.Log.Info($"Goto Inn Started {_whichGrandCompany}");
                GotoInnRunning = true;
                AutoDuty.Plugin.Stage = Stage.Other;
                SchedulerHelper.ScheduleAction("GotoInnTimeOut", Stop, 600000);
                Svc.Framework.Update += GotoInnUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal static void Stop() 
        {
            if (GotoInnRunning)
                Svc.Log.Info($"Goto Inn Finished");
            SchedulerHelper.DescheduleAction("GotoInnTimeOut");
            GotoHelper.Stop();
            _stop = true;
            _whichGrandCompany = 0;
            AutoDuty.Plugin.Action = "";
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool GotoInnRunning = false;
        internal static uint InnTerritoryType(uint _grandCompany) => _grandCompany == 1 ? 177u : (_grandCompany == 2 ? 179u : 178u);
        internal static uint ExitInnDoorDataId(uint _grandCompany) => _grandCompany == 1 ? 2001010u : (_grandCompany == 2 ? 2000087u : 2001011u);
        private static uint _whichGrandCompany = 0;
        private static List<Vector3> _innKeepLocation => _whichGrandCompany == 1 ? [new Vector3(0.80804193f, 40.100555f, 35.524208f), new Vector3(15.42688f, 39.99999f, 12.466553f)] : (_whichGrandCompany == 2 ? [new Vector3(25.6627f, -8f, 99.74237f)] : [new Vector3(28.85994f, 6.999999f, -80.12716f)]);
        private static uint _innKeepDataId => _whichGrandCompany == 1 ? 1000974u : (_whichGrandCompany == 2 ? 1000102u : 1001976u);
        private static IGameObject? _innKeepGameObject => ObjectHelper.GetObjectByDataId(_innKeepDataId);
        private static bool _stop = false;

        internal unsafe static void GotoInnUpdate(IFramework framework)
        {
            if (_stop)
            {
                if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInQuestEvent])
                {
                    _stop = false;
                    GotoInnRunning = false;
                    AutoDuty.Plugin.Stage = AutoDuty.Plugin.PreviousStage;
                    Svc.Framework.Update -= GotoInnUpdate;
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

            if (AutoDuty.Plugin.Started)
                Stop();

            if (!EzThrottler.Check("GotoInn"))
                return;

            EzThrottler.Throttle("GotoInn", 50);

            if (Svc.ClientState.LocalPlayer == null)
                return;

            if (GotoHelper.GotoRunning)
                return;

            AutoDuty.Plugin.Action = "Retiring to Inn";

            if (Svc.ClientState.TerritoryType == InnTerritoryType(_whichGrandCompany))
            {
                Stop();
                return;
            }

            if (Svc.ClientState.TerritoryType != ObjectHelper.GrandCompanyTerritoryType(_whichGrandCompany) || _innKeepGameObject == null || Vector3.Distance(Svc.ClientState.LocalPlayer.Position, _innKeepGameObject.Position) > 7f)
            {
                GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(_whichGrandCompany), _innKeepLocation, 0.25f, 5f, false);
                return;
            }
            else if (ObjectHelper.IsValid)
            {
                ObjectHelper.InteractWithObject(_innKeepGameObject);
                AddonHelper.ClickSelectString(0);
                AddonHelper.ClickSelectYesno();
                AddonHelper.ClickTalk();
            }
        }
    }
}
