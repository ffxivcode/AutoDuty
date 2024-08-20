using ECommons.DalamudServices;
using Lumina.Data.Files;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static AutoDuty.Data.Enum;

namespace AutoDuty.Helpers
{
    public static class RepairNPCHelper
    {
        public class RepairNPC
        {
            public uint DataId { get; set; }
            public string Name { get; set; } = string.Empty;
            public uint TerritoryType { get; set; }
            public Vector3 Position { get; set; }
        }

        internal static List<RepairNPC> RepairNPCs = [];

        internal static void PopulateRepairNPCs()
        {
            var enpcResidentsSheet = Svc.Data.GetExcelSheet<ENpcResident>();
            var levelSheet = Svc.Data.GetExcelSheet<Level>();
            var menderEnpcResidents = enpcResidentsSheet?.ToList().Where(x => x.Singular.RawString.Contains("mender", StringComparison.InvariantCultureIgnoreCase) || x.Title.RawString.Contains("mender", StringComparison.InvariantCultureIgnoreCase));

            if (menderEnpcResidents == null)
                return;

            foreach (var mender in menderEnpcResidents)
            {
                var level = levelSheet?.FirstOrDefault(y => y.Object == mender.RowId);
                uint territoryType = 0;
                Vector3 position = Vector3.Zero;

                if (level == null || level.Territory.Value == null || level.Territory.Value.TerritoryIntendedUse != 0)
                {
                    //var lgb = GetLgbFile(mender.Map)
                }
                else
                {
                    position = new Vector3(level.X, level.Y, level.Z);
                    territoryType = level.Territory.Value.RowId;
                }

                RepairNPCs.Add(new RepairNPC
                {
                    DataId = mender.RowId,
                    Name = mender.Singular.RawString,
                    Position = position,
                    TerritoryType = territoryType,
                });
            }
        }

        private static LgbFile? GetLgbFile(TerritoryType territoryType) => Svc.Data.GetFile<LgbFile>($"bg/{territoryType.Bg.RawString[..(territoryType.Bg.RawString.IndexOf("/level/") + 1)]}level/planevent.lgb");
    }
}
