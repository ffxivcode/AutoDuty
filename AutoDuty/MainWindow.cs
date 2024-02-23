using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ImGuiNET;
namespace AutoDuty;

public class MainWindow : Window, IDisposable
{
    readonly AutoDuty Plugin;
    (string, string) dropdownSelected = ("", "");
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

    public MainWindow(AutoDuty plugin, List<(string, string)> actionsList) : base(
        "AutoDuty", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        _actionsList = actionsList;

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
        SetPlayerPosition();

        var _pathFileExists = File.Exists(_pathFile);

        if (ImGui.BeginTabBar("MainTabBar", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Main"))
            {
                if (_inDungeon)
                {
                    var progress = IPCManager.VNavmesh_TaskProgress;
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
                }
                using (var d = ImRaii.Disabled(!_inDungeon || IPCManager.VNavmesh_NavmeshIsNull || !IPCManager.BossMod_IsEnabled || !IPCManager.VNavmesh_IsEnabled))
                {
                    using (var d2 = ImRaii.Disabled(!_inDungeon || !_pathFileExists || Plugin.Stage > 0))
                    {
                        if (ImGui.Button("Navigate Path"))
                        {
                            LoadPath();
                            Plugin.Stage = 1;
                        }
                    }
                    ImGui.SameLine(0, 5);
                    using (var d2 = ImRaii.Disabled(!_inDungeon || Plugin.Stage == 0))
                    {
                        if (ImGui.Button("Stop Navigating"))
                        {
                            Plugin.Stage = 0;
                        }
                    }
                    if (!ImGui.BeginListBox("##MainList", new Vector2(-1, -1))) return;
                    if (IPCManager.VNavmesh_IsEnabled && IPCManager.BossMod_IsEnabled)
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
                    }
                    else
                    {
                        if (!IPCManager.VNavmesh_IsEnabled)
                            ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires VNavmesh plugin to be Installed and Loaded\nPlease goto https://github.com/awgil/ffxiv_navmesh");
                        if (!IPCManager.BossMod_IsEnabled)
                            ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires BossMod plugin to be Installed and Loaded\nPlease install and load BossMod");
                    }
                    ImGui.EndListBox();
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

                                if (item.Item2.Equals("false"))
                                {
                                    Plugin.ListBoxPOSText.Add($"{ item.Item1}|");
                                }
                                else if (item.Item1.Equals("Boss"))
                                {
                                    ddisboss = true;
                                    input = $"{_playerPosition.X}, {_playerPosition.Y}, {_playerPosition.Z}";
                                    inputIW = 400;
                                    showAddActionUI = true;
                                }
                                else
                                {
                                    ddisboss = false;
                                    inputIW = 400;
                                    input = "";
                                    showAddActionUI = true;
                                }
                                inputTextName = item.Item2;
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