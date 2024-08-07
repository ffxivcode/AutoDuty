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
    public Overlay() : base("AutoDuty Overlay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize)
    {
        if (AutoDuty.Plugin.Configuration.OverlayNoBG)
            Flags |= ImGuiWindowFlags.NoBackground;

        if (AutoDuty.Plugin.Configuration.LockOverlay)
            Flags |= ImGuiWindowFlags.NoMove;
    }

    private static string hideText = " ";
    private static string hideTextAction = " ";
    
    private static string loopsText = "";

    public override void Draw()
    {
        if (!ObjectHelper.IsValid)
            return;

        var _loopTimes = Plugin.Configuration.LoopTimes;
        if (!Plugin.Running && !Plugin.Started)
        {
            MainWindow.GotoAndActions();
            if (!AutoDuty.Plugin.InDungeon)
            {
                ImGui.SameLine(0, 5);
                if (ImGuiEx.IconButton($"\uf013##Config", "OpenAutoDuty"))
                    AutoDuty.Plugin.MainWindow.IsOpen = !AutoDuty.Plugin.MainWindow.IsOpen;
                ImGui.SameLine();
                if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.WindowClose, "CloseOverlay"))
                {
                    this.IsOpen = false;
                    Plugin.MainWindow.IsOpen = true;
                }
            }
    }

        if (Plugin.InDungeon || Plugin.Running)
        {
            using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !ContentPathsManager.DictionaryPaths.ContainsKey(Svc.ClientState.TerritoryType) || Plugin.Stage > 0))
            {
                if (ImGui.Button("Start"))
                {
                    Plugin.LoadPath();
                    Plugin.Run(Svc.ClientState.TerritoryType);
                }
                ImGui.SameLine(0, 5);
            }
            ImGui.PushItemWidth(50 * ImGuiHelpers.GlobalScale);

            if (ImGui.SliderInt("Times", ref _loopTimes, 0, 100))
            {
                Plugin.Configuration.LoopTimes = _loopTimes;
                Plugin.Configuration.Save();
            }
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 5);
            MainWindow.StopResumePause();
            ImGui.SameLine();
            if (ImGuiEx.IconButton($"\uf013##Config", "OpenAutoDuty"))
                AutoDuty.Plugin.MainWindow.IsOpen = !AutoDuty.Plugin.MainWindow.IsOpen;
            ImGui.SameLine();
            if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.WindowClose, "CloseOverlay"))
            {
                this.IsOpen = false;
                Plugin.MainWindow.IsOpen = true;
            }

            if (!Plugin.Configuration.HideDungeonText)
            {
                if (ImGui.Button(hideText))
                {
                    Plugin.Configuration.HideDungeonText = true;
                    Plugin.Configuration.Save();
                }

                if (ImGui.IsItemHovered())
                    hideText = "Hide";
                else
                    hideText = "";

                ImGui.SameLine(0, 5);

                if (Plugin.Running || Plugin.Started)
                    loopsText = $"{(Plugin.CurrentTerritoryContent?.DisplayName!.Length > 20 ? Plugin.CurrentTerritoryContent?.DisplayName![..17] + "..." : Plugin.CurrentTerritoryContent?.DisplayName)}{(Plugin.Running ? $": {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Loops" : "")}";
                else
                    loopsText = $"{(Plugin.CurrentTerritoryContent?.DisplayName!.Length > 40 ? Plugin.CurrentTerritoryContent?.DisplayName![..37] + "..." : Plugin.CurrentTerritoryContent?.DisplayName)}{(Plugin.Running ? $": {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Loops" : "")}";

                ImGui.TextColored(new Vector4(93 / 255f, 226 / 255f, 231 / 255f, 1), loopsText);
            }
        }
        if (Plugin.InDungeon || Plugin.Running || RepairHelper.RepairRunning || GotoHelper.GotoRunning || GotoInnHelper.GotoInnRunning || GotoBarracksHelper.GotoBarracksRunning || GCTurninHelper.GCTurninRunning || ExtractHelper.ExtractRunning || DesynthHelper.DesynthRunning || QueueHelper.QueueRunning)
        {
            if (!Plugin.Configuration.HideActionText)
            {
                if (ImGui.Button(hideTextAction))
                {
                    Plugin.Configuration.HideActionText = true;
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
