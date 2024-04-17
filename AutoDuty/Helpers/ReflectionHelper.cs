using ECommons.Automation;
using ECommons.Reflection;
using System.Reflection;
#nullable disable

namespace AutoDuty.Helpers
{
    internal class ReflectionHelper
    {
        internal static class RotationSolver_Reflection
        {
            private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

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
                if (!GetState || GetStateType == StateTypeEnum.Manual)
                    SetState(StateTypeEnum.Auto);
            }

        }
    }
}
