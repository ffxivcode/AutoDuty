using ECommons.DalamudServices;
using ECommons.MathHelpers;
using Lumina.Excel.GeneratedSheets;
using System.Numerics;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons.Throttlers;
using AutoDuty.IPC;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal static class MapHelper
    {
        internal static unsafe bool IsFlagMarkerSet => AgentMap.Instance()->IsFlagMarkerSet > 0;
        
        internal static unsafe FlagMapMarker GetFlagMarker => AgentMap.Instance()->FlagMapMarker;

        internal static Vector2 ConvertWorldXZToMap(Vector2 coords, Map map) => Dalamud.Utility.MapUtil.WorldToMap(coords, map.OffsetX, map.OffsetY, map.SizeFactor);

        internal static Vector2 ConvertMarkerToMap(MapMarker mapMarker, Map map) => new((float)(mapMarker.X * 42.0 / 2048 / map.SizeFactor * 100 + 1), (float)(mapMarker.Y * 42.0 / 2048 / map.SizeFactor * 100 + 1));

        internal static Aetheryte? GetAetheryteForAethernet(Aetheryte aetheryte) => Svc.Data.GetExcelSheet<Aetheryte>()?.FirstOrDefault(x => x.IsAetheryte == true && x.AethernetGroup == aetheryte.AethernetGroup);

        internal static Aetheryte? GetClosestAethernet(uint territoryType, Vector3 location)
        {
            var closestDistance = float.MaxValue;
            Aetheryte? closestAetheryte = null;
            var map = Svc.Data.GetExcelSheet<TerritoryType>()?.GetRow(territoryType)?.Map.Value;
            var aetherytes = Svc.Data.GetExcelSheet<Aetheryte>();

            if (aetherytes == null || map == null)
                return null;

            foreach (var aetheryte in aetherytes)
            {
                if (( aetheryte.IsAetheryte && aetheryte.Territory.Row != territoryType ) || aetheryte.Territory.Value == null || aetheryte.Territory.Value.RowId != territoryType) continue;
                var mapMarker = Svc.Data.GetExcelSheet<MapMarker>()?.FirstOrDefault(m => m.DataType == 4 && m.DataKey == aetheryte.AethernetName.Value?.RowId);

                if (mapMarker == null) continue;
                var distance = Vector2.Distance(ConvertWorldXZToMap(location.ToVector2(), map), ConvertMarkerToMap(mapMarker, map));

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestAetheryte = aetheryte;
                }
            }

            return closestAetheryte;
        }

        internal static Aetheryte? GetClosestAetheryte(uint territoryType, Vector3 location)
        {
            var closestDistance = float.MaxValue;
            Aetheryte? closestAetheryte = null;
            var map = Svc.Data.GetExcelSheet<TerritoryType>()?.GetRow(territoryType)?.Map.Value;
            var aetherytes = Svc.Data.GetExcelSheet<Aetheryte>();

            if (aetherytes == null || map == null)
                return null;

            foreach (var aetheryte in aetherytes)
            {
                if (!aetheryte.IsAetheryte || aetheryte.Territory.Value == null || aetheryte.Territory.Value.RowId != territoryType || aetheryte.PlaceName.Value == null) continue;

                var mapMarker = Svc.Data.GetExcelSheet<MapMarker>()?.FirstOrDefault(m => m.DataType == 3 && m.DataKey == aetheryte.RowId);

                if (mapMarker == null) continue;

                var distance = Vector2.Distance(ConvertWorldXZToMap(location.ToVector2(), map), ConvertMarkerToMap(mapMarker, map));

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestAetheryte = aetheryte;
                }
            }

            return closestAetheryte;
        }

        internal static void MoveToMapMarker()
        {
            if (!IsFlagMarkerSet)
            {
                Svc.Log.Info("There is no flag marker set");
                return;
            }
            Svc.Log.Info("Moving to Flag Marker");
            MoveToMapMarkerRunning = true;
            if(!AutoDuty.Plugin.States.HasFlag(State.Other))
                AutoDuty.Plugin.States |= State.Other;
            Svc.Framework.Update += MoveToMapMarkerUpdate;
        }

        internal static bool MoveToMapMarkerRunning = false;

        private static Vector3? flagMapMarkerVector3 = Vector3.Zero;
        private static FlagMapMarker? flagMapMarker = null;

        internal unsafe static void StopMoveToMapMarker()
        {
            Svc.Framework.Update -= MoveToMapMarkerUpdate;
            VNavmesh_IPCSubscriber.Path_Stop();
            MoveToMapMarkerRunning = false;
            AutoDuty.Plugin.States -= State.Other;
            flagMapMarker = null;
        }

        internal unsafe static void MoveToMapMarkerUpdate(IFramework _)
        {
            if (!EzThrottler.Throttle("MoveToMapMarker"))
                return;

            if (!ObjectHelper.IsReady)
                return;

            if (flagMapMarker != null && Svc.ClientState.TerritoryType == flagMapMarker.Value.TerritoryId && flagMapMarkerVector3 != null && flagMapMarkerVector3.Value.Y == 0)
            {
                flagMapMarkerVector3 = VNavmesh_IPCSubscriber.Query_Mesh_PointOnFloor(new(flagMapMarker.Value.XFloat, 1024, flagMapMarker.Value.YFloat), false, 5);
                GotoHelper.Stop();
                GotoHelper.Invoke(flagMapMarker.Value.TerritoryId, [flagMapMarkerVector3.Value], 0.25f, 0.25f, false, MovementHelper.IsFlyingSupported);
                return;
            }

            if (GotoHelper.GotoRunning)
                return;

            if (VNavmesh_IPCSubscriber.Path_IsRunning())
                return;

            if (GenericHelpers.TryGetAddonByName("AreaMap", out AtkUnitBase* addonAreaMap) && GenericHelpers.IsAddonReady(addonAreaMap))
                addonAreaMap->Close(true);

            if (flagMapMarker !=null && Svc.ClientState.TerritoryType == flagMapMarker.Value.TerritoryId && ObjectHelper.GetDistanceToPlayer(flagMapMarkerVector3!.Value) < 2)
            {
                StopMoveToMapMarker();
                GotoHelper.Stop();
                return;
            }

            if (IsFlagMarkerSet)
            {
                flagMapMarker = GetFlagMarker;
                flagMapMarkerVector3 = new Vector3(flagMapMarker.Value.XFloat, 0, flagMapMarker.Value.YFloat);
                GotoHelper.Invoke(flagMapMarker.Value.TerritoryId, [flagMapMarkerVector3.Value], 0.25f, 0.25f, false, MovementHelper.IsFlyingSupported);
            }
        }
    }
}
