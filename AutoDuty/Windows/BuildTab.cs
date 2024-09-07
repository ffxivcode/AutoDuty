using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ECommons;
using ImGuiNET;
using System.Text.Json;
using System;
using static AutoDuty.AutoDuty;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using AutoDuty.Helpers;
using Dalamud.Interface.Utility;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using static AutoDuty.Managers.ContentPathsManager;
using ECommons.ImGuiMethods;
using Dalamud.Interface.Components;

namespace AutoDuty.Windows
{
    internal static class BuildTab
    {
        internal static List<(string, string, string)>? ActionsList { get; set; }

        private static bool _scrollBottom = false;
        private static string _changelog = string.Empty;
        private static string _input = "";
        private static PathAction _action = new();
        private static string _inputTextName = "";
        private static bool _dontMove = false;
        private static float _inputIW = 200 * ImGuiHelpers.GlobalScale;
        private static bool _showAddActionUI = false;
        private static (string, string, string) _dropdownSelected = ("", "", "");
        private static int _buildListSelected = -1;
        private static string _addActionButton = "Add"; 
        private static bool _dragDrop = false;

        public static readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true, IgnoreReadOnlyProperties = true};

        private static void ClearAll()
        {
            _input = "";
            _action = new();
            _dropdownSelected = ("", "", "");
            _buildListSelected = -1;
            _dontMove = false;
            _showAddActionUI = false;
        }

        private static void AddAction(PathAction action, int index = -1)
        {
            _scrollBottom = true;
            if (index == -1)
                Plugin.Actions.Add(action);
            else
                Plugin.Actions[index] = action;
            ClearAll();
        }
        internal unsafe static void Draw()
        {
            if (MainWindow.CurrentTabName != "Build")
                MainWindow.CurrentTabName = "Build";
            using var d = ImRaii.Disabled(!Plugin.InDungeon || Plugin.Stage > 0 || !Player.Available);
            ImGui.Text($"Build Path: ({Svc.ClientState.TerritoryType}) {(ContentHelper.DictionaryContent.TryGetValue(Svc.ClientState.TerritoryType, out var content) ? content.Name : TerritoryName.GetTerritoryName(Svc.ClientState.TerritoryType))}");

            string idText = $"({Svc.ClientState.TerritoryType}) ";
            ImGui.Text(idText);
            ImGui.SameLine();
            string path       = Path.GetFileName(Plugin.PathFile).Replace(idText, string.Empty).Replace(".json", string.Empty);
            string pathOrg    = path;

            var textL = ImGui.CalcTextSize(".json");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - textL.Length());
            if (ImGui.InputText("##BuildPathFileName", ref path, 100) && !path.Equals(pathOrg)) 
                Plugin.PathFile = $"{Plugin.PathsDirectory.FullName}{Path.DirectorySeparatorChar}{idText}{path}.json";

