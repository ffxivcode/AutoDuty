using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using static AutoDuty.AutoDuty;
using System.Numerics;
using AutoDuty.Helpers;
using ECommons.DalamudServices;

namespace AutoDuty.Windows
{
    internal static class MiniTab
    {
        private static string hideText = " ";
        private static string hideTextAction = " ";
        private static bool hideRunningText = false;
        private static bool hideActionText = false;

        internal static void Draw()
        {
            if (MainWindow.CurrentTabName != "Mini")
            {
                MainWindow.CurrentTabName = "Mini";
                hideRunningText = false;
                hideActionText = false;
                hideText = " ";
                hideTextAction = " ";
            }

            var _loopTimes = Plugin.Configuration.LoopTimes;
            MainWindow.GotoAndActions();

            using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !FileHelper.DictionaryPathFiles.ContainsKey(Svc.ClientState.TerritoryType) || Plugin.Stage > 0))
            {
                if (ImGui.Button("Start"))
                {
                    Plugin.LoadPath();
                    Plugin.Run(Svc.ClientState.TerritoryType);
                }
                ImGui.SameLine(0, 5);
            }
            ImGui.PushItemWidth(150);
            
            if (ImGui.SliderInt("Times", ref _loopTimes, 0, 100))
            {
                Plugin.Configuration.LoopTimes = _loopTimes;
                Plugin.Configuration.Save();
            }
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 5);
            MainWindow.StopResumePause();

            if (!hideRunningText)
            {
                if (ImGui.Button(hideText))
                hideRunningText = true;

                if (ImGui.IsItemHovered())
                    hideText = "Hide";
                else
                    hideText = "";

                ImGui.SameLine(0, 5);
                ImGui.TextColored(new Vector4(93 / 255f, 226 / 255f, 231 / 255f, 1), $"AutoDuty - Running ({Plugin.CurrentTerritoryContent?.DisplayName}){(Plugin.Running ? $" {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Times" : "")}");
            }

            if (!hideActionText)
            {
                if (ImGui.Button(hideTextAction))
                    hideActionText = true;

                if (ImGui.IsItemHovered())
                    hideTextAction = "Hide";
                else
                    hideTextAction = "";

                ImGui.SameLine(0, 5);
                ImGui.TextColored(new Vector4(0, 255f, 0, 1), Plugin.Action);
            }
        }
    }
}
