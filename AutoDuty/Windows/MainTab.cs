using AutoDuty.IPC;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ImGuiNET;
using System.Collections.Generic;
using static AutoDuty.AutoDuty;
using System.Numerics;
using System.Linq;
using AutoDuty.Helpers;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoDuty.Windows
{
    internal static class MainTab
    {
        private static int _currentIndex = -1;
        private static int _dutyListSelected = -1;
        private static readonly string _pathsURL = "https://github.com/ffxivcode/DalamudPlugins/tree/main/AutoDuty/Paths";

        internal static void Draw()
        {
            var _loopTimes = Plugin.Configuration.LoopTimes;
            var _support = Plugin.Configuration.Support;
            var _trust = Plugin.Configuration.Trust;
            var _squadron = Plugin.Configuration.Squadron;
            var _regular = Plugin.Configuration.Regular;
            var _Trial = Plugin.Configuration.Trial;
            var _raid = Plugin.Configuration.Raid;
            var _unsynced = Plugin.Configuration.Unsynced;
            var _hideUnavailableDuties = Plugin.Configuration.HideUnavailableDuties;

            if (Plugin.InDungeon && Plugin.CurrentTerritoryContent != null)
            {
                var progress = VNavmesh_IPCSubscriber.IsEnabled ? VNavmesh_IPCSubscriber.Nav_BuildProgress() : 0;
                if (progress >= 0)
                {
                    ImGui.Text($"{Plugin.CurrentTerritoryContent.DisplayName} Mesh: Loading: ");
                    ImGui.ProgressBar(progress, new(200, 0));
                }
                else
                    ImGui.Text($"{Plugin.CurrentTerritoryContent.DisplayName} Mesh: Loaded Path: {(FileHelper.DictionaryPathFiles.TryGetValue(Plugin.CurrentTerritoryContent.TerritoryType, out _) ? "Loaded" : "None")}");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                using (var d = ImRaii.Disabled(!VNavmesh_IPCSubscriber.IsEnabled || !Plugin.InDungeon || !VNavmesh_IPCSubscriber.Nav_IsReady() || !BossMod_IPCSubscriber.IsEnabled))
                {
                    using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !FileHelper.DictionaryPathFiles.TryGetValue(Plugin.CurrentTerritoryContent.TerritoryType, out _) || Plugin.Stage > 0))
                    {
                        if (ImGui.Button("Start"))
                        {
                            Plugin.LoadPath();
                            _currentIndex = -1;
                            if (Plugin.MainListClicked)
                                Plugin.StartNavigation(!Plugin.MainListClicked);
                            else
                                Plugin.Run(Svc.ClientState.TerritoryType);
                        }
                        ImGui.SameLine(0, 15);
                    }
                    ImGui.PushItemWidth(150);
                    if (ImGui.InputInt("Times", ref _loopTimes))
                    {
                        Plugin.Configuration.LoopTimes = _loopTimes;
                        Plugin.Configuration.Save();
                    }
                    ImGui.PopItemWidth();
                    //ImGui.SameLine(0, 5);
                    using (var d2 = ImRaii.Disabled(!Plugin.InDungeon || Plugin.Stage == 0))
                    {
                        MainWindow.StopResumePause();
                        if (Plugin.Started)
                        {
                            ImGui.SameLine(0, 5);
                            ImGui.TextColored(new Vector4(0, 255f, 0, 1), $"{Plugin.Action}");
                        }
                    }
                    if (!ImGui.BeginListBox("##MainList", new Vector2(450 * ImGuiHelpers.GlobalScale, 525 * ImGuiHelpers.GlobalScale))) return;

                    if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled && ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
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
                        if (_currentIndex != Plugin.Indexer && _currentIndex > -1 && Plugin.Stage > 0)
                        {
                            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
                            _currentIndex = Plugin.Indexer;
                            if (_currentIndex > 1)
                                ImGui.SetScrollY((_currentIndex - 1) * lineHeight);
                        }
                        else if (_currentIndex == -1 && Plugin.Stage > 0)
                        {
                            _currentIndex = 0;
                            ImGui.SetScrollY(_currentIndex);
                        }
                        if (Plugin.InDungeon && Plugin.ListBoxPOSText.Count <1 && !FileHelper.DictionaryPathFiles.TryGetValue(Plugin.CurrentTerritoryContent.TerritoryType, out _))
                            ImGui.TextColored(new Vector4(0, 255, 0, 1), $"No Path file was found for:\n{TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryContent.TerritoryType).Split('|')[1].Trim()}\n({Plugin.CurrentTerritoryContent.TerritoryType}.json)\nin the Paths Folder:\n{Plugin.PathsDirectory.FullName.Replace('\\','/')}\nPlease download from:\n{_pathsURL}\nor Create in the Build Tab");
                    }
                    else
                    {
                        if (!VNavmesh_IPCSubscriber.IsEnabled)
                            ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires VNavmesh plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                        if (!BossMod_IPCSubscriber.IsEnabled)
                            ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires BossMod plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                        if (!ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
                            ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires Rotation Solver plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json");
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
                        MainWindow.GotoAndActions();
                        if (ImGui.Button("Run"))
                        {
                            if (!Plugin.Configuration.Support && !Plugin.Configuration.Trust && !Plugin.Configuration.Squadron && !Plugin.Configuration.Regular && !Plugin.Configuration.Trial && !Plugin.Configuration.Raid)
                                MainWindow.ShowPopup("Error", "You must select a version\nof the dungeon to run");
                            else if (Svc.Party.PartyId > 0 && (Plugin.Configuration.Support || Plugin.Configuration.Squadron || Plugin.Configuration.Trust))
                                MainWindow.ShowPopup("Error", "You must not be in a party to run Support, Squadron or Trust");
                            else if (Svc.Party.PartyId == 0 && Plugin.Configuration.Regular && !Plugin.Configuration.Unsynced)
                                MainWindow.ShowPopup("Error", "You must be in a group of 4 to run Regular Duties");
                            else if (Plugin.Configuration.Regular && !Plugin.Configuration.Unsynced && !ObjectHelper.PartyValidation())
                                MainWindow.ShowPopup("Error", "You must have the correcty party makeup to run Regular Duties");
                            else if (FileHelper.DictionaryPathFiles.TryGetValue(Plugin.CurrentTerritoryContent?.TerritoryType ?? 0, out _))
                                Plugin.Run();
                            else
                                MainWindow.ShowPopup("Error", "No path was found");
                        }
                    }
                    else
                        MainWindow.StopResumePause();
                }
                using (var d1 = ImRaii.Disabled(Plugin.Running))
                {
                    using (var d2 = ImRaii.Disabled(Plugin.CurrentTerritoryContent == null))
                    {
                        ImGui.SameLine(0, 15);
                        ImGui.PushItemWidth(250);
                        if (ImGui.InputInt("Times", ref _loopTimes))
                        {
                            Plugin.Configuration.LoopTimes = _loopTimes;
                            Plugin.Configuration.Save();
                        }
                        ImGui.PopItemWidth();
                    }
                    if (ImGui.Checkbox("Support", ref _support))
                    {
                        if (_support)
                        {
                            Plugin.Configuration.Support = _support;
                            Plugin.Configuration.Trial = false;
                            Plugin.Configuration.Raid = false;
                            Plugin.Configuration.Trust = false;
                            Plugin.Configuration.Squadron = false;
                            Plugin.Configuration.Regular = false;
                            Plugin.CurrentTerritoryContent = null;
                            _dutyListSelected = -1;
                            Plugin.Configuration.Save();
                        }
                    }                
                    ImGui.SameLine(0, 5);
                    if (ImGui.Checkbox("Trust", ref _trust))
                    {
                        if (_trust)
                        {
                            Plugin.Configuration.Trust = _trust;
                            Plugin.Configuration.Trial = false;
                            Plugin.Configuration.Raid = false;
                            Plugin.Configuration.Support = false;
                            Plugin.Configuration.Squadron = false;
                            Plugin.Configuration.Regular = false;
                            Plugin.CurrentTerritoryContent = null;
                            _dutyListSelected = -1;
                            Plugin.Configuration.Save();
                        }
                    } 
                    ImGui.SameLine(0, 5);
                    if (ImGui.Checkbox("Squadron", ref _squadron))
                    {
                        if (_squadron)
                        {
                            Plugin.Configuration.Squadron = _squadron;
                            Plugin.Configuration.Trial = false;
                            Plugin.Configuration.Raid = false;
                            Plugin.Configuration.Support = false;
                            Plugin.Configuration.Trust = false;
                            Plugin.Configuration.Regular = false;
                            Plugin.CurrentTerritoryContent = null;
                            _dutyListSelected = -1;
                            Plugin.Configuration.Save();
                        }
                    }
                    ImGui.SameLine(0, 5);
                    if (ImGui.Checkbox("Regular", ref _regular))
                    {
                        if (_regular)
                        {
                            Plugin.Configuration.Regular = _regular;
                            Plugin.Configuration.Trial = false;
                            Plugin.Configuration.Raid = false;
                            Plugin.Configuration.Support = false;
                            Plugin.Configuration.Trust = false;
                            Plugin.Configuration.Squadron = false;
                            Plugin.CurrentTerritoryContent = null;
                            _dutyListSelected = -1;
                            Plugin.Configuration.Save();
                        }
                    }
                    ImGui.SameLine(0, 5);
                    if (ImGui.Checkbox("Trial", ref _Trial))
                    {
                        if (_Trial)
                        {
                            Plugin.Configuration.Trial = _Trial;
                            Plugin.Configuration.Raid = false;
                            Plugin.Configuration.Regular = false;
                            Plugin.Configuration.Support = false;
                            Plugin.Configuration.Trust = false;
                            Plugin.Configuration.Squadron = false;
                            Plugin.CurrentTerritoryContent = null;
                            _dutyListSelected = -1;
                            Plugin.Configuration.Save();
                        }
                    }
                    ImGui.SameLine(0, 5);
                    if (ImGui.Checkbox("Raid", ref _raid))
                    {
                        if (_raid)
                        {
                            Plugin.Configuration.Raid = _raid;
                            Plugin.Configuration.Trial = false;
                            Plugin.Configuration.Regular = false;
                            Plugin.Configuration.Support = false;
                            Plugin.Configuration.Trust = false;
                            Plugin.Configuration.Squadron = false;
                            Plugin.CurrentTerritoryContent = null;
                            _dutyListSelected = -1;
                            Plugin.Configuration.Save();
                        }
                    }
                    //ImGui.SameLine(0, 5);
                    //MainWindow.GotoAndActions();
                    if (Plugin.Configuration.Support || Plugin.Configuration.Trust || Plugin.Configuration.Squadron || Plugin.Configuration.Regular || Plugin.Configuration.Trial || Plugin.Configuration.Raid)
                    {
                        //ImGui.SameLine(0, 5);
                        if (ImGui.Checkbox("Hide Unavailable Duties", ref _hideUnavailableDuties))
                        {
                            Plugin.Configuration.HideUnavailableDuties = _hideUnavailableDuties;
                            Plugin.Configuration.Save();
                        }
                    }
                    if (Plugin.Configuration.Regular || Plugin.Configuration.Trial || Plugin.Configuration.Raid)
                    {
                        ImGui.SameLine(0, 5);
                        if (ImGui.Checkbox("Unsynced", ref _unsynced))
                        {
                            Plugin.Configuration.Unsynced = _unsynced;
                            Plugin.Configuration.Save();
                        }
                    }
                    if (!ImGui.BeginListBox("##DutyList", new Vector2(450 * ImGuiHelpers.GlobalScale, 525 * ImGuiHelpers.GlobalScale))) return;

                    if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled)
                    {
                        Dictionary<uint, ContentHelper.Content> dictionary = [];
                        if (Plugin.Configuration.Support)
                            dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.DawnContent).ToDictionary();
                        else if (Plugin.Configuration.Trust)
                            dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.DawnContent && x.Value.ExVersion > 2).ToDictionary();
                        else if (Plugin.Configuration.Squadron)
                            dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.GCArmyContent).ToDictionary();
                        else if (Plugin.Configuration.Regular)
                            dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.ContentType == 2).ToDictionary();
                        else if (Plugin.Configuration.Trial)
                            dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.ContentType == 4).ToDictionary();
                        else if (Plugin.Configuration.Raid)
                            dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.ContentType == 5).ToDictionary();

                        if (dictionary.Count > 0)
                        {
                            foreach (var item in dictionary.Select((Value, Index) => (Value, Index)))
                            {
                                using (var d2 = ImRaii.Disabled(item.Value.Value.ClassJobLevelRequired > Plugin.Player?.Level || !FileHelper.DictionaryPathFiles.TryGetValue(item.Value.Value.TerritoryType, out _)))
                                {
                                    if (Plugin.Configuration.HideUnavailableDuties && (item.Value.Value.ClassJobLevelRequired > Plugin.Player?.Level || !FileHelper.DictionaryPathFiles.TryGetValue(item.Value.Value.TerritoryType, out _)))
                                        continue;
                                    if (ImGui.Selectable($"({item.Value.Value.TerritoryType}) {item.Value.Value.DisplayName}", _dutyListSelected == item.Index))
                                    {
                                        _dutyListSelected = item.Index;
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
