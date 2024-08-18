using ECommons.ImGuiMethods;
using ImGuiNET;
using System.Diagnostics;

namespace AutoDuty.Windows
{
    internal static class InfoTab
    {
        static string infoUrl = "https://docs.google.com/spreadsheets/d/151RlpqRcCpiD_VbQn6Duf-u-S71EP7d0mx3j1PDNoNA";
        static string gitIssueUrl = "https://github.com/ffxivcode/AutoDuty/issues";
        static string punishDiscordUrl = "https://discord.com/channels/1001823907193552978/1236757595738476725";
        private static Configuration Configuration = AutoDuty.Plugin.Configuration;

        public static void Draw()
        {
            if (MainWindow.CurrentTabName != "Info")
                MainWindow.CurrentTabName = "Info";
            ImGui.NewLine();
            ImGuiEx.TextCentered("For assistance with general setup for both AutoDuty and it's dependencies,");
            ImGuiEx.TextCentered("Be sure to check out the setup guide below for more information:");
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Information and Setup").X) / 2);
            if (ImGui.Button("Information and Setup"))
                Process.Start("explorer.exe", infoUrl);
            ImGuiEx.TextCentered("The above guide also has information on the status of each path,");
            ImGuiEx.TextCentered("Such as Path maturity, Module maturity, and general consistency of each path.");
            ImGuiEx.TextCentered("You can also review additional notes or considerations,");
            ImGuiEx.TextCentered("that may need to be made on your part for successful looping.");
            ImGui.NewLine();
            ImGuiEx.TextCentered("For requests, issues, or contributions to AD,");
            ImGuiEx.TextCentered("please use the AutoDuty Github to open an issue:");
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("GitHub Issues").X) / 2);
            if (ImGui.Button("GitHub Issues"))
                Process.Start("explorer.exe", gitIssueUrl);
            ImGui.NewLine();
            ImGuiEx.TextCentered("For everything else, join the puni.sh discord!");
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Punish Discord").X) / 2);
            if (ImGui.Button("Punish Discord"))
                Process.Start("explorer.exe", punishDiscordUrl);
            ImGui.NewLine();



        }
    }
}
