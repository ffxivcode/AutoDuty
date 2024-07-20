﻿using System;
using System.Numerics;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.Funding;
using ECommons.ImGuiMethods;
using ECommons.Schedulers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.STD.Helper;
using ImGuiNET;
using static AutoDuty.AutoDuty;

namespace AutoDuty.Windows;

public class MainWindow : Window, IDisposable
{
    private static bool _showPopup = false;
    private static string _popupText = "";
    private static string _popupTitle = "";
    private string openTabName = "";

    public MainWindow() : base(
        "AutoDuty", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(10, 10),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        TitleBarButtons.Add(new() { Icon = FontAwesomeIcon.Cog, IconOffset = new(1, 1), Click = _ => OpenTab("Config") });
        TitleBarButtons.Add(new() { ShowTooltip = () => ImGui.SetTooltip("Support Herculezz on Ko-fi"), Icon = FontAwesomeIcon.Heart, IconOffset = new(1, 1), Click = _ => GenericHelpers.ShellStart("https://ko-fi.com/Herculezz") });
        PatreonBanner.DonateLink = "https://ko-fi.com/Herculezz";
        PatreonBanner.Text = "Support AutoDuty";
        PatreonBanner.TooltipText = "Left click to support Herculezz in the Development of AutoDuty on Kofi";
        PatreonBanner.RightClickMenu = false;
    }

    internal void OpenTab(string tabName)
    {
        openTabName = tabName;
        _ = new TickScheduler(delegate
        {
            openTabName = "";
        }, 25);
    }

    public void Dispose()
    {
    }

    internal static void Start()
    {
        ImGui.SameLine(0, 5);
    }

    internal static void StopResumePause()
    {
        using (var d = ImRaii.Disabled((!Plugin.Running && !Plugin.Started) || Plugin.CurrentTerritoryContent == null))
        {
            if (ImGui.Button("Stop"))
            {
                Plugin.MainWindow.OpenTab("Main");
                Plugin.Stage = 0;
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
        }
    }

    internal static void GotoAndActions()
    {
        using (var d2 = ImRaii.Disabled(Plugin.Running || Plugin.Started))
        {
            using (var GotoDisabled = ImRaii.Disabled(GCTurninHelper.GCTurninRunning || DesynthHelper.DesynthRunning || ExtractHelper.ExtractRunning))
            {
                if (ImGui.Button("Goto"))
                {
                    ImGui.OpenPopup("GotoPopup");
                }
            }
            ImGui.SameLine(0, 5);
            using (var GCTurninDisabled = ImRaii.Disabled(DesynthHelper.DesynthRunning || ExtractHelper.ExtractRunning || Plugin.Goto))
            {
                if (GCTurninHelper.GCTurninRunning)
                {
                    if (ImGui.Button("Stop TurnIn"))
                        Plugin.StopAndResetALL();
                }
                else
                {
                    if (ImGui.Button("TurnIn"))
                    {
                        if (Deliveroo_IPCSubscriber.IsEnabled)
                            GCTurninHelper.Invoke();
                        else
                            ShowPopup("Missing Plugin", "GC Turnin Requires Deliveroo plugin. Get @ https://git.carvel.li/liza/plugin-repo");
                    }
                    if (Deliveroo_IPCSubscriber.IsEnabled)
                        ToolTip("Click to Goto GC Turnin and Invoke Deliveroo");
                    else
                        ToolTip("GC Turnin Requires Deliveroo plugin. Get @ https://git.carvel.li/liza/plugin-repo");
                }
            }
            ImGui.SameLine(0, 5);
            using (var DesynthDisabled = ImRaii.Disabled(GCTurninHelper.GCTurninRunning || ExtractHelper.ExtractRunning || Plugin.Goto))
            {
                if (DesynthHelper.DesynthRunning)
                {
                    if (ImGui.Button("Stop Desynth"))
                        Plugin.StopAndResetALL();
                }
                else
                {
                    if (ImGui.Button("Desynth"))
                        DesynthHelper.Invoke();
                    ToolTip("Click to Desynth all Items in Inventory");
                }
            }
            ImGui.SameLine(0, 5);
            using (var ExtractDisabled = ImRaii.Disabled(GCTurninHelper.GCTurninRunning || DesynthHelper.DesynthRunning || Plugin.Goto))
            {
                if (ExtractHelper.ExtractRunning)
                {
                    if (ImGui.Button("Stop Extract"))
                        Plugin.StopAndResetALL();
                }
                else
                {
                    if (ImGui.Button("Extract"))
                    {
                        if (QuestManager.IsQuestComplete(66174))
                            ExtractHelper.Invoke();
                        else
                            ShowPopup("Missing Quest Completion", "Materia Extraction requires having completed quest: Forging the Spirit");
                    }
                    if (QuestManager.IsQuestComplete(66174))
                        ToolTip("Click to Extract Materia");
                    else
                        ToolTip("Materia Extraction requires having completed quest: Forging the Spirit");
                }
            }
            if (ImGui.BeginPopup("GotoPopup"))
            {
                if (ImGui.Selectable("Barracks"))
                {
                    Plugin.GotoAction("Barracks");
                }
                if (ImGui.Selectable("Inn"))
                {
                    Plugin.GotoAction("Inn");
                }
                if (ImGui.Selectable("GCSupply"))
                {
                    Plugin.GotoAction("GCSupply");
                }
                if (ImGui.Selectable("Repair"))
                {
                    Plugin.GotoAction("Repair");
                }
                ImGui.EndPopup();
            }
        }
    }

    internal static void ToolTip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            ImGuiEx.Text(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    internal static void CenteredText(string text)
    {
        float windowWidth = ImGui.GetWindowSize().X;
        float textWidth = ImGui.CalcTextSize(text).X;

        ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
        ImGui.Text(text);
    }

    internal static bool CenteredButton(string label, float percentWidth, float xIndent = 0)
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X * percentWidth;
        ImGui.SetCursorPosX(xIndent + (ImGui.GetContentRegionAvail().X - buttonWidth) / 2f);
        return ImGui.Button(label, new(buttonWidth, 35f));
    }

    internal static void ShowPopup(string popupTitle, string popupText)
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
        ImGui.SetNextWindowSize(new(textSize.X + 25, textSize.Y + 100));
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

    public override void Draw()
    {
        DrawPopup();
        
        ImGuiEx.EzTabBar("MainTab", "Thanks", openTabName, ("Main", MainTab.Draw, null, false), ("Build", BuildTab.Draw, null, false), ("Paths", PathsTab.Draw, null, false), ("Config", ConfigTab.Draw, null, false), ("Mini", MiniTab.Draw, null, false));
        ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(PatreonBanner.Text).X * 1.16f);
        PatreonBanner.DrawButton();
        
    }
}
