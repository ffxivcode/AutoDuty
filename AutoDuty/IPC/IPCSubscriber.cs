using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
#nullable disable

namespace AutoDuty.IPC
{
    internal static class AutoRetainer_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(AutoRetainer_IPCSubscriber), "AutoRetainer.PluginState", SafeWrapper.IPCException);

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("AutoRetainer");

        [EzIPC] internal static readonly Func<bool> IsBusy;
        [EzIPC] internal static readonly Func<Dictionary<ulong, HashSet<string>>> GetEnabledRetainers;
        [EzIPC] internal static readonly Func<bool> AreAnyRetainersAvailableForCurrentChara;
        [EzIPC] internal static readonly Action AbortAllTasks;
        [EzIPC] internal static readonly Action DisableAllFunctions;
        [EzIPC] internal static readonly Action EnableMultiMode;
        [EzIPC] internal static readonly Func<int> GetInventoryFreeSlotCount;
        [EzIPC] internal static readonly Action EnqueueHET;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal static class AM_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(AM_IPCSubscriber), "AutoBot", SafeWrapper.IPCException);

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("AutoBot");

        [EzIPC] internal static readonly Action Start;
        [EzIPC] internal static readonly Action Stop;
        [EzIPC] internal static readonly Func<bool> IsRunning;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal static class Marketbuddy_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(Marketbuddy_IPCSubscriber), "Marketbuddy", SafeWrapper.IPCException);

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("Marketbuddy");

        [EzIPC] internal static readonly Func<string, bool> IsLocked;
        [EzIPC] internal static readonly Func<string, bool> Lock;
        [EzIPC] internal static readonly Func<string, bool> Unlock;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
    
    internal static class BossMod_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(BossMod_IPCSubscriber), "BossMod", SafeWrapper.IPCException);

        internal static bool IsEnabled => (IPCSubscriber_Common.IsReady("BossMod") && IPCSubscriber_Common.Version("BossMod") >= new Version(0, 0, 0, 218)) || (IPCSubscriber_Common.IsReady("BossModReborn") && IPCSubscriber_Common.Version("BossModReborn") >= new Version(7, 2, 0, 94));

        [EzIPC] internal static readonly Func<bool> IsMoving;
        [EzIPC] internal static readonly Func<int> ForbiddenZonesCount;
        [EzIPC] internal static readonly Func<uint, bool> HasModuleByDataId;
        [EzIPC] internal static readonly Func<string, bool> ActiveModuleHasComponent;
        [EzIPC] internal static readonly Func<List<string>> ActiveModuleComponentBaseList;
        [EzIPC] internal static readonly Func<List<string>> ActiveModuleComponentList;
        [EzIPC] internal static readonly Func<IReadOnlyList<string>, bool, List<string>> Configuration;
        [EzIPC("Presets.List", true)] internal static readonly Func<List<string>> Presets_List;
        [EzIPC("Presets.Get", true)] internal static readonly Func<string, string?> Presets_Get;
        [EzIPC("Presets.ForClass", true)] internal static readonly Func<byte, List<string>> Presets_ForClass;
        [EzIPC("Presets.Create", true)] internal static readonly Func<string, bool, bool> Presets_Create;
        [EzIPC("Presets.Delete", true)] internal static readonly Func<string, bool> Presets_Delete;
        [EzIPC("Presets.GetActive", true)] internal static readonly Func<string> Presets_GetActive;
        [EzIPC("Presets.SetActive", true)] internal static readonly Func<string, bool> Presets_SetActive;
        [EzIPC("Presets.ClearActive", true)] internal static readonly Func<bool> Presets_ClearActive;
        [EzIPC("Presets.GetForceDisabled", true)] internal static readonly Func<bool> Presets_GetForceDisabled; 
        [EzIPC("Presets.SetForceDisabled", true)] internal static readonly Func<bool> Presets_SetForceDisabled;
        
        [EzIPC("AI.SetPreset", true)] internal static readonly Action<string> AI_SetPreset;
        [EzIPC("AI.GetPreset", true)] internal static readonly Func<string> AI_GetPreset;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    /* Seem's YesAlready is not Initializing this
    internal static class YesAlready_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(YesAlready_IPCSubscriber), "YesAlready", SafeWrapper.IPCException);

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("YesAlready");

        [EzIPC("YesAlready.SetPluginEnabled", false)] internal static readonly Action<bool> SetPluginEnabled;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }*/

    internal static class Deliveroo_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(Deliveroo_IPCSubscriber), "Deliveroo", SafeWrapper.IPCException);

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("Deliveroo");

        [EzIPC] internal static readonly Func<bool> IsTurnInRunning;
        //[EzIPC] internal static readonly Action TurnInStarted;
        //[EzIPC] internal static readonly Action TurnInStopped;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal static class Gearsetter_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(Gearsetter_IPCSubscriber), "Gearsetter", SafeWrapper.IPCException);

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("Gearsetter");

        [EzIPC] internal static readonly Func<byte, List<(uint ItemId, InventoryType? SourceInventory, byte? SourceInventorySlot)>> GetRecommendationsForGearset;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal static class VNavmesh_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(VNavmesh_IPCSubscriber), "vnavmesh", SafeWrapper.IPCException);

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("vnavmesh");

        [EzIPC("Nav.IsReady", true)] internal static readonly Func<bool> Nav_IsReady;
        [EzIPC("Nav.BuildProgress", true)] internal static readonly Func<float> Nav_BuildProgress;
        [EzIPC("Nav.Reload", true)] internal static readonly Action Nav_Reload;
        [EzIPC("Nav.Rebuild", true)] internal static readonly Action Nav_Rebuild;
        [EzIPC("Nav.Pathfind", true)] internal static readonly Func<Vector3, Vector3, bool, Task<List<Vector3>>> Nav_Pathfind;
        [EzIPC("Nav.PathfindCancelable", true)] internal static readonly Func<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>> Nav_PathfindCancelable;
        [EzIPC("Nav.PathfindCancelAll", true)] internal static readonly Action Nav_PathfindCancelAll;
        [EzIPC("Nav.PathfindInProgress", true)] internal static readonly Func<bool> Nav_PathfindInProgress;
        [EzIPC("Nav.PathfindNumQueued", true)] internal static readonly Func<int> Nav_PathfindNumQueued;
        [EzIPC("Nav.IsAutoLoad", true)] internal static readonly Func<bool> Nav_IsAutoLoad;
        [EzIPC("Nav.SetAutoLoad", true)] internal static readonly Action<bool> Nav_SetAutoLoad;

        [EzIPC("Query.Mesh.NearestPoint", true)] internal static readonly Func<Vector3, float, float, Vector3> Query_Mesh_NearestPoint;
        [EzIPC("Query.Mesh.PointOnFloor", true)] internal static readonly Func<Vector3, bool, float, Vector3> Query_Mesh_PointOnFloor;

        [EzIPC("Path.MoveTo", true)] internal static readonly Action<List<Vector3>, bool> Path_MoveTo;
        [EzIPC("Path.Stop", true)] internal static readonly Action Path_Stop;
        [EzIPC("Path.IsRunning", true)] internal static readonly Func<bool> Path_IsRunning;
        [EzIPC("Path.NumWaypoints", true)] internal static readonly Func<int> Path_NumWaypoints;
        [EzIPC("Path.GetMovementAllowed", true)] internal static readonly Func<bool> Path_GetMovementAllowed;
        [EzIPC("Path.SetMovementAllowed", true)] internal static readonly Action<bool> Path_SetMovementAllowed;
        [EzIPC("Path.GetAlignCamera", true)] internal static readonly Func<bool> Path_GetAlignCamera;
        [EzIPC("Path.SetAlignCamera", true)] internal static readonly Action<bool> Path_SetAlignCamera;
        [EzIPC("Path.GetTolerance", true)] internal static readonly Func<float> Path_GetTolerance;
        [EzIPC("Path.SetTolerance", true)] internal static readonly Action<float> Path_SetTolerance;

        [EzIPC("SimpleMove.PathfindAndMoveTo", true)] internal static readonly Func<Vector3, bool, bool> SimpleMove_PathfindAndMoveTo;
        [EzIPC("SimpleMove.PathfindInProgress", true)] internal static readonly Func<bool> SimpleMove_PathfindInProgress;

        [EzIPC("Window.IsOpen", true)] internal static readonly Func<bool> Window_IsOpen;
        [EzIPC("Window.SetOpen", true)] internal static readonly Action<bool> Window_SetOpen;

        [EzIPC("DTR.IsShown", true)] internal static readonly Func<bool> DTR_IsShown;
        [EzIPC("DTR.SetShown", true)] internal static readonly Action<bool> DTR_SetShown;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal static class PandorasBox_IPCSubscriber
    {
        private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(PandorasBox_IPCSubscriber), "PandorasBox", SafeWrapper.IPCException);

        internal static bool IsEnabled => IPCSubscriber_Common.IsReady("PandorasBox");

        [EzIPC] internal static readonly Action<string, int> PauseFeature;
        [EzIPC] internal static readonly Action<string, bool> SetFeatureEnabled;
        [EzIPC] internal static readonly Func<string, bool> GetFeatureEnabled;
        [EzIPC] internal static readonly Action<string, string, bool> SetConfigEnabled;

        internal static void Dispose() => IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }

    internal class IPCSubscriber_Common
    {
        internal static bool IsReady(string pluginName) => DalamudReflector.TryGetDalamudPlugin(pluginName, out _, false, true);

        internal static Version Version(string pluginName) => DalamudReflector.TryGetDalamudPlugin(pluginName, out var dalamudPlugin, false, true) ? dalamudPlugin.GetType().Assembly.GetName().Version : new Version(0, 0, 0, 0);

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
