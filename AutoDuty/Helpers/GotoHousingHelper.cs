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
        internal static void Invoke(Housing whichHousing)
        {
            if (!GotoHousingRunning && !InPrivateHouse(whichHousing))
            {
                Svc.Log.Info($"Goto {whichHousing} Started");
                GotoHousingRunning = true;
                if (!AutoDuty.Plugin.States.HasFlag(State.Other))
                    AutoDuty.Plugin.States |= State.Other;
                _whichHousing = whichHousing;
                SchedulerHelper.ScheduleAction("GotoHousingTimeOut", Stop, 600000);
                Svc.Framework.Update += GotoHousingUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal static void Stop() 
        {
            if (GotoHousingRunning)
                Svc.Log.Info($"Goto {_whichHousing} Finished");
            SchedulerHelper.DescheduleAction("GotoHousingTimeOut");
            GotoHelper.Stop();
            _stop = true;
            _whichHousing = Housing.Apartment;
            AutoDuty.Plugin.Action = "";
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool InPrivateHouse(Housing whichHousing) =>
            //Mist
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 59 && Svc.ClientState.TerritoryType == 608) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 59 && (Svc.ClientState.TerritoryType == 282 || Svc.ClientState.TerritoryType == 283 || Svc.ClientState.TerritoryType == 284)) || (whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 56 && (Svc.ClientState.TerritoryType == 282 || Svc.ClientState.TerritoryType == 283 || Svc.ClientState.TerritoryType == 284)) ||
            //LavenderBeds
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 60 && Svc.ClientState.TerritoryType == 609) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 60 && (Svc.ClientState.TerritoryType == 342 || Svc.ClientState.TerritoryType == 343 || Svc.ClientState.TerritoryType == 344)) || (whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 57 && (Svc.ClientState.TerritoryType == 342 || Svc.ClientState.TerritoryType == 343 || Svc.ClientState.TerritoryType == 344)) ||
            //Goblet
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 61 && Svc.ClientState.TerritoryType == 610) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 61 && (Svc.ClientState.TerritoryType == 345 || Svc.ClientState.TerritoryType == 346 || Svc.ClientState.TerritoryType == 347)) || (whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 58 && (Svc.ClientState.TerritoryType == 345 || Svc.ClientState.TerritoryType == 346 || Svc.ClientState.TerritoryType == 347)) ||
            //Shirogane
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 97 && Svc.ClientState.TerritoryType == 655) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 97 && (Svc.ClientState.TerritoryType == 649 || Svc.ClientState.TerritoryType == 650 || Svc.ClientState.TerritoryType == 651)) || (whichHousing == Housing.FC_Estate && TeleportHelper.PersonalHomeTeleportId == 96 && (Svc.ClientState.TerritoryType == 649 || Svc.ClientState.TerritoryType == 650 || Svc.ClientState.TerritoryType == 651)) ||
            //Empyreum
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 165 && Svc.ClientState.TerritoryType == 999) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 165 && (Svc.ClientState.TerritoryType == 980 || Svc.ClientState.TerritoryType == 981 || Svc.ClientState.TerritoryType == 982)) || (whichHousing == Housing.FC_Estate && TeleportHelper.PersonalHomeTeleportId == 164 && (Svc.ClientState.TerritoryType == 980 || Svc.ClientState.TerritoryType == 981 || Svc.ClientState.TerritoryType == 982));

        internal static bool InHousingArea(Housing whichHousing) =>
            //Mist
            (Svc.ClientState.TerritoryType == 339 &&
            ((whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 56) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 59) || (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 59))) ||
            //Lavender Beds
            (Svc.ClientState.TerritoryType == 340 &&
            ((whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 57) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 60) || (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 60))) ||
            //Goblet
            (Svc.ClientState.TerritoryType == 341 &&
            ((whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 58) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 61) || (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 61))) ||
            //Shirogane
            (Svc.ClientState.TerritoryType == 641 &&
            ((whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 96) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 97) || (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 97))) ||
            //Empyreum
            (Svc.ClientState.TerritoryType == 979 &&
            ((whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 164) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 165) || (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 165)));

        internal static bool GotoHousingRunning = false;
        private static IGameObject? _entranceGameObject => ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj)?.FirstOrDefault(x => x.DataId == 2002737 || x.DataId == 2007402);
        private static bool _stop = false;
        private static Housing _whichHousing = Housing.Apartment;

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
                    AutoDuty.Plugin.States -= State.Other;
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

            AutoDuty.Plugin.Action = $"Retiring to {_whichHousing}";

            if (InPrivateHouse(_whichHousing))
            {
                Svc.Log.Debug($"We are in a private house, Stopping GotoHousing");
                Stop();
                return;
            }

            if (!InHousingArea(_whichHousing))
            {
                if (!ObjectHelper.PlayerIsCasting)
                {
                    Svc.Log.Debug($"We are not in the correct housing area, teleporting there");
                    if (_whichHousing == Housing.Apartment && !TeleportHelper.TeleportApartment() && TeleportHelper.ApartmentTeleportId == 0)
                    {
                        Stop();
                        return;
                    }
                    else if (_whichHousing == Housing.Personal_Home && !TeleportHelper.TeleportPersonalHome() && TeleportHelper.PersonalHomeTeleportId == 0)
                    {
                        Stop();
                        return;
                    }
                    else if (_whichHousing == Housing.FC_Estate && !TeleportHelper.TeleportFCEstate() && TeleportHelper.FCEstateTeleportId == 0)
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
                    Svc.Log.Debug($"We are in range of the entrance door, entering");
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
