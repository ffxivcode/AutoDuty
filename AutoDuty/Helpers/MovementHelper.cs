using AutoDuty.IPC;
using System.Numerics;
using ECommons.DalamudServices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using AutoDuty.External;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoDuty.Helpers
{
    internal static class MovementHelper
    {
        internal static void Face(Vector3 pos)
        {
            AutoDuty.Plugin.OverrideCamera.Enabled = true;
            AutoDuty.Plugin.OverrideCamera.SpeedH = AutoDuty.Plugin.OverrideCamera.SpeedV = 360.Degrees();
            AutoDuty.Plugin.OverrideCamera.DesiredAzimuth = Angle.FromDirectionXZ(pos - Player.Object.Position) + 180.Degrees();
            AutoDuty.Plugin.OverrideCamera.DesiredAltitude = -30.Degrees();
        }
        internal static bool Move(GameObject? gameObject, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool fly = false)
        {
            if (gameObject == null)
                return true;

            return Move(gameObject.Position, tollerance, lastPointTollerance, fly);
        }
        //need to add a stuck check
        internal unsafe static bool Move(Vector3 position, float tollerance = 0.25f, float lastPointTollerance = 0.25f, bool fly = false)
        {
            if (!ObjectHelper.IsValid)
                return false;

            if (position == Vector3.Zero || Vector3.Distance(Player.Object.Position, position) <= lastPointTollerance)
            {
                if (position != Vector3.Zero)
                {
                    Face(position);
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