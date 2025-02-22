﻿using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ECommons;
using ImGuiNET;
using System.Text.Json;
using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using AutoDuty.Helpers;
using Dalamud.Game.ClientState.Objects.Types;
using static AutoDuty.Managers.ContentPathsManager;
using ECommons.ImGuiMethods;
using Dalamud.Interface.Components;
using AutoDuty.Data;
using static AutoDuty.Windows.MainWindow;
using System.Diagnostics;

namespace AutoDuty.Windows
{
    internal static class BuildTab
    {
        internal static List<(string, string, string)>? ActionsList { get; set; }

        private static          bool                     _scrollBottom      = false;
        private static          string                   _changelog         = string.Empty;
        private static          PathAction?              _action            = null;
        private static          string                   _actionText        = string.Empty;
        private static          string                   _note              = string.Empty;
        private static          Vector3                  _position          = Vector3.Zero;
        private static          string                   _positionText      = string.Empty;
        private static          List<string>             _arguments         = [];
        private static          string                   _argumentsString   = string.Empty;
        private static          string                   _argumentHint      = string.Empty;
        private static          bool                     _dontMove          = false;
        private static          bool                     _showAddActionUI   = false;
        private static          (string, string, string) _dropdownSelected  = (string.Empty, string.Empty, string.Empty);
        private static          int                      _buildListSelected = -1;
        private static          string                   _addActionButton   = "Add";
        private static          bool                     _dragDrop          = false;
        private static          bool                     _noArgument        = false;
        private static          bool                     _comment           = false;
        private static          Vector4                  _argumentTextColor = new(1,1,1,1);
        private static          bool                     _deleteItem        = false;
        private static          int                      _deleteItemIndex   = -1;
        private static          ActionTag                _actionTag;
        private static readonly ActionTag[]              _actionTags           = [ActionTag.None, ActionTag.Synced, ActionTag.Unsynced, ActionTag.W2W];
        public static readonly  JsonSerializerOptions    jsonSerializerOptions = new() { WriteIndented = true, IgnoreReadOnlyProperties = true, IncludeFields = true };

        internal unsafe static void Draw()
        {
            SetCurrentTabName("BuildTab");
            using (ImRaii.Disabled(Plugin.States.HasFlag(PluginState.Navigating) || Plugin.States.HasFlag(PluginState.Looping)))
            {
                if (Plugin.InDungeon)
                {
                    DrawPathElements();
                    DrawSeperator();
                    DrawButtons();
                    DrawSeperator();
                }
                DrawBuildList();
            }
        }

        private static void DrawSeperator()
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        private static void DrawPathElements()
        {
            using var d = ImRaii.Disabled(!Plugin.InDungeon || Plugin.Stage > 0 || !Player.Available);
            ImGui.Text($"Build Path: ({Svc.ClientState.TerritoryType}) {(ContentHelper.DictionaryContent.TryGetValue(Svc.ClientState.TerritoryType, out var content) ? content.Name : TerritoryName.GetTerritoryName(Svc.ClientState.TerritoryType))}");

            string idText = $"({Svc.ClientState.TerritoryType}) ";
            ImGui.Text(idText);
            ImGui.SameLine();
            string path = Path.GetFileName(Plugin.PathFile).Replace(idText, string.Empty).Replace(".json", string.Empty);
            string pathOrg = path;

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
        }

