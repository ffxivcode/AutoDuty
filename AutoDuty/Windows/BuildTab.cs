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
using ECommons.GameHelpers;
using static AutoDuty.Managers.ContentPathsManager;

namespace AutoDuty.Windows
{
    internal static class BuildTab
    {
        internal static List<(string, string)>? ActionsList { get; set; }

        private static bool _scrollBottom = false;
        private static string _changelog = string.Empty;
        private static string _input = "";
        private static string _action = "";
        private static string _inputTextName = "";
        private static bool _dontMove = false;
        private static float _inputIW = 200 * ImGuiHelpers.GlobalScale;
        private static bool _showAddActionUI = false;
        private static (string, string) _dropdownSelected = ("", "");
        private static int _buildListSelected = -1;
        private static string _addActionButton = "Add"; 
        private static bool _dragDrop = false;

        public static readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true, IgnoreReadOnlyProperties = true};

        private static string GetPlayerPosition => $"{Plugin.PlayerPosition.X.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}, {Plugin.PlayerPosition.Y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}, {Plugin.PlayerPosition.Z.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";

        private static void ClearAll()
        {
            _input = "";
            _action = "";
            _dropdownSelected = ("", "");
            _buildListSelected = -1;
            _dontMove = false;
            _showAddActionUI = false;
        }

