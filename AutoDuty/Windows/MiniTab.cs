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
            var _loopTimes = Plugin.Configuration.LoopTimes;
            MainWindow.GotoAndActions();
            ImGui.TextColored(new Vector4(93/255f, 226/255f, 231/255f, 1), $"AutoDuty - Running ({Plugin.CurrentTerritoryContent?.DisplayName}){(Plugin.Running ? $" {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Times" : "")}");
            
            using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !FileHelper.DictionaryPathFiles.ContainsKey(Svc.ClientState.TerritoryType) || Plugin.Stage > 0))
            {
                if (ImGui.Button("Start"))
                {
                    Plugin.LoadPath();
                    Plugin.Run(Svc.ClientState.TerritoryType);
                }
                ImGui.SameLine(0, 15);
            }
            ImGui.PushItemWidth(200);
            
            if (ImGui.InputInt("Times", ref _loopTimes))
            {
                Plugin.Configuration.LoopTimes = _loopTimes;
                Plugin.Configuration.Save();
            }
            ImGui.PopItemWidth(); 
            
            MainWindow.StopResumePause();
            ImGui.SameLine(0, 5);
            ImGui.TextColored(new Vector4(0, 255f, 0, 1), Plugin.Action);
        }
    }
}
