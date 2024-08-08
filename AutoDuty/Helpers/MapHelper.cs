using ECommons.DalamudServices;
using ECommons.MathHelpers;
using Lumina.Excel.GeneratedSheets;
using System.Numerics;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons.Throttlers;
using AutoDuty.IPC;
using FFXIVClientStructs.FFXIV.Client.Game;

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

        private static FlagMapMarker flagMapMarker = default;

        internal static void MoveToMapMarker()
        {
            Svc.Framework.Update += MoveToMapMarkerUpdate;
        }

        internal unsafe static void MoveToMapMarkerUpdate(IFramework _)
        {
            if (!EzThrottler.Throttle("MoveToMapMarker"))
                return;

            if (!ObjectHelper.IsReady)
                return;

            if (GotoHelper.GotoRunning)
                return;

            if (!GotoHelper.GotoRunning && Svc.ClientState.TerritoryType == flagMapMarker.TerritoryId)
            {
                if (!Conditions.IsMounted)
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 4);
                else if (!Conditions.IsInFlight)
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                else
                {
                    new ECommons.Automation.Chat().ExecuteCommand("/vnavmesh flyflag");
                    Svc.Framework.Update -= MoveToMapMarkerUpdate;
                }
                return;
            }

            if (IsFlagMarkerSet && flagMapMarker.Equals(default))
            {
                flagMapMarker = GetFlagMarker;
                GotoHelper.Invoke(flagMapMarker.TerritoryId, []);
            }
        }
    }
}