        private static void AddAction(string action, int index = -1)
        {
            _scrollBottom = true;
            if (index == -1)
                Plugin.ListBoxPOSText.Add(action);
            else
                Plugin.ListBoxPOSText[index] = action;
            ClearAll();
        }
        internal static void Draw()
        {
            if (MainWindow.CurrentTabName != "Build")
                MainWindow.CurrentTabName = "Build";
            using var d = ImRaii.Disabled(!Plugin.InDungeon || Plugin.Stage > 0 || Plugin.Player == null);
            ImGui.Text($"Build Path: ({Svc.ClientState.TerritoryType}) {(ContentHelper.DictionaryContent.TryGetValue(Svc.ClientState.TerritoryType, out var content) ? content.DisplayName : TerritoryName.GetTerritoryName(Svc.ClientState.TerritoryType))}");

            string idText = $"({Svc.ClientState.TerritoryType}) ";
            ImGui.Text(idText);
            ImGui.SameLine();
            string path       = Path.GetFileName(Plugin.PathFile).Replace(idText, string.Empty).Replace(".json", string.Empty);
            string pathOrg    = path;

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
                Plugin.ListBoxPOSText.Add($"MoveTo|{GetPlayerPosition}|");
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Add Action"))
            {
                if (_showAddActionUI)
                    ClearAll();
                ImGui.OpenPopup("AddActionPopup");
            }

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
                                _action = $"ExitDuty|0, 0, 0|";
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
                                _action = $"{item.Item1}|{GetPlayerPosition}|";
                                break;
                        }
                        _inputIW = 200 * ImGuiHelpers.GlobalScale;
                        if (item.Item2.Equals("false"))
                            AddAction(_action);
                        _addActionButton = "Add";
                        _showAddActionUI = !item.Item2.Equals("false");
                        _inputTextName = item.Item2;
                    }
                }
                ImGui.EndPopup();
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Clear Path"))
            {
                Plugin.ListBoxPOSText.Clear();
                ClearAll();
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Save Path"))
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
                            if(pathFile.meta.LastUpdatedVersion < Plugin.Configuration.Version || _changelog.Length > 0)
                            {
                                pathFile.meta.changelog.Add(new PathFileChangelogEntry
                                                            {
                                                                version = Plugin.Configuration.Version,
                                                                change  = _changelog
                                                            });
                                _changelog = string.Empty;
                            }
                        }
                    }

                    pathFile ??= PathFile.Default;

                    pathFile.actions = Plugin.ListBoxPOSText.ToArray();

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
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Load Path"))
            {
                Plugin.LoadPath();
                ClearAll();
            }
            ImGui.Text("Changelog:");
            ImGui.SameLine();
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
                if (ImGui.Button(_addActionButton))
                {
                    if (_input.IsNullOrEmpty())
                    {
                        MainWindow.ShowPopup("Error", "You must enter an input");
                        return;
                    }
                    if (_dropdownSelected.Item1.IsNullOrEmpty() && _dropdownSelected.Item2.IsNullOrEmpty() && (_input.Count(c => c == '|') < 2 || !_input.Split('|')[1].All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.')))
                        MainWindow.ShowPopup("Error", "Input is not in the correct format\nAction|Position|ActionParams(if needed)");
                    if (_dontMove && _dropdownSelected.Item1.IsNullOrEmpty() && _dropdownSelected.Item2.IsNullOrEmpty())
                        AddAction($"{_input.Split('|')[0]}|0, 0, 0|{_input.Split('|')[2]}", _buildListSelected);
                    else if (_dropdownSelected.Item1.IsNullOrEmpty() && _dropdownSelected.Item2.IsNullOrEmpty())
                        AddAction($"{_input}", _buildListSelected);
                    else if (_dontMove)
                        AddAction($"{_dropdownSelected.Item1}|0, 0, 0|{_input}");
                    else
                        AddAction($"{_dropdownSelected.Item1}|{GetPlayerPosition}|{_input}");
                }
                ImGui.SameLine(0, 5);
                ImGui.Checkbox("Dont Move", ref _dontMove);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
            if (!ImGui.BeginListBox("##BuildList", new Vector2(355 * ImGuiHelpers.GlobalScale, 575 * ImGuiHelpers.GlobalScale))) return;
            try
            {
                if (Plugin.InDungeon)
                {
                    foreach (var item in Plugin.ListBoxPOSText.Select((Value, Index) => (Value, Index)))
                    {
                        if (ImGui.Selectable(item.Value, item.Index == _buildListSelected, ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            if(_dragDrop)
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
                                _dropdownSelected = ("", "");
                                _addActionButton = "Modify";
                                _inputTextName = "";
                                _input = item.Value;
                                _inputIW = 400 * ImGuiHelpers.GlobalScale;
                            }
                        }
                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
                        {
                            int n_next = item.Index + (ImGui.GetMouseDragDelta(0).Y < 0f ? -1 : 1);
                            if (n_next >= 0 && n_next < Plugin.ListBoxPOSText.Count)
                            {
                                Plugin.ListBoxPOSText[item.Index] = Plugin.ListBoxPOSText[n_next];
                                Plugin.ListBoxPOSText[n_next] = item.Value;
                                _buildListSelected = -1;
                                ImGui.ResetMouseDragDelta();
                                _dragDrop = true;
                                ClearAll();
                            }
                        }
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            // Do stuff on Selectable() double click.
                            if (item.Value.Any(c => c == '|') && !item.Value.Split('|')[1].Equals("0, 0, 0"))
                            {
                                ImGui.SetClipboardText(item.Value.Split('|')[1]);
                                //if (AutoDuty.Plugin.Player != null)
                                //    ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)AutoDuty.Plugin.Player.Address)->SetPosition(float.Parse(item.Split('|')[1].Split(',')[0]), float.Parse(item.Split('|')[1].Split(',')[1]), float.Parse(item.Split('|')[1].Split(',')[2]));
                            }
                            else
                            {
                                ImGui.SetClipboardText(item.Value);
                                //if (AutoDuty.Plugin.Player != null)
                                //    ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)AutoDuty.Plugin.Player.Address)->SetPosition(float.Parse(item.Split(',')[0]), float.Parse(item.Split(',')[1]), float.Parse(item.Split(',')[2]));
                            }
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        {
                            Plugin.ListBoxPOSText.Remove(item.Value);
                            _scrollBottom = true;
                        }
                    }
                }
                else
                    ImGui.TextColored(new Vector4(0, 1, 0, 1), "You must enter a dungeon to Build a Path");
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
