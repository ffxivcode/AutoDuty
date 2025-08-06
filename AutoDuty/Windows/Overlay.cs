using AutoDuty.Helpers;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using static AutoDuty.AutoDuty;
using ECommons.ImGuiMethods;

namespace AutoDuty.Windows;

public unsafe class Overlay : Window
{
    public Overlay() : base("AutoDuty Overlay", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.RespectCloseHotkey = false;
    }

    private static string hideText = " ";
    private static string hideTextAction = " ";
    private static string loopsText = "";

    public override void Draw()
    {
        if (!PlayerHelper.IsValid)
        {
            if (!SchedulerHelper.Schedules.ContainsKey("OpenOverlay"))
                SchedulerHelper.ScheduleAction("OpenOverlay", () => this.IsOpen = true, () => PlayerHelper.IsReady);
            this.IsOpen = false;
            return;
        }

        if(!Plugin.Configuration.ShowOverlay)
        {
            this.IsOpen = false;
            return;
        }


        if (!Plugin.States.HasAnyFlag(PluginState.Looping, PluginState.Navigating))
        {
            if (Plugin.Configuration.HideOverlayWhenStopped)
            {
                this.IsOpen = false;
                return;
            }

            MainWindow.GotoAndActions();
            if (!Plugin.InDungeon)
            {
                ImGui.SameLine(0, 5);
                if (ImGuiEx.IconButton($"\uf013##Config", "OpenAutoDuty"))
                    Plugin.MainWindow.IsOpen = !Plugin.MainWindow.IsOpen;
                ImGui.SameLine(0, 5);
                if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.WindowClose, "CloseOverlay"))
                {
                    this.IsOpen = false;
                    Plugin.Configuration.ShowOverlay = false;
                    Plugin.MainWindow.IsOpen = true;
                }
            }
        }

        if (Plugin.InDungeon || Plugin.States.HasFlag(PluginState.Looping))
        {
            using (ImRaii.Disabled(!Plugin.InDungeon || !ContentPathsManager.DictionaryPaths.ContainsKey(Svc.ClientState.TerritoryType)))
            {
                if (Plugin.Stage == 0)
                {
                    if (!Plugin.States.HasFlag(PluginState.Navigating) && !Plugin.States.HasFlag(PluginState.Looping))
                        if (ImGui.Button("Start"))
                        {
                            Plugin.LoadPath();
                            Plugin.Run(Svc.ClientState.TerritoryType);
                        }
                }
                else
                {
                    MainWindow.StopResumePause();
                }

                ImGui.SameLine(0, 5);
            }
            ImGui.PushItemWidth(75 * ImGuiHelpers.GlobalScale);
            MainWindow.LoopsConfig();
            ImGui.PopItemWidth();
            ImGui.SameLine();
            if (ImGuiEx.IconButton($"\uf013##Config", "OpenAutoDuty"))
                Plugin.MainWindow.IsOpen = !Plugin.MainWindow.IsOpen;
            

            ImGui.SameLine();
            if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.WindowClose, "CloseOverlay"))
            {
                this.IsOpen = false;
                Plugin.Configuration.ShowOverlay = false;
                Plugin.MainWindow.IsOpen = true;
            }

            if (Plugin.Configuration.ShowDutyLoopText)
            {
                if (ImGui.Button($"{hideText}##OverlayHideButton"))
                {
                    Plugin.Configuration.ShowDutyLoopText = false;
                    Plugin.Configuration.Save();
                }

                hideText = ImGui.IsItemHovered() ? "Hide" : "";

                ImGui.SameLine(0, 5);

                if (Plugin.States.HasFlag(PluginState.Navigating) || Plugin.States.HasFlag(PluginState.Navigating))
                    loopsText = $"{(Plugin.CurrentTerritoryContent?.Name!.Length > 20 ? Plugin.CurrentTerritoryContent?.Name![..17] + "..." : Plugin.CurrentTerritoryContent?.Name)}{(Plugin.States.HasFlag(PluginState.Navigating) ? $": {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Loops" : "")}";
                else
                    loopsText = $"{(Plugin.CurrentTerritoryContent?.Name!.Length > 40 ? Plugin.CurrentTerritoryContent?.Name![..37] + "..." : Plugin.CurrentTerritoryContent?.Name)}{(Plugin.States.HasFlag(PluginState.Navigating) ? $": {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Loops" : "")}";

                ImGui.TextColored(new Vector4(93 / 255f, 226 / 255f, 231 / 255f, 1), loopsText);
            }
        }
        if (Plugin.InDungeon || Plugin.States.HasFlag(PluginState.Navigating) || RepairHelper.State == ActionState.Running || GotoHelper.State == ActionState.Running || GotoInnHelper.State == ActionState.Running || GotoBarracksHelper.State == ActionState.Running || GCTurninHelper.State == ActionState.Running || ExtractHelper.State == ActionState.Running || DesynthHelper.State == ActionState.Running || QueueHelper.State == ActionState.Running)
            if (Plugin.Configuration.ShowActionText)
            {
                if (ImGui.Button(hideTextAction + "##OverlayHideActionButton"))
                {
                    Plugin.Configuration.ShowActionText = false;
                    Plugin.Configuration.Save();
                }

                hideTextAction = ImGui.IsItemHovered() ? "Hide" : "";

                ImGui.SameLine(0, 5);
                ImGui.TextColored(new Vector4(0, 255f, 0, 1), Plugin.Action.Length > 40 ? Plugin.Action[..37] + "..." : Plugin.Action);
            }
    }
}
