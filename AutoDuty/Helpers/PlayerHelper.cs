using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System.Linq;

namespace AutoDuty.Helpers
{
    internal static class PlayerHelper
    {
        internal static unsafe short GetCurrentLevelFromSheet(Job? job = null)
        {
            PlayerState* playerState = PlayerState.Instance();
            return playerState->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>()?.GetRow((uint) (job ?? AutoDuty.Plugin.Player?.GetJob() ?? AutoDuty.Plugin.JobLastKnown))?.ExpArrayIndex ?? 0];
        }

        internal static unsafe short GetCurrentItemLevelFromGearSet(int gearsetId = -1, bool updateGearsetBeforeCheck = true)
        {
            RaptureGearsetModule* gearsetModule = RaptureGearsetModule.Instance();
            if (gearsetId < 0)
                gearsetId = gearsetModule->CurrentGearsetIndex;
            if (updateGearsetBeforeCheck)
                gearsetModule->UpdateGearset(gearsetId);
            return gearsetModule->GetGearset(gearsetId)->ItemLevel;
        }

        internal static CombatRole GetRole(this Job? job) => 
            job != null ? GetRole((Job)job) : CombatRole.NonCombat;

        internal static CombatRole GetRole(this Job job)
        {
            return job switch
            {
                Job.GLA or Job.PLD or Job.MRD or Job.WAR or Job.DRK or Job.GNB => CombatRole.Tank,
                Job.CNJ or Job.WHM or Job.SGE or Job.SCH or Job.AST => CombatRole.Healer,
                Job.PGL or Job.MNK or Job.LNC or Job.DRG or Job.SAM or Job.RPR or Job.BRD or Job.DNC or Job.MCH or Job.ROG or Job.NIN or Job.THM or Job.BLM or Job.ARC or Job.SMN or Job.RDM or Job.BLU => CombatRole.DPS,
                _ => CombatRole.NonCombat,
            };
        }

        internal static bool HasStatus(uint statusID) => Svc.ClientState.LocalPlayer != null && Player.Object.StatusList.Any(x => x.StatusId == statusID);
    }
}
