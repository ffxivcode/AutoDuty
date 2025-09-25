using System;
using System.Reflection;
using ECommons.DalamudServices;

namespace AutoDuty.Helpers
{
    using System.Collections;
    using FFXIVClientStructs.FFXIV.Common.Configuration;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using static System.Runtime.InteropServices.JavaScript.JSType;

    internal static class ConfigHelper
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static string ConfigType(FieldInfo field) => field.FieldType.ToString();

        internal static string GetConfig(string configName)
        {
            FieldInfo? field;
            if ((field = FindConfig(configName)) == null)
            {
                Svc.Log.Error($"Unable to find config: {configName}, please type /autoduty cfg list to see all available configs");
                return string.Empty;
            }
            else if (field.FieldType.ToString().Contains("Dalamud.Plugin", StringComparison.InvariantCultureIgnoreCase))
                return string.Empty;
            else
                return field.GetValue(Plugin.Configuration)?.ToString() ?? string.Empty;
        }

        private static object? ModifyConfig(Type configType, string configValue, out string failReason)
        {
            failReason = $"value must be of type: {configType.ToString().Replace("System.", "")}";

            if (configType == typeof(string))
                return configValue;
            else if (configType.IsEnum)
            {
                if (Enum.TryParse(configType, configValue, true, out object? configEnum))
                    return configEnum;
            } else if (configType is { IsGenericType: true, IsGenericTypeDefinition: false } && configType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return Activator.CreateInstance(configType, ModifyConfig(configType.GetGenericArguments()[0], configValue, out failReason));
            }
            else if (configType.GetInterface(nameof(IConvertible)) != null)
            {
                return Convert.ChangeType(configValue, configType, CultureInfo.InvariantCulture);
            }


            return null;
        }

        internal static bool ModifyConfig(string configName, params string[] configValues)
        {

            FieldInfo? field;
            if ((field = FindConfig(configName)) == null)
            {
                Svc.Log.Error($"Unable to find config: {configName}, please type /autoduty cfg list to see all available configs");
                return false;
            }
            else if (field.FieldType.ToString().Contains("Dalamud.Plugin", StringComparison.InvariantCultureIgnoreCase))
                return false;
            else
            {
                void PrintError(string failReason)
                {
                    Svc.Log.Error($"Unable to set config setting: {field.Name.Replace(" > k__BackingField", "").Replace(" < ", "")}: {failReason}");
                }

                var configType = field.FieldType;// ConfigType(field);

                if (configType.IsAssignableTo(typeof(IList)))
                {
                    IList valueList      = (IList)field.GetValue(Plugin.Configuration)!;
                    Type  enumerableType = configType.GetElementType() ?? configType.GenericTypeArguments.First();

                    switch (configValues[0])
                    {
                        case "set":
                            // /ad cfg SelectedTrustMembers set Yshtola Graha Thancred
                            // /ad cfg CustomCommandsTermination set "/snd run Wep" "/snd run Leveling"

                            if (!valueList.IsFixedSize)
                                valueList.Clear();

                            for (int i = 1; i < configValues.Length; i++)
                            {
                                object? val = ModifyConfig(enumerableType, configValues[i], out _);
                                if (val != null)
                                    if (!valueList.IsFixedSize)
                                        valueList.Add(val);
                                    else
                                        valueList[i-1] = val;
                            }
                            break;
                        case "add":
                            // /ad cfg CustomCommandsTermination add "/test"

                            if (valueList.IsFixedSize)
                                PrintError("Can't use add on fixed size configs");

                            foreach (string t in configValues[1..])
                            {
                                object? val = ModifyConfig(enumerableType, t, out _);
                                if (val != null)
                                    valueList.Add(val);
                            }
                            break;
                        case "del":
                        case "delete":
                        case "rem":
                        case "remove":
                            // /ad cfg CustomCommandsTermination del 2 1

                            if (valueList.IsFixedSize)
                                PrintError("Can't use delete on fixed size configs");

                            for (int i = 1; i < configValues.Length; i++)
                            {
                                if (int.TryParse(configValues[i], out int index))
                                    valueList.RemoveAt(index);
                            }
                            break;
                        case "delentry":
                        case "deleteentry":
                        case "rementry":
                        case "removeentry":
                            // /ad cfg CustomCommandsTermination delEntry /test

                            if (valueList.IsFixedSize)
                                PrintError("Can't use delete on fixed size configs");

                            for (int i = 1; i < configValues.Length; i++)
                            {
                                object? entry = ModifyConfig(enumerableType, configValues[i], out string _);
                                
                                if (entry != null)
                                {
                                    int index = valueList.IndexOf(entry);
                                    if (i >= 0)
                                        valueList.RemoveAt(index);
                                }
                            }
                            break;
                        case "insert":
                            // /ad cfg CustomCommandsTermination insert 1 "/test 1" "/test 2"
                            if (valueList.IsFixedSize)
                                PrintError("Can't use insert on fixed size configs");

                            if (int.TryParse(configValues[1], out int insertIndex))
                                for (int i = 2; i < configValues.Length; i++)
                                {
                                    object? entry = ModifyConfig(enumerableType, configValues[i], out string _);

                                    if (entry != null) 
                                        valueList.Insert(insertIndex++, entry);
                                }

                            break;
                    }
                }
                else
                {
                    object? newValue = ModifyConfig(configType, configValues[0], out string failReason);

                    if (newValue != null)
                        field.SetValue(Plugin.Configuration, newValue);
                    else
                        PrintError(failReason);
                }

                Plugin.Configuration.Save();
            }
            return false;
        }

        internal static void ListConfig()
        {
            var i = Assembly.GetExecutingAssembly().GetType("AutoDuty.Windows.Configuration")?.GetFields(All);
            if (i == null) return;
            foreach (var field in i)
            {
                if (!field.FieldType.ToString().Contains("Dalamud.Plugin", StringComparison.InvariantCultureIgnoreCase) && !field.Name.Replace(">k__BackingField", "").Replace("<", "").Equals("Version",StringComparison.InvariantCultureIgnoreCase))
                    Svc.Log.Info($"{field.Name.Replace(">k__BackingField", "").Replace("<", "")} = {field.GetValue(Plugin.Configuration)} ({field.FieldType.ToString().Replace("System.", "")})");
            }
        }

        internal static FieldInfo? FindConfig(string configName)
        {
            var i = Assembly.GetExecutingAssembly().GetType("AutoDuty.Windows.Configuration")?.GetFields(All);
            foreach (var field in i!)
            {
                if (field.Name.Replace(">k__BackingField", "").Replace("<", "").Equals("Version", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                if (field.Name.Replace(">k__BackingField", "").Replace("<", "").Equals(configName, StringComparison.InvariantCultureIgnoreCase))
                    return field;
                
            }
            return null;
        }
    }
}
