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

namespace AutoDuty.Helpers
{
    internal unsafe static class TeleportHelper
    {
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
            if (ObjectHelper.PlayerIsCasting)
                return true;

            if (!ObjectHelper.PlayerIsCasting && EzThrottler.Throttle("TeleportAetheryte", 250))
                TeleportAction(aetheryteId, subindex);

            return false;
        }

        internal static bool MoveToClosestAetheryte(uint toTerritoryType)
        {
            //if (Svc.ClientState.TerritoryType == toTerritoryType)
              //  return true;

            IGameObject? gameObject = null;
            if ((gameObject = ObjectHelper.GetObjectByObjectKind(ObjectKind.Aetheryte)) == null)
                return false;

            return MovementHelper.Move(gameObject, 0.25f, 7f);
        }

        internal static bool TeleportAethernet(string aethernetName, uint toTerritoryType)
        {
            if (aethernetName.IsNullOrEmpty() || !ObjectHelper.IsValid)
                return true;

            ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);

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
        internal uint NumEntries => ReadUInt(0) ?? 0;
        internal uint CurrentDestination => ReadUInt(1) ?? 0;
        internal List<Data> DestinationData => Loop<Data>(3, 4, 20);
        internal List<Names> DestinationName => Loop<Names>(259, 1, 20);

        internal unsafe class Names(nint UnitBasePtr, int BeginOffset = 0) : AtkReader(UnitBasePtr, BeginOffset)
        {
            internal string Name => ReadString(0);
        }

        internal unsafe class Data(nint UnitBasePtr, int BeginOffset = 0) : AtkReader(UnitBasePtr, BeginOffset)
        {
            internal uint Type => ReadUInt(0).Value;
            internal uint State => ReadUInt(1).Value;
            internal uint IconID => ReadUInt(2).Value;
            internal uint CallbackData => ReadUInt(3).Value;
        }
    }
}
