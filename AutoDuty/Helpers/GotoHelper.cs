using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Collections.Generic;
using System.Numerics;
using Lumina.Excel.GeneratedSheets;
using AutoDuty.IPC;

namespace AutoDuty.Helpers
{
    internal unsafe static class GotoHelper
    {
        internal static void Invoke(uint territoryType, List<Vector3> moveLocations, float tollerance = 0.25f, float lastPointTollerance = 0.25f)
        {
            if (!GotoRunning)
            {
                Svc.Log.Info($"Goto Started, Going to {territoryType} and moving to {moveLocations[moveLocations.Count - 1]} using {moveLocations.Count} pathLocations");
                GotoRunning = true;
                _territoryType = territoryType;
                _moveLocations = moveLocations;
                _tollerance = tollerance;
                _lastPointTollerance = lastPointTollerance;
                Svc.Framework.Update += GotoUpdate;
            }
        }

        internal static void Stop() 
        {
            if (GotoRunning)
                Svc.Log.Info($"Goto Finished");
            Svc.Framework.Update -= GotoUpdate;
            GotoRunning = false;
            _territoryType = 0;
            _moveLocations = [];
            _locationIndex = 0;
            _tollerance = 0.25f;
            _lastPointTollerance = 0.25f;
            AutoDuty.Plugin.Action = "";
        }

        internal static bool GotoRunning = false;

        private static uint _territoryType = 0;
        private static List<Vector3> _moveLocations = [];
        private static int _locationIndex = 0;
        private static float _tollerance = 0.25f;
        private static float _lastPointTollerance = 0.25f;

        internal static unsafe void GotoUpdate(IFramework framework)
        {
            
            if (!EzThrottler.Check("Goto"))
                return;

            EzThrottler.Throttle("Goto", 50);

            if (Svc.ClientState.LocalPlayer == null)
                return;

            if (!ObjectHelper.IsValid || ObjectHelper.PlayerIsCasting || ObjectHelper.IsJumping || !VNavmesh_IPCSubscriber.Nav_IsReady())
                return;

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
               
                Aetheryte? aetheryte = MapHelper.GetClosestAetheryte(_territoryType, _moveLocations[0]);
                if (aetheryte == null)
                {
                    aetheryte = MapHelper.GetClosestAethernet(_territoryType, _moveLocations[0]);

                    if (aetheryte == null)
                    {
                        Svc.Log.Info($"We are unable to find the closest Aetheryte to: {_territoryType}, Most likely the zone does not have one");

                        Stop();
                        return;
                    }
                    else
                    {
                        if (Svc.ClientState.TerritoryType != MapHelper.GetAetheryteForAethernet(aetheryte)?.Territory.Value?.RowId)
                        {
                            TeleportHelper.TeleportAetheryte(MapHelper.GetAetheryteForAethernet(aetheryte)?.RowId ?? 0, 0);
                            EzThrottler.Throttle("Goto", 7500, true);
                        }
                        else
                        {
                            if (TeleportHelper.MoveToClosestAetheryte(_territoryType))
                                TeleportHelper.TeleportAethernet(aetheryte.AethernetName.Value?.Name ?? "", _territoryType);
                        }
                        return;
                    }
                }
                TeleportHelper.TeleportAetheryte(aetheryte?.RowId ?? 0, 0);
                EzThrottler.Throttle("Goto", 7500, true);
                return;
            }
            else
            {
                
                Aetheryte? aetheryteLoc = MapHelper.GetClosestAethernet(_territoryType, _moveLocations[0]);
                Aetheryte? aetheryteMe = MapHelper.GetClosestAethernet(_territoryType, Svc.ClientState.LocalPlayer.Position);
                Svc.Log.Info($"{aetheryteMe?.AethernetName.Value?.Name} : {aetheryteLoc?.AethernetName.Value?.Name}");
                if (aetheryteLoc != aetheryteMe)
                {
                    if (TeleportHelper.MoveToClosestAetheryte(_territoryType))
                    {
                        Svc.Log.Info("at");
                        TeleportHelper.TeleportAethernet(aetheryteLoc?.AethernetName.Value?.Name ?? "", _territoryType);
                    }
                    return;
                }
            }

            if (_locationIndex < _moveLocations.Count && ObjectHelper.IsReady)
            {
                if (MovementHelper.Move(_moveLocations[_locationIndex], _tollerance, (_locationIndex + 1) == _moveLocations.Count ? _lastPointTollerance : _tollerance))
                    _locationIndex++;
                return;
            }

            Stop();
        }
    }
}
