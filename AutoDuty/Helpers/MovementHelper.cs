using AutoDuty.IPC;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ECommons.GameFunctions;

namespace AutoDuty.Helpers
{
    using ECommons.Automation;
    using Lumina.Excel.Sheets;

    internal static class MovementHelper
    {
        public unsafe static bool IsFlyingSupported => Svc.ClientState.TerritoryType != 0 && Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(Svc.ClientState.TerritoryType)?.TerritoryIntendedUse.RowId is 1 or 49 or 47 && PlayerState.Instance()->IsAetherCurrentZoneComplete(Svc.Data.GetExcelSheet<TerritoryType>()!.GetRow(Svc.ClientState.TerritoryType)!.Unknown4);

        internal static void Stop() => VNavmesh_IPCSubscriber.Path_Stop();

        internal static bool Move(IGameObject? gameObject, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool fly = false, bool useMesh = true)
        {
            if (gameObject == null)
                return true;

            return Move(gameObject.Position, tollerance, lastPointTollerance, fly, useMesh);
        }

        internal unsafe static bool Move(Vector3 position, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool fly = false, bool useMesh = true)
        {
            if (!PlayerHelper.IsValid)
                return false;

            if (fly && !IsFlyingSupported)
                fly = false;

            if (!Conditions.IsMounted && IsFlyingSupported)
            {
                if (!PlayerHelper.IsCasting)
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
                return false;
            }

            if (fly && !Conditions.IsInFlight)
            {
                if (!PlayerHelper.IsCasting)
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                return false;
            }

            if (position == Vector3.Zero || (Vector3.Distance(position, Player.Position) - (useMesh ? 0 : 1)/*fix for vnav's diff Distance calc*/) <= lastPointTollerance)
            {
                if (position != Vector3.Zero)
                    VNavmesh_IPCSubscriber.Path_Stop();
                
                return true;
            }

            if (PlayerHelper.IsMoving && !Player.Object.Struct()->Character.InCombat && Vector3.Distance(Player.Object.Position, position) >= 10)
            {
                //sprint
                if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 4) == 0 && ActionManager.Instance()->QueuedActionId != 4 && !PlayerHelper.IsCasting)
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 4);

                //peloton
                if (ActionManager.Instance()->GetActionStatus(ActionType.Action, 7557) == 0 && ActionManager.Instance()->QueuedActionId != 7557 && !PlayerHelper.IsCasting && !Player.Object.StatusList.Any(x => x.StatusId == 1199))
                    ActionManager.Instance()->UseAction(ActionType.Action, 7557);
            }

            if (VNavmesh_IPCSubscriber.Path_NumWaypoints() == 1)
                VNavmesh_IPCSubscriber.Path_SetTolerance(lastPointTollerance);

            if (!useMesh)
            {
                if (!VNavmesh_IPCSubscriber.Path_IsRunning())
                {
                    Chat.Instance.ExecuteCommand("/automove off");
                    VNavmesh_IPCSubscriber.Path_MoveTo([position], fly);
                }

                return false;
            }

            if (!PlayerHelper.IsReady || !VNavmesh_IPCSubscriber.Nav_IsReady() || VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() || VNavmesh_IPCSubscriber.Path_NumWaypoints() > 0)
                return false;

            if (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() || VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0)
            {
                Chat.Instance.ExecuteCommand("/automove off");
                VNavmesh_IPCSubscriber.Path_SetTolerance(tollerance);
                VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(position, fly);
            }
            return false;
        }
    }
}