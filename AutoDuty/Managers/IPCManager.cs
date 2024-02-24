using ECommons.DalamudServices;
using ECommons.Reflection;
using System.Numerics;

namespace AutoDuty.Managers
{
    internal static class IPCManager
    {
        internal static bool BossMod_IsEnabled => DalamudReflector.TryGetDalamudPlugin("BossMod", out _, false, true);
        internal static bool BossMod_IsMoving => Svc.PluginInterface.GetIpcSubscriber<bool>("BossMod.IsMoving").InvokeFunc();
        internal static int BossMod_ForbiddenZonesCount => Svc.PluginInterface.GetIpcSubscriber<int>("BossMod.ForbiddenZonesCount").InvokeFunc();

        internal static bool VNavmesh_IsEnabled => DalamudReflector.TryGetDalamudPlugin("vnavmesh", out _, false, true);

        internal static bool VNavmesh_NavmeshIsNull => VNavmesh_IsEnabled ? Svc.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.NavmeshIsNull").InvokeFunc() : false;
        internal static float VNavmesh_TaskProgress => Svc.PluginInterface.GetIpcSubscriber<float>("vnavmesh.TaskProgress").InvokeFunc();
        internal static int VNavmesh_WaypointsCount => Svc.PluginInterface.GetIpcSubscriber<int>("vnavmesh.WaypointsCount").InvokeFunc();
        internal static void VNavmesh_MoveTo(Vector3 v3) => Svc.PluginInterface.GetIpcSubscriber<Vector3, object>("vnavmesh.MoveTo").InvokeAction(v3);
        internal static void VNavmesh_MoveDir(Vector3 v3) => Svc.PluginInterface.GetIpcSubscriber<Vector3, object>("vnavmesh.MoveDir").InvokeAction(v3);
        internal static void VNavmesh_MoveTarget() => Svc.PluginInterface.GetIpcSubscriber<object>("vnavmesh.MoveTarget").InvokeAction();
        internal static void VNavmesh_FlyTo(Vector3 v3) => Svc.PluginInterface.GetIpcSubscriber<Vector3, object>("vnavmesh.FlyTo").InvokeAction(v3);
        internal static void VNavmesh_FlyDir(Vector3 v3) => Svc.PluginInterface.GetIpcSubscriber<Vector3, object>("vnavmesh.FlyDir").InvokeAction(v3);
        internal static void VNavmesh_FlyTarget() => Svc.PluginInterface.GetIpcSubscriber<object>("vnavmesh.FlyTarget").InvokeAction();
        internal static void VNavmesh_SetMovementAllowed(bool b) => Svc.PluginInterface.GetIpcSubscriber<bool, object>("vnavmesh.SetMovementAllowed").InvokeAction(b);
        internal static bool VNavmesh_MovementAllowed => Svc.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.MovementAllowed").InvokeFunc();
        internal static float VNavmesh_Tolerance => Svc.PluginInterface.GetIpcSubscriber<float>("vnavmesh.Tolerance").InvokeFunc();
        internal static void VNavmesh_SetTolerance(float f) => Svc.PluginInterface.GetIpcSubscriber<float, object>("vnavmesh.SetTolerance").InvokeAction(f);
        internal static void VNavmesh_Stop() => Svc.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Stop").InvokeAction();
        internal static void VNavmesh_AutoMesh(bool b) => Svc.PluginInterface.GetIpcSubscriber<bool, object>("vnavmesh.AutoMesh").InvokeAction(b);
        internal static void VNavmesh_Reload() => Svc.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Reload").InvokeAction();
        internal static void VNavmesh_Rebuild() => Svc.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Rebuild").InvokeAction();
        internal static void VNavmesh_MainWindowIsOpen(bool b) => Svc.PluginInterface.GetIpcSubscriber<object>("vnavmesh.MainWindowIsOpen").InvokeAction();
    }
}
