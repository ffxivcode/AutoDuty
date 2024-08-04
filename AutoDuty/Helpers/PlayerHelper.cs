using ECommons.DalamudServices;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using ECommons.ExcelServices;
    using ECommons.GameFunctions;
    using ECommons.GameHelpers;
    using FFXIVClientStructs.FFXIV.Client.UI.Misc;
    using Lumina.Excel.GeneratedSheets;

    internal static class PlayerHelper
    {
        public static unsafe short GetCurrentLevelFromSheet(Job? job = null)
        {
            PlayerState* playerState = PlayerState.Instance();
            return playerState->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>()?.GetRow((uint) (job ?? AutoDuty.Plugin.Player.GetJob()))?.ExpArrayIndex ?? 0];
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

        public static CombatRole GetRole(this Job? job) => 
            job != null ? GetRole((Job)job) : CombatRole.NonCombat;

        public static CombatRole GetRole(this Job job)
        {
            if (ExcelItemHelper.Tanks.Contains(job))
                return CombatRole.Tank;
            if (ExcelItemHelper.Healers.Contains(job))
                return CombatRole.Healer;
            if (ExcelItemHelper.DexterityDPS.Contains(job) ||
                ExcelItemHelper.StrengthDPS.Contains(job)  ||
                ExcelItemHelper.MagicalDPS.Contains(job))
                return CombatRole.DPS;
            return CombatRole.NonCombat;
        }
    }
}
