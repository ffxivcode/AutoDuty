using ECommons.EzIpcManager;
using ECommons.Reflection;
using System;
using System.Collections.Generic;
using System.Numerics;
#nullable disable

namespace AutoDuty.IPC
{

    public class MBT_IPCSubscriber
    {
        public MBT_IPCSubscriber()
        {
            EzIPC.Init(this, "MBT");
        }

        [EzIPC] public readonly Action<bool> SetFollowStatus;
        [EzIPC] public readonly Action<int> SetFollowDistance;
        [EzIPC] public readonly Action<string> SetFollowTarget;
        [EzIPC] public readonly Func<bool> GetFollowStatus;
        [EzIPC] public readonly Func<int> GetFollowDistance;
        [EzIPC] public readonly Func<string> GetFollowTarget;

        public bool IsEnabled => IPCSubscriber_Common.IsReady("MBT");
    }

    public class BossMod_IPCSubscriber
    {
        public BossMod_IPCSubscriber()
        {
            EzIPC.Init(this, "BossMod");
        }

        public bool IsEnabled => IPCSubscriber_Common.IsReady("BossMod");

        [EzIPC] public readonly Func<bool> IsMoving;
        [EzIPC] public readonly Func<int> ForbiddenZonesCount;
    }

    public class VNavmesh_IPCSubscriber
    {
        public VNavmesh_IPCSubscriber()
        {
            EzIPC.Init(this, "vnavmesh");
        }

        public bool IsEnabled => IPCSubscriber_Common.IsReady("vnavmesh");

        [EzIPC("vnavmesh.Nav.IsReady", applyPrefix: false)] public readonly Func<bool> Nav_IsReady;
        [EzIPC("vnavmesh.Nav.BuildProgress", applyPrefix: false)] public readonly Func<float> Nav_BuildProgress;
        [EzIPC("vnavmesh.Nav.Reload", applyPrefix: false)] public readonly Action Nav_Reload;
        [EzIPC("vnavmesh.Nav.Rebuild", applyPrefix: false)] public readonly Action Nav_Rebuild;
        [EzIPC("vnavmesh.Nav.Pathfind", applyPrefix: false)] public readonly Func<Vector3, Vector3, bool, Vector3> Nav_Pathfind;
        [EzIPC("vnavmesh.Nav.IsAutoLoad", applyPrefix: false)] public readonly Func<bool> Nav_IsAutoLoad;
        [EzIPC("vnavmesh.Nav.SetAutoLoad", applyPrefix: false)] public readonly Action<bool> Nav_SetAutoLoad;

        [EzIPC("vnavmesh.Path.NumWaypoints", applyPrefix: false)] public readonly Func<int> Path_NumWaypoints;
        [EzIPC("vnavmesh.Path.IsRunning", applyPrefix: false)] public readonly Func<bool> Path_IsRunning;
        [EzIPC("vnavmesh.Path.MoveTo", applyPrefix: false)] public readonly Action<List<Vector3>, bool> Path_MoveTo;
        [EzIPC("vnavmesh.Path.SetMovementAllowed", applyPrefix: false)] public readonly Action<bool> Path_SetMovementAllowed;
        [EzIPC("vnavmesh.Path.GetMovementAllowed", applyPrefix: false)] public readonly Func<bool> Path_GetMovementAllowed;
        [EzIPC("vnavmesh.Path.SetAlignCamera", applyPrefix: false)] public readonly Action<bool> Path_SetAlignCamera;
        [EzIPC("vnavmesh.Path.GetAlignCamera", applyPrefix: false)] public readonly Func<bool> Path_GetAlignCamera;
        [EzIPC("vnavmesh.Path.SetTolerance", applyPrefix: false)] public readonly Action<float> Path_SetTolerance;
        [EzIPC("vnavmesh.Path.GetTolerance", applyPrefix: false)] public readonly Func<float> Path_GetTolerance;
        [EzIPC("vnavmesh.Path.Stop", applyPrefix: false)] public readonly Action Path_Stop;

        [EzIPC("vnavmesh.Query.Mesh.NearestPoint", applyPrefix: false)] public readonly Func<Vector3, float, float, Vector3> Query_Mesh_NearestPoint;
        [EzIPC("vnavmesh.Query.Mesh.PointOnFloor", applyPrefix: false)] public readonly Func<Vector3, float, Vector3> Query_Mesh_PointOnFloor;

        [EzIPC("vnavmesh.SimpleMove.PathfindAndMoveTo", applyPrefix: false)] public readonly Func<Vector3, bool, bool> SimpleMove_PathfindAndMoveTo;
        [EzIPC("vnavmesh.SimpleMove.PathfindInProgress", applyPrefix: false)] public readonly Func<bool> SimpleMove_PathfindInProgress;

        [EzIPC("vnavmesh.Window.IsOpen", applyPrefix: false)] public readonly Func<bool> Window_IsOpen;
        [EzIPC("vnavmesh.Window.SetOpen", applyPrefix: false)] public readonly Action<bool> Window_SetOpen;
    }

    internal class IPCSubscriber_Common
    {
        internal static bool IsReady(string pluginName) => DalamudReflector.TryGetDalamudPlugin(pluginName, out _, false, true);
    }
}
