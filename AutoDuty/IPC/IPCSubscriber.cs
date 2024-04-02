﻿using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using ECommons.Reflection;
using System;
using System.Collections.Generic;
using System.Numerics;
#nullable disable

namespace AutoDuty.IPC
{
    internal static class BossMod_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(BossMod_IPCSubscriber), "BossMod");

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("BossMod");

        [EzIPC] internal static readonly Func<bool> IsMoving;
        [EzIPC] internal static readonly Func<int> ForbiddenZonesCount;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal static class VNavmesh_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(VNavmesh_IPCSubscriber), "vnavmesh");

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("vnavmesh");

        [EzIPC("vnavmesh.Nav.IsReady", applyPrefix: false)] internal static readonly Func<bool> Nav_IsReady;
        [EzIPC("vnavmesh.Nav.BuildProgress", applyPrefix: false)] internal static readonly Func<float> Nav_BuildProgress;
        [EzIPC("vnavmesh.Nav.Reload", applyPrefix: false)] internal static readonly Action Nav_Reload;
        [EzIPC("vnavmesh.Nav.Rebuild", applyPrefix: false)] internal static readonly Action Nav_Rebuild;
        [EzIPC("vnavmesh.Nav.Pathfind", applyPrefix: false)] internal static readonly Func<Vector3, Vector3, bool, Vector3> Nav_Pathfind;
        [EzIPC("vnavmesh.Nav.IsAutoLoad", applyPrefix: false)] internal static readonly Func<bool> Nav_IsAutoLoad;
        [EzIPC("vnavmesh.Nav.SetAutoLoad", applyPrefix: false)] internal static readonly Action<bool> Nav_SetAutoLoad;

        [EzIPC("vnavmesh.Path.NumWaypoints", applyPrefix: false)] internal static readonly Func<int> Path_NumWaypoints;
        [EzIPC("vnavmesh.Path.IsRunning", applyPrefix: false)] internal static readonly Func<bool> Path_IsRunning;
        [EzIPC("vnavmesh.Path.MoveTo", applyPrefix: false)] internal static readonly Action<List<Vector3>, bool> Path_MoveTo;
        [EzIPC("vnavmesh.Path.SetMovementAllowed", applyPrefix: false)] internal static readonly Action<bool> Path_SetMovementAllowed;
        [EzIPC("vnavmesh.Path.GetMovementAllowed", applyPrefix: false)] internal static readonly Func<bool> Path_GetMovementAllowed;
        [EzIPC("vnavmesh.Path.SetAlignCamera", applyPrefix: false)] internal static readonly Action<bool> Path_SetAlignCamera;
        [EzIPC("vnavmesh.Path.GetAlignCamera", applyPrefix: false)] internal static readonly Func<bool> Path_GetAlignCamera;
        [EzIPC("vnavmesh.Path.SetTolerance", applyPrefix: false)] internal static readonly Action<float> Path_SetTolerance;
        [EzIPC("vnavmesh.Path.GetTolerance", applyPrefix: false)] internal static readonly Func<float> Path_GetTolerance;
        [EzIPC("vnavmesh.Path.Stop", applyPrefix: false)] internal static readonly Action Path_Stop;

        [EzIPC("vnavmesh.Query.Mesh.NearestPoint", applyPrefix: false)] internal static readonly Func<Vector3, float, float, Vector3> Query_Mesh_NearestPoint;
        [EzIPC("vnavmesh.Query.Mesh.PointOnFloor", applyPrefix: false)] internal static readonly Func<Vector3, float, Vector3> Query_Mesh_PointOnFloor;

        [EzIPC("vnavmesh.SimpleMove.PathfindAndMoveTo", applyPrefix: false)] internal static readonly Func<Vector3, bool, bool> SimpleMove_PathfindAndMoveTo;
        [EzIPC("vnavmesh.SimpleMove.PathfindInProgress", applyPrefix: false)] internal static readonly Func<bool> SimpleMove_PathfindInProgress;

        [EzIPC("vnavmesh.Window.IsOpen", applyPrefix: false)] internal static readonly Func<bool> Window_IsOpen;
        [EzIPC("vnavmesh.Window.SetOpen", applyPrefix: false)] internal static readonly Action<bool> Window_SetOpen;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal class IPCSubscriber_Common
    {
        internal static bool IsReady(string pluginName) => DalamudReflector.TryGetDalamudPlugin(pluginName, out _, false, true);

        internal static void DisposeAll(EzIPCDisposalToken[] _disposalTokens)
        {
            foreach (var token in _disposalTokens)
            {
                try
                {
                    token.Dispose();
                }
                catch (Exception ex)
                {
                    Svc.Log.Error($"Error while unregistering IPC: {ex}");
                }
            }
        }
    }
}