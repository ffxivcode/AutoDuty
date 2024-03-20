using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using AutoDuty.IPC;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ImGuiNET;
using Dalamud.Game.ClientState.Objects.Enums;

namespace AutoDuty.Windows;

public class MainWindow : Window, IDisposable
{
    readonly AutoDuty Plugin;
    (string, string) dropdownSelected = ("", "");
    private string pathsURL = "https://github.com/ffxivcode/DalamudPlugins/tree/main/AutoDuty/Paths";
    ushort _territoryType;
    Vector3 _playerPosition;
    bool _inDungeon = false;
    string _pathFile = "";
    string input = "";
    string inputTextName = "";
    int inputIW = 200;
    bool showAddActionUI = false;
    bool ddisboss = false;
    readonly List<(string, string)> _actionsList;
    int _clickedDuty = -1;
    BossMod_IPCSubscriber _vbmIPC;
    VNavmesh_IPCSubscriber _vnavIPC;
    MBT_IPCSubscriber _mbtIPC;

    public MainWindow(AutoDuty plugin, List<(string, string)> actionsList, VNavmesh_IPCSubscriber vnavIPC, BossMod_IPCSubscriber vbmIPC, MBT_IPCSubscriber mbtIPC) : base(
        "AutoDuty", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(425, 375),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        _actionsList = actionsList;
        _vbmIPC = vbmIPC;
        _vnavIPC = vnavIPC;
        _mbtIPC = mbtIPC;

        OnTerritoryChange(Svc.ClientState.TerritoryType);
        Svc.ClientState.TerritoryChanged += OnTerritoryChange;
        
    }

    private void AddAction(string action)
    {
        if (action.Contains("Boss"))
        {
            Plugin.ListBoxPOSText.Add($"Boss|{_playerPosition.X}, {_playerPosition.Y}, {_playerPosition.Z}");
        }
        else
            Plugin.ListBoxPOSText.Add(action + "|" + input);
        input = "";
    }

    public void Dispose()
    {
    }

    private void OnTerritoryChange(ushort t)
    {
        if (t == 0)
            return;

        _territoryType = t;
        _inDungeon = ExcelTerritoryHelper.Get(_territoryType).TerritoryIntendedUse == 3;
        _pathFile = $"{Plugin.PathsDirectory}/{_territoryType}.json";
        if (File.Exists(_pathFile))
            LoadPath();
        else
            Plugin.ListBoxPOSText.Clear();
    }

    private unsafe void SetPlayerPosition()
    {
        if (Player.Available)
            _playerPosition = Player.GameObject->Position;
        else
            _playerPosition = new Vector3(0, 0, 0);
    }

    private void LoadPath()
    {
        try
        {
            using (StreamReader streamReader = new(_pathFile, Encoding.UTF8))
            {
                Plugin.ListBoxPOSText.Clear();
                var json = streamReader.ReadToEnd();
                List<string>? paths;
                if ((paths = JsonSerializer.Deserialize<List<string>>(json)) is not null)
                    Plugin.ListBoxPOSText = paths;
            }
        }
        catch (Exception e)
        {
            Svc.Log.Error(e.ToString());
            //throw;
        }
    }

    public override void Draw()
    {
        if (Plugin.Running)
        {
            ImGui.TextColored(new Vector4(0, 0f, 200f, 1), $"AutoDuty - Running ({Plugin.ListBoxDutyText[Plugin.CurrentTerritoryIndex].Item1}) {Plugin.CurrentLoop} of {Plugin.LoopTimes} Times");
            if (ImGui.Button("Stop"))
            {
                Plugin.Stage = 0;
                Plugin.Running = false;
                Plugin.CurrentLoop = 0;
                SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(425, 375),
                    MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
                };
                Size = new Vector2(425, 375);
                return;
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
            ImGui.SameLine(0, 5);
            ImGui.TextColored(new Vector4(0, 255f, 0, 1), Svc.ClientState.TerritoryType == Plugin.ListBoxDutyText[Plugin.CurrentTerritoryIndex].Item2 ? $"Step: {Plugin.ListBoxPOSText[Plugin.Indexer]}" : $"Loading");
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(325, 70),
                MaximumSize = new Vector2(325, 70)
            };
            Size = new Vector2(325, 75);
            return;
        }
        SetPlayerPosition();

        var _pathFileExists = File.Exists(_pathFile);

