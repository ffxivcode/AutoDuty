using AutoDuty.IPC;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using AutoDuty.External;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Collections.Generic;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Threading.Tasks;
using ECommons;

namespace AutoDuty.Helpers
{
    internal static class MovementHelper
    {
        internal static List<Vector3> MoveWaypoints = [];

        internal static Task<List<Vector3>>? PathfindTask = null;

        internal static void Face(Vector3 pos)
        {
            AutoDuty.Plugin.OverrideCamera.Enabled = true;
            AutoDuty.Plugin.OverrideCamera.SpeedH = AutoDuty.Plugin.OverrideCamera.SpeedV = 360.Degrees();
            AutoDuty.Plugin.OverrideCamera.DesiredAzimuth = Angle.FromDirectionXZ(pos - Player.Object.Position) + 180.Degrees();
            AutoDuty.Plugin.OverrideCamera.DesiredAltitude = -30.Degrees();
        }

        internal static void Stop()
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            MoveWaypoints = [];
            if (PathfindTask != null)
            {
                if (!PathfindTask.IsCompleted)
                    PathfindTask.Wait();
                PathfindTask.Dispose();
                PathfindTask = null;
            }
        }

        internal static bool Pathfind(Vector3 to, Vector3 from, bool fly = false)
        {
            if (PathfindTask != null || (!PathfindTask?.IsCompleted ?? false))
                return false;

            PathfindTask = Task.Run(() => VNavmesh_IPCSubscriber.Nav_Pathfind(to, from, fly));
            return true;
        }

        internal static bool Move(List<Vector3> moveWaypoints, float tollerance = 0.25f, bool fly = false)
        {
            if (moveWaypoints.Count == 0 || VNavmesh_IPCSubscriber.Path_IsRunning())
                return false;

            VNavmesh_IPCSubscriber.Path_SetMovementAllowed(true);
            VNavmesh_IPCSubscriber.Path_SetAlignCamera(true);
            VNavmesh_IPCSubscriber.Path_SetTolerance(tollerance);
            VNavmesh_IPCSubscriber.Path_MoveTo(moveWaypoints, fly);

            return true;
        }

        internal static bool PathfindAndMove(GameObject? gameObject, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool fly = false)
        {
            if (gameObject == null)
                return true;

            return PathfindAndMove(gameObject.Position, tollerance, lastPointTollerance, fly);
        }
        
        internal unsafe static bool PathfindAndMove(Vector3 position, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool fly = false)
        { 
            if (!EzThrottler.Throttle("Move", 250))
                return false;

            if (!ObjectHelper.IsReady || !VNavmesh_IPCSubscriber.Nav_IsReady())
                return false;

            if (position == Vector3.Zero || Vector3.Distance(Player.Object.Position, position) <= lastPointTollerance)
            {
                if (position != Vector3.Zero)
                {
                    Face(position);
                    Stop();
                }
                MoveWaypoints = [];
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
            if (MoveWaypoints.Count > VNavmesh_IPCSubscriber.Path_NumWaypoints())
                MoveWaypoints.TryDequeue(out _);

            if (VNavmesh_IPCSubscriber.Path_NumWaypoints() == 1)
                VNavmesh_IPCSubscriber.Path_SetTolerance(lastPointTollerance);

            if (!VNavmesh_IPCSubscriber.Nav_PathfindInProgress() && MoveWaypoints.Count == 0 && PathfindTask == null)
            {
                VNavmesh_IPCSubscriber.Path_SetTolerance(tollerance);
                Pathfind(Player.Object.Position, position, false);
                return false;
            }

            if (!VNavmesh_IPCSubscriber.Nav_PathfindInProgress() && !VNavmesh_IPCSubscriber.Path_IsRunning() && PathfindTask != null && PathfindTask.IsCompleted)
            {
                MoveWaypoints = PathfindTask.Result;
                PathfindTask = null;
                VNavmesh_IPCSubscriber.Path_SetMovementAllowed(true);
                VNavmesh_IPCSubscriber.Path_SetAlignCamera(true);
                VNavmesh_IPCSubscriber.Path_MoveTo(MoveWaypoints, fly);
                return false;
            }

            return false;
        }
    }
}