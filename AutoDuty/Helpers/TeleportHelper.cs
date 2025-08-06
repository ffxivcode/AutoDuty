using FFXIVClientStructs.FFXIV.Client.Game;
using ECommons;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using ECommons.UIHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using ECommons.Throttlers;
using ECommons.DalamudServices;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;

namespace AutoDuty.Helpers
{
    internal unsafe static class TeleportHelper
    {
        internal static bool TeleportFCEstate() => TeleportHousing(FCEstateTeleportId, 0);

        internal static bool TeleportPersonalHome() => TeleportHousing(PersonalHomeTeleportId, 0);

        internal static bool TeleportApartment() => TeleportHousing(ApartmentTeleportId, 128);

        private static bool TeleportHousing(uint id, byte sub)
        {
            if (id != 0)
            {
                Svc.Log.Debug($"Teleporting to AetheryteId: {id} SubIndex: {sub}");
                return TeleportAetheryte(id, sub);
            }
            else
            {
                Svc.Log.Info("Unable to teleport to specified housing");
                return false;
            }
        }

        internal static MapMarkerData FCEstateMapMarkerData => AgentHUD.Instance()->MapMarkers.ToList().FirstOrDefault(x => x.IconId.EqualsAny((uint[])Enum.GetValuesAsUnderlyingType<FCHousingMarker>()));

        internal static Vector3 FCEstateWardCenterVector3 => new(FCEstateMapMarkerData.Position.X, FCEstateMapMarkerData.Position.Y, FCEstateMapMarkerData.Position.Z);

        internal static uint FCEstateTeleportId => Svc.AetheryteList.FirstOrDefault(x => x is { IsApartment: false, IsSharedHouse: false } && x.AetheryteId.EqualsAny<uint>(56, 57, 58, 96, 164))?.AetheryteId ?? 0;

        internal static IGameObject? FCEstateEntranceGameObject => FCEstateWardCenterVector3 != Vector3.Zero ? ObjectHelper.GetObjectsByObjectKind(ObjectKind.EventObj)?.OrderBy(x => Vector3.Distance(x.Position, FCEstateWardCenterVector3)).FirstOrDefault(x => x.DataId == 2002737) : null;

        internal static MapMarkerData PersonalHomeMapMarkerData => AgentHUD.Instance()->MapMarkers.ToList().FirstOrDefault(x => x.IconId.EqualsAny((uint[])Enum.GetValuesAsUnderlyingType<PrivateHousingMarker>()));

        internal static Vector3 PersonalHomeWardCenterVector3 => new(PersonalHomeMapMarkerData.Position.X, PersonalHomeMapMarkerData.Position.Y, PersonalHomeMapMarkerData.Position.Z);

        internal static uint PersonalHomeTeleportId => Svc.AetheryteList.FirstOrDefault(x => x is { IsApartment: false, IsSharedHouse: false } && x.AetheryteId.EqualsAny<uint>(59, 60, 61, 97, 165))?.AetheryteId ?? 0;

        internal static IGameObject? PersonalHomeEntranceGameObject => PersonalHomeWardCenterVector3 != Vector3.Zero ? ObjectHelper.GetObjectsByObjectKind(ObjectKind.EventObj)?.OrderBy(x => Vector3.Distance(x.Position, PersonalHomeWardCenterVector3)).FirstOrDefault(x => x.DataId == 2002737) : null;

        internal static MapMarkerData ApartmentMapMarkerData => AgentHUD.Instance()->MapMarkers.ToList().FirstOrDefault(x => x.IconId.EqualsAny((uint[])Enum.GetValuesAsUnderlyingType<ApartmentHousingMarker>()));

        internal static Vector3 ApartmentWardCenterVector3 => new(ApartmentMapMarkerData.Position.X, ApartmentMapMarkerData.Position.Y, ApartmentMapMarkerData.Position.Z);

        internal static uint ApartmentTeleportId => Svc.AetheryteList.FirstOrDefault(x => x is { IsApartment: true, IsSharedHouse: false } && x.AetheryteId.EqualsAny<uint>(59, 60, 61, 97, 165))?.AetheryteId ?? 0;

