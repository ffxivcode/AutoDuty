using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.ImGuiMethods;
using ECommons.Throttlers;
using ImGuiNET;
using Serilog.Events;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static AutoDuty.Helpers.GitHubHelper;

namespace AutoDuty.Windows
{
    internal static class LogTab
    {
        internal static void Add(LogMessage message) => _logEntriesToAdd.Enqueue(message);

        private static Task<UserCode?>? _taskUserCode = null;
        private static Task<PollResponseClass?>? _taskPollResponse = null;
        private static Task<string?>? _taskSubmitIssue = null;
        private static readonly Queue<LogMessage> _logEntriesToAdd = [];
        private static string _titleInput = $"[Bug] ";
        private static string _whatHappenedInput = string.Empty;
        private static string _reproStepsInput = string.Empty;
        private static bool _popupOpen = false;
        private static UserCode? _userCode = null;
        private static PollResponseClass? _pollResponse = null;
        private static ImGuiWindowFlags _imGuiWindowFlags = ImGuiWindowFlags.None;
        private static bool _copied = false;
        private static bool _clearedDataAfterPopupClose = true;
        public static async void Draw()
        {
            if (MainWindow.CurrentTabName != "Log")
                MainWindow.CurrentTabName = "Log";
            if (!_popupOpen && !_clearedDataAfterPopupClose)
            {
                _clearedDataAfterPopupClose = false;
                _copied = false;
                _taskPollResponse = null;
                _taskUserCode = null;
                _userCode = null;
                _pollResponse = null;
                _reproStepsInput = string.Empty;
                _taskSubmitIssue = null;
                _titleInput = string.Empty;
                _whatHappenedInput = string.Empty;
            }
            ImGuiEx.Spacing();
            if (ImGui.Checkbox("Auto Scroll", ref Plugin.Configuration.AutoScroll))
                Plugin.Configuration.Save();
            ImGui.SameLine();
            if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.Trash))
                Plugin.DalamudLogEntries.Clear();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Clear log");
            ImGui.SameLine();
            if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.Copy))
                ImGui.SetClipboardText(Plugin.DalamudLogEntries.SelectMulti(x => x.Message).ToList().ToCustomString("\n"));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Copy entire log to clipboard");
            ImGui.SameLine();
            using (ImRaii.Disabled(!_taskUserCode?.IsCompletedSuccessfully ?? false))
            {
                if (ImGui.Button("Create Issue"))
                {
                    if (_pollResponse == null || _pollResponse.Access_Token.IsNullOrEmpty())
                    {
                        if (_userCode == null)
                            _taskUserCode = Task.Run(GetUserCode);
                        _imGuiWindowFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove;
                    }
                    else
                    {
                        ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);
                        _imGuiWindowFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
                    }
                    _popupOpen = true;
                    ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.None, new(0.5f, 0.5f));
                    ImGui.OpenPopup($"Create Issue");
                }
            }
            if (_pollResponse != null && !_pollResponse.Access_Token.IsNullOrEmpty())
            {
                ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);
                ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.None, new(0.5f, 0.5f));
                _imGuiWindowFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
            }    
            if (ImGui.BeginPopupModal($"Create Issue", ref _popupOpen, _imGuiWindowFlags))
            {
                _clearedDataAfterPopupClose = false;
                if (_pollResponse == null || _pollResponse.Access_Token.IsNullOrEmpty())
                    DrawUserCodePopup();
                else
                    DrawIssuePopup();
                ImGui.EndPopup();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Click to open the Create Issue popup (after authenticating with github) to fill in the form and submit and issue to the Repo");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGuiEx.EnumCombo("##LogEventLevel", ref Plugin.Configuration.LogEventLevel))
                Plugin.Configuration.Save();
            
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Filter log event level");
            ImGuiEx.Spacing();

            ImGui.BeginChild("scrolling", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true, ImGuiWindowFlags.HorizontalScrollbar);

            Plugin.DalamudLogEntries.Each(e => { if (e.LogEventLevel >= Plugin.Configuration.LogEventLevel) ImGui.TextColored(GetLogEntryColor(e.LogEventLevel), e.Message); });

            if (EzThrottler.Throttle("AddLogEntries", 25))
            {
                while (_logEntriesToAdd.Count != 0)
                {
                    var logEntry = _logEntriesToAdd.Dequeue();
                    if (logEntry == null)
                        return;
                    Plugin.DalamudLogEntries.Add(logEntry);
                }
            }
            if (Plugin.Configuration.AutoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                ImGui.SetScrollHereY(1.0f);

            ImGui.EndChild();
        }

        private static Vector4 GetLogEntryColor(LogEventLevel logEntryType) => logEntryType switch
        {
            LogEventLevel.Error => ImGuiColors.DalamudRed,
            LogEventLevel.Warning => ImGuiColors.DalamudOrange,
            _ => ImGuiColors.DalamudWhite,
        };

        private static void DrawUserCodePopup()
        { 
            if (_taskPollResponse != null && _userCode != null)
            {
                if (_taskPollResponse.IsCompletedSuccessfully)
                {
                    _pollResponse = _taskPollResponse.Result;
                    if ((_pollResponse == null || _pollResponse.Access_Token.IsNullOrEmpty()) && EzThrottler.Throttle("Polling", _pollResponse != null && _pollResponse.Interval != -1 ? _pollResponse.Interval * 1100 : _userCode!.Interval * 1100))
                        _taskPollResponse = Task.Run(() => PollResponse(_userCode));
                }
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Polling Github for User Authorization: {(_pollResponse != null ? (_pollResponse.Access_Token.IsNullOrEmpty() ? $"{_pollResponse.Error}" : $"{_pollResponse.Access_Token}") : "")}");
            }
            else if (_taskUserCode != null && !_taskUserCode.IsCompletedSuccessfully)
            {
                ImGui.TextColored(new(0, 1, 0, 1), "Waiting for Response from GitHub");
                return;
            }
            else if (_taskUserCode != null && _taskUserCode.IsCompletedSuccessfully)
            {
                _userCode = _taskUserCode.Result;
                _taskUserCode = null;
            }
            else if (_userCode != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.ParsedBlue);
                if (ImGuiEx.Button("Click Here"))
                {
                    ImGui.SetClipboardText(_userCode.User_Code);
                    _copied = true;
                }
                ImGui.SameLine();
                ImGui.Text($" to Copy ");
                ImGui.SameLine();
                ImGui.TextColored(new(0, 1, 0, 1), _userCode.User_Code);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    ImGui.SetClipboardText(_userCode.User_Code);
                    _copied = true;
                }
                ImGui.SameLine();
                ImGui.Text(" to the ClipBoard and:");
                using (ImRaii.Disabled(!_copied))
                {
                    if (ImGui.Button("Open GitHub###OpenUri"))
                    {
                        GenericHelpers.ShellStart($"https://github.com/login/device");
                        if (EzThrottler.Throttle("Polling", _userCode!.Interval * 1100))
                            _taskPollResponse = Task.Run(() => PollResponse(_userCode));
                    }
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    ImGui.Text($" in your browser and Paste it");
                }
            }
        }

        private static void DrawIssuePopup()
        {
            if (_taskSubmitIssue != null && !_taskSubmitIssue.IsCompletedSuccessfully)
            {
                ImGui.TextColored(ImGuiColors.HealerGreen, "Submitting Issue");
                return;
            }
            else if (_taskSubmitIssue != null && _taskSubmitIssue.IsCompletedSuccessfully)
            {
                _popupOpen = false;
                ImGui.CloseCurrentPopup();
                return;
            }
            ImGui.Text("Issue: Bug Report");
            ImGui.Separator();
            ImGui.Text("Add a title");
            ImGui.SameLine(0, 5);
            ImGui.TextColored(ImGuiColors.DalamudRed, "*");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##TitleInput", ref _titleInput, 500);
            ImGui.Separator();
            ImGui.NewLine();
            ImGui.TextWrapped("Please make sure someone else hasn't reported the same bug by going to the issues page and searching for a similar issue. If you find a similar issue, please react to the initial post with 👍 to increase its priority.");
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                GenericHelpers.ShellStart("https://github.com/ffxivcode/AutoDuty/issues");
            ImGui.NewLine();
            ImGui.TextWrapped("What Happened?");
            ImGui.SameLine(0, 5);
            ImGui.TextColored(ImGuiColors.DalamudRed, "*"); 
            ImGui.TextWrapped("Also, what did you expect to happen? Please put any screenshots you can share here as well.");
            ImGui.InputTextMultiline("##WhatHappenedInput", ref _whatHappenedInput, 500, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y / 2.5f));
            ImGui.NewLine();
            ImGui.TextWrapped("Steps to reproduce the error");
            ImGui.SameLine(0, 5);
            ImGui.TextColored(ImGuiColors.DalamudRed, "*");
            ImGui.TextWrapped("List all of the steps we can take to reproduce this error.");
            ImGui.InputTextMultiline("##ReproStepsInput", ref _reproStepsInput, 500, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - (ImGui.CalcTextSize("Submit Issue").Y * 3)));
            ImGui.NewLine();
            using (ImRaii.Disabled(_titleInput.Equals("[Bug] ") || _whatHappenedInput.IsNullOrEmpty() || _reproStepsInput.IsNullOrEmpty()))
            {
                if (ImGui.Button("Submit Issue"))
                {
                    if (_pollResponse != null)
                    {
                        _taskSubmitIssue = Task.Run(static async () => await FileIssue(_titleInput, _whatHappenedInput, _reproStepsInput, _pollResponse.Access_Token));
                    }
                }
            }
        }
    }
}
