using AutoDuty.Helpers;
using Dalamud.Bindings.ImGui;
using ECommons.ImGuiMethods;
using System.Diagnostics;

namespace AutoDuty.Windows
{
    using Dalamud.Interface.Utility.Raii;
    using global::AutoDuty.IPC;
    using static Dalamud.Interface.Utility.Raii.ImRaii;

    internal static class InfoTab
    {
        static string infoUrl = "https://docs.google.com/spreadsheets/d/151RlpqRcCpiD_VbQn6Duf-u-S71EP7d0mx3j1PDNoNA";
        static string gitIssueUrl = "https://github.com/ffxivcode/AutoDuty/issues";
        static string punishDiscordUrl = "https://discord.com/channels/1001823907193552978/1236757595738476725";
        
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

            ImGui.NewLine();

            int id = 0;

            void PluginInstallLine(ExternalPlugin plugin, string message)
            {
                bool isReady = plugin == ExternalPlugin.BossMod ? 
                                   BossMod_IPCSubscriber.IsEnabled : 
                                   IPCSubscriber_Common.IsReady(plugin.GetExternalPluginData().name);
                
                if(!isReady)
                    if (ImGui.Button($"Install##InstallExternalPlugin_{plugin}_{id++}"))
                        PluginInstaller.InstallPlugin(plugin);

                ImGui.NextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(isReady ? EzColor.Green : EzColor.Red, plugin.GetExternalPluginName());

                ImGui.NextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(message);
                ImGui.NextColumn();
            }

            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Required Plugins").X) / 2);
            ImGui.Text("Required Plugins");

            ImGui.Columns(3, "PluginInstallerRequired", false);
            ImGui.SetColumnWidth(0, 60);
            ImGui.SetColumnWidth(1, 100);

            PluginInstallLine(ExternalPlugin.BossMod, "handles boss fights for you");
            PluginInstallLine(ExternalPlugin.vnav, "can move you around");

            ImGui.Columns(1);
            ImGui.NewLine();
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Combat Plugins").X) / 2);
            ImGui.Text("Combat Plugins");

            ImGui.Indent(65f);
            ImGui.TextColored(EzColor.Cyan, "Hotly debated, pick your favorite. You can configure it in the config");
            ImGui.Unindent(65f);

            ImGui.Columns(3, "PluginInstallerCombat", false);
            ImGui.SetColumnWidth(0, 60);
            ImGui.SetColumnWidth(1, 100);

            PluginInstallLine(ExternalPlugin.BossMod,              "has integrated rotations");
            PluginInstallLine(ExternalPlugin.WrathCombo,           "Puni.sh's dedicated rotation plugin");
            PluginInstallLine(ExternalPlugin.RotationSolverReborn, "Reborn's rotation plugin");

            ImGui.Columns(1);
            ImGui.NewLine();
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Recommended Plugins").X) / 2);
            ImGui.Text("Recommended Plugins");
            ImGui.NewLine();
            ImGui.Columns(3, "PluginInstallerRecommended", false);
            ImGui.SetColumnWidth(0, 60);
            ImGui.SetColumnWidth(1, 100);

            PluginInstallLine(ExternalPlugin.AntiAFK,      "keeps you from being marked as afk");
            PluginInstallLine(ExternalPlugin.AutoRetainer, "can be triggered, does GC delivery and discarding");
            PluginInstallLine(ExternalPlugin.Avarice,      "is read for positionals");
            PluginInstallLine(ExternalPlugin.Lifestream,   "incredibly extensive teleporting");
            PluginInstallLine(ExternalPlugin.Pandora,      "chest looting + tankstance");
            PluginInstallLine(ExternalPlugin.Gearsetter,   "recommend items to equip");
            PluginInstallLine(ExternalPlugin.Stylist,      "recommend items to equip");


            ImGui.Columns(1);
        }
    }
}