        internal static IGameObject? ApartmentEntranceGameObject => ApartmentWardCenterVector3 != Vector3.Zero ? ObjectHelper.GetObjectsByObjectKind(ObjectKind.EventObj)?.OrderBy(x => Vector3.Distance(x.Position, ApartmentWardCenterVector3)).FirstOrDefault(x => x.DataId == 2007402) : null;

        internal static bool TeleportGCCity()
        {
            //Limsa=1,128, Gridania=2,132, Uldah=3,130 -- Goto Limsa if no GC
            return UIState.Instance()->PlayerState.GrandCompany switch
            {
                1 => TeleportAetheryte(8, 0),
                2 => TeleportAetheryte(2, 0),
                3 => TeleportAetheryte(9, 0),
                _ => TeleportAetheryte(8, 0),
            };
        }

        internal static bool TeleportAetheryte(uint aetheryteId, byte subindex)
        {
            if (PlayerHelper.IsCasting || aetheryteId == 0)
                return true;

            if (!PlayerHelper.IsCasting && EzThrottler.Throttle("TeleportAetheryte", 250))
                TeleportAction(aetheryteId, subindex);

            return false;
        }

        internal static bool MoveToClosestAetheryte()
        {
            IGameObject? gameObject;
            if ((gameObject = ObjectHelper.GetObjectByObjectKind(ObjectKind.Aetheryte)) == null)
                return false;

            return MovementHelper.Move(gameObject, 0.25f, 7f);
        }

        internal static bool TeleportAethernet(string aethernetName, uint toTerritoryType)
        {
            if (aethernetName.IsNullOrEmpty() || !PlayerHelper.IsValid)
                return true;

            if (!GenericHelpers.TryGetAddonByName("TelepotTown", out AtkUnitBase* addon) || !GenericHelpers.IsAddonReady(addon))
            {
                IGameObject? gameObject;
                if ((gameObject = ObjectHelper.GetObjectByObjectKind(ObjectKind.Aetheryte)) == null)
                    return false;

                if ((addon = ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectString")) == null)
                    return false;

                Callback.Fire(addon, true, 0);
            }

            if (EzThrottler.Throttle("TeleportAethernet", 250))
                Callback.Fire(addon, true, 11, GetAethernetCallback(aethernetName));

            return false;
        }

        internal static bool TeleportAction(uint aetheryteId, byte subindex = 0)
        {
            ActionManager.Instance()->GetActionStatus(ActionType.Action, 5);

            return Telepo.Instance()->Teleport(aetheryteId, subindex);
        }

        internal static uint GetAethernetCallback(string aethernetName)
        {
            if (GenericHelpers.TryGetAddonByName("TelepotTown", out AtkUnitBase* addon) && GenericHelpers.IsAddonReady(addon))
            {
                var readerTelepotTown = new ReaderTelepotTown(addon);
                for (int i = 0; i < readerTelepotTown.DestinationData.Count; i++)
                {
                    if (aethernetName == readerTelepotTown.DestinationName[i].Name)
                        return readerTelepotTown.DestinationData[i].CallbackData;
                }
            }
            return 0;
        }
    }
    //From Lifestream
    internal unsafe class ReaderTelepotTown(AtkUnitBase* UnitBase, int BeginOffset = 0) : AtkReader(UnitBase, BeginOffset)
    {
        internal uint        NumEntries         => ReadUInt(0) ?? 0;
        internal uint        CurrentDestination => ReadUInt(1) ?? 0;
        internal List<Data>  DestinationData    => Loop<Data>(6, 4, 20);
        internal List<Names> DestinationName    => Loop<Names>(262, 1, 20);

        internal unsafe class Names(nint UnitBasePtr, int BeginOffset = 0) : AtkReader(UnitBasePtr, BeginOffset)
        {
            internal string Name => ReadSeString(0).GetText();
        }

        internal unsafe class Data(nint UnitBasePtr, int BeginOffset = 0) : AtkReader(UnitBasePtr, BeginOffset)
        {
            internal uint Type         => ReadUInt(0).Value;
            internal uint State        => ReadUInt(1).Value;
            internal uint IconID       => ReadUInt(2).Value;
            internal uint CallbackData => ReadUInt(3).Value;
        }
    }
}
