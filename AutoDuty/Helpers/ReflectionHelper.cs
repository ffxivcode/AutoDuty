using ECommons.Automation;
using ECommons.Reflection;
using System;
using System.Reflection;
using System.Reflection.Emit;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

#nullable disable

namespace AutoDuty.Helpers
{
    using System.Linq;
    using ECommons.DalamudServices;
    using ECommons.EzSharedDataManager;
    using IPC;
    using static Data.Enums;

    internal class ReflectionHelper
    {
        // What do you mean just (BindingFlags) 60 isn't great ?
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static class YesAlready_Reflection
        {
            internal static bool IsEnabled => DalamudReflector.TryGetDalamudPlugin("YesAlready", out _, false, true);

            internal static void SetPluginEnabled(bool trueFalse)
            {
                if (DalamudReflector.TryGetDalamudPlugin("YesAlready", out var pl, false, true))
                    pl.GetFoP("Config").SetFoP("Enabled", trueFalse);
            }

            internal static bool GetPluginEnabled()
            {
                if (DalamudReflector.TryGetDalamudPlugin("YesAlready", out var pl, false, true))
                    return (bool)pl.GetFoP("Config").GetFoP("Enabled");
                else return false;
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
                        Chat.ExecuteCommand("/rotation manual");
                        break;
                    case StateTypeEnum.Auto:
                        Chat.ExecuteCommand("/rotation auto");
                        break;
                    default:
                        Chat.ExecuteCommand("/rotation cancel");
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

        internal static class BossModReborn_Reflection
        {
            internal static readonly object configInstance;

            internal static FieldRef<object, float> MaxDistanceToTarget;

            static BossModReborn_Reflection()
            {
                try
                {
                    if (BossModReborn_IPCSubscriber.IsEnabled && DalamudReflector.TryGetDalamudPlugin("BossModReborn", out var pl, false, true))
                    {
                        Assembly assembly = Assembly.GetAssembly(pl.GetType());
                        Type configType = assembly.GetType("BossMod.AI.AIConfig");
                        FieldInfo fieldInfo = configType.GetField("MaxDistanceToTarget", (BindingFlags)60);
                        MaxDistanceToTarget = FieldRefAccess<object, float>(fieldInfo, false);
                        Type managerType = assembly.GetType("BossMod.AI.AIManager");
                        object instanceField = managerType.GetField("Instance").GetValue(null);
                        FieldInfo field = managerType.GetField("_config", (BindingFlags)60);
                        configInstance = field.GetValue(instanceField);
                    }
                }
                catch (Exception ex)
                {
                    Svc.Log.Error(ex.ToString());
                }
            }
        }

        internal static class Avarice_Reflection
        {
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

            public static bool PositionalChanged(out Positional positional)
            {
                if (avariceReady && Plugin.Configuration is { AutoManageBossModAISettings: true, positionalAvarice: true })
                {
                    positional = Positional.Any;

                    if (EzSharedData.TryGet<uint[]>("Avarice.PositionalStatus", out uint[] ret))
                    {
                        if (ret[1] == 1)
                            positional = Positional.Rear;
                        if (ret[1] == 2)
                            positional = Positional.Flank;
                    }

                    if (Plugin.Configuration.PositionalEnum != positional)
                    {
                        Plugin.Configuration.PositionalEnum = positional;
                        return true;
                    }
                }
                positional = Plugin.Configuration.PositionalEnum;
                return false;
            }


            static Avarice_Reflection()
            {
                
                if (DalamudReflector.TryGetDalamudPlugin("Avarice", out var pl, false, true))
                {
                    avariceReady = true;
                    
                    /*
                    Assembly assembly = Assembly.GetAssembly(pl.GetType());
                    
                    not used anymore, but might as well keep it here as an example
                    Positionals = StaticFieldRefAccess<SortedList<uint, byte>>(assembly.GetType("Avarice.StaticData.Data").GetField("ActionPositional", BindingFlags.Static | BindingFlags.Public));
                    
                    Type utilType = assembly?.GetType("Avarice.Util");

                    if (utilType != null)
                    {
                        isReaperRear  = MethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsReaperAnticipatedRear"));
                        isSamuraiRear = MethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsSamuraiAnticipatedRear"));
                        isDragoonRear = MethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsDragoonAnticipatedRear"));
                        isViperRear   = MethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsViperAnticipatedRear"));

                        isReaperFlank  = MethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsReaperAnticipatedFlank"));
                        isSamuraiFlank = MethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsSamuraiAnticipatedFlank"));
                        isDragoonFlank = MethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsDragoonAnticipatedFlank"));
                        isViperFlank   = MethodDelegate<StaticBoolMethod>(utilType.GetMethod("IsViperAnticipatedFlank"));

                        avariceReady = true;
                    }*/
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

        public delegate ref F FieldRef<in T, F>(T instance = default);

        internal static FieldRef<T, F> FieldRefAccess<T, F>(FieldInfo fieldInfo, bool needCastclass)
        {
            var delegateInstanceType = typeof(T);
            var declaringType        = fieldInfo.DeclaringType;

            var dm = new DynamicMethod($"__refget_{delegateInstanceType.Name}_fi_{fieldInfo.Name}",
                                       typeof(F).MakeByRefType(), [delegateInstanceType]);

            var il = dm.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);

            if (needCastclass)
                il.Emit(OpCodes.Castclass, declaringType);
            il.Emit(OpCodes.Ldflda, fieldInfo);

            il.Emit(OpCodes.Ret);

            return dm.CreateDelegate<FieldRef<T, F>>();
        }


        public delegate bool StaticBoolMethod();

        public static DelegateType MethodDelegate<DelegateType>(MethodInfo method, object instance = null, Type delegateInstanceType = null) where DelegateType : Delegate
        {
            try
            {
                if ((object)method == null)
                    throw new ArgumentNullException("method");
                Type delegateType = typeof(DelegateType);
                if (method.IsStatic)
                    return (DelegateType)Delegate.CreateDelegate(delegateType, method);

                Type declaringType = method.DeclaringType;

                if (instance is null)
                {
                    ParameterInfo[] delegateParameters = delegateType.GetMethod("Invoke").GetParameters();
                    delegateInstanceType ??= delegateParameters[0].ParameterType;

                    if (declaringType is { IsInterface: true } && delegateInstanceType.IsValueType)
                    {
                        InterfaceMapping interfaceMapping = delegateInstanceType.GetInterfaceMap(declaringType);
                        method        = interfaceMapping.TargetMethods[Array.IndexOf(interfaceMapping.InterfaceMethods, method)];
                        declaringType = delegateInstanceType;
                    }
                }

                ParameterInfo[] parameters     = method.GetParameters();
                int             numParameters  = parameters.Length;
                Type[]          parameterTypes = new Type[numParameters + 1];
                parameterTypes[0] = declaringType;
                for (int i = 0; i < numParameters; i++)
                    parameterTypes[i + 1] = parameters[i].ParameterType;

                Type[]        delegateArgsResolved = delegateType.GetGenericArguments();
                Type[]        dynMethodReturn      = delegateArgsResolved.Length < parameterTypes.Length ? parameterTypes : delegateArgsResolved;
                DynamicMethod dmd                  = new("OpenInstanceDelegate_" + method.Name, method.ReturnType, dynMethodReturn);
                ILGenerator   ilGen                = dmd.GetILGenerator();
                if (declaringType is { IsValueType: true } && delegateArgsResolved.Length > 0 && !delegateArgsResolved[0].IsByRef)

                    ilGen.Emit(OpCodes.Ldarga_S, 0);
                else
                    ilGen.Emit(OpCodes.Ldarg_0);
                for (int i = 1; i < parameterTypes.Length; i++)
                {
                    ilGen.Emit(OpCodes.Ldarg, i);

                    if (parameterTypes[i].IsValueType && i < delegateArgsResolved.Length &&
                        !delegateArgsResolved[i].IsValueType)

                        ilGen.Emit(OpCodes.Unbox_Any, parameterTypes[i]);
                }

                ilGen.Emit(OpCodes.Call, method);
                ilGen.Emit(OpCodes.Ret);
                Svc.Log.Info(delegateType.FullName);
                Svc.Log.Info(string.Join(" | ", delegateType.GenericTypeArguments.Select(t => t.FullName)));
                Svc.Log.Info(string.Join(" | ", parameterTypes.Select(t => t.FullName)));
                return (DelegateType)dmd.CreateDelegate(delegateType);
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }

            return null;
        }
    }
}
