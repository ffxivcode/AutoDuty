using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons;
using System.Linq;
using System.Numerics;
using ECommons.GameHelpers;

namespace AutoDuty.Helpers
{
    internal static class GotoHousingHelper
    {
        internal static void Invoke(int which)
        {
            if (!GotoHousingRunning && !InPrivateHouse)
            {
                Svc.Log.Info($"Goto {(which == 1 ? "Personal Home" : "FC Estate")} Started");
                GotoHousingRunning = true;
                _which = which;
                SchedulerHelper.ScheduleAction("GotoHousingTimeOut", Stop, 600000);
                Svc.Framework.Update += GotoHousingUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal static void Stop() 
        {
            if (GotoHousingRunning)
                Svc.Log.Info($"Goto {(_which == 1 ? "Personal Home" : "FC Estate")} Finished");
            SchedulerHelper.DescheduleAction("GotoHousingTimeOut");
            GotoHelper.Stop();
            _stop = true;
            _which = 1;
            AutoDuty.Plugin.Action = "";
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool InPrivateHouse => Svc.ClientState.TerritoryType == 282 || Svc.ClientState.TerritoryType == 283 || Svc.ClientState.TerritoryType == 284 || Svc.ClientState.TerritoryType == 342 || Svc.ClientState.TerritoryType == 343 || Svc.ClientState.TerritoryType == 344 || Svc.ClientState.TerritoryType == 345 || Svc.ClientState.TerritoryType == 346 || Svc.ClientState.TerritoryType == 347 || Svc.ClientState.TerritoryType == 649 || Svc.ClientState.TerritoryType == 650 || Svc.ClientState.TerritoryType == 651 || Svc.ClientState.TerritoryType == 980 || Svc.ClientState.TerritoryType == 981 || Svc.ClientState.TerritoryType == 982;
        internal static bool InHousingArea => (Svc.ClientState.TerritoryType == 339 && (TeleportHelper.FCEstateTeleportId == 56 || TeleportHelper.PersonalHomeTeleportId == 59)) || (Svc.ClientState.TerritoryType == 340 && (TeleportHelper.FCEstateTeleportId == 57 || TeleportHelper.PersonalHomeTeleportId == 60)) || (Svc.ClientState.TerritoryType == 341 && (TeleportHelper.FCEstateTeleportId == 58 || TeleportHelper.PersonalHomeTeleportId == 61)) || (Svc.ClientState.TerritoryType == 641 && (TeleportHelper.FCEstateTeleportId == 96 || TeleportHelper.PersonalHomeTeleportId == 97)) || (Svc.ClientState.TerritoryType == 979 && (TeleportHelper.FCEstateTeleportId == 164 || TeleportHelper.PersonalHomeTeleportId == 165));
        internal static bool GotoHousingRunning = false;
        private static IGameObject? _entranceGameObject => Svc.Objects.FirstOrDefault(x => x.DataId == 2002737);
        private static bool _stop = false;
        private static int _which = 1;

        internal unsafe static void GotoHousingUpdate(IFramework framework)
        {
            if (_stop)
            {
                Svc.Log.Debug($"Stopping GotoHousing");
                if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                    addonSelectYesno->Close(true);
                else if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                    addonSelectString->Close(true);
                else if (ObjectHelper.IsReady)
                {
                    _stop = false;
                    GotoHousingRunning = false;
                    Svc.Framework.Update -= GotoHousingUpdate;
                }
                return;
            }

            if (AutoDuty.Plugin.Started)
            {
                Svc.Log.Debug($"AutoDuty has Started, Stopping GotoHousing");
                Stop();
            }

            if (!EzThrottler.Check("GotoHousing"))
                return;

            EzThrottler.Throttle("GotoHousing", 50);

            if (Svc.ClientState.LocalPlayer == null)
            {
                Svc.Log.Debug($"Our player is null");
                return;
            }
            if (GotoHelper.GotoRunning)
                return;

            AutoDuty.Plugin.Action = $"Retiring to {(_which == 1 ? "Personal Home" : "FC Estate")}";

            if (InPrivateHouse)
            {
                Svc.Log.Debug($"We are in a private house, Stopping GotoHousing");
                Stop();
                return;
            }

            if (!InHousingArea)
            {
                if (!ObjectHelper.PlayerIsCasting)
                {
                    Svc.Log.Debug($"We are not in the correct housing area, teleporting there");
                    if (_which == 1 && !TeleportHelper.TeleportPersonalHome() && TeleportHelper.PersonalHomeTeleportId == 0)
                    {
                        Stop();
                        return;
                    }
                    else if (!TeleportHelper.TeleportFCEstate() && TeleportHelper.FCEstateTeleportId == 0)
                    {
                        Stop();
                        return;
                    }
                    EzThrottler.Throttle("GotoHousing", 7500, true);
                }
                return;
            }
            else if (ObjectHelper.IsValid)
            {
                if (MovementHelper.Move(_entranceGameObject, 0.25f, 3f, false, false))
                {
                    Svc.Log.Debug($"We are in range of the entracne door, entering");
                    ObjectHelper.InteractWithObject(_entranceGameObject);
                    AddonHelper.ClickSelectString(0);
                    AddonHelper.ClickSelectYesno();
                    AddonHelper.ClickTalk();
                }
                else
                    Svc.Log.Debug($"Moving closer to {_entranceGameObject?.Name} at location {_entranceGameObject?.Position}, we are {Vector3.Distance(_entranceGameObject?.Position ?? Vector3.Zero, Player.Position)} away");
            }
        }
    }
}
