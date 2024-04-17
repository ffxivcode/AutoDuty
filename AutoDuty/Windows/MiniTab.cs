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

        internal static void Draw()
        {
            ImGui.TextColored(new Vector4(93/255f, 226/255f, 231/255f, 1), $"AutoDuty - Running ({Plugin.CurrentTerritoryContent?.Name}){(Plugin.Running ? $" {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Times" : "")}");
            using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !FileHelper.DictionaryPathFiles.TryGetValue(Svc.ClientState.TerritoryType, out _) || Plugin.Stage > 0))
            {
                if (ImGui.Button("Start"))
                {
                    Plugin.LoadPath();
                    Plugin.StartNavigation(!Plugin.MainListClicked);
                }
            }
            ImGui.SameLine(0, 5);
            using var d = ImRaii.Disabled((!Plugin.Running && !Plugin.Started) || Plugin.CurrentTerritoryContent == null);
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
            ImGui.SameLine(0, 5);

            ImGui.TextColored(new Vector4(0, 255f, 0, 1), Plugin.Action);
        }
    }
}