            ImGui.SameLine();
            ImGui.Text($".json");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            if (ImGui.Button("Add POS"))
            {
                _scrollBottom = true;
                Plugin.Actions.Add(new PathAction { Name="MoveTo", Position= Player.Position, Argument = "" });
            }
            ImGui.SameLine(0, 5);
            ImGuiComponents.HelpMarker("Adds a MoveTo step to the path, AutoDuty will Move to the specified position");
            if (ImGuiEx.ButtonWrapped("Add Action"))
            {
                if (_showAddActionUI)
                    ClearAll();
                ImGui.OpenPopup("AddActionPopup");
            }
            ImGuiComponents.HelpMarker("Opens the Add Action popup menu to add action steps to the path");
            if (ImGui.BeginPopup("AddActionPopup"))
            {
                if (ActionsList == null)
                    return;

                foreach (var item in ActionsList)
                {
                    if (ImGui.Selectable(item.Item1))
                    {
                        _dropdownSelected = item;
                        switch (item.Item1)
                        {
                            case "ExitDuty":
                                _action = new PathAction { Name = "ExitDuty" };
                                break;
                            case "SelectYesno":
                                _input = "Yes";
                                break;
                            case "MoveToObject":
                            case "Interactable":
                                IGameObject? targetObject = Player.Object.TargetObject;
                                IGameObject? gameObject   = (targetObject?.ObjectKind == ObjectKind.EventObj ? targetObject : null) ?? Plugin.ClosestInteractableEventObject;
                                _input = gameObject != null ? $"{gameObject.DataId} ({gameObject.Name})" : string.Empty;
                                break;
                            case "Target":
                                _input = Plugin.ClosestTargetableBattleNpc?.Name.TextValue ?? "";
                                break;
                            default:
                                _input = "";

                                _action = new PathAction { Name = $"{item.Item1}", Position = Player.Position };
                                break;
                        }
                        _inputIW = 200 * ImGuiHelpers.GlobalScale;
                        if (item.Item2.Equals("false"))
                            AddAction(_action);
                        _addActionButton = "Add";
                        _showAddActionUI = !item.Item2.Equals("false");
                        _inputTextName = item.Item2;
                    }
                    ImGuiComponents.HelpMarker(item.Item3);
                }
                ImGui.EndPopup();
            }
            ImGui.SameLine(0, 5);
            if (ImGuiEx.ButtonWrapped("Clear Path"))
            {
                Plugin.Actions.Clear();
                ClearAll();
            }
            ImGuiComponents.HelpMarker("Clears the entire path, NOTE: there is no confirmation");
            ImGui.SameLine(0, 5);
            if (ImGuiEx.ButtonWrapped("Save Path"))
            {
                try
                {
                    Svc.Log.Info($"Saving {Plugin.PathFile}");

                    PathFile? pathFile = null;

                    if(DictionaryPaths.TryGetValue(Plugin.CurrentTerritoryContent!.TerritoryType, out ContentPathContainer? container))
                    {
                        DutyPath? dutyPath = container.Paths.FirstOrDefault(dp => dp.FilePath == Plugin.PathFile);
                        if (dutyPath != null)
                        {
                            pathFile = dutyPath.PathFile;
                            if(pathFile.Meta.LastUpdatedVersion < Plugin.Configuration.Version || _changelog.Length > 0)
                            {
                                pathFile.Meta.Changelog.Add(new PathFileChangelogEntry
                                                            {
                                                                Version = Plugin.Configuration.Version,
                                                                Change  = _changelog
                                                            });
                                _changelog = string.Empty;
                            }
                        }
                    }

                    pathFile ??= new();

                    pathFile.Actions = [.. Plugin.Actions];

                    string json = JsonSerializer.Serialize(pathFile, jsonSerializerOptions);
                    File.WriteAllText(Plugin.PathFile, json);
                    Plugin.CurrentPath = 0;
                }
                catch (Exception e)
                {
                    Svc.Log.Error(e.ToString());
                    //throw;
                }
            }
            ImGuiComponents.HelpMarker("Saves the path to the path file specified or the default");
            ImGui.SameLine(0, 5);
            if (ImGuiEx.ButtonWrapped("Load Path"))
            {
                Plugin.LoadPath();
                ClearAll();
            }
            ImGuiComponents.HelpMarker("Loads the path");
            ImGui.Text("Changelog:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##Changelog", ref _changelog, 200);
            if (_showAddActionUI)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.PushItemWidth(_inputIW);
                ImGui.InputText(_inputTextName, ref _input, 100);
                if (!_dropdownSelected.Item1.IsNullOrEmpty() && !_dropdownSelected.Item2.IsNullOrEmpty())
                    ImGui.SameLine(0, 5);
                if (ImGuiEx.ButtonWrapped(_addActionButton))
                {
                    if (_input.IsNullOrEmpty())
                    {
                        MainWindow.ShowPopup("Error", "You must enter an input");
                        return;
                    }
                    if (_dropdownSelected.Item1.Equals("<-- Comment -->", StringComparison.InvariantCultureIgnoreCase))
                    {
                        AddAction(new PathAction { Name = $"<-- {_input} -->" });
                        return;
                    }
                    if (_dropdownSelected.Item1.IsNullOrEmpty() && _dropdownSelected.Item2.IsNullOrEmpty() && !_input.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase) && (_input.Count(c => c == '|') < 2 || !_input.Split('|')[1].All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.')))
                        MainWindow.ShowPopup("Error", "Input is not in the correct format\nAction|Position|ActionParams(if needed)");
                    if (_dontMove && _dropdownSelected.Item1.IsNullOrEmpty() && _dropdownSelected.Item2.IsNullOrEmpty())
                        AddAction(new PathAction { Name = _input.Split('|')[0], Argument = _input.Split('|')[2] }, _buildListSelected);
                    else if (_dropdownSelected.Item1.IsNullOrEmpty() && _dropdownSelected.Item2.IsNullOrEmpty())
                        AddAction(new PathAction { Name = _input.Split('|')[0], Position = Player.Position, Argument = _input.Split('|')[2] }, _buildListSelected);
                    else if (_dontMove)
                        AddAction(new PathAction { Name = _dropdownSelected.Item1, Argument = _input });
                    else
                        AddAction(new PathAction { Name = _dropdownSelected.Item1, Position = Player.Position, Argument = _input });
                }
                ImGui.SameLine(0, 5);
                ImGui.Checkbox("Dont Move", ref _dontMove);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
            if (!ImGui.BeginListBox("##BuildList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y))) return;
            try
            {
                if (Plugin.InDungeon)
                {
                    foreach (var item in Plugin.Actions.Select((Value, Index) => (Value, Index)))
                    {
                        var v4 = item.Value.Name.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase) ? new Vector4(0, 255, 0, 1) : new Vector4(255, 255, 255, 1);

                        var text = item.Value.Name.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase) ? item.Value.Name : $"{item.Value.Name}|{item.Value.Position:F2}|{item.Value.Argument}";

                        ImGui.PushStyleColor(ImGuiCol.Text, v4);
                        if (ImGui.Selectable(text, item.Index == _buildListSelected, ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            if (_dragDrop)
                            {
                                _dragDrop = false;
                                return;
                            }
                            if (_buildListSelected == item.Index)
                            {
                                _showAddActionUI = false;
                                _buildListSelected = -1;
                            }
                            else
                            {
                                _buildListSelected = item.Index;
                                _showAddActionUI = true;
                                _dropdownSelected = ("", "", "");
                                _addActionButton = "Modify";
                                _inputTextName = "";
                                _input = item.Value.Name;
                                _inputIW = 400 * ImGuiHelpers.GlobalScale;
                            }
                        }
                        ImGui.PopStyleColor();
                        
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            // Do stuff on Selectable() double click.
                            if (item.Value.Position != Vector3.Zero)
                            {
                                ImGui.SetClipboardText($"{item.Value.Position:F2}");
                                //if (Player.Available)
                                    //Player.GameObject->SetPosition(item.Value.Position);
                            }
                        }
                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered() && !ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            int n_next = item.Index + (ImGui.GetMouseDragDelta(0).Y < 0f ? -1 : 1);
                            if (n_next >= 0 && n_next < Plugin.Actions.Count)
                            {
                                Plugin.Actions[item.Index] = Plugin.Actions[n_next];
                                Plugin.Actions[n_next] = item.Value;
                                _buildListSelected = -1;
                                ImGui.ResetMouseDragDelta();
                                _dragDrop = true;
                                ClearAll();
                            }
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        {
                            Plugin.Actions.Remove(item.Value);
                            _scrollBottom = true;
                        }
                    }
                }
                else
                    ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), "You must enter a dungeon to Build a Path");
            }
            catch (Exception) { }
            if (_scrollBottom)
            {
                ImGui.SetScrollHereY(1.0f);
                _scrollBottom = false;
            }
            ImGui.EndListBox();
        }
    }
}
