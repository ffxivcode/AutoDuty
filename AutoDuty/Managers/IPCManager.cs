using ECommons.DalamudServices;
using ECommons.Reflection;
using System.Numerics;

namespace AutoDuty.Managers
{
    internal static class IPCManager
    {
        internal static bool  BossMod_IsEnabled                         => IsReady      ("BossMod");
        internal static bool  BossMod_IsMoving                          => Invoke<bool> ("BossMod", "IsMoving");
        internal static int   BossMod_ForbiddenZonesCount               => Invoke<int>  ("BossMod", "ForbiddenZonesCount");
        
        internal static bool  Vnavmesh_IsEnabled                        => IsReady      ("vnavmesh");

        internal static bool  Vnavmesh_Nav_IsReady                      => Invoke<bool> ("vnavmesh", "Nav.IsReady");
        internal static float Vnavmesh_Nav_BuildProgress                => Invoke<float>("vnavmesh", "Nav.BuildProgress");
        internal static void  Vnavmesh_Nav_Reload()                     => Invoke       ("vnavmesh", "Nav.Reload");
        internal static void  Vnavmesh_Nav_Rebuild()                    => Invoke       ("vnavmesh", "Nav.Rebuild");
        internal static bool  Vnavmesh_Nav_IsAutoLoad                   => Invoke<bool> ("vnavmesh", "Nav.IsAutoLoad");
        internal static void  Vnavmesh_Nav_SetAutoLoad(bool b)          => Invoke       ("vnavmesh", "Path.SetAutoLoad", b);

        internal static int   Vnavmesh_Path_NumWaypoints                => Invoke<int>  ("vnavmesh", "Path.NumWaypoints");
        internal static bool  Vnavmesh_Path_IsRunning                   => Invoke<bool> ("vnavmesh", "Path.IsRunning");
        internal static void  Vnavmesh_Path_MoveTo(Vector3 v3)          => Invoke       ("vnavmesh", "Path.MoveTo", v3);
        internal static void  Vnavmesh_Path_MoveDir(Vector3 v3)         => Invoke       ("vnavmesh", "Path.MoveDir", v3);
        internal static void  Vnavmesh_Path_MoveTarget()                => Invoke       ("vnavmesh", "Path.MoveTarget");
        internal static void  Vnavmesh_Path_FlyTo(Vector3 v3)           => Invoke       ("vnavmesh", "Path.FlyTo", v3);
        internal static void  Vnavmesh_Path_FlyDir(Vector3 v3)          => Invoke       ("vnavmesh", "Path.FlyDir", v3);
        internal static void  Vnavmesh_Path_FlyTarget()                 => Invoke       ("vnavmesh", "Path.FlyTarget");
        internal static void  Vnavmesh_Path_SetMovementAllowed(bool b)  => Invoke       ("vnavmesh", "Path.SetMovementAllowed", b);
        internal static bool  Vnavmesh_Path_GetMovementAllowed          => Invoke<bool> ("vnavmesh", "Path.GetMovementAllowed");
        internal static void  Vnavmesh_Path_SetTolerance(float f)       => Invoke       ("vnavmesh", "Path.SetTolerance", f);
        internal static float Vnavmesh_Path_GetTolerance                => Invoke<float>("vnavmesh", "Path.GetTolerance");
        internal static void  Vnavmesh_Path_Stop()                      => Invoke       ("vnavmesh", "Path.Stop");
        
        internal static bool  Vnavmesh_Window_IsOpen                    => Invoke<bool> ("vnavmesh", "Window.IsOpen");
        internal static void  Vnavmesh_Window_SetOpen(bool b)           => Invoke       ("vnavmesh", "Window.SetOpen", b);

        internal static bool  IsReady(string pluginName) => DalamudReflector.TryGetDalamudPlugin(pluginName, out _, false, true);

        internal static TRet? Invoke<TRet>(string pluginName, string funcName)
            => IsReady(pluginName) ? Svc.PluginInterface.GetIpcSubscriber<TRet>($"{pluginName}.{funcName}").InvokeFunc() : default;

        internal static void Invoke(string pluginName, string actionName)
        {
            if (IsReady(pluginName))
                Svc.PluginInterface.GetIpcSubscriber<object>($"{pluginName}.{actionName}").InvokeAction();
        }

        internal static void Invoke<T1>(string pluginName, string actionName, T1 t)
        {
            if (IsReady(pluginName))
                Svc.PluginInterface.GetIpcSubscriber<T1, object>($"{pluginName}.{actionName}").InvokeAction(t);
        }
    }
}
