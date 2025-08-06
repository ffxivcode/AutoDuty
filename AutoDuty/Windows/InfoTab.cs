using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;
using System.Diagnostics;

namespace AutoDuty.Windows
{
    internal static class InfoTab
    {
        static string infoUrl = "https://docs.google.com/spreadsheets/d/151RlpqRcCpiD_VbQn6Duf-u-S71EP7d0mx3j1PDNoNA";
        static string gitIssueUrl = "https://github.com/ffxivcode/AutoDuty/issues";
        static string punishDiscordUrl = "https://discord.com/channels/1001823907193552978/1236757595738476725";
        static string ffxivcodeDiscordUrl = "https://discord.com/channels/1241050921732014090/1273374407653462017";
        private static Configuration Configuration = Plugin.Configuration;

        public static void Draw()
        {
            if (MainWindow.CurrentTabName != "Info")
                MainWindow.CurrentTabName = "Info";
            ImGui.NewLine();
            ImGuiEx.TextWrapped("For assistance with general setup for both AutoDuty and it's dependencies, be sure to check out the setup guide below for more information:");
            ImGui.NewLine();
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Information and Setup").X) / 2);
            if (ImGui.Button("Information and Setup"))
                Process.Start("explorer.exe", infoUrl);
            ImGui.NewLine();
            ImGuiEx.TextWrapped("The above guide also has information on the status of each path, such as Path maturity, module maturity, and general consistency of each path. You can also review additional notes or considerations, that may need to be made on your part for successful looping. For requests, issues, or contributions to AD, please use the AutoDuty Github to open an issue:");
            ImGui.NewLine();
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("GitHub Issues").X) / 2);
            if (ImGui.Button("GitHub Issues"))
                Process.Start("explorer.exe", gitIssueUrl);
            ImGui.NewLine();
            ImGuiEx.TextCentered("For everything else, join the discord!");
            ImGui.NewLine();
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Punish Discord").X) / 2);
            if (ImGui.Button("Punish Discord"))
                Process.Start("explorer.exe", punishDiscordUrl);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("FFXIVCode Discord").X) / 2);
            if (ImGui.Button("FFXIVCode Discord"))
                Process.Start("explorer.exe", ffxivcodeDiscordUrl);
        }
    }
}
