using System;
using System.Collections.Generic;
using System.Numerics;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.EzSharedDataManager;
using ECommons.Funding;
using ECommons.ImGuiMethods;
using ECommons.Schedulers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using static AutoDuty.AutoDuty;

namespace AutoDuty.Windows;

public class MainWindow : Window, IDisposable
{
    internal static string CurrentTabName = "";

    private static bool _showPopup = false;
    private static string _popupText = "";
    private static string _popupTitle = "";
    private static string openTabName = "";

    public MainWindow() : base(
        $"AutoDuty v0.0.0.{Plugin.Configuration.Version}###Autoduty", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(10, 10),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        TitleBarButtons.Add(new() { Icon = FontAwesomeIcon.Cog, IconOffset = new(1, 1), Click = _ => OpenTab("Config") });
        TitleBarButtons.Add(new() { ShowTooltip = () => ImGui.SetTooltip("Support Herculezz on Ko-fi"), Icon = FontAwesomeIcon.Heart, IconOffset = new(1, 1), Click = _ => GenericHelpers.ShellStart("https://ko-fi.com/Herculezz") });
    }

    internal static void OpenTab(string tabName)
    {
        openTabName = tabName;
        _ = new TickScheduler(delegate
        {
            openTabName = "";
        }, 25);
    }

    public void Dispose()
    {
    }

    internal static void Start()
    {
        ImGui.SameLine(0, 5);
    }

    internal static void StopResumePause()
    {
        using (var d = ImRaii.Disabled(!Plugin.States.HasFlag(State.Looping) && !Plugin.States.HasFlag(State.Navigating) && !RepairHelper.RepairRunning && !GotoHelper.GotoRunning && !GotoInnHelper.GotoInnRunning && !GotoBarracksHelper.GotoBarracksRunning && !GCTurninHelper.GCTurninRunning && !ExtractHelper.ExtractRunning && !DesynthHelper.DesynthRunning))
        {
            if (ImGui.Button("Stop"))
            {
                Plugin.Stage = Stage.Stopped;
                return;
            }
            ImGui.SameLine(0, 5);
        }

        using (var d = ImRaii.Disabled((!Plugin.States.HasFlag(State.Looping) && !Plugin.States.HasFlag(State.Navigating) && !RepairHelper.RepairRunning && !GotoHelper.GotoRunning && !GotoInnHelper.GotoInnRunning && !GotoBarracksHelper.GotoBarracksRunning && !GCTurninHelper.GCTurninRunning && !ExtractHelper.ExtractRunning && !DesynthHelper.DesynthRunning) || Plugin.CurrentTerritoryContent == null))
            {
                if (Plugin.Stage == Stage.Paused)
            {
                if (ImGui.Button("Resume"))
                {
                    Plugin.Stage = Stage.Reading_Path;
                }
            }
            else
            {
                if (ImGui.Button("Pause"))
                {
                    Plugin.Stage = Stage.Paused;
                }
            }
        }
    }

