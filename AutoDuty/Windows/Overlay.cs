using AutoDuty.Helpers;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ImGuiNET;
using System.Numerics;
using static AutoDuty.AutoDuty;
using ECommons.ImGuiMethods;
using AutoDuty.Managers;

namespace AutoDuty.Windows;

public unsafe class Overlay : Window
{
    public Overlay() : base("AutoDuty Overlay", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize)
    {

    }

    private static string hideText = " ";
    private static string hideTextAction = " ";
    private static string loopsText = "";

    public override void Draw()
    {
        if (!ObjectHelper.IsValid)
        {
            if (!SchedulerHelper.Schedules.ContainsKey("OpenOverlay"))
                SchedulerHelper.ScheduleAction("OpenOverlay", () => IsOpen = true, () => ObjectHelper.IsReady);
            IsOpen = false;
        }

        if (!Plugin.States.HasFlag(State.Looping) && !Plugin.States.HasFlag(State.Navigating))
        {
            MainWindow.GotoAndActions();
            if (!Plugin.InDungeon || !Plugin.States.HasFlag(State.Navigating))
            {
                ImGui.SameLine(0, 5);
                if (!Plugin.InDungeon)
                    if (ImGuiEx.IconButton($"\uf013##Config", "OpenAutoDuty"))
                        Plugin.MainWindow.IsOpen = !Plugin.MainWindow.IsOpen;
                ImGui.SameLine();
                if (!Plugin.States.HasFlag(State.Navigating))
                {
                    if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.WindowClose, "CloseOverlay"))
                    {
                        IsOpen = false;
                        Plugin.Configuration.ShowOverlay = false;
                        Plugin.MainWindow.IsOpen = true;
                    }
                }
            }
        }

        if (Plugin.InDungeon || Plugin.States.HasFlag(State.Navigating))
        {
            using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !ContentPathsManager.DictionaryPaths.ContainsKey(Svc.ClientState.TerritoryType) || Plugin.Stage > 0))
            {
                if (!Plugin.States.HasFlag(State.Navigating) && !Plugin.States.HasFlag(State.Looping))
                {
                    if (ImGui.Button("Start"))
                    {
                        Plugin.LoadPath();
                        Plugin.Run(Svc.ClientState.TerritoryType);
                    }
                    ImGui.SameLine(0, 5);
                }
            }
            ImGui.PushItemWidth(75 * ImGuiHelpers.GlobalScale);
            if (Plugin.Configuration.UseSliderInputs)
            {
                if (ImGui.SliderInt("Times", ref Plugin.Configuration.LoopTimes, 1, 100))
                {
                    if (Plugin.Configuration.LoopTimes < 1) Plugin.Configuration.LoopTimes = 1;
                Plugin.Configuration.Save();
                }
            }
            else
            {
                if (ImGui.InputInt("Times", ref Plugin.Configuration.LoopTimes))
                {
                    if (Plugin.Configuration.LoopTimes < 1) Plugin.Configuration.LoopTimes = 1;
                Plugin.Configuration.Save();
                }
            }
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 5);
            MainWindow.StopResumePause();
            ImGui.SameLine();
            if (ImGuiEx.IconButton($"\uf013##Config", "OpenAutoDuty"))
                Plugin.MainWindow.IsOpen = !Plugin.MainWindow.IsOpen;
            
            if (Plugin.States.HasFlag(State.Navigating) || Plugin.States.HasFlag(State.Navigating))
            {
                ImGui.SameLine();
                if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.WindowClose, "CloseOverlay"))
                {
                    IsOpen = false;
                    Plugin.Configuration.ShowOverlay = false;
                    Plugin.MainWindow.IsOpen = true;
                }
            }

            if (Plugin.Configuration.ShowDutyLoopText)
            {
                if (ImGui.Button(hideText))
                {
                    Plugin.Configuration.ShowDutyLoopText = false;
                    Plugin.Configuration.Save();
                }

                if (ImGui.IsItemHovered())
                    hideText = "Hide";
                else
                    hideText = "";

                ImGui.SameLine(0, 5);

                if (Plugin.States.HasFlag(State.Navigating) || Plugin.States.HasFlag(State.Navigating))
                    loopsText = $"{(Plugin.CurrentTerritoryContent?.DisplayName!.Length > 20 ? Plugin.CurrentTerritoryContent?.DisplayName![..17] + "..." : Plugin.CurrentTerritoryContent?.DisplayName)}{(Plugin.States.HasFlag(State.Navigating) ? $": {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Loops" : "")}";
                else
                    loopsText = $"{(Plugin.CurrentTerritoryContent?.DisplayName!.Length > 40 ? Plugin.CurrentTerritoryContent?.DisplayName![..37] + "..." : Plugin.CurrentTerritoryContent?.DisplayName)}{(Plugin.States.HasFlag(State.Navigating) ? $": {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Loops" : "")}";

                ImGui.TextColored(new Vector4(93 / 255f, 226 / 255f, 231 / 255f, 1), loopsText);
            }
        }
        if (Plugin.InDungeon || Plugin.States.HasFlag(State.Navigating) || RepairHelper.RepairRunning || GotoHelper.GotoRunning || GotoInnHelper.GotoInnRunning || GotoBarracksHelper.GotoBarracksRunning || GCTurninHelper.GCTurninRunning || ExtractHelper.ExtractRunning || DesynthHelper.DesynthRunning || QueueHelper.QueueRunning)
        {
            if (Plugin.Configuration.ShowActionText)
            {
                if (ImGui.Button(hideTextAction))
                {
                    Plugin.Configuration.ShowActionText = false;
                    Plugin.Configuration.Save();
                }

            if (ImGui.IsItemHovered())
                    hideTextAction = "Hide";
                else
                    hideTextAction = "";

                ImGui.SameLine(0, 5);
                ImGui.TextColored(new Vector4(0, 255f, 0, 1), Plugin.Action.Length > 40 ? Plugin.Action[..37] + "..." : Plugin.Action);
            }
        }
    }
}
