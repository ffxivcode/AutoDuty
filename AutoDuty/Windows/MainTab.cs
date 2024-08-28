using AutoDuty.Helpers;
using AutoDuty.IPC;
using AutoDuty.Managers;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static AutoDuty.AutoDuty;

namespace AutoDuty.Windows
{
    internal static class MainTab
    {
        private static int _currentStepIndex = -1;
        private static ContentPathsManager.ContentPathContainer? _dutySelected;
        private static readonly string _pathsURL = "https://github.com/ffxivcode/AutoDuty/tree/master/AutoDuty/Paths";
        internal static readonly (string Normal, string GameFont) Digits = ("0123456789", "");
        private static List<string> LevelingDuties = [
            "L15 (i0): Sastasha",
            "L16-L23 (i0): The TamTara Deepcroft",
            "L24-31 (i0): The Thousand Maws of TotoRak", 
            "L32-40 (i0): Brayflox's Longstop",
            "L41-52 (i0): The Stone Vigil",
            "L53-60 (i105): Sohm Al",
            "L61-66 (i240): The Sirensong Sea",
            "L67-70 (i255): Doma Castle",
            "L71-74 (i370): Holminster Switch",
            "L75-80 (i380): Qitana Ravel",
            "L81-86 (i500): The Tower of Zot",
            "L87-90 (i515): Ktisis Hyporboreia",
            "L91-100 (i630): Highest Level DT Dungeons"];

        internal static void Draw()
        {
            if (MainWindow.CurrentTabName != "Main")
                MainWindow.CurrentTabName = "Main";
            var _support = Plugin.Configuration.Support;
            var _trust = Plugin.Configuration.Trust;
            var _squadron = Plugin.Configuration.Squadron;
            var _regular = Plugin.Configuration.Regular;
            var _trial = Plugin.Configuration.Trial;
            var _raid = Plugin.Configuration.Raid;
            var _variant = Plugin.Configuration.Variant;
            var leveling = false;

            void DrawPathSelection()
            {
                if (Plugin.CurrentTerritoryContent == null || !ObjectHelper.IsReady)
                    return;

                using var d = ImRaii.Disabled(Plugin is { InDungeon: true, Stage: > 0 });

                if (ContentPathsManager.DictionaryPaths.TryGetValue(Plugin.CurrentTerritoryContent.TerritoryType, out var container))
                {
                    List<ContentPathsManager.DutyPath> curPaths = container.Paths;
                    if (curPaths.Count > 1)
                    {
                        int curPath = Math.Clamp(Plugin.CurrentPath, 0, curPaths.Count - 1);
                        ImGui.PushItemWidth(240 * ImGuiHelpers.GlobalScale);
                        if (ImGui.Combo("##SelectedPath", ref curPath, [.. curPaths.Select(dp => dp.Name)], curPaths.Count))
                        {
                            if (!Plugin.Configuration.PathSelections.ContainsKey(Plugin.CurrentTerritoryContent!.TerritoryType))
                                Plugin.Configuration.PathSelections.Add(Plugin.CurrentTerritoryContent.TerritoryType, []);

                            Plugin.Configuration.PathSelections[Plugin.CurrentTerritoryContent.TerritoryType][Svc.ClientState.LocalPlayer.GetJob()] = curPath;
                            Plugin.Configuration.Save();
                            Plugin.CurrentPath = curPath;
                            Plugin.LoadPath();
                        }
                        ImGui.PopItemWidth();
                        ImGui.SameLine();

                        using var d2 = ImRaii.Disabled(!Plugin.Configuration.PathSelections.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType) ||
                                                       !Plugin.Configuration.PathSelections[Plugin.CurrentTerritoryContent.TerritoryType].ContainsKey(Svc.ClientState.LocalPlayer.GetJob()));
                        if (ImGui.Button("Clear Saved Path"))
                        {
                            Plugin.Configuration.PathSelections[Plugin.CurrentTerritoryContent.TerritoryType].Remove(Svc.ClientState.LocalPlayer.GetJob());
                            Plugin.Configuration.Save();
                            if (!Plugin.InDungeon)
                                container.SelectPath(out Plugin.CurrentPath);
                        }
                    }
                }
            }

