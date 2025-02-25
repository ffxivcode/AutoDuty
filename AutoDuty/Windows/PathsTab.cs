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
using ECommons.ImGuiMethods;
using AutoDuty.Updater;

namespace AutoDuty.Windows
{
    using Data;
    using ECommons;

    internal static class PathsTab
    {
        //private static Dictionary<CombatRole, Job[]> _jobs = Enum.GetValues<Job>().Where(j => !j.IsUpgradeable() && j != Job.BLU).GroupBy(j => j.GetRole()).Where(ig => ig.Key != CombatRole.NonCombat).ToDictionary(ig => ig.Key, ig => ig.ToArray());
        private static ContentPathsManager.DutyPath? _selectedDutyPath;
        private static bool                          _checked = false;

        private static readonly Dictionary<uint, bool> headers = [];

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
                if (ImGuiEx.ButtonWrapped("Open File"))
                    Process.Start("explorer", _selectedDutyPath?.FilePath ?? string.Empty);
            }
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 1, 1));
            if (ImGuiEx.CheckboxWrapped($"Do not overwrite on update", ref _checked))
                CheckBoxOnChange();

            ImGui.PopStyleColor();
            ImGui.SameLine();
            using (ImRaii.Disabled(!Plugin.Configuration.PathSelectionsByPath.Any(kvp => kvp.Value.Any())))
            {
                if (ImGuiEx.ButtonWrapped("Clear all cached classes"))
                {
                    Plugin.Configuration.PathSelectionsByPath.Clear();
                    Plugin.Configuration.Save();
                }
            }

            bool anyHeaderOpen = headers.Values.Any(b => b);
            if (ImGuiEx.ButtonWrapped(anyHeaderOpen ? "Collapse All" : "Reveal All"))
            {
                foreach (uint key in headers.Keys) 
                    headers[key] = !anyHeaderOpen;
            }

            using (ImRaii.Disabled(Patcher.PatcherState == ActionState.Running))
            {
                if (ImGuiEx.ButtonWrapped("Download Paths"))
                {
                    Patcher.Patch();
                }
            }
            bool showJobSelection = _selectedDutyPath is { container.Paths.Count: > 1 };
            ImGui.BeginTable("##PathTabContent", _selectedDutyPath != null ? 2 : 1);

            ImGuiStylePtr style = ImGui.GetStyle();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, style.Colors[(int)ImGuiCol.FrameBg]);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, style.FrameRounding);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, style.FrameBorderSize);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,   style.FramePadding);

            float dutyListWidth = showJobSelection ? ImGui.GetContentRegionAvail().X/3*2 : ImGui.GetContentRegionAvail().X;
            
            ImGui.BeginChild("##DutyList", new Vector2(dutyListWidth, 0), false,
                             ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar);
            try
            {
                foreach ((_, ContentPathsManager.ContentPathContainer? container) in ContentPathsManager.DictionaryPaths)
                {
                    bool multiple = false;

                    if (!headers.TryGetValue(container.id, out bool open))
                        headers[container.id] = open = true;

                    if (container.Paths.Count > 0)
                    {
                        multiple = true;
                        if (ImGui.Selectable("##PathHeader_" + container.id, false))
                            headers[container.id] = !open;
                        ImGui.SameLine();
                        ImGuiHelper.ColoredText(container.ColoredNameRegex, $"({container.id}) {container.Content.Name}");
                    }

                    List<Tuple<CombatRole, Job>>[]   pathJobs       = Enumerable.Range(0, container.Paths.Count).Select(_ => new List<Tuple<CombatRole, Job>>()).ToArray();
                    Dictionary<string, JobWithRole>? pathSelections = null;
                    if (open)
                    {
                        if (multiple)
                        {
                            ImGui.BeginGroup();
                            ImGui.Indent(20);

                            if (Plugin.Configuration.PathSelectionsByPath.TryGetValue(container.id, out pathSelections))
                                foreach ((string? path, JobWithRole jobs) in pathSelections)
                                    ;//pathJobs[container.Paths.IndexOf(dp => dp.FileName.Equals(jobs))].Add(new Tuple<CombatRole, Job>(path.GetCombatRole(), path));
                        }

                        foreach (ContentPathsManager.DutyPath path in container.Paths)
                        {
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

                                showJobSelection = false;
                            }

                            if (ImGui.IsItemHovered() && path.PathFile.Meta.Notes.Count > 0)
                                ImGui.SetTooltip(string.Join("\n", path.PathFile.Meta.Notes));
                            ImGui.SetItemAllowOverlap();
                            ImGui.SameLine(multiple ? 20 : 1);

                            if (!multiple)
                            {
                                ImGuiHelper.ColoredText(container.ColoredNameRegex, container.Content.Name!);
                                ImGui.SameLine(0, 0);
                                ImGui.Text(" => ");
                                ImGui.SameLine(0, 0);
                            }


                            ImGui.TextColored(ImGuiHelper.VersionColor, $"(v{path.PathFile.Meta.LastUpdatedVersion})");
                            ImGui.SameLine(0, 2);
                            ImGuiHelper.ColoredText(path.ColoredNameRegex, path.Name);

                            if (multiple && pathSelections != null)
                            {
                                if (pathSelections.TryGetValue(path.FileName, out JobWithRole jobs))
                                {
                                    if(jobs == JobWithRole.None)
                                        continue;
                                    
                                    ImGui.SameLine(0, 15);
                                    ImGui.Spacing();
                                    ImGui.AlignTextToFramePadding();

                                    void DrawRole(JobWithRole jwr, Vector4 col)
                                    {
                                        JobWithRole jb = jobs & jwr;
                                        if (jb != JobWithRole.None)
                                        {
                                            ImGui.SameLine(0, 5);
                                            ImGui.TextColored(col, jb.ToString().Replace('_', ' '));
                                        }
                                    }

                                    DrawRole(JobWithRole.DPS,   ImGuiHelper.RoleDPSColor);
                                    DrawRole(JobWithRole.Healers, ImGuiHelper.RoleHealerColor);
                                    DrawRole(JobWithRole.Tanks,   ImGuiHelper.RoleTankColor);
                                }
                            }
                        }

                        if (multiple)
                            ImGui.EndGroup();
                    }
                }
            }
            catch (InvalidOperationException) { }
            finally
            {
                ImGui.EndChild();
                ImGui.PopStyleColor();
                ImGui.PopStyleVar(3);
            }

            if (showJobSelection)
            {
                ImGui.NextColumn();
                ImGui.Indent(ImGui.GetContentRegionAvail().X / 3*2);

                ImGui.BeginChild("##PathsTabJobConfigurationHeader");

                ImGui.Text(_selectedDutyPath.Name);

                if (ImGui.Button("Clear job selection for this Duty"))
                {
                    ImGui.EndChild();
                    ImGui.EndTable();

                    Plugin.Configuration.PathSelectionsByPath.Remove(_selectedDutyPath.container.id);
                    Plugin.Configuration.Save();

                    _selectedDutyPath = null;

                    return;
                }

                ImGui.BeginChild("##PathsTabJobConfiguration", new Vector2(ImGui.GetContentRegionAvail().X, 0), false, ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar);


                bool        firstPath   = _selectedDutyPath.container.IsFirstPath(_selectedDutyPath);
                JobWithRole jwr = JobWithRole.None;

                PathSelectionHelper.AddPathSelectionEntry(_selectedDutyPath.container.id);

                if (Plugin.Configuration.PathSelectionsByPath.TryGetValue(_selectedDutyPath.container.id, out Dictionary<string, JobWithRole>? pathSelections))
                    if (pathSelections!.TryGetValue(_selectedDutyPath.FileName, out JobWithRole dutyRoles)) 
                        jwr = dutyRoles;

                JobWithRole jwrCheck = jwr;

                JobWithRoleHelper.DrawCategory(JobWithRole.All, ref jwr);

                if(jwr != jwrCheck)
                {
                    Dictionary<string, JobWithRole> pathJobConfigs = Plugin.Configuration.PathSelectionsByPath[_selectedDutyPath.container.id]!;

                    foreach (string key in pathJobConfigs.Keys) 
                        pathJobConfigs[key] &= ~jwr;

                    pathJobConfigs[_selectedDutyPath.FileName] = jwr;

                    PathSelectionHelper.RebuildDefaultPaths(_selectedDutyPath.container.id);

                    Plugin.Configuration.Save();
                }

                ImGui.EndChild();
                ImGui.EndChild();
                ImGui.Unindent();
            }

            ImGui.EndTable();
        }

        internal static void PathsUpdated()
        {
            _selectedDutyPath = null;
        }
    }
}
