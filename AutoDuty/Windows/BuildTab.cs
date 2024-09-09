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
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using static AutoDuty.Managers.ContentPathsManager;
using ECommons.ImGuiMethods;
using Dalamud.Interface.Components;
using AutoDuty.Data;

namespace AutoDuty.Windows
{
    internal static class BuildTab
    {
        internal static List<(string, string, string)>? ActionsList { get; set; }

        private static bool _scrollBottom = false;
        private static string _changelog = string.Empty;
        private static PathAction? _action = null;
        private static string _actionText = string.Empty;
        private static string _note = string.Empty;
        private static Vector3 _position = Vector3.Zero;
        private static string _positionText = string.Empty;
        private static string _argument = string.Empty;
        private static string _argumentHint = string.Empty;
        private static bool _dontMove = false;
        private static bool _showAddActionUI = false;
        private static (string, string, string) _dropdownSelected = (string.Empty, string.Empty, string.Empty);
        private static int _buildListSelected = -1;
        private static string _addActionButton = "Add"; 
        private static bool _dragDrop = false;
        private static bool _noArgument = false;
        private static bool _comment = false;

        public static readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true, IgnoreReadOnlyProperties = true, IncludeFields = true };

        private static void ClearAll()
        {
            _actionText = string.Empty;
            _note = string.Empty;
            _position = Vector3.Zero;
            _positionText = string.Empty;
            _argument = string.Empty;
            _argumentHint = string.Empty;
            _dropdownSelected = (string.Empty, string.Empty, string.Empty);
            _dontMove = false;
            _showAddActionUI = false;
            _noArgument = false;
            _addActionButton = "Add";
            _buildListSelected = -1;
            _action = null;
            _comment = false;
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
            ImGui.Text("Changelog:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##Changelog", ref _changelog, 200);
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
                        _argumentHint = item.Item2.Equals("false", StringComparison.InvariantCultureIgnoreCase) ? string.Empty : item.Item2;
                        _actionText = item.Item1;
                        _noArgument = item.Item2.Equals("false", StringComparison.InvariantCultureIgnoreCase);
                        _addActionButton = "Add";
                        _showAddActionUI = true;
                        _comment = item.Item1.Equals("<-- Comment -->", StringComparison.InvariantCultureIgnoreCase);
                        _position = Player.Available && !_comment ? Player.Position : Vector3.Zero;
                        _positionText = _position.ToCustomString();
                        switch (item.Item1)
                        {
                            case "SelectYesno":
                                _argument = "Yes";
                                break;
                            case "MoveToObject":
                            case "Interactable":
                                IGameObject? targetObject = Player.Object.TargetObject;
                                IGameObject? gameObject   = (targetObject?.ObjectKind == ObjectKind.EventObj ? targetObject : null) ?? Plugin.ClosestInteractableEventObject;
                                _argument = gameObject != null ? $"{gameObject.DataId}" : string.Empty;
                                _note = gameObject != null ? gameObject.Name.ExtractText() : string.Empty;
                                break;
                            case "Target":
                                _argument = Plugin.ClosestTargetableBattleNpc?.Name.TextValue ?? "";
                                break;
                            default:
                                _argument = string.Empty;
                                break;
                        }
                        _action = new() { Name = _actionText, Position = _position, Argument = _argument, Note = _note };
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
            
            using (ImRaii.Disabled(!_showAddActionUI))
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                ImGui.Text($"Action: ");
                ImGui.SameLine();
                ImGui.TextColored(new(0, 1, 0, 1), $"{_actionText}");
                ImGui.SameLine(0, 25);

                if (ImGuiEx.ButtonWrapped(_addActionButton))
                {
                    if (_action == null) return;

                    if (_argument.IsNullOrEmpty() && !_noArgument && !_comment)
                    {
                        MainWindow.ShowPopup("Error", "You must enter an argument");
                        return;
                    }
                    _action.Name = _actionText;
                    _action.Argument = _argument;
                    var position = _positionText.Replace(" ", string.Empty).Split(",");
                    if (!_comment && position.Length == 3 && float.TryParse(position[0], out var p1) && float.TryParse(position[1], out var p2) && float.TryParse(position[2], out var p3))
                        _action.Position = new(p1, p2, p3);
                    else
                        _action.Position = Vector3.Zero;
                    _action.Note = _comment && !_note.StartsWith("<--") && !_note.EndsWith("-->") ? $"<-- {_note} -->" : _note;
                    AddAction(_action, _buildListSelected);
                }
                ImGui.SameLine();
                ImGuiEx.CheckboxWrapped("Dont Move", ref _dontMove);
                ImGui.SameLine();
                if (ImGuiEx.ButtonWrapped("Delete"))
                                {
                    Plugin.Actions.RemoveAt(_buildListSelected);
                    _scrollBottom = true;
                    ClearAll();
                }
                ImGui.SameLine();
                if (ImGuiEx.ButtonWrapped("Copy to Clipboard"))
                    ImGui.SetClipboardText(_action?.ToCustomString());
                ImGui.SameLine();
                using (ImRaii.Disabled(!Player.Available || _action == null))
                {
                    if (ImGuiEx.ButtonWrapped("Teleport To"))
                        Player.GameObject->SetPosition(_action!.Position.X, _action.Position.Y, _action.Position.Z);
                }
                
                using (ImRaii.Disabled(_noArgument || _comment))
                {
                    ImGui.Text("Argument:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.InputTextWithHint("##Argument", _argumentHint, ref _argument, 200);
                }
                using (ImRaii.Disabled(_comment))
                {
                    ImGui.Text("Position:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.InputText("##Position", ref _positionText, 200);
                }
                ImGui.Text("Note:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                ImGui.InputText("##Note", ref _note, 200);
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

                        var text = item.Value.Name.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase) ? item.Value.Note : $"{item.Value.ToCustomString()}";

                        ImGui.PushStyleColor(ImGuiCol.Text, v4);
                        if (ImGui.Selectable($"{text}###Text{item.Index}", item.Index == _buildListSelected))
                        {
                            /*if (_dragDrop)
                            {
                                _dragDrop = false;
                                return;
                            }*/
                            if (_buildListSelected == item.Index)
                                ClearAll();
                            else
                            {
                                _comment = item.Value.Name.Equals($"<-- Comment -->", StringComparison.InvariantCultureIgnoreCase);
                                _noArgument = ActionsList?.Any(x => x.Item1.Equals($"{item.Value.Name}", StringComparison.InvariantCultureIgnoreCase) && x.Item2.Equals("false", StringComparison.InvariantCultureIgnoreCase)) ?? false;
                                _dontMove = item.Value.Position == Vector3.Zero;
                                _actionText = item.Value.Name;
                                _note = item.Value.Note;
                                _argument = item.Value.Argument;
                                _showAddActionUI = false;
                                _position = item.Value.Position;
                                _positionText = _position.ToCustomString();
                                _buildListSelected = item.Index;
                                _showAddActionUI = true;
                                _dropdownSelected = ("", "", "");
                                _addActionButton = "Modify";
                                _action = item.Value;
                            }
                        }
                        ImGui.PopStyleColor();
                        /*if (ImGui.IsItemActive() && !ImGui.IsItemHovered() && !ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
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
                        }*/

                        
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