        private static void DrawButtons()
        {
            if (ImGui.Button("Add POS"))
            {
                _scrollBottom = true;
                Plugin.Actions.Add(new PathAction { Name = "MoveTo", Position = Player.Position });
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
                        _buildListSelected = -1;
                        _argumentHint = item.Item2.Equals("false", StringComparison.InvariantCultureIgnoreCase) ? string.Empty : item.Item2;
                        _actionText = item.Item1;
                        _noArgument = item.Item2.Equals("false", StringComparison.InvariantCultureIgnoreCase);
                        _addActionButton = "Add";
                        _comment = item.Item1.Equals("<-- Comment -->", StringComparison.InvariantCultureIgnoreCase);
                        _position = Player.Available ? Player.Position : Vector3.Zero;
                        _actionTag = ActionTag.None;
                        switch (item.Item1)
                        {
                            case "<-- Comment -->":
                                _actionTag = ActionTag.Comment;
                                _position = Vector3.Zero;
                                break;
                            case "Revival":
                                _actionTag = ActionTag.Revival;
                                _position = Vector3.Zero;
                                break;
                            case "TreasureCoffer":
                                _actionTag = ActionTag.Treasure;
                                break;
                            case "ExitDuty":
                                _position = Vector3.Zero;
                                break;
                            case "SelectYesno":
                                _arguments = ["Yes"];
                                break;
                            case "MoveToObject":
                            case "Interactable":
                            case "Target":
                                IGameObject? targetObject = Player.Object.TargetObject;
                                IGameObject? gameObject = (targetObject ?? null) ?? ClosestObject;
                                _arguments = [gameObject != null ? $"{gameObject.DataId}" : string.Empty];
                                _note = gameObject != null ? gameObject.Name.ExtractText() : string.Empty;
                                break;
                            default:
                                break;
                        }
                        _positionText = _position.ToCustomString();
                        _argumentsString = _arguments.ToCustomString();
                        _action = new() { Name = _actionText, Position = _position, Arguments = _arguments, Note = _note, Tag = _actionTag };
                        _showAddActionUI = true;
                    }
                    ImGuiComponents.HelpMarker(item.Item3);
                }
                ImGui.EndPopup();
            }
            if (_showAddActionUI && !ImGui.IsPopupOpen($"Add Action: ({_action?.Name})###AddActionUI"))
            {
                ImGui.SetNextWindowSize(new Vector2(ImGui.CalcTextSize("X").X * 55, ImGui.GetTextLineHeight() * 7), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.FirstUseEver, new(0.5f, 0.5f));
                ImGui.OpenPopup($"Add Action: ({_action?.Name})###AddActionUI");
            }
            if (ImGui.BeginPopupModal($"Add Action: ({_action?.Name})###AddActionUI", ref _showAddActionUI))
            {
                DrawAddActionUIPopup();
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
                    if (Plugin.Actions.Count < 1)
                    {
                        Svc.Log.Error($"You must add at least one action to save the path, please add an action and try again");
                        return;
                    }
                    Svc.Log.Info($"Saving {Plugin.PathFile}");

                    PathFile? pathFile = null;

                    if (DictionaryPaths.TryGetValue(Svc.ClientState.TerritoryType, out ContentPathContainer? container))
                    {
                        DutyPath? dutyPath = container.Paths.FirstOrDefault(dp => dp.FilePath == Plugin.PathFile);
                        if (dutyPath != null)
                        {
                            pathFile = dutyPath.PathFile;
                            if (pathFile.Meta.LastUpdatedVersion < Plugin.Configuration.Version || _changelog.Length > 0)
                            {
                                pathFile.Meta.Changelog.Add(new PathFileChangelogEntry
                                {
                                    Version = Plugin.Configuration.Version,
                                    Change = _changelog
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
            ImGui.SameLine(0, 5);
            using (ImRaii.Disabled(Plugin.PathFile.IsNullOrEmpty()))
            {
                if (ImGuiEx.ButtonWrapped("Open File"))
                    Process.Start("explorer",  Plugin.PathFile ?? string.Empty);
            }
        }

        private unsafe static void DrawAddActionUIPopup()
        {
            if (_action == null)
            {
                _showAddActionUI = false;
                ImGui.CloseCurrentPopup();
                return;
            }

            using (ImRaii.Disabled(_argumentsString.IsNullOrEmpty() && !_noArgument && !_comment))
            {
                if (ImGuiEx.ButtonWrapped(_addActionButton))
                {
                    if (_action.Name is "MoveToObject" or "Target" or "Interactable")
                    {
                        if (uint.TryParse(_arguments[0], out var dataId))
                            AddAction();
                        else
                            ShowPopup("Error", $"{_action.Name}'s must be uint's corresponding to the objects DataId", true);
                    }
                    else
                        AddAction();
                }
            }
            ImGui.SameLine();
            ImGuiEx.CheckboxWrapped("Dont Move", ref _dontMove);
            ImGui.SameLine();
            using (ImRaii.Disabled(_buildListSelected < 0))
            {
                if (ImGuiEx.ButtonWrapped("Delete"))
                {
                    _deleteItem = true;
                    _deleteItemIndex = _buildListSelected;
                    ClearAll();
                }

                ImGui.SameLine();
                if (ImGuiEx.ButtonWrapped("Copy to Clipboard"))
                    ImGui.SetClipboardText(_action?.ToCustomString());
                if (Plugin.isDev)
                {
                    ImGui.SameLine();
                    using (ImRaii.Disabled(!Player.Available || _action == null))
                    {
                        if (ImGuiEx.ButtonWrapped("Teleport To"))
                            Player.GameObject->SetPosition(_action!.Position.X, _action.Position.Y, _action.Position.Z);
                    }
                }
            }
            using (ImRaii.Disabled(_noArgument || _comment))
            {
                ImGui.TextColored(_argumentTextColor, "Argument: (arg1,arg2)");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                ImGui.InputTextWithHint("##Argument", _argumentHint, ref _argumentsString, 200);
            }
            using (ImRaii.Disabled(_comment))
            {
                if (ImGui.Button("Position:"))
                    _positionText = Player.Position.ToCustomString() == _positionText ? Vector3.Zero.ToCustomString() : Player.Position.ToCustomString();
  
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                ImGui.InputText("##Position", ref _positionText, 200);
            }
            ImGui.Text("Note:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##Note", ref _note, 200);
            using (ImRaii.Disabled(_action == null || _action.Tag.HasAnyFlag(ActionTag.Comment, ActionTag.Revival, ActionTag.Treasure)))
            {
                ImGui.Text("Tag:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.BeginCombo("##TagSelection", _actionTag.HasAnyFlag(ActionTag.None, ActionTag.Synced, ActionTag.Unsynced) ? _actionTag.ToCustomString() : ActionTag.None.ToCustomString()))
                {
                    foreach (var actionTag in _actionTags)
                    {
                        bool selected = _actionTag.HasFlag(actionTag);
                        if (ImGui.Selectable(actionTag.ToCustomString(), selected))
                            if (selected)
                                _actionTag &= ~actionTag;
                            else
                                _actionTag |= actionTag;
                    }
                    ImGui.EndCombo();
                }
            }
        }

        private static void DrawBuildList()
        {
            if (!ImGui.BeginListBox("##BuildList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y))) return;
            try
            {
                if (Plugin.InDungeon)
                {
                    int? dragIndex = null;
                    int? dragNext  = null;


                    foreach (var item in Plugin.Actions.Select((Value, Index) => (Value, Index)))
                    {
                        var v4 = item.Value.Name.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase) ? new Vector4(0, 255, 0, 1) : new Vector4(255, 255, 255, 1);

                        ImGui.PushStyleColor(ImGuiCol.Text, v4);

                        if (ImGui.Selectable($"{item.Index}: ###Text{item.Index}", item.Index == _buildListSelected))
                        {
                            if (_buildListSelected == item.Index)
                            {
                                ClearAll();
                            }
                            else
                            {
                                _comment = item.Value.Name.Equals($"<-- Comment -->", StringComparison.InvariantCultureIgnoreCase);
                                _noArgument = (ActionsList?.Any(x => x.Item1.Equals($"{item.Value.Name}", StringComparison.InvariantCultureIgnoreCase) &&
                                                                     x.Item2.Equals("false", StringComparison.InvariantCultureIgnoreCase)) ??
                                               false); // || item.Value.Name.Equals("MoveTo", StringComparison.InvariantCultureIgnoreCase);
                                _dontMove          = item.Value.Position == Vector3.Zero;
                                _actionText        = item.Value.Name;
                                _note              = item.Value.Note;
                                _arguments         = item.Value.Arguments;
                                _argumentsString   = item.Value.Arguments.ToCustomString();
                                _position          = item.Value.Position;
                                _positionText      = _position.ToCustomString();
                                _buildListSelected = item.Index;
                                _showAddActionUI   = true;
                                _dropdownSelected  = ("", "", "");
                                _addActionButton   = "Modify";
                                _action            = item.Value;
                                _actionTag         = item.Value.Tag;
                            }
                        }

                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        {
                            _deleteItem      = true;
                            _deleteItemIndex = item.Index;
                        }

                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered() && !_dragDrop) 
                            _buildListSelected = item.Index;

                        if (_buildListSelected == item.Index && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                        {
                            float mouseYDelta = ImGui.GetMouseDragDelta(0).Y;

                            if (MathF.Abs(mouseYDelta) > ImGui.GetTextLineHeight())
                            {
                                _dragDrop = true;
                                dragIndex = item.Index;
                                dragNext  = item.Index + (mouseYDelta < 0f ? -1 : 1);
                            }
                        }

                        item.Value.GetCustomText(item.Index).ForEach(x =>
                                                                     {
                                                                         ImGui.SameLine(0, 0);
                                                                         ImGui.TextColored(x.color, x.text);
                                                                     });
                        ImGui.PopStyleColor();
                    }

                    if (dragIndex.HasValue && dragNext is >= 0 && dragNext < Plugin.Actions.Count)
                    {
                        (Plugin.Actions[dragNext.Value], Plugin.Actions[dragIndex.Value]) = (Plugin.Actions[dragIndex.Value], Plugin.Actions[dragNext.Value]);
                        _buildListSelected                                                = dragNext.Value;
                        ImGui.ResetMouseDragDelta();
                    }
                    else if(!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !_showAddActionUI)
                    {
                        _dragDrop          = false;
                        _buildListSelected = -1;
                    }

                    if (_deleteItem)
                    {
                        Plugin.Actions.RemoveAt(_deleteItemIndex);
                        _deleteItemIndex = -1;
                        _deleteItem      = false;
                    }
                }
                else
                    ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), "You must enter a dungeon to Build a Path");
            }
            catch (Exception ex) { Svc.Log.Error(ex.ToString()); }
            if (_scrollBottom)
            {
                ImGui.SetScrollHereY(1.0f);
                _scrollBottom = false;
            }
            ImGui.EndListBox();
        }

        private static void ClearAll()
        {
            _actionText = string.Empty;
            _note = string.Empty;
            _position = Vector3.Zero;
            _positionText = string.Empty;
            _arguments = [];
            _argumentHint = string.Empty;
            _dropdownSelected = (string.Empty, string.Empty, string.Empty);
            _dontMove = false;
            _showAddActionUI = false;
            _noArgument = false;
            _addActionButton = "Add";
            _buildListSelected = -1;
            _action = null;
            _comment = false;
            _actionTag = ActionTag.None;
        }

        private static void AddAction()
        {
            if (_action == null) return;

            _action.Name = _actionText;
            _action.Arguments = [.. _argumentsString.Split(",", StringSplitOptions.TrimEntries)];
            _action.Tag = _actionTag;
            _action.Position = !_comment && _positionText.TryGetVector3(out var position) ? position : Vector3.Zero;
            _action.Note = _comment && !_note.StartsWith("<--") && !_note.EndsWith("-->") ? $"<-- {_note} -->" : _note;
            if (_buildListSelected == -1)
            {
                Plugin.Actions.Add(_action);
                _scrollBottom = true;
            }
            else
                Plugin.Actions[_buildListSelected] = _action;
            ImGui.CloseCurrentPopup();
            ClearAll();
        }
    }
}
