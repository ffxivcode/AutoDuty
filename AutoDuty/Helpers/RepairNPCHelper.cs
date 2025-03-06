using ECommons;
using ECommons.DalamudServices;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AutoDuty.Helpers
{
    using Lumina.Excel.Sheets;

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
            // todo: use IDs instead of english string
            var menderENpcResidents = enpcResidentsSheet?.ToList().Where(x => x.RowId != 1025308 && (x.Singular.ToString().Contains("mender", StringComparison.InvariantCultureIgnoreCase) || x.Title.ToString().Contains("mender", StringComparison.InvariantCultureIgnoreCase) || x.Title.ToString().Contains("Repairman", StringComparison.InvariantCultureIgnoreCase)));
            var menderENpcBases = enpcBaseSheet?.ToList();

            var cityAreaTerritoryTypes = Svc.Data.GetExcelSheet<TerritoryType>().ToList().Where(x => x.TerritoryIntendedUse.Value.RowId == 0);

            if (menderENpcResidents == null)
                return;

            BuildEnpcFromLgbFile(cityAreaTerritoryTypes);

            foreach (ENpcBase npcBase in menderENpcBases)
            {
                int repairIndex = npcBase.ENpcData.IndexOf(x => x.RowId == 720915);
                if (repairIndex >= 0)
                {
                    Level   level         = levelSheet.FirstOrDefault(y => y.Object.RowId == npcBase.RowId);
                    Vector3 position      = Vector3.Zero;
                    uint    territoryType = 0;

                    if (level.RowId != default)
                    {
                        if (level.Territory.Value.RowId != 0 || level.Territory.Value.TerritoryIntendedUse.RowId != 0)
                        {
                            var npc = cityENpcResidents.FirstOrDefault(x => x.DataId == npcBase.RowId);
                            if (npc == null) 
                                continue;

                            RepairNPCs.Add(new RepairNpcData
                                           {
                                               DataId        = npc.DataId,
                                               Name          = npc.Name,
                                               Position      = npc.Position,
                                               TerritoryType = npc.TerritoryType,
                                               RepairIndex   = repairIndex
                                           });
                        }
                        else
                        {
                            var npc = enpcResidentsSheet.GetRow(npcBase.RowId);
                            position      = new Vector3(level.X, level.Y, level.Z);
                            territoryType = level.Territory.Value.RowId;
                            RepairNPCs.Add(new RepairNpcData
                                           {
                                               DataId        = npcBase.RowId,
                                               Name          = npc.Singular.ToString(),
                                               Position      = position,
                                               TerritoryType = territoryType,
                                               RepairIndex   = repairIndex
                                           });
                        }
                    }
                }
            }

            RepairNPCs.Sort((first, second) => first.TerritoryType.CompareTo(second.TerritoryType));
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
                            Name = eNpcResident.Value.Singular.ToString(),
                            Position = new Vector3(instanceObject.Transform.Translation.X, instanceObject.Transform.Translation.Y, instanceObject.Transform.Translation.Z),
                            TerritoryType = territoryType.RowId,
                        });
                    }
                }
            }
            
        }
        private static LgbFile? GetLgbFile(TerritoryType territoryType) => Svc.Data.GetFile<LgbFile>($"bg/{territoryType.Bg.ToString()[..(territoryType.Bg.ToString().IndexOf("/level/") + 1)]}level/planevent.lgb");
    }
}