            if (Plugin.InDungeon)
            {
                if (Plugin.CurrentTerritoryContent == null)
                    Plugin.LoadPath();
                else
                {
                    var progress = VNavmesh_IPCSubscriber.IsEnabled ? VNavmesh_IPCSubscriber.Nav_BuildProgress() : 0;
                    if (progress >= 0)
                    {
                        ImGui.Text($"{Plugin.CurrentTerritoryContent.Name} Mesh: Loading: ");
                        ImGui.ProgressBar(progress, new(200, 0));
                    }
                    else
                        ImGui.Text($"{Plugin.CurrentTerritoryContent.Name} Mesh: Loaded Path: {(ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType) ? "Loaded" : "None")}");

                    ImGui.Separator();
                    ImGui.Spacing();

                    DrawPathSelection();
                    if (!Plugin.States.HasFlag(State.Looping) && !Plugin.Overlay.IsOpen)
                        MainWindow.GotoAndActions();
                    using (var d = ImRaii.Disabled(!VNavmesh_IPCSubscriber.IsEnabled || !Plugin.InDungeon || !VNavmesh_IPCSubscriber.Nav_IsReady() || !BossMod_IPCSubscriber.IsEnabled))
                    {
                        using (var d1 = ImRaii.Disabled(!Plugin.InDungeon || !ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType) || Plugin.Stage > 0))
                        {
                            if (ImGui.Button("Start"))
                            {
                                Plugin.LoadPath();
                                _currentStepIndex = -1;
                                if (Plugin.MainListClicked)
                                    Plugin.StartNavigation(!Plugin.MainListClicked);
                                else
                                    Plugin.Run(Svc.ClientState.TerritoryType);
                            }
                            ImGui.SameLine(0, 15);
                        }
                        ImGui.PushItemWidth(150 * ImGuiHelpers.GlobalScale);
                        if (Plugin.Configuration.UseSliderInputs)
                        {
                            if (ImGui.SliderInt("Times", ref Plugin.Configuration.LoopTimes, 1, 100))
                            {
                                if (Plugin.Configuration.LoopTimes < 1) Plugin.Configuration.LoopTimes = 1;
                                Plugin.Configuration.Save();
                            }
                        }
                        else
                        {
                            if (ImGui.InputInt("Times", ref Plugin.Configuration.LoopTimes))
                            {
                                if (Plugin.Configuration.LoopTimes < 1) Plugin.Configuration.LoopTimes = 1;
                                Plugin.Configuration.Save();
                            }
                        }
                        ImGui.PopItemWidth();
                        ImGui.SameLine(0, 5);
                        using (var d2 = ImRaii.Disabled(!Plugin.InDungeon || Plugin.Stage == 0))
                        {
                            MainWindow.StopResumePause();
                            if (Plugin.States.HasFlag(State.Navigating))
                            {
                                //ImGui.SameLine(0, 5);
                                ImGui.TextColored(new Vector4(0, 255f, 0, 1), $"{Plugin.Action}");
                            }
                        }
                        if (!ImGui.BeginListBox("##MainList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y))) return;

                        if ((VNavmesh_IPCSubscriber.IsEnabled || Plugin.Configuration.UsingAlternativeMovementPlugin) && (BossMod_IPCSubscriber.IsEnabled || Plugin.Configuration.UsingAlternativeBossPlugin) && (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled || BossMod_IPCSubscriber.IsEnabled  || Plugin.Configuration.UsingAlternativeRotationPlugin))
                        {
                            foreach (var item in Plugin.ListBoxPOSText.Select((name, index) => (name, index)))
                            {
                                Vector4 v4 = new();
                                if (item.index == Plugin.Indexer)
                                    v4 = new Vector4(0, 255, 0, 1);
                                else
                                    v4 = new Vector4(255, 255, 255, 1);
                                ImGui.TextColored(v4, item.name);
                                if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && Plugin.Stage == 0)
                                {
                                    if (item.index == Plugin.Indexer)
                                    {
                                        Plugin.Indexer = -1;
                                        Plugin.MainListClicked = false;
                                    }
                                    else
                                    {
                                        Plugin.Indexer = item.index;
                                        Plugin.MainListClicked = true;
                                    }
                                }
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
                            if (Plugin.InDungeon && Plugin.ListBoxPOSText.Count < 1 && !ContentPathsManager.DictionaryPaths.ContainsKey(Plugin.CurrentTerritoryContent.TerritoryType))
                                ImGui.TextColored(new Vector4(0, 255, 0, 1), $"No Path file was found for:\n{TerritoryName.GetTerritoryName(Plugin.CurrentTerritoryContent.TerritoryType).Split('|')[1].Trim()}\n({Plugin.CurrentTerritoryContent.TerritoryType}.json)\nin the Paths Folder:\n{Plugin.PathsDirectory.FullName.Replace('\\', '/')}\nPlease download from:\n{_pathsURL}\nor Create in the Build Tab");
                        }
                        else
                        {
                            if (!VNavmesh_IPCSubscriber.IsEnabled && !Plugin.Configuration.UsingAlternativeMovementPlugin)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires VNavmesh plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!BossMod_IPCSubscriber.IsEnabled && !Plugin.Configuration.UsingAlternativeBossPlugin)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires BossMod plugin to be Installed and Loaded\nPlease add 3rd party repo:\nhttps://puni.sh/api/repository/veyn");
                            if (!ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled && !BossMod_IPCSubscriber.IsEnabled && !Plugin.Configuration.UsingAlternativeRotationPlugin)
                                ImGui.TextColored(new Vector4(255, 0, 0, 1), "AutoDuty Requires a Rotation plugin to be Installed and Loaded (Either Rotation Solver Reborn or BossMod AutoRotation)");
                        }
                        ImGui.EndListBox();
                    }
                }
            }
            else
            {
                if (!Plugin.States.HasFlag(State.Looping) && !Plugin.Overlay.IsOpen)
                    MainWindow.GotoAndActions();

                using (var d2 = ImRaii.Disabled(Plugin.CurrentTerritoryContent == null || (Plugin.Configuration.Trust && Plugin.Configuration.SelectedTrustMembers.Any(x => x is null))))
                {
                    if (!Plugin.States.HasFlag(State.Looping))
                    {
                        if (ImGui.Button("Run"))
                        {
                            if (!Plugin.Configuration.Support && !Plugin.Configuration.Trust && !Plugin.Configuration.Squadron && !Plugin.Configuration.Regular && !Plugin.Configuration.Trial && !Plugin.Configuration.Raid && !Plugin.Configuration.Variant)
                                MainWindow.ShowPopup("Error", "You must select a version\nof the dungeon to run");
                            else if (Svc.Party.PartyId > 0 && (Plugin.Configuration.Support || Plugin.Configuration.Squadron || Plugin.Configuration.Trust))
                                MainWindow.ShowPopup("Error", "You must not be in a party to run Support, Squadron or Trust");
                            else if (Plugin.Configuration.Regular && !Plugin.Configuration.Unsynced && !Plugin.Configuration.OverridePartyValidation && Svc.Party.PartyId == 0)
                                MainWindow.ShowPopup("Error", "You must be in a group of 4 to run Regular Duties");
                            else if (Plugin.Configuration.Regular && !Plugin.Configuration.Unsynced && !Plugin.Configuration.OverridePartyValidation && !ObjectHelper.PartyValidation())
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
                using (ImRaii.Disabled(Plugin.States.HasFlag(State.Looping)))
                {
                    using (ImRaii.Disabled(Plugin.CurrentTerritoryContent == null))
                    {
                        ImGui.SameLine(0, 15);
                        ImGui.PushItemWidth(200 * ImGuiHelpers.GlobalScale);
                        if (Plugin.Configuration.UseSliderInputs)
                        {
                            if (ImGui.SliderInt("Times", ref Plugin.Configuration.LoopTimes, 0, 100))
                                Plugin.Configuration.Save();
                        }
                        else
                        {
                            if (ImGui.InputInt("Times", ref Plugin.Configuration.LoopTimes))
                                Plugin.Configuration.Save();
                        }
                        ImGui.PopItemWidth();
                    }


                    if (ImGui.Checkbox("Support", ref Plugin.Configuration.support))
                    {
                        if (Plugin.Configuration.support)
                        {
                            Plugin.Configuration.Support = Plugin.Configuration.support;
                            _dutySelected = null;
                            Plugin.Configuration.Save();
                        }
                    }

                    if (ImGuiEx.CheckboxWrapped("Trust", ref Plugin.Configuration.trust))
                    {
                        if (Plugin.Configuration.trust)
                        {
                            Plugin.Configuration.Trust = Plugin.Configuration.trust;
                            _dutySelected = null;
                            Plugin.Configuration.Save();
                        }
                    }

                    if (ImGuiEx.CheckboxWrapped("Squadron", ref Plugin.Configuration.squadron))
                    {
                        if (Plugin.Configuration.squadron)
                        {
                            Plugin.Configuration.Squadron = Plugin.Configuration.squadron;
                            _dutySelected = null;
                            Plugin.Configuration.Save();
                        }
                    }

                    if (ImGuiEx.CheckboxWrapped("Regular", ref Plugin.Configuration.regular))
                    {
                        if (Plugin.Configuration.regular)
                        {
                            Plugin.Configuration.Regular = Plugin.Configuration.regular;
                            _dutySelected = null;
                            Plugin.Configuration.Save();
                        }
                    }

                    if (ImGuiEx.CheckboxWrapped("Trial", ref Plugin.Configuration.trial))
                    {
                        if (Plugin.Configuration.trial)
                        {
                            Plugin.Configuration.Trial = Plugin.Configuration.trial;
                            _dutySelected = null;
                            Plugin.Configuration.Save();
                        }
                    }

                    if (ImGuiEx.CheckboxWrapped("Raid", ref Plugin.Configuration.raid))
                    {
                        if (Plugin.Configuration.raid)
                        {
                            Plugin.Configuration.Raid = Plugin.Configuration.raid;
                            _dutySelected = null;
                            Plugin.Configuration.Save();
                        }
                    }

                    if (ImGuiEx.CheckboxWrapped("Variant", ref Plugin.Configuration.variant))
                    {
                        Plugin.Configuration.Variant = Plugin.Configuration.variant;
                        if (Plugin.Configuration.variant)
                        {
                            _dutySelected = null;
                            Plugin.Configuration.Save();
                        }
                    }


                    if (Plugin.Configuration.Support || Plugin.Configuration.Trust || Plugin.Configuration.Squadron || Plugin.Configuration.Regular || Plugin.Configuration.Trial || Plugin.Configuration.Raid || Plugin.Configuration.Variant)
                    {
                        //ImGui.SameLine(0, 15);
                        ImGui.Separator();
                        if (ImGui.Checkbox("Hide Unavailable Duties", ref Plugin.Configuration.HideUnavailableDuties))
                            Plugin.Configuration.Save();

                        if (Plugin.Configuration.Support || Plugin.Configuration.Trust)
                        {
                            leveling =  _support ? Plugin.SupportLeveling :
                                        _trust   ? Plugin.TrustLeveling : false;
                            bool equip = Plugin.Configuration.AutoEquipRecommendedGear;

                            if (ImGuiEx.CheckboxWrapped("Leveling", ref leveling))
                            {
                                if (leveling)
                                {
                                    if (equip)
                                        AutoEquipHelper.Invoke();

                                    ContentHelper.Content? duty = LevelingHelper.SelectHighestLevelingRelevantDuty(Plugin.Configuration.Trust);
                                    if (duty != null)
                                    {
                                        _dutySelected = ContentPathsManager.DictionaryPaths[duty.TerritoryType];
                                        Plugin.CurrentTerritoryContent = duty;

                                        _dutySelected.SelectPath(out Plugin.CurrentPath);

                                        if (Plugin.Configuration.Support)
                                            Plugin.SupportLeveling = leveling;
                                        else if (Plugin.Configuration.Trust)
                                            Plugin.TrustLeveling = leveling;
                                    }
                                }
                                else
                                {
                                    _dutySelected = null;
                                    Plugin.MainListClicked = false;
                                    Plugin.CurrentTerritoryContent = null;
                                    if (Plugin.Configuration.Support)
                                        Plugin.SupportLeveling = leveling;
                                    else if (Plugin.Configuration.Trust)
                                        Plugin.TrustLeveling = leveling;
                                }
                            }
                            if (!Plugin.Configuration.Trust) ImGuiComponents.HelpMarker("Leveling Mode will queue you for the most CONSISTENT dungeon considering your lvl + Ilvl. \nIt will NOT always queue you for the highest level dungeon, it follows our stable dungeon list instead:\nL16-L23 (i0): TamTara \nL24-31 (i0): Totorak\nL32-40 (i0): Brayflox\nL41-52 (i0): Stone Vigil\nL53-60 (i105): Sohm Al\nL61-66 (i240): Sirensong Sea\nL67-70 (i255): Doma Castle\nL71-74 (i370): Holminster\nL75-80 (i380): Qitana\nL81-86 (i500): Tower of Zot\nL87-90 (i515): Ktisis\nL91-100 (i630): Highest Level DT Dungeons");
                            else ImGuiComponents.HelpMarker("TRUST Leveling Mode will queue you for the most CONSISTENT dungeon considering your lvl + Ilvl, as well as the LOWEST LEVEL trust members you have, in an attempt to level them all equally.. \nIt will NOT always queue you for the highest level dungeon, it follows our stable dungeon list instead:\nL71-74 (i370): Holminster\nL75-80 (i380): Qitana\nL81-86 (i500): Tower of Zot\nL87-90 (i515): Ktisis\nL91-100 (i630): Highest Level DT Dungeons");

                        }

                        if (Plugin.Configuration.Trust)
                        {
                            ImGui.Separator();
                            if (_dutySelected != null && _dutySelected.Content.TrustMembers.Count > 0)
                            {
                                ImGuiEx.LineCentered(() => ImGuiEx.TextUnderlined("Select your Trust Party"));
                                ImGui.Columns(3, null, false);

                                TrustManager.ResetTrustIfInvalid();
                                for (int i = 0; i < Plugin.Configuration.SelectedTrustMembers.Length; i++)
                                {
                                    TrustMemberName? member = Plugin.Configuration.SelectedTrustMembers[i];

                                    if (member is null)
                                        continue;

                                    if (_dutySelected.Content.TrustMembers.All(x => x.MemberName != member))
                                    {
                                        Svc.Log.Debug($"Killing {member}");
                                        Plugin.Configuration.SelectedTrustMembers[i] = null;
                                    }
                                }

                                using (ImRaii.Disabled(Plugin.TrustLevelingEnabled))
                                {
                                    foreach (TrustMember member in _dutySelected.Content.TrustMembers)
                                    {
                                        bool       enabled        = Plugin.Configuration.SelectedTrustMembers.Where(x => x != null).Any(x => x == member.MemberName);
                                        CombatRole playerRole     = Player.Job.GetRole();
                                        int        numberSelected = Plugin.Configuration.SelectedTrustMembers.Count(x => x != null);

                                        TrustMember?[] members = Plugin.Configuration.SelectedTrustMembers.Select(tmn => tmn != null ? TrustManager.members[(TrustMemberName)tmn] : null).ToArray();

                                        bool canSelect = members.CanSelectMember(member, playerRole) && member.Level >= _dutySelected.Content.ClassJobLevelRequired;

                                        using (ImRaii.Disabled(!enabled && (numberSelected == 3 || !canSelect)))
                                        {
                                            if (ImGui.Checkbox($"###{member.Index}{_dutySelected.id}", ref enabled))
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

                                if (_dutySelected.Content.TrustMembers.Count == 7)
                                    ImGui.NextColumn();

                                if(ImGui.Button("Refresh", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                                    TrustManager.ClearCachedLevels();
                                ImGui.NextColumn();
                                ImGui.Columns(1, null, true);
                            } else if (ImGui.Button("Refresh trust member levels"))
                            {
                                TrustManager.ClearCachedLevels();
                            }
                        }

                        DrawPathSelection();
                    }
                    if (Plugin.Configuration.Regular || Plugin.Configuration.Trial || Plugin.Configuration.Raid)
                    {
                        if (ImGuiEx.CheckboxWrapped("Unsynced", ref Plugin.Configuration.Unsynced))
                            Plugin.Configuration.Save();
                    }
                    
                    if (!ImGui.BeginListBox("##DutyList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y))) return;

                    if (leveling)
                    {
                        ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), $"Leveling Mode: L{Player.Level} (i{PlayerHelper.GetCurrentItemLevelFromGearSet(updateGearsetBeforeCheck: false)})");
                        foreach (var item in LevelingDuties)
                        {
                            if (item.Contains(Plugin.CurrentTerritoryContent?.Name ?? "-"))
                                ImGuiEx.TextWrapped(new Vector4(0, 1, 1, 1), $"{item}");
                            else
                                ImGuiEx.TextWrapped(new Vector4(1, 1, 1, 1), $"{item}");
                        }
                    }
                    else if (VNavmesh_IPCSubscriber.IsEnabled && BossMod_IPCSubscriber.IsEnabled)
                    {
                        if (ObjectHelper.IsReady)
                        {
                            if (Player.Job.GetRole() == CombatRole.NonCombat)
                                ImGuiEx.TextWrapped(new Vector4(255, 1, 0, 1), "Please switch to a combat job to use AutoDuty.");

                            if ((Player.Job.GetRole() != CombatRole.NonCombat && Player.Job != Job.BLU) || (Player.Job == Job.BLU && (Plugin.Configuration.Regular || Plugin.Configuration.Trial || Plugin.Configuration.Raid)))
                            {
                                Dictionary<uint, ContentHelper.Content> dictionary = [];
                                if (Plugin.Configuration.Support)
                                    dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.DawnContent).ToDictionary();
                                else if (Plugin.Configuration.Trust)
                                    dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.TrustContent).ToDictionary();
                                else if (Plugin.Configuration.Squadron)
                                    dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.GCArmyContent).ToDictionary();
                                else if (Plugin.Configuration.Regular)
                                    dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.ContentType == 2).ToDictionary();
                                else if (Plugin.Configuration.Trial)
                                    dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.ContentType == 4).ToDictionary();
                                else if (Plugin.Configuration.Raid)
                                    dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.ContentType == 5).ToDictionary();
                                else if (Plugin.Configuration.Variant)
                                    dictionary = ContentHelper.DictionaryContent.Where(x => x.Value.VariantContent).ToDictionary();

                                if (dictionary.Count > 0 && ObjectHelper.IsReady)
                                {
                                    short level = PlayerHelper.GetCurrentLevelFromSheet();
                                    short ilvl = PlayerHelper.GetCurrentItemLevelFromGearSet(updateGearsetBeforeCheck: false);

                                    foreach ((uint _, ContentHelper.Content? content) in dictionary)
                                    {
                                        bool canRun = content.CanRun(level, ilvl) && (!_trust || content.CanTrustRun());
                                        using (ImRaii.Disabled(!canRun))
                                        {
                                            if (Plugin.Configuration.HideUnavailableDuties && !canRun)
                                                continue;
                                            if (ImGui.Selectable($"({content.TerritoryType}) {content.Name}", _dutySelected?.id == content.TerritoryType))
                                            {
                                                _dutySelected = ContentPathsManager.DictionaryPaths[content.TerritoryType];
                                                Plugin.CurrentTerritoryContent = content;
                                                _dutySelected.SelectPath(out Plugin.CurrentPath);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (ObjectHelper.IsReady)
                                        ImGuiEx.TextWrapped(new Vector4(0, 1, 0, 1), "Please select one of Support, Trust, Squadron or Regular\nto Populate the Duty List");
                                }
                            }
                            else
                            {
                                if (ObjectHelper.IsReady && Player.Job == Job.BLU)
                                    ImGuiEx.TextWrapped(new Vector4(0, 1, 1, 1), "Blue Mage cannot run Trust, Duty Support, Squadron or Variant dungeons. Please switch jobs or select a different category.");
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

        internal static void PathsUpdated()
        {
            _dutySelected = null;
        }
    }
}
