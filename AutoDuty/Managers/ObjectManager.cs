using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.GameFunctions;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System;

namespace AutoDuty.Managers
{
    internal static class ObjectManager
    {
        internal static List<GameObject> GetObjectsByRadius(List<GameObject> gameObjects, float radius) => gameObjects.Where(o => GetDistanceToPlayer(o) <= radius).ToList();

        internal static List<GameObject> GetObjectsByName(List<GameObject> gameObjects, string name) => gameObjects.Where(o => o.Name.ToString().ToUpper() == name.ToUpper()).ToList();

        internal static GameObject? GetClosestObjectByName(List<GameObject> gameObjects, string name) => gameObjects.OrderBy(GetDistanceToPlayer).FirstOrDefault(p => p.Name.ToString().ToUpper().Equals(name.ToUpper()) && p.IsTargetable);

        internal unsafe static float GetDistanceToPlayer(GameObject gameObject) => Vector3.Distance(gameObject.Position, Player.GameObject->Position);

        //RotationSolver
        internal unsafe static float GetBattleDistanceToPlayer(GameObject gameObject)
        {
            if (gameObject == null) return float.MaxValue;
            var player = Player.Object;
            if (player == null) return float.MaxValue;

            var distance = Vector3.Distance(player.Position, gameObject.Position) - player.HitboxRadius;
            distance -= gameObject.HitboxRadius;
            return distance;
        }

        internal static BNpcBase? GetObjectNPC(GameObject gameObject) => GetSheet<BNpcBase>()?.GetRow(gameObject.DataId) ?? null;

        internal static ExcelSheet<T>? GetSheet<T>() where T : ExcelRow => Svc.Data.GetExcelSheet<T>();

        //From RotationSolver
        internal static bool IsBossFromIcon(BattleChara battleChara)
        {
            if (battleChara == null) return false;

            //Icon
            if (GetObjectNPC(battleChara)?.Rank is 1 or 2 /*or 4*/ or 6) return true;

            return false;
        }
        internal static float JobRange
        {
            get
            {
                float radius = 15;
                if (!Player.Available) return radius;
                switch (Svc.Data.GetExcelSheet<ClassJob>()?.GetRow(
                    Player.Object.ClassJob.Id)?.GetJobRole() ?? JobRole.None)
                {
                    case JobRole.Tank:
                    case JobRole.Melee:
                        radius = 3;
                        break;
                }
                return radius;
            }
        }

        internal static JobRole GetJobRole(this ClassJob job)
        {
            var role = (JobRole)job.Role;

            if (role is JobRole.Ranged or JobRole.None)
            {
                role = job.ClassJobCategory.Row switch
                {
                    30 => JobRole.RangedPhysical,
                    31 => JobRole.RangedMagical,
                    32 => JobRole.DiscipleOfTheLand,
                    33 => JobRole.DiscipleOfTheHand,
                    _ => JobRole.None,
                };
            }
            return role;
        }

        /// <summary>
        /// The role of jobs.
        /// </summary>
        internal enum JobRole : byte
        {
            /// <summary>
            /// 
            /// </summary>
            None = 0,

            /// <summary>
            /// 
            /// </summary>
            Tank = 1,

            /// <summary>
            /// 
            /// </summary>
            Melee = 2,

            /// <summary>
            /// 
            /// </summary>
            Ranged = 3,

            /// <summary>
            /// 
            /// </summary>
            Healer = 4,

            /// <summary>
            /// 
            /// </summary>
            RangedPhysical = 5,

            /// <summary>
            /// 
            /// </summary>
            RangedMagical = 6,

            /// <summary>
            /// 
            /// </summary>
            DiscipleOfTheLand = 7,

            /// <summary>
            /// 
            /// </summary>
            DiscipleOfTheHand = 8,
        }

        internal static unsafe bool BetweenAreas => Svc.Condition.Any()
        && Svc.Condition[ConditionFlag.BetweenAreas]
        && Svc.Condition[ConditionFlag.BetweenAreas51];

        internal static unsafe bool IsValid => Svc.Condition.Any()
        && !Svc.Condition[ConditionFlag.BetweenAreas]
        && !Svc.Condition[ConditionFlag.BetweenAreas51]
        && Player.Available
        && Player.Interactable;

        internal static unsafe bool InCombat(this BattleChara battleChara) => battleChara.Struct()->Character.InCombat;
        
        internal static unsafe void InteractWithObject(GameObject gameObject)
        {
            try
            {
                if (gameObject == null || !gameObject.IsTargetable) return;
                var gameObjectPointer = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
                TargetSystem.Instance()->InteractWithObject(gameObjectPointer, true);
            }
            catch (Exception ex)
            {
                //Svc.Log.Error(ex.ToString());
            }
        }

        internal static unsafe bool PlayerIsCasting => Player.Character->IsCasting;
    }
}
