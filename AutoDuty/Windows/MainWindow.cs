using System;
using System.Numerics;
using AutoDuty.Helpers;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ImGuiNET;

namespace AutoDuty.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly AutoDuty Plugin;
    private static bool _showPopup = false;
    private static string _popupText = "";
    private static string _popupTitle = "";
    private string openTabName = "";

    public MainWindow(AutoDuty plugin) : base(
        "AutoDuty", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(425, 375),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        Plugin = plugin;
        KoFiButton.DonateLink = "https://ko-fi.com/Herculezz";
    }

    internal void OpenConfig()
    {
        openTabName = "Config";
    }

    public void Dispose()
    {
    }

    internal void SetWindowSize(int x, int y)
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
            ImGui.TextColored(new Vector4(0, 0f, 200f, 1), $"AutoDuty - Running ({ContentHelper.ListContent[Plugin.CurrentTerritoryIndex].Name}) {Plugin.CurrentLoop} of {Plugin.LoopTimes} Times");
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
            
            ImGui.TextColored(new Vector4(0, 255f, 0, 1), Plugin.Action);

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

        KoFiButton.DrawRight();
        ImGuiEx.EzTabBar("MainTab", true, openTabName, ("Main", MainTab.Draw, null, false), ("Build", BuildTab.Draw, null, false), ("Config", ConfigTab.Draw, null, false));
        openTabName = "";
    }
}