using AutoDuty.IPC;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ImGuiNET;
using System.Collections.Generic;
using static AutoDuty.AutoDuty;
using System.Numerics;
using System.Linq;
using AutoDuty.Helpers;
using System.Collections.Immutable;

namespace AutoDuty.Windows
{
    internal static class MainTab
    {
        private static ContentHelper.Content? _clickedDuty = null;
        private static int currentIndex = -1;
        private static int dutyListSelected = -1;
        private static string pathsURL = "https://github.com/ffxivcode/DalamudPlugins/tree/main/AutoDuty/Paths";

        internal static void Draw()
        {
            if (Plugin.InDungeon && Plugin.CurrentTerritoryContent != null)
            {
                var progress = VNavmesh_IPCSubscriber.Nav_BuildProgress();
                if (progress >= 0)
                {
                    ImGui.Text($"{Plugin.CurrentTerritoryContent.Name} Mesh: Loading: ");
                    ImGui.ProgressBar(progress, new(200, 0));
                }
                else
                    ImGui.Text($"{Plugin.CurrentTerritoryContent.Name} Mesh: Loaded Path: {(FileHelper.PathFileExists.GetValueOrDefault(Plugin.CurrentTerritoryContent.TerritoryType) ? "Loaded" : "None")}");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                using (var d = ImRaii.Disabled(!Plugin.InDungeon || !VNavmesh_IPCSubscriber.Nav_IsReady() || !BossMod_IPCSubscriber.IsEnabled || !VNavmesh_IPCSubscriber.IsEnabled))
                {
                    using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !FileHelper.PathFileExists.GetValueOrDefault(Plugin.CurrentTerritoryContent.TerritoryType) || Plugin.Stage > 0))
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
                        if (Plugin.InDungeon && !FileHelper.PathFileExists.GetValueOrDefault(Plugin.CurrentTerritoryContent.TerritoryType))
                            ImGui.TextColored(new Vector4(0, 255, 0, 1), $"No Path file was found for:\n{TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryContent.TerritoryType).Split('|')[1].Trim()}\n({Plugin.CurrentTerritoryContent.TerritoryType}.json)\nin the Paths Folder:\n{Plugin.PathsDirectory.FullName.Replace('\\','/')}\nPlease download from:\n{pathsURL}\nor Create in the Build Tab");
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
                using (var d2 = ImRaii.Disabled(Plugin.CurrentTerritoryContent == null))
                {
                    if (!Plugin.Running)
                    {
                        if (ImGui.Button("Run"))
                        {
                            if (Plugin.Regular || Plugin.Trust)
                                MainWindow.ShowPopup("Error", "This has not yet been implemented");
                            else if (!Plugin.Support && !Plugin.Trust && !Plugin.Squadron && !Plugin.Regular)
                                MainWindow.ShowPopup("Error", "You must select a version\nof the dungeon to run"); 
                            else if (FileHelper.PathFileExists.GetValueOrDefault(Plugin.CurrentTerritoryContent?.TerritoryType ?? 0))
                                Plugin.Run();
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
                            Plugin.MainWindow.SetWindowSize(new Vector2(425, 375));
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
                    using (var d2 = ImRaii.Disabled(Plugin.CurrentTerritoryContent == null))
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
                            Plugin.CurrentTerritoryContent = null;
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
                            Plugin.CurrentTerritoryContent = null;
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
                            Plugin.CurrentTerritoryContent = null;
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
                            Plugin.CurrentTerritoryContent = null;
                            dutyListSelected = -1;
                        }
                        ImGui.Checkbox("Unsynced", ref Plugin.Unsynced);
                    }
                    if (!ImGui.BeginListBox("##DutyList", new Vector2(-1, -1))) return;

                    if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled)
                    {
                        Dictionary<uint, ContentHelper.Content> dictionary = [];
                        if (Plugin.Support)
                            dictionary = (Dictionary<uint, ContentHelper.Content>)ContentHelper.DictionaryContent.Where(x => x.Value.DawnContent);
                        else if (Plugin.Trust)
                            dictionary = (Dictionary<uint, ContentHelper.Content>)ContentHelper.DictionaryContent.Where(x => x.Value.DawnContent && x.Value.ExVersion > 2);
                        else if (Plugin.Squadron)
                            dictionary = (Dictionary<uint, ContentHelper.Content>)ContentHelper.DictionaryContent.Where(x => x.Value.GCArmyContent);
                        else if (Plugin.Regular)
                            dictionary = ContentHelper.DictionaryContent;

                        if (dictionary.Count > 0)
                        {
                            foreach (var item in dictionary.Select((Value, Index) => (Value, Index)))
                            {
                                using (var d2 = ImRaii.Disabled(item.Value.Value.ClassJobLevelRequired > Plugin.Player?.Level || !FileHelper.PathFileExists.GetValueOrDefault(item.Value.Value.TerritoryType)))
                                {
                                    if (ImGui.Selectable($"({item.Value.Value.TerritoryType}) {item.Value.Value.Name}", dutyListSelected == item.Index))
                                    {
                                        dutyListSelected = item.Index;
                                        Plugin.CurrentTerritoryContent = item.Value.Value;
                                    }
                                }
                            }
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(0, 1, 0, 1), "Please select one of Support, Trust, Squadron or Regular\nto Populate the Duty List");
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
