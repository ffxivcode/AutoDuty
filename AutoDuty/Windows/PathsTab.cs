using Dalamud.Interface.Utility.Raii;
using ECommons;
using ImGuiNET;
using static AutoDuty.AutoDuty;
using System.Numerics;
using System.Linq;
using AutoDuty.Helpers;
using System.Diagnostics;
using Dalamud.Interface.Utility;

namespace AutoDuty.Windows
{
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Net.Mime;
    using System.Text.RegularExpressions;
    using ECommons.DalamudServices;

    internal static class PathsTab
    {
        private static int _selectedIndex = -1;
        private static string _selectedPath = "";
        private static bool _checked = false;

        private static void CheckBoxOnChange()
        {
            if (_selectedPath.IsNullOrEmpty())
            {
                _checked = false;
                return;
            }

            if (_checked)
                Plugin.Configuration.DoNotUpdatePathFiles.Add(_selectedPath);
            else
                Plugin.Configuration.DoNotUpdatePathFiles.Remove(_selectedPath);

            Plugin.Configuration.Save();
        }

        internal static void Draw()
        {
            ImGui.Text($"Path Files");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Button("Open Folder"))
                Process.Start("explorer.exe", Plugin.PathsDirectory.FullName);

            ImGui.SameLine();
            using (var d = ImRaii.Disabled(_selectedPath.IsNullOrEmpty()))
            {
                if (ImGui.Button("Open File"))
                    Process.Start("explorer", $"\"{Plugin.PathsDirectory.FullName}{Path.DirectorySeparatorChar}{_selectedPath}\"");
            }

            if (ImGui.Checkbox($"Do not overwrite on update", ref _checked))
                CheckBoxOnChange();

            if (!ImGui.BeginListBox("##DutyList", new Vector2(500 * ImGuiHelpers.GlobalScale, 400 * ImGuiHelpers.GlobalScale))) return;

            foreach (var pathFileKVP in FileHelper.DictionaryPathFiles.Select((Value, Index) => (Value, Index)))
            {
                bool   multiple = false;

                const string idColor   = "<0,0,1>";
                const string dutyColor = "<0,1,0>";

                string dutyText = $"({idColor}{pathFileKVP.Value.Key}</>) {dutyColor}{ContentHelper.DictionaryContent[pathFileKVP.Value.Key].DisplayName}</>";
                if (pathFileKVP.Value.Value.Count > 1)
                {
                    multiple = true;
                    ImGui.NewLine();
                    ImGui.SameLine(1);
                    ColoredText(dutyText);
                    ImGui.BeginGroup();
                    ImGui.Indent(20);
                }

                foreach (string path in pathFileKVP.Value.Value)
                {
                    if (ImGui.Selectable(string.Empty, pathFileKVP.Index == _selectedIndex && path == _selectedPath))
                    {
                        if (path == _selectedPath)
                        {
                            _selectedIndex = -1;
                            _selectedPath  = "";
                        }
                        else
                        {
                            _checked = Plugin.Configuration.DoNotUpdatePathFiles.Contains(path);

                            _selectedIndex = pathFileKVP.Index;
                            _selectedPath  = path;
                        }
                    }
                    ImGui.SetItemAllowOverlap();
                    ImGui.SameLine(multiple ? 20 : 1);

                    Match pathMatch = Regex.Match(path, @"(\()([0-9]{3,4})(\))(.*)(\.json)");

                    string pathUI = pathMatch.Success ? $"{pathMatch.Groups[1]}{idColor}{pathMatch.Groups[2]}</>{pathMatch.Groups[3]}<0.8,0.8,0.8>{pathMatch.Groups[4]}</><0.5,0.5,0.5>{pathMatch.Groups[5]}</>" : path;

                    ColoredText(multiple ? $"{pathUI}" : $"{dutyText} => {pathUI}");
                }

                if (multiple)
                    ImGui.EndGroup();
            }

            ImGui.EndListBox();
        }

        public static void ColoredText(string text)
        {
            Match regex = Regex.Match(text, @"([^<]*)?(?><?([0-9\. ]*\,[0-9\. ]*\,[0-9\. ]*)>([^<]*)<\/>)?");

            void SameLine() => ImGui.SameLine(0, 0);


            if (regex.Success)
            {
                bool first = true;

                do
                {
                    bool nonColoredSet = false;

                    //Svc.Log.Debug(string.Join(" | ", regex.Groups.Values.Select(g=> g.Value)));

                    string nonColored = regex.Groups[1].Value;
                    if (!nonColored.IsNullOrEmpty())
                    {
                        if(!first)
                            SameLine();

                        first = false;
                        ImGui.Text(nonColored);
                        nonColoredSet = true;
                        //Svc.Log.Debug("non colored: " + nonColored);
                    }

                    string colorText   = regex.Groups[2].Value;
                    string coloredText = regex.Groups[3].Value;
                    if (!colorText.IsNullOrEmpty() && !coloredText.IsNullOrEmpty())
                    {
                        string[] split = colorText.Split(',');
                        if (split.Length >= 3)
                        {
                            if (float.TryParse(split[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float r))
                                if (float.TryParse(split[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float g))
                                    if (float.TryParse(split[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float b))
                                    {
                                        float a = 1;
                                        if (split.Length == 4 && float.TryParse(split[3], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out a))
                                        {
                                        }

                                        if(nonColoredSet)
                                            SameLine();
                                        else if (!first)
                                            SameLine();

                                        first = false;

                                        Vector4 color = new Vector4(r, g, b, a);
                                        ImGui.TextColored(color, coloredText);

                                        //Svc.Log.Debug("colored: " + coloredText + " in: " + color);
                                    }
                        }
                    }
                    regex = regex.NextMatch();
                } while (regex.Success);
            }
            else
            {
                ImGui.Text(text);
            }
        }
    }
}
