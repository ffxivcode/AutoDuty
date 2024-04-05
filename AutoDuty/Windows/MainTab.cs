using AutoDuty.IPC;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ImGuiNET;
using System.Collections.Generic;
using static AutoDuty.AutoDuty;
using System.Numerics;
using System.Linq;
using AutoDuty.Helpers;

namespace AutoDuty.Windows
{
    internal static class MainTab
    {
        private static int _clickedDuty = -1;
        private static int currentIndex = -1;
        private static int dutyListSelected = -1;
        private static string pathsURL = "https://github.com/ffxivcode/DalamudPlugins/tree/main/AutoDuty/Paths";

        internal static void Draw()
        {
            if (Plugin.InDungeon)
            {
                var progress = VNavmesh_IPCSubscriber.Nav_BuildProgress();
                if (progress >= 0)
                {
                    ImGui.Text(TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryType).Split('|')[1].Trim() + " Mesh: Loading: ");
                    ImGui.ProgressBar(progress, new(200, 0));
                }
                else
                    ImGui.Text($"{TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryType).Split('|')[1].Trim()} Mesh: Loaded Path: {(FileHelper.PathFileExists.GetValueOrDefault(Plugin.CurrentTerritoryType) ? "Loaded" : "None")}");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                using (var d = ImRaii.Disabled(!Plugin.InDungeon || !VNavmesh_IPCSubscriber.Nav_IsReady() || !BossMod_IPCSubscriber.IsEnabled || !VNavmesh_IPCSubscriber.IsEnabled))
                {
                    using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !FileHelper.PathFileExists.GetValueOrDefault(Plugin.CurrentTerritoryType) || Plugin.Stage > 0))
                    {
                        if (ImGui.Button("Start"))
                        {
                            Plugin.LoadPath();
                            Plugin.StartNavigation(!Plugin.MainListClicked);
                            currentIndex = -1;
                        }
                    }
                    ImGui.SameLine(0, 5);
                    using (var d2 = ImRaii.Disabled(!Plugin.InDungeon || Plugin.Stage == 0))
                    {
                        if (ImGui.Button("Stop"))
                        {
                            Plugin.Stage = 0;
                        }
                        ImGui.SameLine(0, 5);
                        if (Plugin.Stage == 5)
                        {
                            if (ImGui.Button("Resume"))
                            {
                                Plugin.Stage = 1;
                            }
                        }
                        else
                        {
                            if (ImGui.Button("Pause"))
                            {
                                Plugin.Stage = 5;
                            }
                        }
                        if (Plugin.Started)
                        {
                            ImGui.SameLine(0, 5);
                            ImGui.TextColored(new Vector4(0, 255f, 0, 1), Plugin.Action);
                        }
                    }
                    if (!ImGui.BeginListBox("##MainList", new Vector2(-1, -1))) return;

