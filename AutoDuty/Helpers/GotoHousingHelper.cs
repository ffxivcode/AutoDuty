using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

namespace AutoDuty.Helpers
{
    internal static class GotoHousingHelper
    {
        internal static void Invoke(Housing whichHousing)
        {
            if (State != ActionState.Running && !InPrivateHouse(whichHousing))
            {
                Svc.Log.Info($"Goto {whichHousing} Started");
                State = ActionState.Running;
                AutoDuty.Plugin.States |= PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(false);
                _whichHousing = whichHousing;
                SchedulerHelper.ScheduleAction("GotoHousingTimeOut", Stop, 600000);
                Svc.Framework.Update += GotoHousingUpdate;
            }
        }

        internal static void Stop() 
        {
            if (State == ActionState.Running)
                Svc.Log.Info($"Goto {_whichHousing} Finished");
            SchedulerHelper.DescheduleAction("GotoHousingTimeOut");
            GotoHelper.Stop();
            Svc.Framework.Update += GotoHousingStopUpdate;
            Svc.Framework.Update -= GotoHousingUpdate;
            _whichHousing = Housing.Apartment;
            AutoDuty.Plugin.Action = "";
            _index = 0;
        }

        internal static bool InPrivateHouse(Housing whichHousing) =>
            //Mist
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 59 && Svc.ClientState.TerritoryType == 608) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 59 && (Svc.ClientState.TerritoryType == 282 || Svc.ClientState.TerritoryType == 283 || Svc.ClientState.TerritoryType == 284)) || (whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 56 && (Svc.ClientState.TerritoryType == 282 || Svc.ClientState.TerritoryType == 283 || Svc.ClientState.TerritoryType == 284)) ||
            //LavenderBeds
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 60 && Svc.ClientState.TerritoryType == 609) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 60 && (Svc.ClientState.TerritoryType == 342 || Svc.ClientState.TerritoryType == 343 || Svc.ClientState.TerritoryType == 344)) || (whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 57 && (Svc.ClientState.TerritoryType == 342 || Svc.ClientState.TerritoryType == 343 || Svc.ClientState.TerritoryType == 344)) ||
            //Goblet
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 61 && Svc.ClientState.TerritoryType == 610) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 61 && (Svc.ClientState.TerritoryType == 345 || Svc.ClientState.TerritoryType == 346 || Svc.ClientState.TerritoryType == 347)) || (whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 58 && (Svc.ClientState.TerritoryType == 345 || Svc.ClientState.TerritoryType == 346 || Svc.ClientState.TerritoryType == 347)) ||
            //Shirogane
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 97 && Svc.ClientState.TerritoryType == 655) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 97 && (Svc.ClientState.TerritoryType == 649 || Svc.ClientState.TerritoryType == 650 || Svc.ClientState.TerritoryType == 651)) || (whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 96 && (Svc.ClientState.TerritoryType == 649 || Svc.ClientState.TerritoryType == 650 || Svc.ClientState.TerritoryType == 651)) ||
            //Empyreum
            (whichHousing == Housing.Apartment && TeleportHelper.ApartmentTeleportId == 165 && Svc.ClientState.TerritoryType == 999) || (whichHousing == Housing.Personal_Home && TeleportHelper.PersonalHomeTeleportId == 165 && (Svc.ClientState.TerritoryType == 980 || Svc.ClientState.TerritoryType == 981 || Svc.ClientState.TerritoryType == 982)) || (whichHousing == Housing.FC_Estate && TeleportHelper.FCEstateTeleportId == 164 && (Svc.ClientState.TerritoryType == 980 || Svc.ClientState.TerritoryType == 981 || Svc.ClientState.TerritoryType == 982));
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

        internal static ActionState State = ActionState.None;

        private static IGameObject? _entranceGameObject => _whichHousing == Housing.FC_Estate ? TeleportHelper.FCEstateEntranceGameObject : (_whichHousing == Housing.Personal_Home ? TeleportHelper.PersonalHomeEntranceGameObject : TeleportHelper.ApartmentEntranceGameObject);
        private static Housing _whichHousing = Housing.Apartment;
        private static List<Vector3> _entrancePath => _whichHousing == Housing.Personal_Home ? AutoDuty.Plugin.Configuration.PersonalHomeEntrancePath : AutoDuty.Plugin.Configuration.FCEstateEntrancePath;
        private static int _index = 0;

        internal unsafe static void GotoHousingStopUpdate(IFramework framework)
        {
            if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                addonSelectYesno->Close(true);
            else if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                addonSelectString->Close(true);
            else if (PlayerHelper.IsReady)
            {
                State = ActionState.None;
                AutoDuty.Plugin.States &= ~PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(true);
                Svc.Framework.Update -= GotoHousingStopUpdate;
            }
            return;
        }

        internal unsafe static void GotoHousingUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.States.HasFlag(PluginState.Navigating))
            {
                Svc.Log.Debug($"AutoDuty has Started, Stopping GotoHousing");
                Stop();
            }

            if (!EzThrottler.Check("GotoHousing"))
                return;

            EzThrottler.Throttle("GotoHousing", 50);

            if (!Player.Available)
            {
                Svc.Log.Debug($"Our player is null");
                return;
            }

            if (GotoHelper.State == ActionState.Running)
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
                if (!PlayerHelper.IsCasting)
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
            else if (PlayerHelper.IsValid)
            {
                if (_index < _entrancePath.Count)
                {
                    Svc.Log.Debug($"Our entrancePath has entries, moving to index {_index} which is {_entrancePath[_index]}");
                    if (((_index + 1) != _entrancePath.Count && MovementHelper.Move(_entrancePath[_index], 0.25f, 0.25f, false, false)) || MovementHelper.Move(_entrancePath[_index], 0.25f, 3f, false, false))
                    {
                        Svc.Log.Debug($"We are at index {_index} increasing our index");
                        _index++;
                    }
                }
                else if (_entranceGameObject == null)
                    Svc.Log.Debug($"unable to find entrance door {TeleportHelper.FCEstateWardCenterVector3} {TeleportHelper.FCEstateEntranceGameObject}");
                else if (MovementHelper.Move(_entranceGameObject, 0.25f, 3f, false, false))
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