    internal static void GotoAndActions()
    {
        using (var d2 = ImRaii.Disabled(Plugin.States.HasFlag(State.Looping) || Plugin.States.HasFlag(State.Navigating)))
        {
            using (var GotoDisabled = ImRaii.Disabled(GCTurninHelper.GCTurninRunning || DesynthHelper.DesynthRunning || ExtractHelper.ExtractRunning || RepairHelper.RepairRunning))
            {
                if ((GotoHelper.GotoRunning && !GCTurninHelper.GCTurninRunning && !RepairHelper.RepairRunning) || MapHelper.MoveToMapMarkerRunning || GotoHousingHelper.GotoHousingRunning)
                {
                    if (ImGui.Button("Stop"))
                        Plugin.Stage = Stage.Stopped;
                }
                else
                {
                    if (ImGui.Button("Goto"))
                    {
                        ImGui.OpenPopup("GotoPopup");
                    }
                }
            }
            ImGui.SameLine(0, 5);
            using (var GCButton = ImRaii.Disabled(!Plugin.Configuration.AutoGCTurnin))
            {
                using (var GCTurninDisabled = ImRaii.Disabled(DesynthHelper.DesynthRunning || ExtractHelper.ExtractRunning || (GotoHelper.GotoRunning && !GCTurninHelper.GCTurninRunning) || RepairHelper.RepairRunning))
                {
                    if (GCTurninHelper.GCTurninRunning)
                    {
                        if (ImGui.Button("Stop"))
                            Plugin.Stage = Stage.Stopped;
                    }
                    else
                    {
                        if (ImGui.Button("TurnIn"))
                        {
                            if (Deliveroo_IPCSubscriber.IsEnabled)
                                GCTurninHelper.Invoke();
                            else
                                ShowPopup("Missing Plugin", "GC Turnin Requires Deliveroo plugin. Get @ https://git.carvel.li/liza/plugin-repo");
                        }
                        if (Deliveroo_IPCSubscriber.IsEnabled)
                            ToolTip("Click to Goto GC Turnin and Invoke Deliveroo");
                        else
                            ToolTip("GC Turnin Requires Deliveroo plugin. Get @ https://git.carvel.li/liza/plugin-repo");
                    }
                }
            }
            ImGui.SameLine(0, 5);
            using (var DesynthButton = ImRaii.Disabled(!Plugin.Configuration.AutoDesynth))
            {
                using (var DesynthDisabled = ImRaii.Disabled(GCTurninHelper.GCTurninRunning || ExtractHelper.ExtractRunning || GotoHelper.GotoRunning || RepairHelper.RepairRunning))
                {
                    if (DesynthHelper.DesynthRunning)
                    {
                        if (ImGui.Button("Stop"))
                            Plugin.Stage = Stage.Stopped;
                    }
                    else
                    {
                        if (ImGui.Button("Desynth"))
                            DesynthHelper.Invoke();
                        ToolTip("Click to Desynth all Items in Inventory");
                    }
                }
            }
            ImGui.SameLine(0, 5);
            using (var ExtractButton = ImRaii.Disabled(!Plugin.Configuration.AutoExtract))
            {
                using (var ExtractDisabled = ImRaii.Disabled(GCTurninHelper.GCTurninRunning || DesynthHelper.DesynthRunning || GotoHelper.GotoRunning || RepairHelper.RepairRunning))
                {
                    if (ExtractHelper.ExtractRunning)
                    {
                        if (ImGui.Button("Stop"))
                            Plugin.Stage = Stage.Stopped;
                    }
                    else
                    {
                        if (ImGui.Button("Extract"))
                        {
                            if (QuestManager.IsQuestComplete(66174))
                                ExtractHelper.Invoke();
                            else
                                ShowPopup("Missing Quest Completion", "Materia Extraction requires having completed quest: Forging the Spirit");
                        }
                        if (QuestManager.IsQuestComplete(66174))
                            ToolTip("Click to Extract Materia");
                        else
                            ToolTip("Materia Extraction requires having completed quest: Forging the Spirit");
                    }
                }
            }
            ImGui.SameLine(0, 5);
            using (var RepairButton = ImRaii.Disabled(!Plugin.Configuration.AutoRepair))
            {
                using (var RepairDisabled = ImRaii.Disabled(GCTurninHelper.GCTurninRunning || DesynthHelper.DesynthRunning || ExtractHelper.ExtractRunning || (GotoHelper.GotoRunning && !RepairHelper.RepairRunning)))
                {
                    if (RepairHelper.RepairRunning)
                    {
                        if (ImGui.Button("Stop"))
                            Plugin.Stage = Stage.Stopped;
                    }
                    else
                    {
                        if (ImGui.Button("Repair"))
                        {
                            if (InventoryHelper.CanRepair(100))
                                RepairHelper.Invoke();
                            //else
                                //ShowPopup("", "");
                        }
                        //if ()
                            ToolTip("Click to Repair");
                        //else
                            //ToolTip("");
                    }
                }
            }
            if (ImGui.BeginPopup("GotoPopup"))
            {
                if (ImGui.Selectable("Barracks"))
                {
                    GotoBarracksHelper.Invoke();
                }
                if (ImGui.Selectable("Inn"))
                {
                    GotoInnHelper.Invoke();
                }
                if (ImGui.Selectable("GCSupply"))
                {
                    GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(ObjectHelper.GrandCompany), [GCTurninHelper.GCSupplyLocation], 0.25f, 3f);
                }
                if (ImGui.Selectable("Flag Marker"))
                {
                    MapHelper.MoveToMapMarker();
                }
                if (ImGui.Selectable("Summoning Bell"))
                {
                    SummoningBellHelper.Invoke(AutoDuty.Plugin.Configuration.PreferredSummoningBellEnum);
                }
                if (ImGui.Selectable("Apartment"))
                {
                    GotoHousingHelper.Invoke(Housing.Apartment);
                }
                if (ImGui.Selectable("Personal Home"))
                {
                    GotoHousingHelper.Invoke(Housing.Personal_Home);
                }
                if (ImGui.Selectable("FC Estate"))
                {
                    GotoHousingHelper.Invoke(Housing.FC_Estate);
                }
                ImGui.EndPopup();
            }
        }
    }

    internal static void ToolTip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            ImGuiEx.Text(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    internal static void CenteredText(string text)
    {
        float windowWidth = ImGui.GetWindowSize().X;
        float textWidth = ImGui.CalcTextSize(text).X;

        ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
        ImGui.Text(text);
    }

    internal static bool CenteredButton(string label, float percentWidth, float xIndent = 0)
    {
        var buttonWidth = ImGui.GetContentRegionAvail().X * percentWidth;
        ImGui.SetCursorPosX(xIndent + (ImGui.GetContentRegionAvail().X - buttonWidth) / 2f);
        return ImGui.Button(label, new(buttonWidth, 35f));
    }

    internal static void ShowPopup(string popupTitle, string popupText)
    {
        _popupTitle = popupTitle;
        _popupText = popupText;
        _showPopup = true;
    }

    private void DrawPopup()
    {
        if (_showPopup)
        {
            ImGui.OpenPopup(_popupTitle);
        }
        Vector2 textSize = ImGui.CalcTextSize(_popupText);
        ImGui.SetNextWindowSize(new(textSize.X + 25, textSize.Y + 100));
        if (ImGui.BeginPopupModal(_popupTitle, ref _showPopup, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoMove))
        {
            CenteredText(_popupText);
            ImGui.Spacing();
            if (CenteredButton("OK", .5f, 15))
            {
                _showPopup = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private static void KofiLink()
    {
        OpenTab(CurrentTabName);
        if (EzThrottler.Throttle("KofiLink", 15000))
        {
            _ = new TickScheduler(delegate
            {
                GenericHelpers.ShellStart("https://ko-fi.com/Herculezz");
            }, 500);
        }
    }

    //ECommons
    static uint ColorNormal
    {
        get
        {
            var vector1 = ImGuiEx.Vector4FromRGB(0x022594);
            var vector2 = ImGuiEx.Vector4FromRGB(0x940238);

            var gen = GradientColor.Get(vector1, vector2).ToUint();
            var data = EzSharedData.GetOrCreate<uint[]>("ECommonsPatreonBannerRandomColor", [gen]);
            if (!GradientColor.IsColorInRange(data[0].ToVector4(), vector1, vector2))
            {
                data[0] = gen;
            }
            return data[0];
        }
    }
    public static void EzTabBar(string id, string KoFiTransparent, string openTabName, ImGuiTabBarFlags flags, params (string name, Action function, Vector4? color, bool child)[] tabs)
    {
        ImGui.BeginTabBar(id, flags);
        foreach (var x in tabs)
        {
            if (x.name == null) continue;
            if (x.color != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Tab, x.color.Value);
            }
            if (ImGuiEx.BeginTabItem(x.name, openTabName == x.name ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                if (x.color != null)
                {
                    ImGui.PopStyleColor();
                }
                if (x.child) ImGui.BeginChild(x.name + "child");
                x.function();
                if (x.child) ImGui.EndChild();
                ImGui.EndTabItem();
            }
            else
            {
                if (x.color != null)
                {
                    ImGui.PopStyleColor();
                }
            }
        }
        if (KoFiTransparent != null) PatreonBanner.RightTransparentTab();
        ImGui.EndTabBar();
    }

    private static List<(string, Action, Vector4?, bool)> tabList =
        [("Main", MainTab.Draw, null, false), ("Build", BuildTab.Draw, null, false), ("Paths", PathsTab.Draw, null, false), ("Config", ConfigTab.Draw, null, false), ("Info", InfoTab.Draw, null, false), ("Support AutoDuty", KofiLink, ImGui.ColorConvertU32ToFloat4(ColorNormal), false)
        ];

    public override void Draw()
    {
        DrawPopup();
        EzTabBar("MainTab", null, openTabName, ImGuiTabBarFlags.None, tabList.ToArray());
    }
}
