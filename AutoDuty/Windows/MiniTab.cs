using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using static AutoDuty.AutoDuty;
using System.Numerics;
using AutoDuty.Helpers;
using ECommons.DalamudServices;
using AutoDuty.IPC;

namespace AutoDuty.Windows
{
    internal static class MiniTab
    {

        internal static void Draw()
        {
            var _loopTimes = Plugin.Configuration.LoopTimes;
            ImGui.TextColored(new Vector4(93/255f, 226/255f, 231/255f, 1), $"AutoDuty - Running ({Plugin.CurrentTerritoryContent?.DisplayName}){(Plugin.Running ? $" {Plugin.CurrentLoop} of {Plugin.Configuration.LoopTimes} Times" : "")}");
            ImGui.TextColored(new Vector4(0, 255f, 0, 1), Plugin.Action);
            using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !FileHelper.DictionaryPathFiles.TryGetValue(Svc.ClientState.TerritoryType, out _) || Plugin.Stage > 0))
            {
                if (ImGui.Button("Start"))
                {
                    Plugin.LoadPath();
                    Plugin.Run(Svc.ClientState.TerritoryType);
                }
                ImGui.SameLine(0, 15);
                
            }
            ImGui.PushItemWidth(150);
            if (ImGui.InputInt("Times", ref _loopTimes))
            {
                Plugin.Configuration.LoopTimes = _loopTimes;
                Plugin.Configuration.Save();
            }
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 5);
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
            using (var d2 = ImRaii.Disabled(Plugin.Running || Plugin.Started))
            {
                ImGui.SameLine(0, 5);
                if (ImGui.Button("Goto"))
                {
                    ImGui.OpenPopup("GotoPopup");
                }
                ImGui.SameLine(0, 5);
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
                            MainWindow.ShowPopup("Missing Plugin", "GC Turnin Requires Deliveroo plugin. Get @ https://git.carvel.li/liza/plugin-repo");
                    }
                    if (Deliveroo_IPCSubscriber.IsEnabled)
                        MainWindow.ToolTip("Click to Goto GC Turnin and Invoke Deliveroo");
                    else
                        MainWindow.ToolTip("GC Turnin Requires Deliveroo plugin. Get @ https://git.carvel.li/liza/plugin-repo");
                }
                ImGui.SameLine(0, 5);
                if (DesynthHelper.DesynthRunning)
                {
                    if (ImGui.Button("Stop Desynth"))
                        Plugin.StopAndResetALL();
                }
                else
                {
                    if (ImGui.Button("Desynth"))
                        DesynthHelper.Invoke();
                    MainWindow.ToolTip("Click to Desynth all Items in Inventory");
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
    }
}
