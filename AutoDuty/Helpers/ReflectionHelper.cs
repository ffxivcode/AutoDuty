using ECommons.Automation;
using ECommons.Reflection;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
#nullable disable

namespace AutoDuty.Helpers
{
    using System.Collections.Generic;
    using ECommons.DalamudServices;

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

            internal static void SetConfigValue(string configName, string value)
            {
                //not yet implemented
            }

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

        internal static class Avarice_Reflection
        {
            /*
            || Util.IsReaperAnticipatedRear()
               || Util.IsSamuraiAnticipatedRear()
               || Util.IsDragoonAnticipatedRear()
               || Util.IsViperAnticipatedRear()

            */
            private static readonly StaticBoolMethod isReaperRear;
            private static readonly StaticBoolMethod isSamuraiRear;
            private static readonly StaticBoolMethod isDragoonRear;
            private static readonly StaticBoolMethod isViperRear;

            public static bool IsRear() =>
                isReaperRear()  ||
                isSamuraiRear() ||
                isDragoonRear() ||
                isViperRear();

            private static readonly StaticBoolMethod isReaperFlank;
            private static readonly StaticBoolMethod isSamuraiFlank;
            private static readonly StaticBoolMethod isDragoonFlank;
            private static readonly StaticBoolMethod isViperFlank;

            public static bool IsFlank() =>
                isReaperFlank()  ||
                isSamuraiFlank() ||
                isDragoonFlank() ||
                isViperFlank();

            public static readonly bool avariceReady;

            //internal static readonly FieldRef<SortedList<uint, byte>> Positionals;

            static Avarice_Reflection()
            {
                if (DalamudReflector.TryGetDalamudPlugin("Avarice", out var pl, false, true))
                {
                    Assembly assembly = Assembly.GetAssembly(pl.GetType());
                    /* not used anymore, but might as well keep it here as an example
                    Positionals = StaticFieldRefAccess<SortedList<uint, byte>>(assembly.GetType("Avarice.StaticData.Data").GetField("ActionPositional", BindingFlags.Static | BindingFlags.Public));
                    */
                    Type utilType = assembly?.GetType("Avarice.Util");

                    if (utilType != null)
                    {
                        isReaperRear  = StaticMethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsReaperAnticipatedRear"));
                        isSamuraiRear = StaticMethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsSamuraiAnticipatedRear"));
                        isDragoonRear = StaticMethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsDragoonAnticipatedRear"));
                        isViperRear   = StaticMethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsViperAnticipatedRear"));

                        isReaperFlank  = StaticMethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsReaperAnticipatedFlank"));
                        isSamuraiFlank = StaticMethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsSamuraiAnticipatedFlank"));
                        isDragoonFlank = StaticMethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsDragoonAnticipatedFlank"));
                        isViperFlank   = StaticMethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsViperAnticipatedFlank"));

                        avariceReady = true;
                    }
                }

            }
        }



        public delegate ref F FieldRef<F>();
        internal static FieldRef<F> StaticFieldRefAccess<F>(FieldInfo fieldInfo)
        {
            if (fieldInfo.IsStatic is false)
                throw new ArgumentException("Field must be static");

            DynamicMethod dm = new($"__refget_{fieldInfo.DeclaringType?.Name ?? "null"}_static_fi_{fieldInfo.Name}", typeof(F).MakeByRefType(), []);

            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldsflda, fieldInfo);
            il.Emit(OpCodes.Ret);

            return (FieldRef<F>)dm.CreateDelegate(typeof(FieldRef<F>));
        }

        public delegate bool StaticBoolMethod();



        public static DelegateType StaticMethodDelegate<DelegateType>(MethodInfo method) where DelegateType : Delegate
        {
            if ((object)method == null) 
                throw new ArgumentNullException("method");
            Type delegateType = typeof(DelegateType);
            if (method.IsStatic) 
                return (DelegateType)Delegate.CreateDelegate(delegateType, method);
            return null;
        }
    }
}
