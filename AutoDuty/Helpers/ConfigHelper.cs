using System;
using System.Reflection;
using ECommons.DalamudServices;

namespace AutoDuty.Helpers
{
    internal static class ConfigHelper
    {
        internal static bool ModifyConfig(string configName, string configValue)
        {
            FieldInfo? field = null;
            if ((field = FindConfig(configName)) == null)
            {
                Svc.Log.Error($"Unable to find config: {configName}, please type /autoduty cfg list to see all available configs");
                return false;
            }
            else if (field.FieldType.ToString().Contains("System.Collections", StringComparison.InvariantCultureIgnoreCase) || field.FieldType.ToString().Contains("Dalamud.Plugin", StringComparison.InvariantCultureIgnoreCase))
                return false;
            else
            {
                var configType = ConfigType(field);
                if (configType == "System.Boolean" && (configValue.ToLower().Equals("true") || configValue.ToLower().Equals("false")))
                    field.SetValue(AutoDuty.Plugin.Configuration, bool.Parse(configValue));
                else if (configType == "System.Int32" && int.TryParse(configValue, out var i))
                    field.SetValue(AutoDuty.Plugin.Configuration, i);
                else if (configType == "System.String")
                    field.SetValue(AutoDuty.Plugin.Configuration, configValue);
                else
                    Svc.Log.Error($"Unable to set config setting: {field.Name.Replace(">k__BackingField", "").Replace("<", "")}, value must be of type: {field.FieldType.ToString().Replace("System.", "")}");
            }
            return false;
        }

        internal static void ListConfig()
        {
            var i = Assembly.GetExecutingAssembly().GetType("AutoDuty.Windows.Configuration")?.GetFields(All);
            foreach (var field in i)
            {
                if (!field.FieldType.ToString().Contains("System.Collections",StringComparison.InvariantCultureIgnoreCase) && !field.FieldType.ToString().Contains("Dalamud.Plugin", StringComparison.InvariantCultureIgnoreCase) && !field.Name.Replace(">k__BackingField", "").Replace("<", "").Equals("Version",StringComparison.InvariantCultureIgnoreCase))
                    Svc.Log.Info($"{field.Name.Replace(">k__BackingField", "").Replace("<", "")} = {field.GetValue(AutoDuty.Plugin.Configuration)} ({field.FieldType.ToString().Replace("System.", "")})");
            }
        }

        internal static FieldInfo? FindConfig(string configName)
        {
            var i = Assembly.GetExecutingAssembly().GetType("AutoDuty.Windows.Configuration")?.GetFields(All);
            foreach (var field in i!)
            {
                //Getting the Name
                //Getting the Name and current value
                //Getting the Type
                //Svc.Log.Info($"{field.Name.Replace(">k__BackingField", "").Replace("<", "")} = {field.GetValue(AutoDuty.Plugin.Configuration)} typeOf {field.FieldType}");
                if (field.Name.Replace(">k__BackingField", "").Replace("<", "").Equals("Version", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                if (field.Name.Replace(">k__BackingField", "").Replace("<", "").Equals(configName, StringComparison.InvariantCultureIgnoreCase))
                    return field;
                
            }
            return null;
        }

        private static string ConfigType(FieldInfo field) => field.FieldType.ToString();

        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        
    }
}
