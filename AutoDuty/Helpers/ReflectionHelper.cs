using Dalamud.Plugin;
using ECommons.Automation;
using ECommons.Reflection;
using System.Reflection;
#nullable disable

namespace AutoDuty.Helpers
{
    internal class ReflectionHelper
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        internal static class YesAlready_Reflection
        {
            internal static bool IsEnabled => DalamudReflector.TryGetDalamudPlugin("YesAlready", out _, false, true);

            internal static void SetPluginEnabled(bool trueFalse)
            {
                if (DalamudReflector.TryGetDalamudPlugin("YesAlready", out var pl, false, true))
                    pl.GetFoP("Config").SetFoP("Enabled", trueFalse);
            }
        }

        internal static class RotationSolver_Reflection
        { 
            internal static bool RotationSolverEnabled => DalamudReflector.TryGetDalamudPlugin("RotationSolver", out _, false, true);

            internal static bool GetState => DalamudReflector.TryGetDalamudPlugin("RotationSolver", out var pl, false, true) && (bool)Assembly.GetAssembly(pl.GetType()).GetType("RotationSolver.Commands.RSCommands").GetField("_lastState", All).GetValue(null);

            internal static StateTypeEnum GetStateType => DalamudReflector.TryGetDalamudPlugin("RotationSolver", out var pl, false, true) && ((string)Assembly.GetAssembly(pl.GetType()).GetType("RotationSolver.Commands.RSCommands").GetField("_stateString", All).GetValue(null)).Equals("Manual Target") ? StateTypeEnum.Manual : ((string)Assembly.GetAssembly(pl.GetType()).GetType("RotationSolver.Commands.RSCommands").GetField("_stateString", All).GetValue(null)).Equals("Off") ? StateTypeEnum.Off : StateTypeEnum.Auto;

            internal static void SetState(StateTypeEnum stateType)
            {
                switch (stateType)
                {
                    case StateTypeEnum.Manual:
                        new Chat().ExecuteCommand("/rotation manual");
                        break;
                    case StateTypeEnum.Auto:
                        new Chat().ExecuteCommand("/rotation auto");
                        break;
                    default:
                        new Chat().ExecuteCommand("/rotation cancel");
                        break;
                }
            }

            internal enum StateTypeEnum : byte
            {
                Off,
                Auto,
                Manual,
            }

            internal static void RotationAuto()
            {
                if (!GetState || GetStateType == StateTypeEnum.Manual || GetStateType == StateTypeEnum.Off)
                    SetState(StateTypeEnum.Auto);
            }

            internal static void RotationStop() => SetState(StateTypeEnum.Off);
        }
    }
}