        if (ImGui.BeginTabBar("MainTabBar", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Main"))
            {
                if (_inDungeon)
                {
                    var progress = _vnavIPC.Nav_BuildProgress();
                    if (progress >= 0)
                    {
                        ImGui.Text(TerritoryName.GetTerritoryName(_territoryType).Split('|')[1].Trim() + " Mesh Loading: ");
                        ImGui.ProgressBar(progress, new(200, 0));
                    }
                    else
                        ImGui.Text(TerritoryName.GetTerritoryName(_territoryType).Split('|')[1].Trim() + " Mesh Loaded");
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                    using (var d = ImRaii.Disabled(!_inDungeon || !_vnavIPC.Nav_IsReady() || !_vbmIPC.IsEnabled || !_vnavIPC.IsEnabled || !_mbtIPC.IsEnabled))
                    {
                        using (var d2 = ImRaii.Disabled(!_inDungeon || !_pathFileExists || Plugin.Stage > 0))
                        {
                            if (ImGui.Button("Start"))
                            {
                                LoadPath();
                                Plugin.StartNavigation();
                            }
                        }
                        ImGui.SameLine(0, 5);
                        using (var d2 = ImRaii.Disabled(!_inDungeon || Plugin.Stage == 0))
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
                        }
                        if (!ImGui.BeginListBox("##MainList", new Vector2(-1, -1))) return;

                        if (_vnavIPC.IsEnabled && _vbmIPC.IsEnabled && _mbtIPC.IsEnabled)
                        {
                            foreach (var item in Plugin.ListBoxPOSText.Select((name, index) => (name, index)))
                            {
                                Vector4 v4 = new();
                                if (item.index == Plugin.Indexer && Plugin.Stage > 0)
                                    v4 = new Vector4(0, 255, 0, 1);
                                else
                                    v4 = new Vector4(255, 255, 255, 1);
                                ImGui.TextColored(v4, item.name);
                            }
                            if (_inDungeon && !_pathFileExists)
                                ImGui.TextColored(new Vector4(0, 255, 0, 1), $"No Path file was found for:\n{TerritoryName.GetTerritoryName(_territoryType).Split('|')[1].Trim()}\n({_territoryType}.json)\nin the Paths Folder:\n{Plugin.PathsDirectory}\nPlease download from:\n{pathsURL}\nor Create in the Build Tab");
                        }
                        else
                        {
                            if (!_vnavIPC.IsEnabled)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires VNavmesh plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!_vbmIPC.IsEnabled)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires BossMod plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!_mbtIPC.IsEnabled)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires MBT plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://raw.githubusercontent.com/ffxivcode/DalamudPlugins/main/repo.json");
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
                                if (File.Exists($"{Plugin.PathsDirectory}/{Plugin.ListBoxDutyText[_clickedDuty].Item2}.json"))
                                    Plugin.Run(_clickedDuty);
                            }
                        }
                        else
                        {
                            if (ImGui.Button("Stop"))
                            {
                                Plugin.Stage = 0;
                                Plugin.Running = false;
                                Plugin.CurrentLoop = 0;
                                SizeConstraints = new WindowSizeConstraints
                                {
                                    MinimumSize = new Vector2(425, 375),
                                    MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
                                };
                                Size = new Vector2(425, 375);
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
                        if (!ImGui.BeginListBox("##DutyList", new Vector2(-1, -1))) return;

                        if (_vnavIPC.IsEnabled && _vbmIPC.IsEnabled && _mbtIPC.IsEnabled)
                        {
                            foreach (var item in Plugin.ListBoxDutyText.Select((Value, Index) => (Value, Index)))
                            {
                                Vector4 v4 = new();
                                if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                    _clickedDuty = item.Index - 1;
                                if (_clickedDuty == item.Index)
                                    v4 = new Vector4(0, 255, 0, 1);
                                else
                                    v4 = new Vector4(255, 255, 255, 1);
                                ImGui.TextColored(v4, item.Value.Item1);
                            }
                        }
                        else
                        {
                            if (!_vnavIPC.IsEnabled)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires VNavmesh plugin to be Installed and Loaded\nFor proper navigation and movement\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!_vbmIPC.IsEnabled)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires BossMod plugin to be Installed and Loaded\nFor proper named mechanic handling\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!_mbtIPC.IsEnabled)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires MBT plugin to be Installed and Loaded\nFor proper AutoFollow\nPlease add 3rd party repo:\nhttps://raw.githubusercontent.com/ffxivcode/DalamudPlugins/main/repo.json");
                        }
                        ImGui.EndListBox();
                    }
                }
                ImGui.EndTabItem();
            }
            using (var d = ImRaii.Disabled(!_inDungeon || Plugin.Stage > 0))
            {
                if (ImGui.BeginTabItem("Build"))
                {
                    ImGui.Text("Build Path:");
                    if (ImGui.Button("Add POS"))
                    {
                        Plugin.ListBoxPOSText.Add($"{_playerPosition.X}, {_playerPosition.Y}, {_playerPosition.Z}");
                    }
                    ImGui.SameLine(0, 5);
                    if (ImGui.Button("Add Action"))
                        ImGui.OpenPopup("AddActionPopup");

                    if (ImGui.BeginPopup("AddActionPopup"))
                    {
                        foreach (var item in _actionsList)
                        {
                            if (ImGui.Selectable(item.Item1))
                            {
                                dropdownSelected = item;
                                ddisboss = false;
                                if (item.Item2.Equals("false"))
                                {
                                    Plugin.ListBoxPOSText.Add($"{item.Item1}|");
                                }
                                else
                                {
                                    switch (item.Item1)
                                    {
                                        case "Boss":
                                            ddisboss = true;
                                            input = $"{_playerPosition.X}, {_playerPosition.Y}, {_playerPosition.Z}";
                                            break;
                                        case "MoveToObject":
                                        case "Interactable":
                                            input = Managers.ObjectManager.GetObjectsByRadius([.. Svc.Objects.Where(a => a.IsTargetable && !a.ObjectKind.Equals(ObjectKind.Player))], 10).FirstOrDefault()?.Name.TextValue ?? "";
                                            break;
                                        case "SelectYesno":
                                            input = "Yes";
                                            break;
                                        default:
                                            input = "";
                                            break;
                                    }
                                    inputIW = 400;
                                    showAddActionUI = true;
                                    inputTextName = item.Item2;
                                }
                            }
                        }
                        ImGui.EndPopup();
                    }
                    ImGui.SameLine(0, 5);
                    if (ImGui.Button("Clear Path"))
                    {
                        Plugin.ListBoxPOSText.Clear();
                    }
                    ImGui.SameLine(0, 5);
                    if (ImGui.Button("Save Path"))
                    {
                        try
                        {
                            string json = JsonSerializer.Serialize(Plugin.ListBoxPOSText);
                            File.WriteAllText(_pathFile, json);
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
                        LoadPath();
                    }
                    if (showAddActionUI)
                    {
                        ImGui.PushItemWidth(inputIW);
                        if (ddisboss)
                            input = $"{_playerPosition.X}, {_playerPosition.Y}, {_playerPosition.Z}";
                        ImGui.InputText(inputTextName, ref input, 50);
                        ImGui.SameLine(0, 5);
                        if (ImGui.Button("Add"))
                        {
                            AddAction(dropdownSelected.Item1);
                            showAddActionUI = false;
                        }
                    }
                    if (!ImGui.BeginListBox("##BuildList", new Vector2(-1, -1))) return;
                    foreach (var item in Plugin.ListBoxPOSText)
                    {
                        ImGui.Selectable(item, ImGui.IsItemClicked(ImGuiMouseButton.Right) || ImGui.IsItemClicked(ImGuiMouseButton.Left));

                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            // Do stuff on Selectable() double click.
                            if (item.Split('|')[0].Equals("Wait") || item.Split('|')[0].Equals("Interactable") || item.Split('|')[0].Equals("Boss") || item.Split('|')[0].Equals("SelectYesno") || item.Split('|')[0].Equals("MoveToObject") || item.Split('|')[0].Equals("WaitFor"))
                            {
                                //do nothing
                            }
                            //else
                            //  Plugin.TeleportPOS(new Vector3(float.Parse(item.Split(',')[0]), float.Parse(item.Split(',')[1]), float.Parse(item.Split(',')[2])));
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                            Plugin.ListBoxPOSText.Remove(item);
                        else if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                        {
                            //Add a inputbox that when this is selected it puts this item in the input box and allows direct modification of items
                        }
                    }
                    ImGui.EndListBox();
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
    }
}