using ECommons.DalamudServices;
using ECommons.MathHelpers;
using Lumina.Excel.GeneratedSheets;
using System.Numerics;
using System.Linq;

namespace AutoDuty.Helpers
{
    internal static class MapHelper
    {
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
    }
}
