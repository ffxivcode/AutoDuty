using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using AutoDuty.IPC;
using AutoDuty.Managers;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ImGuiNET;
using static AutoDuty.Managers.ContentManager;

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
    TaskManager _taskManager;
    ContentManager _contentManager;
    bool _scrollBottom = false;
    int currentIndex = -1;
    float currentY = 0;
    bool _showPopup = false;
    string _popupText = "";
    string _popupTitle = "";
    bool anyItemClicked = false;
    int dutyListSelected = -1;
    public MainWindow(AutoDuty plugin, List<(string, string)> actionsList, TaskManager taskManager, ContentManager contentManager) : base(
        "AutoDuty", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(425, 375),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        _actionsList = actionsList;
        _taskManager = taskManager;
        _contentManager = contentManager;

        OnTerritoryChange(Svc.ClientState.TerritoryType);
        Svc.ClientState.TerritoryChanged += OnTerritoryChange;
    }

    private void AddAction(string action)
    {
        _scrollBottom = true;
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

    private void SetPlayerPosition()
    {
        if (Plugin.Player != null)
            _playerPosition = Plugin.Player.Position;
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
    public void SetWindowSize(int x, int y)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(x, y),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Size = new Vector2(x, y);
    }
    public static void CenteredText(string text)
    {
        float windowWidth = ImGui.GetWindowSize().X;
        float textWidth = ImGui.CalcTextSize(text).X;

        ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
        ImGui.Text(text);
    }
    public static bool CenteredButton(string label, float percentWidth, float xIndent = 0)
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X * percentWidth;
        ImGui.SetCursorPosX(xIndent + (ImGui.GetContentRegionAvail().X - buttonWidth) / 2f);
        return ImGui.Button(label, new Vector2(buttonWidth, 35f));
    }
    private void ShowPopup(string popupTitle, string popupText)
    {
        _popupTitle = popupTitle;
        _popupText = popupText;
        _showPopup = true;
    }
    private void DrawPopup()
    {
        if (_showPopup)
        {
            ImGui.OpenPopup(_popupTitle);
        }
        Vector2 textSize = ImGui.CalcTextSize(_popupText);
        ImGui.SetNextWindowSize(new Vector2(textSize.X + 25, textSize.Y + 100));
        if (ImGui.BeginPopupModal(_popupTitle, ref _showPopup, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoMove))
        {
            CenteredText(_popupText);
            ImGui.Spacing();
            if (CenteredButton("OK", .5f, 15))
            {
                _showPopup = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }
    public unsafe override void Draw()
    {
        DrawPopup();
        if (Plugin.Running)
        {
            ImGui.TextColored(new Vector4(0, 0f, 200f, 1), $"AutoDuty - Running ({_contentManager.ListContent[Plugin.CurrentTerritoryIndex].Name}) {Plugin.CurrentLoop} of {Plugin.LoopTimes} Times");
            if (ImGui.Button("Stop"))
            {
                Plugin.Stage = 0;
                SetWindowSize(425, 375);
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
            
            ImGui.TextColored(new Vector4(0, 255f, 0, 1), Svc.ClientState.TerritoryType == _contentManager.ListContent[Plugin.CurrentTerritoryIndex].TerritoryType ? $"Step: {Plugin.ListBoxPOSText[Plugin.Indexer]}" : Plugin.Repairing? $"Repairing" : $"Loading");
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(325, 70),
                MaximumSize = new Vector2(325, 70)
            };
            Size = new Vector2(325, 75);
            return;
        }
        else
        {
            if (Size != new Vector2(425, 375))
                SetWindowSize(425, 375);
        }
        SetPlayerPosition();

        var _pathFileExists = File.Exists(_pathFile);

        if (ImGui.BeginTabBar("MainTabBar", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Main"))
            {
                if (_inDungeon)
                {
                    var progress = VNavmesh_IPCSubscriber.Nav_BuildProgress();
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
                    using (var d = ImRaii.Disabled(!_inDungeon || !VNavmesh_IPCSubscriber.Nav_IsReady() || !BossMod_IPCSubscriber.IsEnabled || !VNavmesh_IPCSubscriber.IsEnabled))
                    {
                        using (var d2 = ImRaii.Disabled(!_inDungeon || !_pathFileExists || Plugin.Stage > 0))
                        {
                            if (ImGui.Button("Start"))
                            {
                                LoadPath();
                                Plugin.StartNavigation();
                                currentIndex = -1;
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

                        if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled)
                        {
                            anyItemClicked = false;
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
                                    Svc.Log.Info("Clicked");
                                    Plugin.Indexer = item.index;
                                    anyItemClicked = true;
                                    Plugin.MainListClicked = true;
                                }
                            }
                            //if (!anyItemClicked && Plugin.Stage == 0 && Plugin.Indexer > 0)
                              //  Plugin.Indexer = 0;
                            if (currentIndex != Plugin.Indexer && currentIndex > -1 && Plugin.Stage > 0)
                            {
                                //currentY = ImGui.GetScrollY();
                                var lineHeight = ImGui.GetTextLineHeightWithSpacing();
                                currentIndex = Plugin.Indexer;
                                if (currentIndex > 1)
                                    ImGui.SetScrollY((currentIndex - 1) * lineHeight);
                                //currentY = ImGui.GetScrollY();
                            }
                            else if (currentIndex == -1 && Plugin.Stage > 0)
                            {
                                currentIndex = 0;
                                ImGui.SetScrollY(currentIndex);
                            }
                            if (_inDungeon && !_pathFileExists)
                                ImGui.TextColored(new Vector4(0, 255, 0, 1), $"No Path file was found for:\n{TerritoryName.GetTerritoryName(_territoryType).Split('|')[1].Trim()}\n({_territoryType}.json)\nin the Paths Folder:\n{Plugin.PathsDirectory}\nPlease download from:\n{pathsURL}\nor Create in the Build Tab");
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
                                    ShowPopup("Error", "This has not yet been implemented");
                                else if (!Plugin.Support && !Plugin.Trust && !Plugin.Squadron && !Plugin.Regular)
                                    ShowPopup("Error", "You must select a version\nof the dungeon to run");
                                else if (File.Exists($"{Plugin.PathsDirectory}/{_contentManager.ListContent[_clickedDuty].TerritoryType}.json"))
                                    Plugin.Run(_clickedDuty);
                                else
                                    ShowPopup("Error", "No path was found");
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
                        }
                        if (!ImGui.BeginListBox("##DutyList", new Vector2(-1, -1))) return;

                        if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled)
                        {
                            List<Content> list = [];
                            if (Plugin.Support)
                                list = _contentManager.ListContent.Where(x => x.DawnContent).ToList();
                            else if (Plugin.Trust)
                                list = _contentManager.ListContent.Where(x => x.DawnContent && x.ExVersion > 2).ToList();
                            else if (Plugin.Squadron)
                                list = _contentManager.ListContent.Where(x => x.GCArmyContent).ToList();
                            else if (Plugin.Regular)
                                list = _contentManager.ListContent;

                            if (list.Count > 0)
                            {
                                foreach (var item in list.Select((Value, Index) => (Value, Index)))
                                {
                                    using (var d2 = ImRaii.Disabled(item.Value.ClassJobLevelRequired > Plugin.Player?.Level))
                                    {
                                        if (ImGui.Selectable(item.Value.Name, dutyListSelected == item.Index))
                                        {
                                            dutyListSelected = item.Index;
                                            _clickedDuty = _contentManager.ListContent.FindIndex(a => a.Name == item.Value.Name);
                                            Svc.Log.Info($"{item.Value.Name} : {item.Value.GCArmyIndex}");
                                            Svc.Log.Info($"{_contentManager.ListContent[_clickedDuty].Name} : {_contentManager.ListContent[_clickedDuty].GCArmyIndex}");
                                        }
                                    }
                                    //ImGui.PopStyleColor();

                                    /*if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                    {

                                        _clickedDuty = _contentManager.ListContent.FindIndex(a => a.Name == item.Value.Name) -1; 
                                        Svc.Log.Info($"{item.Value.Name} : {_clickedDuty} = {_contentManager.ListContent.FindIndex(a => a.Name == item.Value.Name)}");
                                    }
                                    if (_clickedDuty == _contentManager.ListContent.FindIndex(a => a.Name == item.Value.Name)) { }
                                    //v4 = new Vector4(0, 255, 0, 1);
                                    else
                                    {
                                        //v4 = new Vector4(255, 255, 255, 1);
                                    }
                                    using (var d2 = ImRaii.Disabled(item.Value.ClassJobLevelRequired > Plugin.Player?.Level))
                                    {
                                        if (item.Value.ClassJobLevelRequired > Plugin.Player?.Level) { }
                                            //v4 = new Vector4(0, 0, 0, 1);
                                        //ImGui.TextColored(v4, $"{item.Value.Name}");
                                    }*/
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
                ImGui.EndTabItem();
            }
            using (var d = ImRaii.Disabled(!_inDungeon || Plugin.Stage > 0))
            {
                if (ImGui.BeginTabItem("Build"))
                {
                    ImGui.Text("Build Path:");
                    if (ImGui.Button("Add POS"))
                    {
                        _scrollBottom = true;
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
                            Svc.Log.Info($"Saving {_pathFile}");
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
                        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 1)));
                        //ImGui.PushStyleColor(new Vector4(0, 255, 0, 1));
                        
                        ImGui.Selectable(item, ImGui.IsItemClicked(ImGuiMouseButton.Right) || ImGui.IsItemClicked(ImGuiMouseButton.Left));
                        ImGui.PopStyleColor();
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            // Do stuff on Selectable() double click.
                            if (item.Split('|')[0].Equals("Wait") || item.Split('|')[0].Equals("Interactable") || item.Split('|')[0].Equals("Boss") || item.Split('|')[0].Equals("SelectYesno") || item.Split('|')[0].Equals("MoveToObject") || item.Split('|')[0].Equals("WaitFor"))
                            {
                                //do nothing
                            }
                            else
                            {
                                if (AutoDuty.Plugin.Player != null)
                                    ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)AutoDuty.Plugin.Player.Address)->SetPosition(float.Parse(item.Split(',')[0]), float.Parse(item.Split(',')[1]), float.Parse(item.Split(',')[2]));
                            }
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        {
                            Plugin.ListBoxPOSText.Remove(item);
                            _scrollBottom = true;
                        }
                        else if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                        {
                           
                            //Add a inputbox that when this is selected it puts this item in the input box and allows direct modification of items
                        }
                    }
                    if (_scrollBottom)
                    {
                        ImGui.SetScrollHereY(1.0f);
                        _scrollBottom = false;
                    }
                    ImGui.EndListBox();
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
        ImGui.SameLine(0, 5);
        if (ImGui.Button("Config"))
        {
            Plugin.OpenConfigUI();
        }
    }
}