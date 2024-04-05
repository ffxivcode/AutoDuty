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

namespace AutoDuty.Windows
{
    internal static class BuildTab
    {
        internal static List<(string, string)>? ActionsList { get; set; }

        private static bool _scrollBottom = false;
        private static string _input = "";
        private static string _action = "";
        private static string inputTextName = "";
        private static int inputIW = 200;
        private static bool showAddActionUI = false;
        private static (string, string) dropdownSelected = ("", "");

        private static void AddAction(string action)
        {
            _scrollBottom = true;
            Plugin.ListBoxPOSText.Add(action);
            _input = "";
            _action = "";
        }
        internal static void Draw()
        {
            using var d = ImRaii.Disabled(!Plugin.InDungeon || Plugin.Stage > 0 || Plugin.Player == null);
            ImGui.Text($"Build Path: {(TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryType).Contains('|') ? TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryType).Split('|')[1].Trim() : TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryType))} ({Plugin.CurrentTerritoryType})");
            if (ImGui.Button("Add POS"))
            {
                _scrollBottom = true;
                Plugin.ListBoxPOSText.Add($"MoveTo|{Plugin.PlayerPosition.X:0.00}, {Plugin.PlayerPosition.Y:0.00}, {Plugin.PlayerPosition.Z:0.00}|");
            }
            ImGui.SameLine(0, 5);
            if (ImGui.Button("Add Action"))
                ImGui.OpenPopup("AddActionPopup");

            if (ImGui.BeginPopup("AddActionPopup"))
            {
                if (ActionsList == null)
                    return;

                foreach (var item in ActionsList)
                {
                    if (ImGui.Selectable(item.Item1))
                    {
                        dropdownSelected = item;
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
                                _input = Plugin.ClosestInteractableEventObject?.Name.TextValue ?? "";
                                break;
                            case "Target":
                                _input = Plugin.ClosestTargetableBattleNpc?.Name.TextValue ?? "";
                                break;
                            default:
                                _input = "";
                                _action = $"{item.Item1}|{Plugin.PlayerPosition.X:0.00}, {Plugin.PlayerPosition.Y:0.00}, {Plugin.PlayerPosition.Z:0.00}|";
                                break;
                        }
                        inputIW = 400;
                        if (item.Item2.Equals("false"))
                            AddAction(_action);
                        showAddActionUI = !item.Item2.Equals("false");
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
                    Svc.Log.Info($"Saving {Plugin.PathFile}");
                    string json = JsonSerializer.Serialize(Plugin.ListBoxPOSText);
                    File.WriteAllText(Plugin.PathFile, json);
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
            }
            if (showAddActionUI)
            {
                ImGui.PushItemWidth(inputIW);
                ImGui.InputText(inputTextName, ref _input, 50);
                ImGui.SameLine(0, 5);
                if (ImGui.Button("Add"))
                {
                    if (_input.IsNullOrEmpty())
                    {
                        MainWindow.ShowPopup("Error", "You must enter an input");
                        return;
                    }
                    AddAction($"{dropdownSelected.Item1}|{Plugin.PlayerPosition.X:0.00}, {Plugin.PlayerPosition.Y:0.00}, {Plugin.PlayerPosition.Z:0.00}|{_input}");
                    showAddActionUI = false;
                }
            }
            if (!ImGui.BeginListBox("##BuildList", new Vector2(-1, -1))) return;
            try
            {
                if (Plugin.InDungeon)
                {
                    foreach (var item in Plugin.ListBoxPOSText)
                    {
                        ImGui.Selectable(item, ImGui.IsItemClicked(ImGuiMouseButton.Right) || ImGui.IsItemClicked(ImGuiMouseButton.Left));
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            // Do stuff on Selectable() double click.
                            if (item.Any(c => c == '|') && !item.Split('|')[1].Equals("0, 0, 0"))
                            {
                                ImGui.SetClipboardText(item.Split('|')[1]);
                                //if (AutoDuty.Plugin.Player != null)
                                //    ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)AutoDuty.Plugin.Player.Address)->SetPosition(float.Parse(item.Split('|')[1].Split(',')[0]), float.Parse(item.Split('|')[1].Split(',')[1]), float.Parse(item.Split('|')[1].Split(',')[2]));
                            }
                            else
                            {
                                //if (AutoDuty.Plugin.Player != null)
                                //    ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)AutoDuty.Plugin.Player.Address)->SetPosition(float.Parse(item.Split(',')[0]), float.Parse(item.Split(',')[1]), float.Parse(item.Split(',')[2]));
                            }
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        {
                            Plugin.ListBoxPOSText.Remove(item);
                            _scrollBottom = true;
                        }
                        else if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                        {

                            //Add a inputbox that when this is selected it puts this item in the input box and allows direct modification of items or if add is hit with an item selected it will add directly after that item.
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
