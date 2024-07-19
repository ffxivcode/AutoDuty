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
    using System.IO;

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
                string dutyText = $"({pathFileKVP.Value.Key}) {ContentHelper.DictionaryContent[pathFileKVP.Value.Key].DisplayName}";
                if (pathFileKVP.Value.Value.Count > 1)
                {
                    multiple = true;
                    ImGui.Text(dutyText);
                    ImGui.BeginGroup();
                    ImGui.Indent(20);
                }

                foreach (string path in pathFileKVP.Value.Value)
                {
                    if (ImGui.Selectable(multiple ? path : $"{dutyText} => {path}", pathFileKVP.Index == _selectedIndex && path == _selectedPath))
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
                }

                if (multiple)
                    ImGui.EndGroup();
            }

            ImGui.EndListBox();
        }
    }
}
