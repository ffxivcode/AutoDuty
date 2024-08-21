using ECommons;
using ECommons.DalamudServices;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AutoDuty.Helpers
{
    public static class RepairNPCHelper
    {
        public class ENpcResidentData
        {
            public uint DataId { get; set; }
            public string Name { get; set; } = string.Empty;
            public uint TerritoryType { get; set; }
            public Vector3 Position { get; set; }
        }

        public class RepairNpcData : ENpcResidentData
        {
            public int RepairIndex { get; set; }
        }

        internal static List<RepairNpcData> RepairNPCs = [];

        private static List<ENpcResidentData> cityENpcResidents = [];

        internal static void PopulateRepairNPCs()
        {
            var enpcResidentsSheet = Svc.Data.GetExcelSheet<ENpcResident>();
            var enpcBaseSheet = Svc.Data.GetExcelSheet<ENpcBase>();
            var levelSheet = Svc.Data.GetExcelSheet<Level>();
            var menderENpcResidents = enpcResidentsSheet?.ToList().Where(x => x.RowId != 1025308 && (x.Singular.RawString.Contains("mender", StringComparison.InvariantCultureIgnoreCase) || x.Title.RawString.Contains("mender", StringComparison.InvariantCultureIgnoreCase) || x.Title.RawString.Contains("Repairman", StringComparison.InvariantCultureIgnoreCase)));
            var menderENpcBases = enpcBaseSheet?.ToList();

            var cityAreaTerritoryTypes = Svc.Data.GetExcelSheet<TerritoryType>()?.ToList().Where(x => x.TerritoryIntendedUse == 0);

            if (menderENpcResidents == null || cityAreaTerritoryTypes == null)
                return;

            BuildEnpcFromLgbFile(cityAreaTerritoryTypes);

            foreach (var mender in menderENpcResidents)
            {
                var level = levelSheet?.FirstOrDefault(y => y.Object == mender.RowId);
                var eNpcBaseENpcData = menderENpcBases?.FirstOrDefault(z => z.RowId == mender.RowId)?.ENpcData;
                var repairIndex = eNpcBaseENpcData.IndexOf(x => x == 720915);
                uint territoryType = 0;
                Vector3 position = Vector3.Zero;

                if (level == null || level.Territory.Value == null || level.Territory.Value.TerritoryIntendedUse != 0)
                {
                    var npc = cityENpcResidents.FirstOrDefault(x => x.DataId == mender.RowId);
                    if (npc == null) continue;
                    RepairNPCs.Add(new RepairNpcData
                    {
                        DataId = npc.DataId,
                        Name = npc.Name,
                        Position = npc.Position,
                        TerritoryType = npc.TerritoryType,
                        RepairIndex = repairIndex
                    });
                }
                else
                {
                    position = new Vector3(level.X, level.Y, level.Z);
                    territoryType = level.Territory.Value.RowId;
                    RepairNPCs.Add(new RepairNpcData
                    {
                        DataId = mender.RowId,
                        Name = mender.Singular.RawString,
                        Position = position,
                        TerritoryType = territoryType,
                        RepairIndex = repairIndex
                    });
                }
            }
            cityENpcResidents = [];
        }

        private static void BuildEnpcFromLgbFile(IEnumerable<TerritoryType> territoryTypes)
        {
            foreach (var territoryType in territoryTypes)
            {
                var lgbFile = GetLgbFile(territoryType);

                if (lgbFile == null) continue;

                foreach (var sLgbGroup in lgbFile.Layers)
                {
                    foreach (var instanceObject in sLgbGroup.InstanceObjects)
                    {
                        if (instanceObject.AssetType != LayerEntryType.EventNPC)  continue;
                    
                        var eNPCInstanceObject = (LayerCommon.ENPCInstanceObject)instanceObject.Object;
                        var eNpcResidentDataId = eNPCInstanceObject.ParentData.ParentData.BaseId;
                        
                        if (eNpcResidentDataId == 0) continue;

                        var eNpcResident = Svc.Data.GetExcelSheet<ENpcResident>()?.GetRow(eNpcResidentDataId);
                        var eNpcBase = Svc.Data.GetExcelSheet<ENpcBase>()?.GetRow(eNpcResidentDataId);
                        
                        if (eNpcBase == null || eNpcResident == null) continue;

                        cityENpcResidents.Add(new ENpcResidentData
                        {
                            DataId = eNpcResidentDataId,
                            Name = eNpcResident.Singular.RawString,
                            Position = new Vector3(instanceObject.Transform.Translation.X, instanceObject.Transform.Translation.Y, instanceObject.Transform.Translation.Z),
                            TerritoryType = territoryType.RowId,
                        });
                    }
                }
            }
            
        }
        private static LgbFile? GetLgbFile(TerritoryType territoryType) => Svc.Data.GetFile<LgbFile>($"bg/{territoryType.Bg.RawString[..(territoryType.Bg.RawString.IndexOf("/level/") + 1)]}level/planevent.lgb");
    }
}
