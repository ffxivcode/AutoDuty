using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Throttlers;
using AutoDuty.IPC;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoDuty.Helpers
{
    using FFXIVClientStructs.FFXIV.Client.Game.Object;
    using Lumina.Excel.Sheets;
    using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

    internal static class ObjectHelper
    {
        internal static bool TryGetObjectByDataId(uint dataId, out IGameObject? gameObject) => (gameObject = Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(x => x.DataId == dataId)) != null;
        internal static bool TryGetObjectByDataId(uint dataId, Func<IGameObject, bool> condition, out IGameObject? gameObject) => (gameObject = Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(x => x.DataId == dataId && condition(x))) != null;

        internal static List<IGameObject>? GetObjectsByObjectKind(ObjectKind objectKind) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(o => o.ObjectKind == objectKind)];

        internal static IGameObject? GetObjectByObjectKind(ObjectKind objectKind) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => o.ObjectKind == objectKind);

        internal static List<IGameObject>? GetObjectsByRadius(float radius) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(o => GetDistanceToPlayer(o) <= radius)];

        internal static IGameObject? GetObjectByRadius(float radius) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => GetDistanceToPlayer(o) <= radius);

        internal static List<IGameObject>? GetObjectsByName(string name) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(o => o.Name.TextValue.Equals(name, StringComparison.CurrentCultureIgnoreCase))];

        internal static IGameObject? GetObjectByName(string name) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => o.Name.TextValue.Equals(name, StringComparison.CurrentCultureIgnoreCase));

        internal static IGameObject? GetObjectByDataId(uint id) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => o.DataId == id);

        internal static List<IGameObject>? GetObjectsByPartialName(string name) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(o => o.Name.TextValue.Contains(name, StringComparison.CurrentCultureIgnoreCase))];

        internal static IGameObject? GetObjectByPartialName(string name) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(o => o.Name.TextValue.Contains(name, StringComparison.CurrentCultureIgnoreCase));

        internal static List<IGameObject>? GetObjectsByNameAndRadius(string objectName) => [.. Svc.Objects.OrderBy(GetDistanceToPlayer).Where(g => g.Name.TextValue.Equals(objectName, StringComparison.CurrentCultureIgnoreCase) && Vector3.Distance(Player.Object.Position, g.Position) <= 10)];

        internal static IGameObject? GetObjectByNameAndRadius(string objectName) => Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(g => g.Name.TextValue.Equals(objectName, StringComparison.CurrentCultureIgnoreCase) && Vector3.Distance(Player.Object.Position, g.Position) <= 10);

        internal static IBattleChara? GetBossObject(int radius = 100) => GetObjectsByRadius(radius)?.OfType<IBattleChara>().FirstOrDefault(b => IsBossFromIcon(b) || BossMod_IPCSubscriber.HasModuleByDataId(b.DataId));

        internal static unsafe float GetDistanceToPlayer(IGameObject gameObject) => GetDistanceToPlayer(gameObject.Position);

        internal static unsafe float GetDistanceToPlayer(Vector3 v3) => Vector3.Distance(v3, Player.GameObject->Position);

        internal static unsafe bool BelowDistanceToPlayer(Vector3 v3, float maxDistance, float maxHeightDistance) => BelowDistanceToPoint(v3, Player.GameObject->Position, maxDistance, maxHeightDistance);

        internal static bool BelowDistanceToPoint(Vector3 target, Vector3 origin, float maxDistance, float maxHeightDistance) => Vector3.Distance(target, origin) < maxDistance &&
                                                                                                                     MathF.Abs(target.Y - origin.Y) < maxHeightDistance;
        /// <summary>
        ///     Converts a GameObject pointer to an IGameObject from the object table.
        /// </summary>
        /// <param name="ptr">The GameObject pointer to convert.</param>
        /// <returns>An IGameObject if found in the object table; otherwise, null.</returns>
        public static unsafe IGameObject? GetObjectFrom(GameObject* ptr) =>
            ptr == null ? null : Svc.Objects.FirstOrDefault(x => x.Address == (IntPtr)ptr);

        internal static unsafe IGameObject? GetPartyMemberFromRole(string role)
        {
            if (Player.Object != null && Player.Object.ClassJob.Value.GetJobRole().ToString().Contains(role, StringComparison.InvariantCultureIgnoreCase))
            {
                return Player.Object;
            }

            if (Svc.Party.PartyId != 0)
            {
                return Svc.Party.FirstOrDefault(x => x.ClassJob.Value.GetJobRole().ToString().Contains(role, StringComparison.InvariantCultureIgnoreCase))?.GameObject;
            }

            var buddies = UIState.Instance()->Buddy.BattleBuddies.ToArray().Where(x => x.DataId != 0);
            foreach (var buddy in buddies)
            {
                var gameObject = Svc.Objects.FirstOrDefault(x => x.EntityId == buddy.EntityId);

                if (gameObject == null) 
                    continue;

                var classJob = ((ICharacter)gameObject).ClassJob.ValueNullable;

                if (classJob == null) 
                    continue;

                if (classJob.Value.GetJobRole().ToString().Contains(role, StringComparison.InvariantCultureIgnoreCase))
                    return gameObject;
            }
            return null;
        }

        internal static unsafe IGameObject? GetTankPartyMember() => GetPartyMemberFromRole("Tank");

        internal static unsafe IGameObject? GetHealerPartyMember() => GetPartyMemberFromRole("Healer");

        //RotationSolver
        internal static unsafe float GetBattleDistanceToPlayer(IGameObject gameObject)
        {
            if (gameObject == null) return float.MaxValue;
            var player = Player.Object;
            if (player == null) return float.MaxValue;

            var distance = Vector3.Distance(player.Position, gameObject.Position) - player.HitboxRadius;
            distance -= gameObject.HitboxRadius;
            return distance;
        }

        internal static BNpcBase? GetObjectNPC(IGameObject gameObject) => Svc.Data.GetExcelSheet<BNpcBase>()?.GetRow(gameObject.DataId) ?? null;

        //From RotationSolver
        internal static bool IsBossFromIcon(IGameObject gameObject) => GetObjectNPC(gameObject)?.Rank is 1 or 2 or 6;

        internal static unsafe void InteractWithObject(IGameObject? gameObject, bool face = true)
        {
            try
            {
                if (gameObject == null || !gameObject.IsTargetable) 
                    return;
                if (face) 
                    Plugin.OverrideCamera.Face(gameObject.Position);
                var gameObjectPointer = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
                TargetSystem.Instance()->InteractWithObject(gameObjectPointer, false);
            }
            catch (Exception ex)
            {
                Svc.Log.Info($"InteractWithObject: Exception: {ex}");
            }
        }
        internal static unsafe AtkUnitBase* InteractWithObjectUntilAddon(IGameObject? gameObject, string addonName)
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && GenericHelpers.IsAddonReady(addon))
                return addon;

            if (EzThrottler.Throttle("InteractWithObjectUntilAddon"))
                InteractWithObject(gameObject);
            
            return null;
        }

        internal static unsafe bool InteractWithObjectUntilNotValid(IGameObject? gameObject)
        {
            if (gameObject == null || !PlayerHelper.IsValid)
                return true;

            if (EzThrottler.Throttle("InteractWithObjectUntilNotValid"))
                InteractWithObject(gameObject);
            
            return false;
        }

        internal static unsafe bool InteractWithObjectUntilNotTargetable(IGameObject? gameObject)
        {
            if (gameObject == null || !gameObject.IsTargetable)
                return true;

            if (EzThrottler.Throttle("InteractWithObjectUntilNotTargetable"))
                InteractWithObject(gameObject);

            return false;
        }

        internal static bool PartyValidation()
        {
            if (Svc.Party.Count < 4)
                return false;

            var healer = false;
            var tank = false;
            var dpsCount = 0;

            foreach (var item in Svc.Party)
            {
                switch (item.ClassJob.ValueNullable?.Role)
                {
                    case 1:
                        tank = true;
                        break;
                    case 2:
                    case 3:
                        dpsCount++;
                        break;
                    case 4:
                        healer = true;
                        break;
                    default:
                        break;
                }
            }
            return (tank && healer && dpsCount > 1);
        }
    }
}
