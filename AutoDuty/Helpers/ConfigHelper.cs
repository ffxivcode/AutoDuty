using System;
using System.Reflection;
using ECommons.DalamudServices;

namespace AutoDuty.Helpers
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

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
            else if (field.FieldType.ToString().Contains("System.Collections", StringComparison.InvariantCultureIgnoreCase) || field.FieldType.ToString().Contains("Dalamud.Plugin", StringComparison.InvariantCultureIgnoreCase))
                return string.Empty;
            else
                return field.GetValue(Plugin.Configuration)?.ToString() ?? string.Empty;
        }

        internal static bool ModifyConfig(string configName, string configValue)
        {
            FieldInfo? field;
            if ((field = FindConfig(configName)) == null)
            {
                Svc.Log.Error($"Unable to find config: {configName}, please type /autoduty cfg list to see all available configs");
                return false;
            }
            else if (field.FieldType.ToString().Contains("System.Collections", StringComparison.InvariantCultureIgnoreCase) || field.FieldType.ToString().Contains("Dalamud.Plugin", StringComparison.InvariantCultureIgnoreCase))
                return false;
            else
            {
                var configType = field.FieldType;// ConfigType(field);

                if (configType == typeof(string))
                {
                    field.SetValue(Plugin.Configuration, configValue);
                } else if (configType.IsEnum)
                {
                    if(Enum.TryParse(configType, configValue, true, out object? configEnum))
                    {
                        field.SetValue(Plugin.Configuration, configEnum);
                    } else
                    {
                        Svc.Log.Error($"Unable to set config setting: {field.Name.Replace(">k__BackingField", "").Replace("<", "")}, value must be of type: {field.FieldType.ToString().Replace("System.", "")}");
                        return false;
                    }
                }
                else if (configType == typeof(TrustMemberName?[]))
                {
                    string[] memberNames = configValue.Split(",");

                    if (memberNames.Length > 3)
                    {
                        Svc.Log.Error("Unable to set more than 3 trust members");
                        return false;
                    }

                    List<TrustMemberName> members = [];

                    foreach (string memberName in memberNames)
                        if (Enum.TryParse(typeof(TrustMemberName), memberName, true, out object? member))
                            members.Add((TrustMemberName)member);

                    if (members.Count <= 0)
                    {
                        Svc.Log.Error("No trust members recognized");
                        return false;
                    }

                    TrustMemberName?[] value = (TrustMemberName?[]) field.GetValue(Plugin.Configuration);

                    for (int i = 0; i < members.Count; i++)
                    {
                        TrustMemberName member = members[i];
                        value[i] = member;
                    }
                    field.SetValue(Plugin.Configuration, value);
                }
                else if(configType.GetInterface(nameof(IConvertible)) != null)
                {
                    object newConfigValue = Convert.ChangeType(configValue, configType, CultureInfo.InvariantCulture);
                    if(newConfigValue != null)
                    {
                        field.SetValue(Plugin.Configuration, newConfigValue);
                    }
                    else
                    {
                        Svc.Log.Error($"Unable to set config setting: {field.Name.Replace(">k__BackingField", "").Replace("<", "")}, value must be of type: {field.FieldType.ToString().Replace("System.", "")}");
                        return false;
                    }
                }
                else
                {
                    Svc.Log.Error($"Unable to set config setting: {field.Name.Replace(">k__BackingField", "").Replace("<", "")}, value must be of type: {field.FieldType.ToString().Replace("System.", "")}");
                    return false;
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
                if (!field.FieldType.ToString().Contains("System.Collections",StringComparison.InvariantCultureIgnoreCase) && !field.FieldType.ToString().Contains("Dalamud.Plugin", StringComparison.InvariantCultureIgnoreCase) && !field.Name.Replace(">k__BackingField", "").Replace("<", "").Equals("Version",StringComparison.InvariantCultureIgnoreCase))
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
