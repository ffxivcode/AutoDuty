using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using ECommons.Reflection;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
#nullable disable

namespace AutoDuty.IPC
{
    internal static class AutoMarket_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(AutoMarket_IPCSubscriber), "AutoBot");

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("AutoBot");

        [EzIPC] internal static readonly Action Start;
        [EzIPC] internal static readonly Action Stop;
        [EzIPC] internal static readonly Func<bool> IsRunning;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal static class Marketbuddy_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(Marketbuddy_IPCSubscriber), "Marketbuddy");

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("Marketbuddy");

        [EzIPC] internal static readonly Func<string, bool> IsLocked;
        [EzIPC] internal static readonly Func<string, bool> Lock;
        [EzIPC] internal static readonly Func<string, bool> Unlock;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal static class BossMod_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(BossMod_IPCSubscriber), "BossMod");

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("BossMod") || IPCSubscriber_Common.IsReady("BossModReborn");

        [EzIPC] internal static readonly Func<bool> IsMoving;
        [EzIPC] internal static readonly Func<int> ForbiddenZonesCount;
        [EzIPC] internal static readonly Func<uint, bool> HasModuleByDataId;
        [EzIPC] internal static readonly Func<string, bool> ActiveModuleHasComponent;
        [EzIPC] internal static readonly Func<List<string>> ActiveModuleComponentBaseList;
        [EzIPC] internal static readonly Func<List<string>> ActiveModuleComponentList;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
    
    /* Seem's YesAlready is not Initializing this
    internal static class YesAlready_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(YesAlready_IPCSubscriber), "YesAlready");

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("YesAlready");

        [EzIPC("YesAlready.SetPluginEnabled", applyPrefix: false)] internal static readonly Action<bool> SetPluginEnabled;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }*/

    internal static class Deliveroo_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(Deliveroo_IPCSubscriber), "Deliveroo");

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("Deliveroo");

        [EzIPC] internal static readonly Func<bool> IsTurnInRunning;
        //[EzIPC] internal static readonly Action TurnInStarted;
        //[EzIPC] internal static readonly Action TurnInStopped;

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
        [EzIPC("vnavmesh.Nav.Pathfind", applyPrefix: false)] internal static readonly Func<Vector3, Vector3, bool, Task<List<Vector3>>> Nav_Pathfind;
        [EzIPC("vnavmesh.Nav.PathfindCancelable", applyPrefix: false)] internal static readonly Func<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>> Nav_PathfindCancelable;
        [EzIPC("vnavmesh.Nav.PathfindCancelAll", applyPrefix: false)] internal static readonly Action Nav_PathfindCancelAll;
        [EzIPC("vnavmesh.Nav.PathfindInProgress", applyPrefix: false)] internal static readonly Func<bool> Nav_PathfindInProgress;
        [EzIPC("vnavmesh.Nav.PathfindNumQueued", applyPrefix: false)] internal static readonly Func<int> Nav_PathfindNumQueued;
        [EzIPC("vnavmesh.Nav.IsAutoLoad", applyPrefix: false)] internal static readonly Func<bool> Nav_IsAutoLoad;
        [EzIPC("vnavmesh.Nav.SetAutoLoad", applyPrefix: false)] internal static readonly Action<bool> Nav_SetAutoLoad;

        [EzIPC("vnavmesh.Query.Mesh.NearestPoint", applyPrefix: false)] internal static readonly Func<Vector3, float, float, Vector3> Query_Mesh_NearestPoint;
        [EzIPC("vnavmesh.Query.Mesh.PointOnFloor", applyPrefix: false)] internal static readonly Func<Vector3, float, Vector3> Query_Mesh_PointOnFloor;

        [EzIPC("vnavmesh.Path.MoveTo", applyPrefix: false)] internal static readonly Action<List<Vector3>, bool> Path_MoveTo;
        [EzIPC("vnavmesh.Path.Stop", applyPrefix: false)] internal static readonly Action Path_Stop;
        [EzIPC("vnavmesh.Path.IsRunning", applyPrefix: false)] internal static readonly Func<bool> Path_IsRunning;
        [EzIPC("vnavmesh.Path.NumWaypoints", applyPrefix: false)] internal static readonly Func<int> Path_NumWaypoints;
        [EzIPC("vnavmesh.Path.GetMovementAllowed", applyPrefix: false)] internal static readonly Func<bool> Path_GetMovementAllowed;
        [EzIPC("vnavmesh.Path.SetMovementAllowed", applyPrefix: false)] internal static readonly Action<bool> Path_SetMovementAllowed;
        [EzIPC("vnavmesh.Path.GetAlignCamera", applyPrefix: false)] internal static readonly Func<bool> Path_GetAlignCamera;
        [EzIPC("vnavmesh.Path.SetAlignCamera", applyPrefix: false)] internal static readonly Action<bool> Path_SetAlignCamera;
        [EzIPC("vnavmesh.Path.GetTolerance", applyPrefix: false)] internal static readonly Func<float> Path_GetTolerance;
        [EzIPC("vnavmesh.Path.SetTolerance", applyPrefix: false)] internal static readonly Action<float> Path_SetTolerance;

        [EzIPC("vnavmesh.SimpleMove.PathfindAndMoveTo", applyPrefix: false)] internal static readonly Func<Vector3, bool, bool> SimpleMove_PathfindAndMoveTo;
        [EzIPC("vnavmesh.SimpleMove.PathfindInProgress", applyPrefix: false)] internal static readonly Func<bool> SimpleMove_PathfindInProgress;

        [EzIPC("vnavmesh.Window.IsOpen", applyPrefix: false)] internal static readonly Func<bool> Window_IsOpen;
        [EzIPC("vnavmesh.Window.SetOpen", applyPrefix: false)] internal static readonly Action<bool> Window_SetOpen;

        [EzIPC("vnavmesh.DTR.IsShown", applyPrefix: false)] internal static readonly Func<bool> DTR_IsShown;
        [EzIPC("vnavmesh.DTR.SetShown", applyPrefix: false)] internal static readonly Action<bool> DTR_SetShown;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal static class PandorasBox_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(PandorasBox_IPCSubscriber), "PandorasBox");

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("PandorasBox");

        [EzIPC] internal static readonly Action<string, int> PauseFeature;

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
