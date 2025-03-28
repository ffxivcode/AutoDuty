using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoDuty.Helpers
{
    using System;
    using FFXIVClientStructs.FFXIV.Client.Game.Control;
    using Lumina.Excel.Sheets;

    internal static class PlayerHelper
    {
        internal static unsafe uint GetGrandCompanyTerritoryType(uint grandCompany) => grandCompany switch
        {
            1 => 128u,
            2 => 132u,
            _ => 130u
        };

        internal static unsafe uint GetGrandCompany() => UIState.Instance()->PlayerState.GrandCompany;

        internal static unsafe uint GetGrandCompanyRank() => UIState.Instance()->PlayerState.GetGrandCompanyRank();

        internal static uint GetMaxDesynthLevel() => Svc.Data.Excel.GetSheet<Item>().Where(x => x.Desynth > 0).OrderBy(x => x.LevelItem.RowId).LastOrDefault().LevelItem.RowId;

        internal static unsafe float GetDesynthLevel(uint classJobId) => PlayerState.Instance()->GetDesynthesisLevel(classJobId);

        internal static unsafe short GetCurrentLevelFromSheet(Job? job = null)
        {
            PlayerState* playerState = PlayerState.Instance();
            return playerState->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>().GetRowOrDefault((uint)(job ?? GetJob()))?.ExpArrayIndex ?? 0];
        }

        internal static float JobRange
        {
            get
            {
                float radius = 25;
                if (!Player.Available) 
                    return radius;
                radius = (Svc.Data.GetExcelSheet<ClassJob>().GetRowOrDefault(Player.Object.ClassJob.RowId)?.GetJobRole() ?? JobRole.None) switch
                {
                    JobRole.Tank or JobRole.Melee => 2.6f,
                    _ => radius
                };
                return radius;
            }
        }

        internal static float AoEJobRange
        {
            get
            {
                float radius = 10;
                if (!Player.Available) return radius;
                radius = (Svc.Data.GetExcelSheet<ClassJob>().GetRowOrDefault(Player.Object.ClassJob.RowId)?.GetJobRole() ?? JobRole.None) switch
                {
                    JobRole.Tank or JobRole.Melee => 2.6f,
                    _ => radius
                };

                if (Player.Object.ClassJob.RowId == 38)
                    radius = 3;
                return radius;
            }
        }

        internal static JobRole GetJobRole(this ClassJob job)
        {
            var role = (JobRole)job.Role;

            if (role is JobRole.Ranged or JobRole.None)
            {
                role = job.ClassJobCategory.RowId switch
                {
                    30 => JobRole.Ranged_Physical,
                    31 => JobRole.Ranged_Magical,
                    32 => JobRole.Disciple_Of_The_Land,
                    33 => JobRole.Disciple_Of_The_Hand,
                    _ => JobRole.None,
                };
            }
            return role;
        }

        internal static unsafe bool IsValid =>
            Control.GetLocalPlayer() != null
         && Environment.CurrentManagedThreadId == 1
         && Svc.Condition.Any()
         && !Svc.Condition[ConditionFlag.BetweenAreas]
         && !Svc.Condition[ConditionFlag.BetweenAreas51]
         && Player.Available
         && Player.Interactable;

        internal static bool IsJumping => Svc.Condition.Any()
        && (Svc.Condition[ConditionFlag.Jumping]
        || Svc.Condition[ConditionFlag.Jumping61]);

        internal static unsafe bool IsAnimationLocked => ActionManager.Instance()->AnimationLock > 0;

        internal static bool IsReady => IsValid && !IsOccupied;

        internal static bool IsOccupied => GenericHelpers.IsOccupied() || Svc.Condition[ConditionFlag.Jumping61];

        internal static bool IsReadyFull => IsValid && !IsOccupiedFull;

        internal static bool IsOccupiedFull => IsOccupied || IsAnimationLocked;

        internal static unsafe bool IsCasting => Player.Character->IsCasting;

        internal static unsafe bool IsMoving => AgentMap.Instance()->IsPlayerMoving;

        internal static unsafe bool InCombat => Svc.Condition[ConditionFlag.InCombat];

        /*internal static unsafe short GetCurrentItemLevelFromGearSet(int gearsetId = -1, bool updateGearsetBeforeCheck = true)
        {
            RaptureGearsetModule* gearsetModule = RaptureGearsetModule.Instance();
            if (gearsetId < 0)
                gearsetId = gearsetModule->CurrentGearsetIndex;
            if (updateGearsetBeforeCheck)
                gearsetModule->UpdateGearset(gearsetId);
            return gearsetModule->GetGearset(gearsetId)->ItemLevel;
        }*/

        internal static Job GetJob() => Player.Available ? Player.Job : Plugin.JobLastKnown;

        internal static CombatRole GetCombatRole(this Job? job) => 
            job != null ? GetCombatRole((Job)job) : CombatRole.NonCombat;

        internal static CombatRole GetCombatRole(this Job job)
        {
            return job switch
            {
                Job.GLA or Job.PLD or Job.MRD or Job.WAR or Job.DRK or Job.GNB => CombatRole.Tank,
                Job.CNJ or Job.WHM or Job.SGE or Job.SCH or Job.AST => CombatRole.Healer,
                Job.PGL or Job.MNK or Job.LNC or Job.DRG or Job.ROG or Job.NIN or Job.SAM or Job.RPR or Job.VPR or 
                    Job.ARC or Job.BRD or Job.DNC or Job.MCH or
                    Job.THM or Job.BLM or Job.ACN or Job.SMN or Job.RDM or Job.PCT or Job.BLU => CombatRole.DPS,
                _ => CombatRole.NonCombat,
            };
        }

        internal static bool HasStatus(uint statusID, float minTime = 0) => Svc.ClientState.LocalPlayer != null && Player.Object.StatusList.Any(x => x.StatusId == statusID && (minTime <= 0 || x.RemainingTime > minTime));
    }
}
