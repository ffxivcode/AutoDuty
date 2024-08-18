using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using static AutoDuty.AutoDuty;
using System.Numerics;
using System.Linq;
using AutoDuty.Helpers;
using System.Diagnostics;
using Dalamud.Interface.Utility;
using System;
using System.Collections.Generic;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using AutoDuty.Managers;

namespace AutoDuty.Windows
{
    internal static class PathsTab
    {
        //private static Dictionary<CombatRole, Job[]> _jobs = Enum.GetValues<Job>().Where(j => !j.IsUpgradeable() && j != Job.BLU).GroupBy(j => j.GetRole()).Where(ig => ig.Key != CombatRole.NonCombat).ToDictionary(ig => ig.Key, ig => ig.ToArray());
        private static ContentPathsManager.DutyPath? _selectedDutyPath;
        private static bool                          _checked = false;

        private static void CheckBoxOnChange()
        {
            if (_selectedDutyPath == null)
            {
                _checked = false;
                return;
            }

            if (_checked)
                Plugin.Configuration.DoNotUpdatePathFiles.Add(_selectedDutyPath.FileName);
            else
                Plugin.Configuration.DoNotUpdatePathFiles.Remove(_selectedDutyPath.FileName);

            _selectedDutyPath.UpdateColoredNames();

            Plugin.Configuration.Save();
        }

        internal static void Draw()
        {
            if (MainWindow.CurrentTabName != "Paths")
                MainWindow.CurrentTabName = "Paths";
            ImGui.Text($"Path Files");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Button("Open Folder"))
                Process.Start("explorer.exe", Plugin.PathsDirectory.FullName);

            ImGui.SameLine();
            using (var d = ImRaii.Disabled(_selectedDutyPath == null))
            {
                if (ImGui.Button("Open File"))
                    Process.Start("explorer", _selectedDutyPath?.FilePath ?? string.Empty);
            }
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 1, 1));
            if (ImGui.Checkbox($"Do not overwrite on update", ref _checked))
                CheckBoxOnChange();

            ImGui.PopStyleColor();
            ImGui.SameLine();
            using (var savedPathsDisabled = ImRaii.Disabled(!Plugin.Configuration.PathSelections.Any(kvp => kvp.Value.Any())))
            {
                if (ImGui.Button("Clear all cached classes"))
                {
                    Plugin.Configuration.PathSelections.Clear();
                    Plugin.Configuration.Save();
                }
            }


            ImGuiStylePtr style = ImGui.GetStyle();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, style.Colors[(int)ImGuiCol.FrameBg]);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, style.FrameRounding);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, style.FrameBorderSize);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,   style.FramePadding);

            ImGui.BeginChild("##DutyList", new Vector2(500 * ImGuiHelpers.GlobalScale, 550 * ImGuiHelpers.GlobalScale), false,
                             ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar);
            try
            {
                foreach ((uint key, ContentPathsManager.ContentPathContainer? container) in ContentPathsManager.DictionaryPaths)
                {
                    bool multiple = false;

                    if (container.Paths.Count > 1)
                    {
                        multiple = true;
                        ImGui.NewLine();
                        ImGui.SameLine(1);
                        ImGuiHelper.ColoredText(container.ColoredNameRegex, $"({key}) {container.Content.Name}");
                        ImGui.BeginGroup();
                        ImGui.Indent(20);
                    }

                    List<Tuple<CombatRole, Job>>[] pathJobs = Enumerable.Range(0, container.Paths.Count).Select(_ => new List<Tuple<CombatRole, Job>>()).ToArray();

                    if (multiple)
                        if (Plugin.Configuration.PathSelections.TryGetValue(key, out Dictionary<Job, int>? pathSelections))
                            foreach ((Job job, int index) in pathSelections)
                                pathJobs[index].Add(new Tuple<CombatRole, Job>(job.GetRole(), job));

                    for (int pathIndex = 0; pathIndex < container.Paths.Count; pathIndex++)
                    {
                        ContentPathsManager.DutyPath path = container.Paths[pathIndex];

                        if (ImGui.Selectable("###PathList" + path.FileName, path == _selectedDutyPath))
                        {
                            if (path == _selectedDutyPath)
                            {
                                _selectedDutyPath = null;
                            }
                            else
                            {
                                _checked          = Plugin.Configuration.DoNotUpdatePathFiles.Contains(path.FileName);
                                _selectedDutyPath = path;
                            }
                        }

                        if(ImGui.IsItemHovered() && path.PathFile.meta.notes.Count > 0)
                            ImGui.SetTooltip(string.Join("\n", path.PathFile.meta.notes));
                        ImGui.SetItemAllowOverlap();
                        ImGui.SameLine(multiple ? 20 : 1);

                        if (!multiple)
                        {
                            ImGuiHelper.ColoredText(container.ColoredNameRegex, container.Content.Name!);
                            ImGui.SameLine(0, 0);
                            ImGui.Text(" => ");
                            ImGui.SameLine(0, 0);
                        }

                        ImGuiHelper.ColoredText(path.ColoredNameRegex, path.Name);

                        ImGui.SameLine(0, 2);
                        ImGui.TextColored(ImGuiHelper.VersionColor, $"v{path.PathFile.meta.LastUpdatedVersion}");

                        if (multiple)
                        {
                            foreach ((CombatRole role, Job job) in pathJobs[pathIndex])
                            {
                                ImGui.SameLine(0, 2);
                                ImGui.TextColored(role switch
                                {
                                    CombatRole.DPS => ImGuiHelper.RoleDPSColor,
                                    CombatRole.Healer => ImGuiHelper.RoleHealerColor,
                                    CombatRole.Tank => ImGuiHelper.RoleTankColor,
                                    _ => Vector4.One
                                }, job.ToString());
                            }
                        }
                    }

                    if (multiple)
                        ImGui.EndGroup();
                }
            }
            catch (InvalidOperationException ex)
            {
                Svc.Log.Warning(ex.ToString());
            }
            finally
            {
                ImGui.EndChild();
                ImGui.PopStyleColor();
                ImGui.PopStyleVar(3);
            }
        }

        internal static void PathsUpdated()
        {
            _selectedDutyPath = null;
        }
    }
}
