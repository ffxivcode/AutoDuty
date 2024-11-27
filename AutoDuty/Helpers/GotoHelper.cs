using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Collections.Generic;
using System.Numerics;
using AutoDuty.IPC;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.ClientState.Objects.Types;

namespace AutoDuty.Helpers
{
    using Lumina.Excel.Sheets;

    internal static class GotoHelper
    {
        internal static void Invoke(uint territoryType) => Invoke(territoryType, 0);

        internal static void Invoke(uint territoryType, uint gameObjectDataId) => Invoke(territoryType, [], gameObjectDataId, 0.25f, 0.25f, false, false, true);

        internal static void Invoke(uint territoryType, Vector3 moveLocation) => Invoke(territoryType, [moveLocation], 0, 0.25f, 0.25f, false, false, true);

        internal static void Invoke(uint territoryType, List<Vector3> moveLocations) => Invoke(territoryType, moveLocations, 0, 0.25f, 0.25f, false, false, true);

        internal static void Invoke(uint territoryType, uint gameObjectDataId, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool useAethernetTravel = false, bool useFlight = false, bool useMesh = true) => Invoke(territoryType, [], gameObjectDataId, tollerance, lastPointTollerance, useAethernetTravel, useFlight, useMesh);
        
        internal static void Invoke(uint territoryType, Vector3 moveLocation, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool useAethernetTravel = false, bool useFlight = false, bool useMesh = true) => Invoke(territoryType, [moveLocation], 0, tollerance, lastPointTollerance, useAethernetTravel, useFlight, useMesh);

        internal static void Invoke(uint territoryType, List<Vector3> moveLocations, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool useAethernetTravel = false, bool useFlight = false, bool useMesh = true) => Invoke(territoryType, moveLocations, 0, tollerance, lastPointTollerance, useAethernetTravel, useFlight, useMesh);

        internal static void Invoke(uint territoryType, List<Vector3> moveLocations, uint gameObjectDataId, float tollerance, float lastPointTollerance, bool useAethernetTravel, bool useFlight, bool useMesh)
        {
            if (State != ActionState.Running)
            {
                Svc.Log.Info($"Goto Started, Going to {territoryType}{(moveLocations.Count>0 ? $" and moving to {moveLocations[^1]} using {moveLocations.Count} pathLocations" : "")}");
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;
                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(false);
                _territoryType = territoryType;
                _gameObjectDataId = gameObjectDataId;
                _moveLocations = moveLocations;
                _tollerance = tollerance;
                _lastPointTollerance = lastPointTollerance;
                _useAethernetTravel = useAethernetTravel;
                _useFlight = useFlight;
                _useMesh = useMesh;
                Svc.Framework.Update += GotoUpdate;
            }
        }

        internal unsafe static void Stop() 
        {
            if (State == ActionState.Running)
                Svc.Log.Info($"Goto Finished");
            Svc.Framework.Update -= GotoUpdate;
            State = ActionState.None;
            Plugin.States &= ~PluginState.Other;
            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(true);
            _territoryType = 0;
            _gameObjectDataId = 0;
            _moveLocations = [];
            _locationIndex = 0;
            _tollerance = 0.25f;
            _lastPointTollerance = 0.25f;
            _useAethernetTravel = false;
            _useFlight = false;
            _useMesh = true;
            Plugin.Action = "";
            if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                addonSelectYesno->Close(true);
            if (VNavmesh_IPCSubscriber.IsEnabled && VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.Path_Stop();
        }

        internal static ActionState State = ActionState.None;

        private static uint _territoryType = 0;
        private static uint _gameObjectDataId = 0;
        private static List<Vector3> _moveLocations = [];
        private static int _locationIndex = 0;
        private static float _tollerance = 0.25f;
        private static float _lastPointTollerance = 0.25f;
        private static bool _useAethernetTravel = false;
        private static bool _useFlight = false;
        private static bool _useMesh = true;
        private static IGameObject? _gameObject => _gameObjectDataId > 0 ? ObjectHelper.GetObjectByDataId(_gameObjectDataId) : null;

        internal unsafe static void GotoUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating))
                Stop();

            if (!EzThrottler.Check("Goto"))
                return;

            EzThrottler.Throttle("Goto", 50);

            Plugin.Action = $"Going to {TerritoryName.GetTerritoryName(_territoryType)}{(_moveLocations.Count > 0 ? $" at {_moveLocations[^1]}" : "")}";

            if (Svc.ClientState.LocalPlayer == null)
                return;

            if (!PlayerHelper.IsValid || PlayerHelper.IsCasting || PlayerHelper.IsJumping || !VNavmesh_IPCSubscriber.Nav_IsReady())
                return;

