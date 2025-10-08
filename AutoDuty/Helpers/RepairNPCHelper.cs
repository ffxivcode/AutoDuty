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
    using Serilog;

    public static class RepairNPCHelper
    {
        public class ENpcResidentData
        {
            public uint DataId { get; set; }
            public string Name { get; set; } = string.Empty;
            public uint TerritoryType { get; set; }
            public Vector3 Position { get; set; }

            public ENpcBase?     NPCBase     { get; set; }
            public ENpcResident? NPCResident { get; set; }
        }

        public class RepairNpcData : ENpcResidentData
        {
            public int RepairIndex { get; set; }
        }

        internal static List<RepairNpcData> RepairNPCs = [];

        private static List<ENpcResidentData> cityENpcResidents = [];

        internal static void PopulateRepairNPCs()
        {
            List<TerritoryType>        territories            = Svc.Data.GetExcelSheet<TerritoryType>().ToList();
            IEnumerable<TerritoryType> cityAreaTerritoryTypes = territories.Where(x => x.TerritoryIntendedUse.Value.RowId == 0);
                                                                                            //Sinus   Phaenna         Apartment Lobbies
            BuildEnpcFromLgbFile(territories.Where(t => t.RowId is not (1237 or 1291 or (573 or 574 or 575 or 654 or 985))));

            foreach (ENpcResidentData npc in cityENpcResidents)
            {
                if(RepairNPCs.Any(rnd => rnd.DataId == npc.DataId))
                    continue;

                int repairIndex = npc.NPCBase!.Value.ENpcData.IndexOf(x => x.RowId == 720915);
                if (repairIndex >= 0)
                    RepairNPCs.Add(new RepairNpcData
                                   {
                                       DataId        = npc.DataId,
                                       Name          = npc.Name,
                                       Position      = npc.Position,
                                       TerritoryType = npc.TerritoryType,
                                       RepairIndex   = repairIndex
                                   });
            }
            
            RepairNPCs.Sort((first, second) => 
                            {
                                int cityFirst  = cityAreaTerritoryTypes.IndexOf(t => t.RowId == first.TerritoryType);
                                int citySecond = cityAreaTerritoryTypes.IndexOf(t => t.RowId == second.TerritoryType);

                                long scoreFirst  = (cityFirst  < 0 ? 5000 : cityFirst)  + first.TerritoryType;
                                long scoreSecond = (citySecond < 0 ? 5000 : citySecond) + second.TerritoryType;

                                return scoreFirst.CompareTo(scoreSecond);
                            });
            
            cityENpcResidents = [];
        }

        private static void BuildEnpcFromLgbFile(IEnumerable<TerritoryType> territoryTypes)
        {
            foreach (TerritoryType territoryType in territoryTypes)
            {
                LgbFile? lgbFile = GetLgbFile(territoryType);

                if (lgbFile == null) continue;

                foreach (LayerCommon.Layer sLgbGroup in lgbFile.Layers)
                {
                    foreach (LayerCommon.InstanceObject instanceObject in sLgbGroup.InstanceObjects)
                    {
                        if (instanceObject.AssetType != LayerEntryType.EventNPC)  
                            continue;
                    
                        LayerCommon.ENPCInstanceObject eNPCInstanceObject = (LayerCommon.ENPCInstanceObject)instanceObject.Object;
                        uint eNpcResidentDataId = eNPCInstanceObject.ParentData.ParentData.BaseId;
                        
                        if (eNpcResidentDataId == 0) 
                            continue;

                        ENpcResident? eNpcResident = Svc.Data.GetExcelSheet<ENpcResident>()?.GetRow(eNpcResidentDataId);
                        ENpcBase?     eNpcBase     = Svc.Data.GetExcelSheet<ENpcBase>()?.GetRow(eNpcResidentDataId);
                        
                        if (eNpcBase == null || eNpcResident == null) 
                            continue;

                        cityENpcResidents.Add(new ENpcResidentData
                        {
                            DataId = eNpcResidentDataId,
                            Name = eNpcResident.Value.Singular.ToString(),
                            Position = new Vector3(instanceObject.Transform.Translation.X, instanceObject.Transform.Translation.Y, instanceObject.Transform.Translation.Z),
                            TerritoryType = territoryType.RowId,
                            NPCBase = eNpcBase.Value,
                            NPCResident = eNpcResident.Value
                        });
                    }
                }
            }
            
        }
        private static LgbFile? GetLgbFile(TerritoryType territoryType) => Svc.Data.GetFile<LgbFile>($"bg/{territoryType.Bg.ToString()[..(territoryType.Bg.ToString().IndexOf("/level/") + 1)]}level/planevent.lgb");
    }
}