                    if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled)
                    {
                        foreach (var item in Plugin.ListBoxPOSText.Select((name, index) => (name, index)))
                        {
                            Vector4 v4 = new();
                            if (item.index == Plugin.Indexer)
                                v4 = new Vector4(0, 255, 0, 1);
                            else
                                v4 = new Vector4(255, 255, 255, 1);
                            ImGui.TextColored(v4, item.name);
                            if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && Plugin.Stage == 0)
                            {
                                if (item.index == Plugin.Indexer)
                                {
                                    Plugin.Indexer = -1;
                                    Plugin.MainListClicked = false;
                                }
                                else
                                {
                                    Plugin.Indexer = item.index;
                                    Plugin.MainListClicked = true;
                                }
                            }
                        }
                        if (currentIndex != Plugin.Indexer && currentIndex > -1 && Plugin.Stage > 0)
                        {
                            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
                            currentIndex = Plugin.Indexer;
                            if (currentIndex > 1)
                                ImGui.SetScrollY((currentIndex - 1) * lineHeight);
                        }
                        else if (currentIndex == -1 && Plugin.Stage > 0)
                        {
                            currentIndex = 0;
                            ImGui.SetScrollY(currentIndex);
                        }
                        if (Plugin.InDungeon && !FileHelper.PathFileExists.GetValueOrDefault(Plugin.CurrentTerritoryType))
                            ImGui.TextColored(new Vector4(0, 255, 0, 1), $"No Path file was found for:\n{TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryType).Split('|')[1].Trim()}\n({Plugin.CurrentTerritoryType}.json)\nin the Paths Folder:\n{Plugin.PathsDirectory.FullName.Replace('\\','/')}\nPlease download from:\n{pathsURL}\nor Create in the Build Tab");
                    }
                    else
                    {
                        if (!VNavmesh_IPCSubscriber.IsEnabled)
                            ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires VNavmesh plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                        if (!BossMod_IPCSubscriber.IsEnabled)
                            ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires BossMod plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                    }
                    ImGui.EndListBox();
                }
            }
            else
            {
                using (var d2 = ImRaii.Disabled(_clickedDuty == -1))
                {
                    if (!Plugin.Running)
                    {
                        if (ImGui.Button("Run"))
                        {
                            if (Plugin.Regular || Plugin.Trust)
                                MainWindow.ShowPopup("Error", "This has not yet been implemented");
                            else if (!Plugin.Support && !Plugin.Trust && !Plugin.Squadron && !Plugin.Regular)
                                MainWindow.ShowPopup("Error", "You must select a version\nof the dungeon to run"); 
                            else if (FileHelper.PathFileExists.GetValueOrDefault(ContentHelper.ListContent[_clickedDuty].TerritoryType))
                                Plugin.Run(_clickedDuty);
                            else
                                MainWindow.ShowPopup("Error", "No path was found");
                        }
                    }
                    else
                    {
                        if (ImGui.Button("Stop"))
                        {
                            Plugin.Stage = 0;
                            Plugin.Running = false;
                            Plugin.CurrentLoop = 0;
                            Plugin.MainWindow.SizeConstraints = new()
                            {
                                MinimumSize = new Vector2(425, 375),
                                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
                            };
                            Plugin.MainWindow.Size = new Vector2(425, 375);
                        }
                        ImGui.SameLine(0, 5);
                        if (Plugin.Stage == 5)
                        {
                            if (ImGui.Button("Resume"))
                            {
                                Plugin.Stage = 1;
                            }
                        }
                        else
                        {
                            if (ImGui.Button("Pause"))
                            {
                                Plugin.Stage = 5;
                            }
                        }
                    }
                }
                using (var d1 = ImRaii.Disabled(Plugin.Running))
                {
                    using (var d2 = ImRaii.Disabled(_clickedDuty == -1))
                    {
                        ImGui.SameLine(0, 15);
                        ImGui.InputInt("Times", ref Plugin.LoopTimes);
                    }
                    if (ImGui.Checkbox("Support", ref Plugin.Support))
                    {
                        if (Plugin.Support)
                        {
                            Plugin.Trust = false;
                            Plugin.Squadron = false;
                            Plugin.Regular = false;
                            _clickedDuty = -1;
                            dutyListSelected = -1;
                        }
                    }
                    ImGui.SameLine(0, 5);
                    if (ImGui.Checkbox("Trust", ref Plugin.Trust))
                    {
                        if (Plugin.Trust)
                        {
                            Plugin.Support = false;
                            Plugin.Squadron = false;
                            Plugin.Regular = false;
                            _clickedDuty = -1;
                            dutyListSelected = -1;
                        }
                    }
                    ImGui.SameLine(0, 5);
                    if (ImGui.Checkbox("Squadron", ref Plugin.Squadron))
                    {
                        if (Plugin.Squadron)
                        {
                            Plugin.Support = false;
                            Plugin.Trust = false;
                            Plugin.Regular = false;
                            _clickedDuty = -1;
                            dutyListSelected = -1;
                        }
                    }
                    ImGui.SameLine(0, 5);
                    if (ImGui.Checkbox("Regular", ref Plugin.Regular))
                    {
                        if (Plugin.Regular)
                        {
                            Plugin.Support = false;
                            Plugin.Trust = false;
                            Plugin.Squadron = false;
                            _clickedDuty = -1;
                            dutyListSelected = -1;
                        }
                        ImGui.Checkbox("Unsynced", ref Plugin.Unsynced);
                    }
                    if (!ImGui.BeginListBox("##DutyList", new Vector2(-1, -1))) return;

                    if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled)
                    {
                        List<ContentHelper.Content> list = [];
                        if (Plugin.Support)
                            list = ContentHelper.ListContent.Where(x => x.DawnContent).ToList();
                        else if (Plugin.Trust)
                            list = ContentHelper.ListContent.Where(x => x.DawnContent && x.ExVersion > 2).ToList();
                        else if (Plugin.Squadron)
                            list = ContentHelper.ListContent.Where(x => x.GCArmyContent).ToList();
                        else if (Plugin.Regular)
                            list = ContentHelper.ListContent;

                        if (list.Count > 0)
                        {
                            foreach (var item in list.Select((Value, Index) => (Value, Index)))
                            {
                                using (var d2 = ImRaii.Disabled(item.Value.ClassJobLevelRequired > Plugin.Player?.Level || !FileHelper.PathFileExists.GetValueOrDefault(item.Value.TerritoryType)))
                                {
                                    if (ImGui.Selectable($"({item.Value.TerritoryType}) {item.Value.Name}", dutyListSelected == item.Index))
                                    {
                                        dutyListSelected = item.Index;
                                        _clickedDuty = ContentHelper.ListContent.FindIndex(a => a.Name == item.Value.Name);
                                    }
                                }
                            }
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(0, 1, 0, 1), "Please select one of Support, Trust, Squadron or Regular\nto Populate the Duty List");
                            _clickedDuty = -1;
                        }
                    }
                    else
                    {
                        if (!VNavmesh_IPCSubscriber.IsEnabled)
                            ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires VNavmesh plugin to be Installed and Loaded\nFor proper navigation and movement\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                        if (!BossMod_IPCSubscriber.IsEnabled)
                            ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires BossMod plugin to be Installed and Loaded\nFor proper named mechanic handling\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                    }
                    ImGui.EndListBox();
                }
            }
        }
    }
}
