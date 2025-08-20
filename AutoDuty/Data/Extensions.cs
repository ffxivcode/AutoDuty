using ECommons;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace AutoDuty.Data
{
    public static class Extensions
    {
        public static void DrawCustomText(this PathAction pathAction, int index, Action? clickedAction)
        {
            ImGui.NewLine();
            GetCustomText(pathAction, index).ForEach(x => TextClicked(x.color, x.text, clickedAction));
        }

        public static List<(Vector4 color, string text)> GetCustomText(this PathAction pathAction, int index)
        {
            List<(Vector4 color, string text)> results = [];

            var v4 = index == Plugin.Indexer ? new Vector4(0, 255, 255, 1) : (pathAction.Name.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase) ? new Vector4(0, 255, 0, 1) : new Vector4(255, 255, 255, 1));

            if (pathAction.Tag.HasFlag(ActionTag.Comment))
            {
                results.Add((new Vector4(0, 1, 0, 1), pathAction.Note));
                return results;
            }
            if (!pathAction.Tag.HasAnyFlag(ActionTag.Revival) && pathAction.Tag != ActionTag.None)
            {
                results.Add((index == Plugin.Indexer ? v4 : new(1, 165 / 255f, 0, 1), $"{pathAction.Tag}"));
                results.Add((v4, "|"));
            }
            results.Add((v4, $"{pathAction.Name}"));
            results.Add((v4, "|"));
            results.Add((v4, $"{pathAction.Position.ToCustomString()}"));
            if (!pathAction.Arguments.All(x => x.IsNullOrEmpty()))
            {
                results.Add((v4, "|"));
                results.Add((v4, $"{pathAction.Arguments.ToCustomString()}"));
            }
            if (!pathAction.Note.IsNullOrEmpty())
            {
                results.Add((v4, "|"));
                results.Add((index == Plugin.Indexer ? v4 : new(0, 1, 0, 1), $"{pathAction.Note}"));
            }
            return results;
        }

        private static void TextClicked(Vector4 col, string text, Action? clicked)
        {
            ImGui.SameLine(0, 0);
            ImGui.TextColored(col, text);
            if (clicked != null && ImGui.IsItemClicked(ImGuiMouseButton.Left) && Plugin.Stage == 0)
                clicked();
        }

        public static string ToCustomString(this Enum T) => T.ToString().Replace("_", " ") ?? "";

        public static bool StartsWithIgnoreCase(this string str, string strsw) => str.StartsWith(strsw, StringComparison.OrdinalIgnoreCase);

        public static string ToCustomString(this List<string> strings, string delimiter = ",")
        {
            string outString = string.Empty;

            foreach (var stringIter in strings.Select((Value, Index) => (Value, Index)))
                outString += (stringIter.Index + 1) < strings.Count ? $"{stringIter.Value}{delimiter}" : $"{stringIter.Value}";

            return outString;
        }

        public static string ToCustomString(this PathAction pathAction) =>$"{(pathAction.Tag.HasAnyFlag(ActionTag.None, ActionTag.Treasure, ActionTag.Revival) ? "" : $"{pathAction.Tag.ToCustomString()}|")}{pathAction.Name}|{pathAction.Position.ToCustomString()}{(pathAction.Arguments.All(x => x.IsNullOrEmpty()) ? "" : $"|{pathAction.Arguments.ToCustomString()}")}{(pathAction.Note.IsNullOrEmpty() ? "" : $"|{pathAction.Note}")}";

        public static string ToCustomString(this Vector3 vector3) => vector3.ToString("F2", CultureInfo.InvariantCulture).Trim('<', '>');

        public static List<string> ToConditional(this string conditionalString)
        {
            var list = new List<string>();
            var firstParse = conditionalString.Replace(" ", string.Empty).Split('(');
            if (firstParse.Length < 2) return list;
            var secondparse = firstParse[1].Split(")");
            if (secondparse.Length < 2) return list;
            var method = firstParse[0];
            var argument = secondparse[0];
            string? operatorValue;
            string? rightSideValue;
            if (secondparse[1][1] == '=')
            {
                operatorValue = $"{secondparse[1][0]}{secondparse[1][1]}";
                rightSideValue = secondparse[1].Replace(operatorValue, string.Empty);
            }
            else
            {
                operatorValue = $"{secondparse[1][0]}";
                rightSideValue = secondparse[1].Replace(operatorValue, string.Empty);
            }
            list.Add(method, argument, operatorValue, rightSideValue);
            return list;
        }


        public static bool TryGetVector3(this string vector3String, out Vector3 vector3)
        {
            vector3 = Vector3.Zero;
            var cul = CultureInfo.InvariantCulture;
            var strcomp = StringComparison.InvariantCulture;
            var splitString = vector3String.Replace(" ", string.Empty, strcomp).Replace("<", string.Empty, strcomp).Replace(">", string.Empty, strcomp).Split(",");

            if (splitString.Length < 3) return false;
            
            vector3 = new(float.Parse(splitString[0], cul), float.Parse(splitString[1], cul), float.Parse(splitString[2], cul));

            return true;
        }

        public static string ToName(this Sounds value)
        {
            return value switch
            {
                Sounds.None => "None",
                Sounds.Sound01 => "Sound Effect 1",
                Sounds.Sound02 => "Sound Effect 2",
                Sounds.Sound03 => "Sound Effect 3",
                Sounds.Sound04 => "Sound Effect 4",
                Sounds.Sound05 => "Sound Effect 5",
                Sounds.Sound06 => "Sound Effect 6",
                Sounds.Sound07 => "Sound Effect 7",
                Sounds.Sound08 => "Sound Effect 8",
                Sounds.Sound09 => "Sound Effect 9",
                Sounds.Sound10 => "Sound Effect 10",
                Sounds.Sound11 => "Sound Effect 11",
                Sounds.Sound12 => "Sound Effect 12",
                Sounds.Sound13 => "Sound Effect 13",
                Sounds.Sound14 => "Sound Effect 14",
                Sounds.Sound15 => "Sound Effect 15",
                Sounds.Sound16 => "Sound Effect 16",
                _ => "Unknown",
            };
        }

        public static bool IsTrustLeveling(this LevelingMode mode) =>
            mode is LevelingMode.TrustGroup or LevelingMode.TrustSolo;


        public static (string url, string name) GetExternalPluginData(this ExternalPlugin plugin) =>
            plugin switch
            {
                ExternalPlugin.vnav => (@"https://puni.sh/api/repository/veyn", "vnavmesh"),
                ExternalPlugin.BossMod => (@"https://puni.sh/api/repository/veyn", "BossMod"),
                ExternalPlugin.Avarice => (@"https://love.puni.sh/ment.json", "Avarice"),
                ExternalPlugin.RotationSolverReborn => (@"https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json", "RotationSolver"),
                ExternalPlugin.WrathCombo => (@"https://love.puni.sh/ment.json", "WrathCombo"),
                ExternalPlugin.AutoRetainer => (@"https://love.puni.sh/ment.json", "AutoRetainer"),
                ExternalPlugin.Gearsetter => (@"https://plugins.carvel.li/", "Gearsetter"),
                ExternalPlugin.Stylist => (@"https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json", "Stylist"),
                ExternalPlugin.Lifestream => (@"https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json", "Lifestream"),
                ExternalPlugin.AntiAFK => (@"https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json", "AntiAfkKick-Dalamud"),
                _ => throw new ArgumentOutOfRangeException(nameof(plugin), plugin, null)
            };

        public static string GetExternalPluginName(this ExternalPlugin plugin) =>
            plugin switch
            {
                ExternalPlugin.vnav => "vnavmesh",
                ExternalPlugin.BossMod => "Boss Mod",
                ExternalPlugin.Avarice => "Avarice",
                ExternalPlugin.RotationSolverReborn => "Rotation Solver Reborn",
                ExternalPlugin.WrathCombo => "Wrath Combo",
                ExternalPlugin.AutoRetainer => "AutoRetainer",
                ExternalPlugin.Gearsetter => "Gearsetter",
                ExternalPlugin.Stylist => "Stylist",
                ExternalPlugin.Lifestream => "Lifestream",
                ExternalPlugin.AntiAFK => "Anti-AfkKick",
                _ => throw new ArgumentOutOfRangeException(nameof(plugin), plugin, null)
            };
    }
}
