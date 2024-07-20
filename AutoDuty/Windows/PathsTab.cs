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

            if (!ImGui.BeginListBox("##DutyList", new Vector2(450 * ImGuiHelpers.GlobalScale, 575 * ImGuiHelpers.GlobalScale))) return;

            foreach (var pathFileKVP in FileHelper.DictionaryPathFiles.Select((Value, Index) => (Value, Index)))
            {
                if (ImGui.Selectable(pathFileKVP.Value.Value, pathFileKVP.Index == _selectedIndex))
                {
                    if (pathFileKVP.Index == _selectedIndex)
                    {
                        _selectedIndex = -1;
                        _selectedPath = "";
                    }
                    else
                    {
                        if (Plugin.Configuration.DoNotUpdatePathFiles.Contains(pathFileKVP.Value.Value))
                            _checked = true;
                        else
                            _checked = false;

                        _selectedIndex = pathFileKVP.Index;
                        _selectedPath = pathFileKVP.Value.Value;
                    }
                }
            }

            ImGui.EndListBox();
        }
    }
}
