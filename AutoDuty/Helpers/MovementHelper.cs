using AutoDuty.IPC;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;

namespace AutoDuty.Helpers
{
    internal static class MovementHelper
    {
        public static bool IsFlyingSupported => Svc.ClientState.TerritoryType != 0 && Svc.Data.GetExcelSheet<TerritoryType>()!.GetRow(Svc.ClientState.TerritoryType)?.TerritoryIntendedUse is 1 or 49 or 47;

        internal static bool Move(IGameObject? gameObject, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool fly = false, bool useMesh = true)
        {
            if (gameObject == null)
                return true;

            return Move(gameObject.Position, tollerance, lastPointTollerance, fly, useMesh);
        }

        internal unsafe static bool Move(Vector3 position, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool fly = false, bool useMesh = true)
        {
            if (!ObjectHelper.IsValid)
                return false;

            //Svc.Log.Debug($"Move(Vector3 {position}, float {tollerance} = 0.25f, float {lastPointTollerance} = 0.25f, bool {fly} = false, bool {useMesh} = true) Dist: {Vector3.Distance(Player.Object.Position, position)} <= {lastPointTollerance}");

            if (fly && !IsFlyingSupported)
                fly = false;

            if (fly && !Conditions.IsMounted)
            {
                if (!ObjectHelper.PlayerIsCasting)
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
                return false;
            }

            if (fly && !Conditions.IsInFlight)
            {
                if (!ObjectHelper.PlayerIsCasting)
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                return false;
            }

            if (position == Vector3.Zero || (Vector3.Distance(position, Player.Position) - (useMesh ? 0 : 1)/*fix for vnav's diff Distance calc*/) <= lastPointTollerance)
            {
                if (position != Vector3.Zero)
                {
                    AutoDuty.Plugin.OverrideCamera.Face(position);
                    VNavmesh_IPCSubscriber.Path_Stop();
                }
                return true;
            }

            if (AgentMap.Instance()->IsPlayerMoving == 1 && !Player.Object.InCombat() && Vector3.Distance(Player.Object.Position, position) >= 10)
            {
                //sprint
                if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 4) == 0 && ActionManager.Instance()->QueuedActionId != 4 && !ObjectHelper.PlayerIsCasting)
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 4);

                //peloton
                if (ActionManager.Instance()->GetActionStatus(ActionType.Action, 7557) == 0 && ActionManager.Instance()->QueuedActionId != 7557 && !ObjectHelper.PlayerIsCasting && !Player.Object.StatusList.Any(x => x.StatusId == 1199))
                    ActionManager.Instance()->UseAction(ActionType.Action, 7557);
            }

            if (VNavmesh_IPCSubscriber.Path_NumWaypoints() == 1)
                VNavmesh_IPCSubscriber.Path_SetTolerance(lastPointTollerance);

            if (!useMesh)
            {
                if (!VNavmesh_IPCSubscriber.Path_IsRunning())
                    VNavmesh_IPCSubscriber.Path_MoveTo([position], fly);
                return false;
            }

            if (!ObjectHelper.IsReady || !VNavmesh_IPCSubscriber.Nav_IsReady() || VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() || VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                return false;

            if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() || VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
            {
                VNavmesh_IPCSubscriber.Path_SetTolerance(tollerance);
                VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(position, fly);
            }
            return false;
        }
    }
}