            if (Plugin.InDungeon)
            {
                ExitDutyHelper.Invoke();
                return;
            }

            if (Svc.ClientState.TerritoryType != _territoryType)
            {
                uint which = _territoryType == 128 ? 1u : (_territoryType == 132 ? 2u : (_territoryType == 130 ? 3u : 0u));
                bool moveFromInnOrBarracks = _territoryType == 128 || _territoryType == 132 || _territoryType == 130;
                
                if (moveFromInnOrBarracks && (Svc.ClientState.TerritoryType == GotoBarracksHelper.BarracksTerritoryType(which) || Svc.ClientState.TerritoryType == GotoInnHelper.InnTerritoryType(which)))
                {
                    var exitGameObject = Svc.ClientState.TerritoryType == GotoBarracksHelper.BarracksTerritoryType(which) ? ObjectHelper.GetObjectByDataId(GotoBarracksHelper.ExitBarracksDoorDataId(which)) : ObjectHelper.GetObjectByDataId(GotoInnHelper.ExitInnDoorDataId(which));
                    if (MovementHelper.Move(exitGameObject, 0.25f, 3f))
                        if (ObjectHelper.InteractWithObjectUntilAddon(exitGameObject, "SelectYesno") != null)
                            AddonHelper.ClickSelectYesno();
                    return;
                }
               
                Aetheryte? aetheryte = MapHelper.GetClosestAetheryte(_territoryType, _moveLocations.Count > 0 ? _moveLocations[0] : Vector3.Zero);
                if (aetheryte == null)
                {
                    aetheryte = MapHelper.GetClosestAethernet(_territoryType, _moveLocations.Count > 0 ? _moveLocations[0] : Vector3.Zero);

                    if (aetheryte == null)
                    {
                        Svc.Log.Info($"We are unable to find the closest Aetheryte to: {_territoryType}, Most likely the zone does not have one");

                        Stop();
                        return;
                    }

                    if (Svc.ClientState.TerritoryType != MapHelper.GetAetheryteForAethernet(aetheryte.Value)?.Territory.ValueNullable?.RowId)
                    {
                        TeleportHelper.TeleportAetheryte(MapHelper.GetAetheryteForAethernet(aetheryte.Value)?.RowId ?? 0, 0);
                        EzThrottler.Throttle("Goto", 7500, true);
                    }
                    else
                    {
                        if (TeleportHelper.MoveToClosestAetheryte())
                            TeleportHelper.TeleportAethernet(aetheryte.Value.AethernetName.ValueNullable?.Name.ToString() ?? "", _territoryType);
                    }
                    return;
                }
                TeleportHelper.TeleportAetheryte(aetheryte?.RowId ?? 0, 0);
                EzThrottler.Throttle("Goto", 7500, true);
                return;
            }
            else if(_useAethernetTravel)
            {
                Aetheryte? aetheryteLoc = MapHelper.GetClosestAethernet(_territoryType, _moveLocations.Count > 0 ? _moveLocations[0] : Vector3.Zero);
                Aetheryte? aetheryteMe = MapHelper.GetClosestAethernet(_territoryType, Svc.ClientState.LocalPlayer.Position);

                if (aetheryteLoc?.RowId != aetheryteMe?.RowId)
                {
                    if (TeleportHelper.MoveToClosestAetheryte())
                    {
                        TeleportHelper.TeleportAethernet(aetheryteLoc?.AethernetName.ValueNullable?.Name.ToString() ?? "", _territoryType);
                    }
                    return;
                }
            }
            //Svc.Log.Info($"{_locationIndex < _moveLocations.Count} || ({_gameObject} != null && {ObjectHelper.GetDistanceToPlayer(_gameObject!)} > {_lastPointTollerance}) && {PlayerHelper.IsReady}");
            if (_locationIndex < _moveLocations.Count || (_gameObject != null && ObjectHelper.GetDistanceToPlayer(_gameObject) > _lastPointTollerance) && PlayerHelper.IsReady)
            {
                Vector3 moveLoc;
                float lastPointTollerance = _lastPointTollerance;
                if (_gameObject != null)
                    moveLoc = _gameObject.Position;
                else if (_locationIndex < _moveLocations.Count)
                {
                    moveLoc = _moveLocations[_locationIndex];
                    if (_locationIndex < (_moveLocations.Count - 1))
                        lastPointTollerance = _tollerance;
                }
                else
                    return;

                if (MovementHelper.Move(moveLoc, _tollerance, lastPointTollerance, _useFlight, _useMesh))
                    _locationIndex++;
                return;
            }

            Stop();
        }
    }
}
