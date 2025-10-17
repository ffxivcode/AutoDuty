using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoDuty.Windows
{
    using Dalamud.Interface;
    using Dalamud.Interface.Utility;
    using Data;
    using SharpDX;
    using static Data.Classes;
    using Vector2 = System.Numerics.Vector2;
    using Vector4 = System.Numerics.Vector4;

    internal static class MainTab
    {
        internal static ContentPathsManager.ContentPathContainer? DutySelected;
        internal static readonly (string Normal, string GameFont) Digits = ("0123456789", "");

        private static int _currentStepIndex = -1;
        private static readonly string _pathsURL = "https://github.com/ffxivcode/AutoDuty/tree/master/AutoDuty/Paths";

        // New search text field for filtering duties
        private static string _searchText = string.Empty;

        internal static void Draw()
        {
            MainWindow.CurrentTabName = "Main";
            
            var dutyMode = Plugin.Configuration.DutyModeEnum;
            var levelingMode = Plugin.LevelingModeEnum;

            static void DrawSearchBar()
            {
                // Set the maximum search to 10 characters
                int inputMaxLength = 10;
                
                // Calculate the X width of the maximum amount of search characters
                Vector2 _characterWidth = ImGui.CalcTextSize("W");
                float inputMaxWidth = ImGui.CalcTextSize("W").X * inputMaxLength;
                
                // Set the width of the search box to the calculated width
                ImGui.SetNextItemWidth(inputMaxWidth);
                
                ImGui.InputTextWithHint("##search", "Search duties...", ref _searchText, inputMaxLength);

                // Apply filtering based on the search text
                if (_searchText.Length > 0)
                {
                    // Trim and convert to lowercase for case-insensitive search
                    _searchText = _searchText.Trim().ToLower();
                }
            }

            static void DrawPathSelection()
            {
                if (Plugin.CurrentTerritoryContent == null || !PlayerHelper.IsReady)
                    return;

                using var d = ImRaii.Disabled(Plugin is { InDungeon: true, Stage: > 0 });

                if (ContentPathsManager.DictionaryPaths.TryGetValue(Plugin.CurrentTerritoryContent.TerritoryType, out var container))
                {
                    List<ContentPathsManager.DutyPath> curPaths = container.Paths;
                    if (curPaths.Count > 1)
                    {
                        int                              curPath       = Math.Clamp(Plugin.CurrentPath, 0, curPaths.Count - 1);

                        Dictionary<string, JobWithRole>? pathSelection    = null;
                        JobWithRole                      curJob = Svc.ClientState.LocalPlayer.GetJob().JobToJobWithRole();
                        using (ImRaii.Disabled(curPath <= 0 ||
                                               !Plugin.Configuration.PathSelectionsByPath.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType) || 
                                               !(pathSelection = Plugin.Configuration.PathSelectionsByPath[Plugin.CurrentTerritoryContent.TerritoryType]).Any(kvp => kvp.Value.HasJob(Svc.ClientState.LocalPlayer.GetJob()))))
                        {
                            if (ImGui.Button("Clear Saved Path"))
                            {
                                foreach (KeyValuePair<string, JobWithRole> keyValuePair in pathSelection) 
                                    pathSelection[keyValuePair.Key] &= ~curJob;

                                PathSelectionHelper.RebuildDefaultPaths(Plugin.CurrentTerritoryContent.TerritoryType);
                                Plugin.Configuration.Save();
                                if (!Plugin.InDungeon)
                                    container.SelectPath(out Plugin.CurrentPath);
                            }
                        }
                        ImGui.SameLine();
                        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                        if (ImGui.BeginCombo("##SelectedPath", curPaths[curPath].Name))
                        {
                            foreach ((ContentPathsManager.DutyPath Value, int Index) path in curPaths.Select((value, index) => (Value: value, Index: index)))
                            {
                                if (ImGui.Selectable(path.Value.Name))
                                {
                                    curPath = path.Index;
                                    PathSelectionHelper.AddPathSelectionEntry(Plugin.CurrentTerritoryContent!.TerritoryType);
                                    Dictionary<string, JobWithRole> pathJobs = Plugin.Configuration.PathSelectionsByPath[Plugin.CurrentTerritoryContent.TerritoryType]!;
                                    pathJobs.TryAdd(path.Value.FileName, JobWithRole.None);
                                    
                                    foreach (string jobsKey in pathJobs.Keys) 
                                        pathJobs[jobsKey] &= ~curJob;

                                    pathJobs[path.Value.FileName] |= curJob;

                                    PathSelectionHelper.RebuildDefaultPaths(Plugin.CurrentTerritoryContent.TerritoryType);

                                    Plugin.Configuration.Save();
                                    Plugin.CurrentPath = curPath;
                                    Plugin.LoadPath();
                                }
                                if (ImGui.IsItemHovered() && !path.Value.PathFile.Meta.Notes.All(x => x.IsNullOrEmpty()))
                                    ImGui.SetTooltip(string.Join("\n", path.Value.PathFile.Meta.Notes));
                            }
                            ImGui.EndCombo();
                        }
                        ImGui.PopItemWidth();
                        
                        if (ImGui.IsItemHovered() && !curPaths[curPath].PathFile.Meta.Notes.All(x => x.IsNullOrEmpty()))
                            ImGui.SetTooltip(string.Join("\n", curPaths[curPath].PathFile.Meta.Notes));
                        
                    }
                }
            }

            if (Plugin.InDungeon)
            {
                if (Plugin.CurrentTerritoryContent == null)
                    Plugin.LoadPath();
                else
                {
                    ImGui.AlignTextToFramePadding();
                    var progress = VNavmesh_IPCSubscriber.IsEnabled ? VNavmesh_IPCSubscriber.Nav_BuildProgress() : 0;
                    if (progress >= 0)
                    {
                        ImGui.Text($"{Plugin.CurrentTerritoryContent.Name} Mesh: Loading: ");
                        ImGui.SameLine();
                        ImGui.ProgressBar(progress, new Vector2(200, 0));
                    }
                    else
                        ImGui.Text($"{Plugin.CurrentTerritoryContent.Name} Mesh: Loaded Path: {(ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType) ? "Loaded" : "None")}");

                    ImGui.Separator();
                    ImGui.Spacing();

                    if (dutyMode == DutyMode.Trust && Plugin.CurrentTerritoryContent != null)
                    {
                        ImGui.Columns(3);
                        using (ImRaii.Disabled()) 
                            DrawTrustMembers(Plugin.CurrentTerritoryContent);
                        ImGui.Columns(1);
                        ImGui.Spacing();
                    }

                    DrawPathSelection();
                    if (!Plugin.States.HasFlag(PluginState.Looping) && !Plugin.Overlay.IsOpen)
                        MainWindow.GotoAndActions();
                    using (ImRaii.Disabled(!VNavmesh_IPCSubscriber.IsEnabled || !Plugin.InDungeon || !VNavmesh_IPCSubscriber.Nav_IsReady() || !BossMod_IPCSubscriber.IsEnabled))
                    {
                        using (ImRaii.Disabled(!Plugin.InDungeon || !ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType)))
                        {
                            if (Plugin.Stage == 0)
                            {
                                if (ImGui.Button("Start"))
                                {
                                    Plugin.LoadPath();
                                    _currentStepIndex = -1;
                                    if (Plugin.MainListClicked)
                                        Plugin.Run(Svc.ClientState.TerritoryType, 0, !Plugin.MainListClicked);
                                    else
                                        Plugin.Run(Svc.ClientState.TerritoryType);
                                }
                            }
                            else
                                MainWindow.StopResumePause();
                            ImGui.SameLine(0, 15);
                        }
                        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                        MainWindow.LoopsConfig();
                        ImGui.PopItemWidth();

                        if (!ImGui.BeginListBox("##MainList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y))) return;

                        if ((VNavmesh_IPCSubscriber.IsEnabled || Plugin.Configuration.UsingAlternativeMovementPlugin) &&
                            (BossMod_IPCSubscriber.IsEnabled  || Plugin.Configuration.UsingAlternativeBossPlugin)     &&
                            (RSR_IPCSubscriber.IsEnabled      || BossMod_IPCSubscriber.IsEnabled || Plugin.Configuration.UsingAlternativeRotationPlugin))
                        {
                            foreach (var item in Plugin.Actions.Select((Value, Index) => (Value, Index)))
                            {
                                item.Value.DrawCustomText(item.Index, () => ItemClicked(item));
                                //var text = item.Value.Name.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase) ? item.Value.Note : $"{item.Value.ToCustomString()}";
                                ////////////////////////////////////////////////////////////////
                            }

                            if (_currentStepIndex != Plugin.Indexer && _currentStepIndex > -1 && Plugin.Stage > 0)
                            {
                                var lineHeight = ImGui.GetTextLineHeightWithSpacing();
                                _currentStepIndex = Plugin.Indexer;
                                if (_currentStepIndex > 1)
                                    ImGui.SetScrollY((_currentStepIndex - 1) * lineHeight);
                            }
                            else if (_currentStepIndex == -1 && Plugin.Stage > 0)
                            {
                                _currentStepIndex = 0;
                                ImGui.SetScrollY(_currentStepIndex);
                            }

                            if (Plugin.InDungeon && Plugin.Actions.Count < 1 && !ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType))
                                ImGui.TextColored(new Vector4(0, 255, 0, 1),
                                                  $"No Path file was found for:\n{TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryContent.TerritoryType).Split('|')[1].Trim()}\n({Plugin.CurrentTerritoryContent.TerritoryType}.json)\nin the Paths Folder:\n{Plugin.PathsDirectory.FullName.Replace('\\', '/')}\nPlease download from:\n{_pathsURL}\nor Create in the Build Tab");
                        }
                        else
                        {
                            if (!VNavmesh_IPCSubscriber.IsEnabled && !Plugin.Configuration.UsingAlternativeMovementPlugin)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires VNavmesh plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!BossMod_IPCSubscriber.IsEnabled && !Plugin.Configuration.UsingAlternativeBossPlugin)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires BossMod plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!Wrath_IPCSubscriber.IsEnabled && !RSR_IPCSubscriber.IsEnabled && !BossMod_IPCSubscriber.IsEnabled && !Plugin.Configuration.UsingAlternativeRotationPlugin)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires a Rotation plugin to be Installed and Loaded (Either Wrath Combo, Rotation Solver Reborn, or BossMod AutoRotation)");
                        }
                        ImGui.EndListBox();
                    }
                }
            }
            else
            {
                if (!Plugin.States.HasFlag(PluginState.Looping) && !Plugin.Overlay.IsOpen)
                    MainWindow.GotoAndActions();
                

                using (ImRaii.Disabled(Plugin.States.HasFlag(PluginState.Looping)))
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextColored(ImGuiHelper.StateGoodColor, "Select Mode: ");
                    ImGui.SameLine(0);
                    ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.BeginCombo("##AutoDutyModeEnum", Plugin.Configuration.AutoDutyModeEnum.ToCustomString()))
                    {
                        foreach (AutoDutyMode mode in Enum.GetValues(typeof(AutoDutyMode)))
                        {
                            if (ImGui.Selectable(mode.ToCustomString(), Plugin.Configuration.AutoDutyModeEnum == mode))
                            {
                                Plugin.Configuration.AutoDutyModeEnum = mode;
                                Plugin.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                    ImGui.PopItemWidth();
                }

                using (ImRaii.Disabled(Plugin.CurrentTerritoryContent == null))
                {
                    if (!Plugin.States.HasFlag(PluginState.Looping))
                    {
                        if (ImGui.Button("Run"))
                        {
                            if (Plugin.Configuration.DutyModeEnum == DutyMode.None)
                                MainWindow.ShowPopup("Error", "You must select a version\nof the dungeon to run");
                            else if (Svc.Party.PartyId > 0 && (Plugin.Configuration.DutyModeEnum == DutyMode.Support || Plugin.Configuration.DutyModeEnum == DutyMode.Squadron || Plugin.Configuration.DutyModeEnum == DutyMode.Trust))
                                MainWindow.ShowPopup("Error", "You must not be in a party to run Support, Squadron or Trust");
                            else if (Plugin.Configuration.DutyModeEnum == DutyMode.Regular && !Plugin.Configuration.Unsynced && !Plugin.Configuration.OverridePartyValidation && Svc.Party.PartyId == 0)
                                MainWindow.ShowPopup("Error", "You must be in a group of 4 to run Regular Duties");
                            else if (Plugin.Configuration.DutyModeEnum == DutyMode.Regular && !Plugin.Configuration.Unsynced && !Plugin.Configuration.OverridePartyValidation && !ObjectHelper.PartyValidation())
                                MainWindow.ShowPopup("Error", "You must have the correct party makeup to run Regular Duties");
                            else if (ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent?.TerritoryType ?? 0))
                                Plugin.Run();
                            else
                                MainWindow.ShowPopup("Error", "No path was found");
                        }
                    }
                    else
                        MainWindow.StopResumePause();
                }


                
                using (ImRaii.Disabled(Plugin.States.HasFlag(PluginState.Looping)))
                {
                    switch (Plugin.Configuration.AutoDutyModeEnum)
                    {
                        case AutoDutyMode.Looping:
                        {
                            using (ImRaii.Disabled(Plugin.CurrentTerritoryContent == null))
                            {
                                ImGui.SameLine(0, 15);
                                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                                MainWindow.LoopsConfig();
                                ImGui.PopItemWidth();
                            }

                            ImGui.AlignTextToFramePadding();
                            ImGui.TextColored(Plugin.Configuration.DutyModeEnum == DutyMode.None ? ImGuiHelper.StateBadColor : ImGuiHelper.StateGoodColor, "Select Duty Mode: ");
                            ImGui.SameLine(0);
                            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                            if (ImGui.BeginCombo("##DutyModeEnum", Plugin.Configuration.DutyModeEnum.ToCustomString()))
                            {
                                foreach (DutyMode mode in Enum.GetValues(typeof(DutyMode)))
                                {
                                    if (ImGui.Selectable(mode.ToCustomString(), Plugin.Configuration.DutyModeEnum == mode))
                                    {
                                        Plugin.Configuration.DutyModeEnum = mode;
                                        Plugin.Configuration.Save();
                                    }
                                }
                                ImGui.EndCombo();
                            }
                            ImGui.PopItemWidth();

                            if (Plugin.Configuration.DutyModeEnum != DutyMode.None)
                            {
                                if (Plugin.Configuration.DutyModeEnum == DutyMode.Support || Plugin.Configuration.DutyModeEnum == DutyMode.Trust)
                                {
                                    ImGui.AlignTextToFramePadding();
                                    ImGui.TextColored(Plugin.LevelingModeEnum == LevelingMode.None ? ImGuiHelper.StateBadColor : ImGuiHelper.StateGoodColor, "Select Leveling Mode: ");
                                    ImGui.SameLine(0);

                                    ImGuiComponents.HelpMarker("Leveling Mode will queue you for the most CONSISTENT dungeon considering your lvl + Ilvl.\n" +
                                                               (Plugin.Configuration.DutyModeEnum != DutyMode.Trust ?
                                                                    string.Empty :
                                                                    "GROUP will level your trust members equally.\nSOLO will only level them as much as needed") +
                                                               "\n\nIt will NOT always queue you for the highest level dungeon, it follows our stable dungeon list instead.");
                                    ImGui.SameLine(0);
                                    ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                                    if (ImGui.BeginCombo("##LevelingModeEnum", Plugin.LevelingModeEnum switch
                                        {
                                            LevelingMode.None => "None",
                                            _ => $"{Plugin.LevelingModeEnum.ToCustomString().Replace(Plugin.Configuration.DutyModeEnum.ToString(), null)} Auto".Trim()
                                        }))
                                    {
                                        if (ImGui.Selectable("None", Plugin.LevelingModeEnum == LevelingMode.None))
                                        {
                                            Plugin.LevelingModeEnum = LevelingMode.None;
                                            Plugin.Configuration.Save();
                                        }

                                        LevelingMode autoLevelMode = (Plugin.Configuration.DutyModeEnum == DutyMode.Support ? LevelingMode.Support : LevelingMode.Trust_Group);
                                        if (ImGui.Selectable($"{autoLevelMode.ToCustomString().Replace(Plugin.Configuration.DutyModeEnum.ToString(), null)} Auto".Trim(), Plugin.LevelingModeEnum == autoLevelMode))
                                        {
                                            Plugin.LevelingModeEnum = autoLevelMode;
                                            Plugin.Configuration.Save();
                                            if (Plugin.Configuration.AutoEquipRecommendedGear)
                                                AutoEquipHelper.Invoke();
                                        }

                                        if (Plugin.Configuration.DutyModeEnum == DutyMode.Trust)
                                            if (ImGui.Selectable($"{LevelingMode.Trust_Solo.ToCustomString().Replace(Plugin.Configuration.DutyModeEnum.ToString(), null)} Auto".Trim(), Plugin.LevelingModeEnum == LevelingMode.Trust_Solo))
                                            {
                                                Plugin.LevelingModeEnum = LevelingMode.Trust_Solo;
                                                Plugin.Configuration.Save();
                                                if (Plugin.Configuration.AutoEquipRecommendedGear)
                                                    AutoEquipHelper.Invoke();
                                            }


                                        ImGui.EndCombo();
                                    }

                                    ImGui.PopItemWidth();
                                }

                                if (Plugin.Configuration.DutyModeEnum == DutyMode.Support && levelingMode == LevelingMode.Support)
                                {
                                    if (ImGui.Checkbox("Prefer Trust over Support Leveling", ref Plugin.Configuration.PreferTrustOverSupportLeveling))
                                        Plugin.Configuration.Save();
                                }

                                if (Plugin.Configuration.DutyModeEnum == DutyMode.Trust && Player.Available)
                                {
                                    ImGui.Separator();
                                    if (DutySelected != null && DutySelected.Content.TrustMembers.Count > 0)
                                    {
                                        ImGuiEx.LineCentered(() => ImGuiEx.TextUnderlined("Select your Trust Party"));


                                        TrustHelper.ResetTrustIfInvalid();
                                        for (int i = 0; i < Plugin.Configuration.SelectedTrustMembers.Length; i++)
                                        {
                                            TrustMemberName? member = Plugin.Configuration.SelectedTrustMembers[i];

                                            if (member is null)
                                                continue;

                                            if (DutySelected.Content.TrustMembers.All(x => x.MemberName != member))
                                            {
                                                Svc.Log.Debug($"Killing {member}");
                                                Plugin.Configuration.SelectedTrustMembers[i] = null;
                                            }
                                        }

                                        ImGui.Columns(3);
                                        using (ImRaii.Disabled(Plugin.TrustLevelingEnabled && TrustHelper.Members.Any(tm => tm.Value.Level < tm.Value.LevelCap)))
                                        {
                                            DrawTrustMembers(DutySelected.Content);
                                        }

                                        //ImGui.Columns(3, null, false);
                                        if (DutySelected.Content.TrustMembers.Count == 7)
                                            ImGui.NextColumn();

                                        if (ImGui.Button("Refresh", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                                        {
                                            if (InventoryHelper.CurrentItemLevel < 370)
                                                Plugin.LevelingModeEnum = LevelingMode.None;
                                            TrustHelper.ClearCachedLevels();

                                            SchedulerHelper.ScheduleAction("Refresh Levels - ShB", () => TrustHelper.GetLevels(ContentHelper.DictionaryContent[837u]),  () => TrustHelper.State == ActionState.None);
                                            SchedulerHelper.ScheduleAction("Refresh Levels - EW",  () => TrustHelper.GetLevels(ContentHelper.DictionaryContent[952u]),  () => TrustHelper.State == ActionState.None);
                                            SchedulerHelper.ScheduleAction("Refresh Levels - DT",  () => TrustHelper.GetLevels(ContentHelper.DictionaryContent[1167u]), () => TrustHelper.State == ActionState.None);
                                        }

                                        ImGui.NextColumn();
                                        ImGui.Columns(1);
                                    }
                                    else if (ImGui.Button("Refresh trust member levels"))
                                    {
                                        if (InventoryHelper.CurrentItemLevel < 370)
                                            Plugin.LevelingModeEnum = LevelingMode.None;
                                        TrustHelper.ClearCachedLevels();

                                        SchedulerHelper.ScheduleAction("Refresh Levels - ShB", () => TrustHelper.GetLevels(ContentHelper.DictionaryContent[837u]),  () => TrustHelper.State == ActionState.None);
                                        SchedulerHelper.ScheduleAction("Refresh Levels - EW",  () => TrustHelper.GetLevels(ContentHelper.DictionaryContent[952u]),  () => TrustHelper.State == ActionState.None);
                                        SchedulerHelper.ScheduleAction("Refresh Levels - DT",  () => TrustHelper.GetLevels(ContentHelper.DictionaryContent[1167u]), () => TrustHelper.State == ActionState.None);
                                    }
                                }

                                DrawPathSelection();
                                ImGui.Separator();

                                DrawSearchBar();
                                ImGui.SameLine();
                                if (ImGui.Checkbox("Hide Unavailable Duties", ref Plugin.Configuration.HideUnavailableDuties))
                                    Plugin.Configuration.Save();
                                if (Plugin.Configuration.DutyModeEnum is DutyMode.Regular or DutyMode.Trial or DutyMode.Raid)
                                {
                                    if (ImGuiEx.CheckboxWrapped("Unsynced", ref Plugin.Configuration.Unsynced))
                                        Plugin.Configuration.Save();
                                }
                            }

                            break;
                        }
                        case AutoDutyMode.Playlist:
                            ImGui.Separator();
                            break;
                        default:
                            Plugin.Configuration.AutoDutyModeEnum = AutoDutyMode.Looping;
                            break;
                    }
                    
                    ushort ilvl = InventoryHelper.CurrentItemLevel;
                    if (!ImGui.BeginListBox("##DutyList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y))) return;

                    if (Player.Job.GetCombatRole() == CombatRole.NonCombat)
                    {
                        ImGuiEx.TextWrapped(new Vector4(255, 1, 0, 1), "Please switch to a combat job to use AutoDuty.");
                    }
                    else if (Player.Job == Job.BLU && Plugin.Configuration.DutyModeEnum is not (DutyMode.Regular or DutyMode.Trial or DutyMode.Raid))
                    {
                        ImGuiEx.TextWrapped(new Vector4(0, 1, 1, 1), "Blue Mage cannot run Trust, Duty Support, Squadron or Variant dungeons. Please switch jobs or select a different category.");
                    }
                    else if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled)
                    {
                        if (PlayerHelper.IsReady)
                        {
                            switch (Plugin.Configuration.AutoDutyModeEnum)
                            {
                                case AutoDutyMode.Looping:
                                    if (Plugin.LevelingModeEnum != LevelingMode.None)
                                    {
                                        if (Player.Job.GetCombatRole() == CombatRole.NonCombat ||
                                            (Plugin.LevelingModeEnum.IsTrustLeveling() &&
                                             (ilvl < 370 || Plugin.CurrentPlayerItemLevelandClassJob.Value != null && Plugin.CurrentPlayerItemLevelandClassJob.Value != Player.Job)))
                                        {
                                            Svc.Log.Debug($"You are on a non-compatible job: {Player.Job.GetCombatRole()}, or your doing trust and your iLvl({ilvl}) is below 370, or your iLvl has changed, Disabling Leveling Mode");
                                            Plugin.LevelingModeEnum = LevelingMode.None;
                                        }
                                        else if (ilvl > 0 && ilvl != Plugin.CurrentPlayerItemLevelandClassJob.Key)
                                        {
                                            Svc.Log.Debug($"Your iLvl has changed, Selecting new Duty.");
                                            Plugin.CurrentTerritoryContent = LevelingHelper.SelectHighestLevelingRelevantDuty(Plugin.LevelingModeEnum);
                                        }
                                        else
                                        {
                                            ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), $"Leveling Mode: L{Player.Level} (i{ilvl})");
                                            foreach (var item in LevelingHelper.LevelingDuties.Select((Value, Index) => (Value, Index)))
                                            {
                                                if (Plugin.Configuration.DutyModeEnum == DutyMode.Trust && !item.Value.DutyModes.HasFlag(DutyMode.Trust))
                                                    continue;
                                                var disabled = !item.Value.CanRun();
                                                if (!Plugin.Configuration.HideUnavailableDuties || !disabled)
                                                {
                                                    using (ImRaii.Disabled(disabled))
                                                    {
                                                        ImGuiEx.TextWrapped(item.Value == Plugin.CurrentTerritoryContent ? new Vector4(0, 1, 1, 1) : new Vector4(1, 1, 1, 1),
                                                                            $"L{item.Value.ClassJobLevelRequired} (i{item.Value.ItemLevelRequired}): {item.Value.EnglishName}");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Dictionary<uint, Content> dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.DutyModes.HasFlag(Plugin.Configuration.DutyModeEnum)).ToDictionary();

                                        if (dictionary.Count > 0 && PlayerHelper.IsReady)
                                        {
                                            short level = PlayerHelper.GetCurrentLevelFromSheet();
                                            foreach ((uint _, Content? content) in dictionary)
                                            {
                                                // Apply search filter
                                                if (!string.IsNullOrWhiteSpace(_searchText) && !content.Name.ToLower().Contains(_searchText))
                                                    continue; // Skip duties that do not match the search text

                                                bool canRun = content.CanRun(level);
                                                using (ImRaii.Disabled(!canRun))
                                                {
                                                    if (Plugin.Configuration.HideUnavailableDuties && !canRun)
                                                        continue;
                                                    if (ImGui.Selectable($"L{content.ClassJobLevelRequired} ({content.TerritoryType}) {content.Name}", DutySelected?.id == content.TerritoryType))
                                                    {
                                                        DutySelected                   = ContentPathsManager.DictionaryPaths[content.TerritoryType];
                                                        Plugin.CurrentTerritoryContent = content;
                                                        DutySelected.SelectPath(out Plugin.CurrentPath);
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (PlayerHelper.IsReady)
                                                ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), "Please select one of Support, Trust, Squadron or Regular\nto Populate the Duty List");
                                        }
                                    }

                                    break;
                                case AutoDutyMode.Playlist:
                                    for (int i = 0; i < Plugin.PlaylistCurrent.Count; i++)
                                    {
                                        PlaylistEntry entry = Plugin.PlaylistCurrent[i];

                                        ImGui.NewLine();
                                        ImGui.SameLine(0, 1);

                                        ImGui.AlignTextToFramePadding();
                                        ImGui.SetItemAllowOverlap();
                                        if (ImGui.Selectable($"{i+1}", Plugin.PlaylistIndex == i, ImGuiSelectableFlags.AllowItemOverlap)) 
                                            Plugin.PlaylistIndex = i;

                                        ImGui.SameLine(0, 3);

                                        //ImGui.AlignTextToFramePadding();
                                        //ImGui.Text($"{i}:"); // {entry.dutyMode} {entry.id}");
                                        //ImGui.SameLine(0, 0);

                                        ContentPathsManager.ContentPathContainer entryContainer = ContentPathsManager.DictionaryPaths[entry.Id];
                                        Content                                  entryContent   = ContentHelper.DictionaryContent[entry.Id];
                                        
                                        ImGui.PushItemWidth(100 * ImGuiHelpers.GlobalScale);
                                        if (ImGui.BeginCombo($"##Playlist{i}DutyModeEnum", entry.DutyMode.ToCustomString()))
                                        {
                                            foreach (DutyMode mode in Enum.GetValues(typeof(DutyMode)))
                                            {
                                                if (mode == DutyMode.None)
                                                    continue;

                                                using (ImRaii.PushColor(ImGuiCol.Text, ImGuiHelper.StateGoodColor, entryContent.DutyModes.HasFlag(mode)))
                                                {
                                                    if (ImGui.Selectable(mode.ToCustomString(), entry.DutyMode == mode)) 
                                                        entry.DutyMode = mode;
                                                }
                                            }

                                            ImGui.EndCombo();
                                        }

                                        ImGui.PopItemWidth();
                                        ImGui.SameLine();
                                        ImGui.PushItemWidth((entryContainer.Paths.Count > 1 ? (ImGui.GetContentRegionAvail().X - 107) / 2 : ImGui.GetContentRegionAvail().X - 100) * ImGuiHelpers.GlobalScale);
                                        if (ImGui.BeginCombo($"##Playlist{i}DutySelection", $"({entry.Id}) {entryContent.Name}"))
                                        {
                                            short level = PlayerHelper.GetCurrentLevelFromSheet();
                                            DrawSearchBar();

                                            foreach (uint key in ContentPathsManager.DictionaryPaths.Keys)
                                            {
                                                Content content = ContentHelper.DictionaryContent[key];

                                                if (!string.IsNullOrWhiteSpace(_searchText) && !(content.Name?.ToLower().Contains(_searchText) ?? false))
                                                    continue;

                                                if (content.DutyModes.HasFlag(entry.DutyMode) && content.CanRun(level, entry.DutyMode == DutyMode.Trust, unsync: Plugin.Configuration.Unsynced))
                                                    if (ImGui.Selectable($"({key}) {content.Name}", entry.Id == key))
                                                        entry.Id = key;
                                            }

                                            ImGui.EndCombo();
                                        }

                                        if(entry.Id != entryContent.TerritoryType)
                                            continue;

                                        if (entryContainer.Paths.Count > 1)
                                        {
                                            ImGui.SameLine();
                                            if (ImGui.BeginCombo($"##Playlist{i}PathSelection", entryContainer.Paths.First(dp => dp.FileName == entry.path).Name))
                                            {
                                                foreach (ContentPathsManager.DutyPath path in entryContainer.Paths)
                                                    if(ImGui.Selectable(path.Name, path.FileName == entry.path)) 
                                                        entry.path = path.FileName;

                                                ImGui.EndCombo();
                                            }
                                        }
                                    

                                        ImGui.PopItemWidth();
                                        ImGui.SameLine();

                                        using (ImRaii.Disabled(i <= 0))
                                        {
                                            if (ImGuiComponents.IconButton($"Playlist{i}Up", FontAwesomeIcon.ArrowUp))
                                            {
                                                Plugin.PlaylistCurrent.Remove(entry);
                                                Plugin.PlaylistCurrent.Insert(i - 1, entry);
                                            }
                                        }

                                        ImGui.SameLine();

                                        using(ImRaii.Disabled(Plugin.PlaylistCurrent.Count <= i+1))
                                        {
                                            if (ImGuiComponents.IconButton($"Playlist{i}Down", FontAwesomeIcon.ArrowDown))
                                            {
                                                Plugin.PlaylistCurrent.Remove(entry);
                                                Plugin.PlaylistCurrent.Insert(i+1, entry);
                                            }
                                        }

                                        ImGui.SameLine();

                                        if (ImGuiComponents.IconButton($"Playlist{i}Trash", FontAwesomeIcon.TrashAlt))
                                            Plugin.PlaylistCurrent.RemoveAt(i);
                                    }

                                    if (ImGuiComponents.IconButton("PlaylistAdd", FontAwesomeIcon.Plus)) 
                                        Plugin.PlaylistCurrent.Add(new PlaylistEntry { DutyMode = DutyMode.Support });

                                    break;
                            }
                        }
                        else
                            ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), "Busy...");
                    }
                    else
                    {
                        if (!VNavmesh_IPCSubscriber.IsEnabled)
                            ImGuiEx.TextWrapped(new Vector4(255, 0, 0, 1), "AutoDuty requires vnavmesh plugin to be installed and loaded for proper navigation and movement. Please add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                        if (!BossMod_IPCSubscriber.IsEnabled)
                            ImGuiEx.TextWrapped(new Vector4(255, 0, 0, 1), "AutoDuty requires BossMod plugin to be installed and loaded for proper mechanic handling. Please add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                    }
                    ImGui.EndListBox();
                }
            }
        }

        private static void DrawTrustMembers(Content content)
        {
            foreach (TrustMember member in content.TrustMembers)
            {
                bool       enabled        = Plugin.Configuration.SelectedTrustMembers.Where(x => x != null).Any(x => x == member.MemberName);
                CombatRole playerRole     = Player.Job.GetCombatRole();
                int        numberSelected = Plugin.Configuration.SelectedTrustMembers.Count(x => x != null);

                TrustMember?[] members = Plugin.Configuration.SelectedTrustMembers.Select(tmn => tmn != null ? TrustHelper.Members[(TrustMemberName)tmn] : null).ToArray();

                bool canSelect = members.CanSelectMember(member, playerRole) && member.Level >= content.ClassJobLevelRequired;

                using (ImRaii.Disabled(!enabled && (numberSelected == 3 || !canSelect)))
                {
                    if (ImGui.Checkbox($"###{member.Index}{content.Id}", ref enabled))
                    {
                        if (enabled)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                if (Plugin.Configuration.SelectedTrustMembers[i] is null)
                                {
                                    Plugin.Configuration.SelectedTrustMembers[i] = member.MemberName;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if (Plugin.Configuration.SelectedTrustMembers.Where(x => x != null).Any(x => x == member.MemberName))
                            {
                                int idx = Plugin.Configuration.SelectedTrustMembers.IndexOf(x => x != null && x == member.MemberName);
                                Plugin.Configuration.SelectedTrustMembers[idx] = null;
                            }
                        }

                        Plugin.Configuration.Save();
                    }
                }

                ImGui.SameLine(0, 2);
                ImGui.SetItemAllowOverlap();
                ImGui.TextColored(member.Role switch
                {
                    TrustRole.DPS => ImGuiHelper.RoleDPSColor,
                    TrustRole.Healer => ImGuiHelper.RoleHealerColor,
                    TrustRole.Tank => ImGuiHelper.RoleTankColor,
                    TrustRole.AllRounder => ImGuiHelper.RoleAllRounderColor,
                    _ => Vector4.One
                }, member.Name);
                if (member.Level > 0)
                {
                    ImGui.SameLine(0, 2);
                    ImGuiEx.TextV(member.Level < member.LevelCap ? ImGuiHelper.White : ImGuiHelper.MaxLevelColor, $"{member.Level.ToString().ReplaceByChar(Digits.Normal, Digits.GameFont)}");
                }

                ImGui.NextColumn();
            }
        }

        private static void ItemClicked((PathAction, int) item)
        {
            if (item.Item2 == Plugin.Indexer || item.Item1.Name.StartsWith("<--", StringComparison.InvariantCultureIgnoreCase))
            {
                Plugin.Indexer = -1;
                Plugin.MainListClicked = false;
            }
            else
            {
                Plugin.Indexer = item.Item2;
                Plugin.MainListClicked = true;
            }
        }

        internal static void PathsUpdated()
        {
            DutySelected = null;
        }
    }
}
