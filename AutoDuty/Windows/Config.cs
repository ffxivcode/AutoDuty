using AutoDuty.IPC;
using Dalamud.Configuration;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.ExcelServices;
using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;
using ECommons;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using AutoDuty.Managers;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Common.Math;
using AutoDuty.Helpers;
using static AutoDuty.Data.Enum;

namespace AutoDuty.Windows;

[Serializable]
public class Configuration : IPluginConfiguration
{
    //Meta
    public int Version { get => 121; set { } }
    public HashSet<string> DoNotUpdatePathFiles = [];
    public Dictionary<uint, Dictionary<Job, int>> PathSelections = [];

    //General Options
    public int LoopTimes = 1;
    internal bool support = false;
    public bool Support
    {
        get => support;
        set
        {
            support = value;
            if (value)
            {
                Variant = false;
                Raid = false;
                Trial = false;
                Regular = false;
                Trust = false;
                Squadron = false;
                AutoDuty.Plugin.CurrentTerritoryContent = null;
            }
        }
    }
    internal bool trust = false;
    public bool Trust
    {
        get => trust;
        set
        {
            trust = value;
            if (value)
            {
                Variant = false;
                Raid = false;
                Trial = false;
                Regular = false;
                Support = false;
                Squadron = false;
                AutoDuty.Plugin.CurrentTerritoryContent = null;
            }
        }
    }
    internal bool squadron = false;
    public bool Squadron
    {
        get => squadron;
        set
        {
            squadron = value;
            if (value)
            {
                Variant = false;
                Raid = false;
                Trial = false;
                Regular = false;
                Support = false;
                Trust = false;
                AutoDuty.Plugin.CurrentTerritoryContent = null;
            }
        }
    }
    internal bool regular = false;
    public bool Regular
    {
        get => regular;
        set
        {
            regular = value;
            if (value)
            {
                Variant = false;
                Raid = false;
                Trial = false;
                Support = false;
                Trust = false;
                Squadron = false;
                AutoDuty.Plugin.CurrentTerritoryContent = null;
            }
        }
    }
    internal bool trial = false;
    public bool Trial
    {
        get => trial;
        set
        {
            trial = value;
            if (value)
            {
                Variant = false;
                Raid = false;
                Regular = false;
                Support = false;
                Trust = false;
                Squadron = false;
                AutoDuty.Plugin.CurrentTerritoryContent = null;
            }
        }
    }
    internal bool raid = false;
    public bool Raid
    {
        get => raid;
        set
        {
            raid = value;
            if (value)
            {
                Variant = false;
                Trial = false;
                Regular = false;
                Support = false;
                Trust = false;
                Squadron = false;
                AutoDuty.Plugin.CurrentTerritoryContent = null;
            }
        }
    }
    internal bool variant = false;
    public bool Variant
    {
        get => variant;
        set
        {
            variant = value;
            if (value)
            {
                Raid = false;
                Trial = false;
                Regular = false;
                Support = false;
                Trust = false;
                Squadron = false;
                AutoDuty.Plugin.CurrentTerritoryContent = null;
            }
        }
    }
    public bool Unsynced = false;
    public bool HideUnavailableDuties = false;

    public bool ShowMainWindowOnStartup = false;
    //Overlay Config Options
    internal bool showOverlay = true;
    public bool ShowOverlay
    {
        get => showOverlay;
        set
        {
            showOverlay = value;
            if (AutoDuty.Plugin.Overlay != null)
                AutoDuty.Plugin.Overlay.IsOpen = value;
        }
    }
    internal bool hideOverlayWhenStopped = false;
    public bool HideOverlayWhenStopped
    {
        get => hideOverlayWhenStopped;
        set 
        {
            hideOverlayWhenStopped = value;
            if (AutoDuty.Plugin.Overlay != null)
            {
                SchedulerHelper.ScheduleAction("LockOverlaySetter", () => AutoDuty.Plugin.Overlay.IsOpen = !value || AutoDuty.Plugin.Running || AutoDuty.Plugin.Started, () => AutoDuty.Plugin.Overlay != null);
            }
        }
    }
    internal bool lockOverlay = false;
    public bool LockOverlay
    {
        get => lockOverlay;
        set 
        {
            lockOverlay = value;
            if (value)
                SchedulerHelper.ScheduleAction("LockOverlaySetter", () => { if (!AutoDuty.Plugin.Overlay.Flags.HasFlag(ImGuiWindowFlags.NoMove)) AutoDuty.Plugin.Overlay.Flags |= ImGuiWindowFlags.NoMove; }, () => AutoDuty.Plugin.Overlay != null);
            else
                SchedulerHelper.ScheduleAction("LockOverlaySetter", () => { if (AutoDuty.Plugin.Overlay.Flags.HasFlag(ImGuiWindowFlags.NoMove)) AutoDuty.Plugin.Overlay.Flags -= ImGuiWindowFlags.NoMove; }, () => AutoDuty.Plugin.Overlay != null);
        }
    }
    internal bool overlayNoBG = false;
    public bool OverlayNoBG
    {
        get => overlayNoBG;
        set
        {
            overlayNoBG = value;
            if (value)
                SchedulerHelper.ScheduleAction("OverlayNoBGSetter", () => { if (!AutoDuty.Plugin.Overlay.Flags.HasFlag(ImGuiWindowFlags.NoBackground)) AutoDuty.Plugin.Overlay.Flags |= ImGuiWindowFlags.NoBackground; }, () => AutoDuty.Plugin.Overlay != null);
            else
                SchedulerHelper.ScheduleAction("OverlayNoBGSetter", () => { if (AutoDuty.Plugin.Overlay.Flags.HasFlag(ImGuiWindowFlags.NoBackground)) AutoDuty.Plugin.Overlay.Flags -= ImGuiWindowFlags.NoBackground; }, () => AutoDuty.Plugin.Overlay != null);
        }
    }
    public bool ShowDutyLoopText = true;
    public bool ShowActionText = true;
    public bool UseSliderInputs = false;

    //Duty Config Options
    public bool AutoExitDuty = true;
    public bool AutoManageRSRState = true;
    internal bool autoManageBossModAISettings = true;
    public bool AutoManageBossModAISettings
    {
        get => autoManageBossModAISettings;
        set
        {
            autoManageBossModAISettings = value;
            HideBossModAIConfig = !value;
        }
    }
    public bool LootTreasure = true;
    public LootMethod LootMethodEnum = LootMethod.AutoDuty;
    public bool LootBossTreasureOnly = false;
    public int TreasureCofferScanDistance = 25;
    public bool UsingAlternativeRotationPlugin = false;
    public bool UsingAlternativeMovementPlugin = false;
    public bool UsingAlternativeBossPlugin = false;

    //PreLoop Config Options
    public bool RetireMode = false;
    public RetireLocation RetireLocationEnum = RetireLocation.Inn;
    public bool AutoEquipRecommendedGear;
    public bool AutoBoiledEgg = false;
    public bool AutoRepair = false;
    public int AutoRepairPct = 50;
    public bool AutoRepairSelf = false;

    //Between Loop Config Options
    public int WaitTimeBeforeAfterLoopActions = 0;
    public bool AutoExtract = false;
    internal bool autoExtractEquipped = true;
    public bool AutoExtractEquipped 
    {
        get => autoExtractEquipped;
        set
        {
            autoExtractEquipped = value;
            if (value)
                AutoExtractAll = false;
        }
    }
    internal bool autoExtractAll = false;
    public bool AutoExtractAll
    {
        get => autoExtractAll;
        set
        {
            autoExtractAll = value;
            if (value)
                AutoExtractEquipped = false;
        }
    }
    internal bool autoDesynth = false;
    public bool AutoDesynth
    {
        get => autoDesynth;
        set
        {
            autoDesynth = value;
            if (value)
                AutoGCTurnin = false;
        }
    }
    internal bool autoGCTurnin = false;
    public bool AutoGCTurnin
    {
        get => autoGCTurnin;
        set
        {
            autoGCTurnin = value;
            if (value)
                AutoDesynth = false;
        }
    }
    public int AutoGCTurninSlotsLeft = 5;
    public bool AutoGCTurninSlotsLeftBool = false;
    public bool EnableAutoRetainer = false;
    public SummoningBellLocations PreferredSummoningBellEnum = 0;
    public bool AM = false;
    public bool UnhideAM = false;

    //Termination Config Options
    public bool StopLevel = false;
    public int StopLevelInt = 1;
    public bool StopNoRestedXP = false;
    public bool StopItemQty = false;
    public Dictionary<uint, KeyValuePair<string, int>> StopItemQtyItemDictionary = [];
    public int StopItemQtyInt = 1;
    public TerminationMode TerminationMethodEnum = TerminationMode.Do_Nothing;
    public bool TerminationKeepActive = true;

    //BMAI Config Options
    public bool HideBossModAIConfig = false;
    public bool FollowDuringCombat = true;
    public bool FollowDuringActiveBossModule = true;
    public bool FollowOutOfCombat = false;
    public bool FollowTarget = true;
    internal bool followSelf = true;
    public bool FollowSelf
    {
        get => followSelf;
        set
        {
            followSelf = value;
            if (value)
            {
                FollowSlot = false;
                FollowRole = false;
            }
        }
    }
    internal bool followSlot = false;
    public bool FollowSlot
    {
        get => followSlot;
        set
        {
            followSlot = value;
            if (value)
            {
                FollowSelf = false;
                FollowRole = false;
            }
        }
    }
    public int FollowSlotInt = 1;
    internal bool followRole = false;
    public bool FollowRole
    {
        get => followRole;
        set
        {
            followRole = value;
            if (value)
            {
                FollowSelf = false;
                FollowSlot = false;
                SchedulerHelper.ScheduleAction("FollowRoleBMRoleChecks", () => AutoDuty.Plugin.BMRoleChecks(), () => ObjectHelper.IsReady);
            }
        }
    }
    public Role FollowRoleEnum = Role.Healer;
    internal bool maxDistanceToTargetRoleBased = true;
    public bool MaxDistanceToTargetRoleBased
    {
        get => maxDistanceToTargetRoleBased;
        set
        {
            maxDistanceToTargetRoleBased = value;
            if (value)
                SchedulerHelper.ScheduleAction("MaxDistanceToTargetRoleBasedBMRoleChecks", () => AutoDuty.Plugin.BMRoleChecks(), () => ObjectHelper.IsReady);
        }
    }
    public int MaxDistanceToTarget = 3;
    public int MaxDistanceToTargetAoE = 12;
    public int MaxDistanceToSlot = 1;
    internal bool positionalRoleBased = true;
    public bool PositionalRoleBased
    {
        get => positionalRoleBased;
        set
        {
            positionalRoleBased = value;
            if (value)
                SchedulerHelper.ScheduleAction("PositionalRoleBasedBMRoleChecks", () => AutoDuty.Plugin.BMRoleChecks(), () => ObjectHelper.IsReady);
        }
    }
    public Positional PositionalEnum = Positional.Any;

    public void Save()
    {
        AutoDuty.PluginInterface.SavePluginConfig(this);
    }

    public TrustMemberName?[] SelectedTrustMembers = new TrustMemberName?[3];
}

public static class ConfigTab
{
    internal static string FollowName = "";

    private static Configuration Configuration = AutoDuty.Plugin.Configuration;

    private static Dictionary<uint, string> Items { get; set; } = Svc.Data.GetExcelSheet<Item>()?.Where(x => !x.Name.RawString.IsNullOrEmpty()).ToDictionary(x => x.RowId, x => x.Name.RawString)!;
    private static string stopItemQtyItemNameInput = "";
    private static KeyValuePair<uint, string> selectedItem = new(0, "");

    private static bool overlayHeaderSelected = false;
    private static bool dutyConfigHeaderSelected = false;
    private static bool advModeHeaderSelected = false;
    private static bool preLoopHeaderSelected = false;
    private static bool betweenLoopHeaderSelected = false;
    private static bool terminationHeaderSelected = false;

    public static void Draw()
    {
        if (MainWindow.CurrentTabName != "Config")
            MainWindow.CurrentTabName = "Config";
        
        //Start of Overlay Settings
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
        var overlayHeader = ImGui.Selectable("Overlay Settings", overlayHeaderSelected, ImGuiSelectableFlags.DontClosePopups);
        ImGui.PopStyleVar();      
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (overlayHeader)
            overlayHeaderSelected = !overlayHeaderSelected;

        if (overlayHeaderSelected == true)
        {
            ImGui.Columns(2, "##OverlayColumns", false);

            if (ImGui.Checkbox("Show Overlay", ref Configuration.showOverlay))
            {
                Configuration.ShowOverlay = Configuration.showOverlay;
                Configuration.Save();
            }
            using (var openOverlayDisable = ImRaii.Disabled(!Configuration.ShowOverlay))
            {
                ImGui.NextColumn();

                //ImGui.SameLine(0, 53);
                if (ImGui.Checkbox("Hide When Stopped", ref Configuration.hideOverlayWhenStopped))
                {
                    Configuration.HideOverlayWhenStopped = Configuration.hideOverlayWhenStopped;
                    Configuration.Save();
                }
                ImGui.NextColumn();
                if (ImGui.Checkbox("Lock Overlay", ref Configuration.lockOverlay))
                {
                    Configuration.LockOverlay = Configuration.lockOverlay;
                    Configuration.Save();
                }
                ImGui.NextColumn();
                //ImGui.SameLine(0, 57);
                if (ImGui.Checkbox("Use Transparent BG", ref Configuration.overlayNoBG))
                {
                    Configuration.OverlayNoBG = Configuration.overlayNoBG;
                    Configuration.Save();
                }
                ImGui.NextColumn();
                if (ImGui.Checkbox("Show Duty/Loops Text", ref Configuration.ShowDutyLoopText))
                    Configuration.Save();
                ImGui.NextColumn();
                if (ImGui.Checkbox("Show AD Action Text", ref Configuration.ShowActionText))
                    Configuration.Save();
                ImGui.NextColumn();
                if (ImGui.Checkbox("Use Slider Inputs", ref Configuration.UseSliderInputs))
                    Configuration.Save();
                ImGui.Columns(1);
            }

            if (ImGui.Checkbox("Show Main Window on Startup", ref Configuration.ShowMainWindowOnStartup))
                Configuration.Save();
        }

        //Start of Duty Config Settings
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
        var dutyConfigHeader = ImGui.Selectable("Duty Config Settings", dutyConfigHeaderSelected, ImGuiSelectableFlags.DontClosePopups);
        ImGui.PopStyleVar();
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (dutyConfigHeader)
            dutyConfigHeaderSelected = !dutyConfigHeaderSelected;

        if (dutyConfigHeaderSelected == true)
        {
            if (ImGui.Checkbox("Auto Leave Duty", ref Configuration.AutoExitDuty))
                Configuration.Save();
            ImGuiComponents.HelpMarker("Will automatically exit the dungeon upon completion of the path.");

            if (ImGui.Checkbox("Auto Manage Rotation Solver State", ref Configuration.AutoManageRSRState))
                Configuration.Save();
            ImGuiComponents.HelpMarker("Autoduty will enable RS Auto States at the start of each duty.");

            if (ImGui.Checkbox("Auto Manage BossMod AI Settings", ref Configuration.autoManageBossModAISettings))
                Configuration.Save();
            ImGuiComponents.HelpMarker("Autoduty will enable BMAI and any options you configure at the start of each duty.");
            ImGui.SameLine(0, 5);

            using (var autoManageBossModAISettingsDisable = ImRaii.Disabled(!Configuration.autoManageBossModAISettings))
            {
                if (ImGui.Button(Configuration.HideBossModAIConfig ? "Show" : "Hide"))
                {
                    Configuration.HideBossModAIConfig = !Configuration.HideBossModAIConfig;
                    Configuration.Save();
                }
            }

            if (ImGui.Checkbox("Loot Treasure Coffers", ref Configuration.LootTreasure))
                Configuration.Save();

            using (var lootTreasureDisabled = ImRaii.Disabled(!Configuration.LootTreasure))
            {
                ImGui.Text("Select Method: ");
                ImGui.SameLine(0, 5);
                ImGui.PushItemWidth(150 * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo("##ConfigLootMethod", EnumString(Configuration.LootMethodEnum)))
                {
                    foreach (LootMethod lootMethod in Enum.GetValues(typeof(LootMethod)))
                    {
                        using (var lootMethodAutoDutyDisabled = ImRaii.Disabled((lootMethod == LootMethod.Pandora && !PandorasBox_IPCSubscriber.IsEnabled) || (lootMethod == LootMethod.RotationSolver && !ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)))
                        {
                            if (ImGui.Selectable(EnumString(lootMethod)))
                            {
                                Configuration.LootMethodEnum = lootMethod;
                                Configuration.Save();
                            }
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGuiComponents.HelpMarker("RSR Toggles Not Yet Implemented");
                using (var lootMethodAutoDutyDisabled = ImRaii.Disabled(Configuration.LootMethodEnum != LootMethod.AutoDuty))
                {
                    if (ImGui.Checkbox("Loot Boss Treasure Only", ref Configuration.LootBossTreasureOnly))
                        Configuration.Save();
                }
            }
            ImGuiComponents.HelpMarker("AutoDuty will ignore all non-boss chests, and only loot boss chests. (Only works with AD Looting)");         
            /*/
        disabled for now
            using (var d0 = ImRaii.Disabled(true))
            {
                if (ImGui.SliderInt("Scan Distance", ref treasureCofferScanDistance, 1, 100))
                {
                    Configuration.TreasureCofferScanDistance = treasureCofferScanDistance;
                    Configuration.Save();
                }
            }*/
            ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
            var advModeHeader = ImGui.Selectable("Advanced Config Options", advModeHeaderSelected, ImGuiSelectableFlags.DontClosePopups);
            ImGui.PopStyleVar();
            if (ImGui.IsItemHovered())
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (advModeHeader)
                advModeHeaderSelected = !advModeHeaderSelected;

            if (advModeHeaderSelected == true)
            {
                if (ImGui.Checkbox("Using Alternative Rotation Plugin", ref Configuration.UsingAlternativeRotationPlugin))
                    Configuration.Save();
                ImGuiComponents.HelpMarker("You are deciding to use a plugin other than Rotation Solver.");

                if (ImGui.Checkbox("Using Alternative Movement Plugin", ref Configuration.UsingAlternativeMovementPlugin))
                    Configuration.Save();
                ImGuiComponents.HelpMarker("You are deciding to use a plugin other than Vnavmesh.");

                if (ImGui.Checkbox("Using Alternative Boss Plugin", ref Configuration.UsingAlternativeBossPlugin))
                    Configuration.Save();
                ImGuiComponents.HelpMarker("You are deciding to use a plugin other than BossMod/BMR.");
            }
        }

        //Start of Pre-Loop Settings
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
        var preLoopHeader = ImGui.Selectable("Pre-Loop Initialization Settings", preLoopHeaderSelected, ImGuiSelectableFlags.DontClosePopups);
        ImGui.PopStyleVar();
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (preLoopHeader)
            preLoopHeaderSelected = !preLoopHeaderSelected;

        if (preLoopHeaderSelected == true)
        {
            if (ImGui.Checkbox("Retire To ", ref Configuration.RetireMode))
                Configuration.Save();

            using (var d1 = ImRaii.Disabled(!Configuration.RetireMode))
            {
                ImGui.SameLine(0, 5);
                ImGui.PushItemWidth(125 * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo(" Before Each Loop", EnumString(Configuration.RetireLocationEnum)))
                {
                    foreach (RetireLocation retireLocation in Enum.GetValues(typeof(RetireLocation)))
                    {
                        if (ImGui.Selectable(EnumString(retireLocation)))
                        {
                            Configuration.RetireLocationEnum = retireLocation;
                            Configuration.Save();
                        }
                    }
                    ImGui.EndCombo();
                }
            }
            if (ImGui.Checkbox("Auto Equip Recommended Gear", ref Configuration.AutoEquipRecommendedGear))
                Configuration.Save();
            ImGuiComponents.HelpMarker("Uses Gear from Armory Chest Only");
            if (ImGui.Checkbox("Auto Consume Boiled Eggs", ref Configuration.AutoBoiledEgg))
                Configuration.Save();
            ImGuiComponents.HelpMarker("Will use Boiled Eggs in inventory for +3% Exp.");

            if (ImGui.Checkbox("Auto Repair", ref Configuration.AutoRepair)) 
                Configuration.Save();

            using (ImRaii.Disabled(!Configuration.AutoRepair))
            {
                ImGui.SameLine();

                if (ImGui.RadioButton("Self", Configuration.AutoRepairSelf))
                {
                    Configuration.AutoRepairSelf = true;
                    Configuration.Save();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("Will use DarkMatter to Self Repair (Requires Leveled Crafters!)");
                ImGui.SameLine();
                
                if (ImGui.RadioButton("CityNpc", !Configuration.AutoRepairSelf))
                {
                    Configuration.AutoRepairSelf = false;
                    Configuration.Save();
                }
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("Will use Npc near Inn to Repair.");
            }

            using (var autoRepairDisable = ImRaii.Disabled(!Configuration.AutoRepair))
            {
                ImGui.Indent();
                ImGui.Text("Trigger @");
                ImGui.SameLine();
                ImGui.PushItemWidth(150 * ImGuiHelpers.GlobalScale);
                if (ImGui.SliderInt("##Repair@", ref Configuration.AutoRepairPct, 1, 99, "%d%%"))
                    Configuration.Save();
                ImGui.PopItemWidth();
                ImGui.Unindent();
            }
        }

        //Between Loop Settings
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
        var betweenLoopHeader = ImGui.Selectable("Between Loop Settings", betweenLoopHeaderSelected, ImGuiSelectableFlags.DontClosePopups);
        ImGui.PopStyleVar();
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (betweenLoopHeader)
            betweenLoopHeaderSelected = !betweenLoopHeaderSelected;

        if (betweenLoopHeaderSelected == true)
        {
            ImGui.PushItemWidth(100 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt("(s) Wait time between loops", ref Configuration.WaitTimeBeforeAfterLoopActions))
            {
                if (Configuration.WaitTimeBeforeAfterLoopActions < 0) Configuration.WaitTimeBeforeAfterLoopActions = 0;
                Configuration.Save();
            }
            ImGui.PopItemWidth();
            ImGuiComponents.HelpMarker("Will delay all AutoDuty between-loop Processes for X seconds.");
            ImGui.Separator();
            if (ImGui.Checkbox("Auto Extract", ref Configuration.AutoExtract))
                Configuration.Save();
            ImGui.SameLine(0, 10);
            using (var d1 = ImRaii.Disabled(!Configuration.AutoExtract))
            {
                if (ImGui.Checkbox("Extract Equipped", ref Configuration.autoExtractEquipped))
                {
                    Configuration.AutoExtractEquipped = Configuration.autoExtractEquipped;
                    Configuration.Save();
                }

                ImGui.SameLine(0, 5);
                if (ImGui.Checkbox("Extract All", ref Configuration.autoExtractAll))
                {
                        Configuration.AutoExtractAll = Configuration.autoExtractAll;
                        Configuration.Save();
                }
            }
            if (ImGui.Checkbox("Auto Desynth", ref Configuration.autoDesynth))
            {
                Configuration.AutoDesynth = Configuration.autoDesynth;
                Configuration.Save();
            }
            ImGui.SameLine(0, 5);
            using (var autoGcTurninDisabled = ImRaii.Disabled(!Deliveroo_IPCSubscriber.IsEnabled))
            {
                if (ImGui.Checkbox("Auto GC Turnin", ref Configuration.autoGCTurnin))
                {
                    Configuration.AutoGCTurnin = Configuration.autoGCTurnin;
                    Configuration.Save();
                }
                using (var autoGcTurninConfigDisabled = ImRaii.Disabled(!Configuration.AutoGCTurnin))
                {
                    if (ImGui.Checkbox("Inventory Slots Left @", ref Configuration.AutoGCTurninSlotsLeftBool))
                        Configuration.Save();
                    ImGui.SameLine(0);
                    using (var autoGcTurninSlotsLeftDisabled = ImRaii.Disabled(!Configuration.AutoGCTurninSlotsLeftBool))
                    {
                        ImGui.PushItemWidth(125 * ImGuiHelpers.GlobalScale);
                        if (Configuration.UseSliderInputs)
                        {
                            if (ImGui.SliderInt("##Slots", ref Configuration.AutoGCTurninSlotsLeft, 1, 140))
                                Configuration.Save();
                        }
                        else
                        {
                            if (Configuration.AutoGCTurninSlotsLeft < 0) Configuration.AutoGCTurninSlotsLeft = 0;
                            else if (Configuration.AutoGCTurninSlotsLeft > 140) Configuration.AutoGCTurninSlotsLeft = 140;
                            if (ImGui.InputInt("##Slots", ref Configuration.AutoGCTurninSlotsLeft))
                                Configuration.Save();
                        }
                        ImGui.PopItemWidth();
                    }
                }
            }
            if (!Deliveroo_IPCSubscriber.IsEnabled)
            {
                if (Configuration.AutoGCTurnin)
                {
                    Configuration.AutoGCTurnin = false;
                    Configuration.Save();
                }
                ImGui.Text("* Auto GC Turnin Requires Deliveroo plugin");
                ImGui.Text("Get @ ");
                ImGui.SameLine(0, 0);
                ImGuiEx.TextCopy(ImGuiHelper.LinkColor, @"https://plugins.carvel.li");
            }
            using (var autoRetainerDisabled = ImRaii.Disabled(!AutoRetainer_IPCSubscriber.IsEnabled))
            {
                if (ImGui.Checkbox("Enable AutoRetainer Integration", ref Configuration.EnableAutoRetainer))
                    Configuration.Save();
            }

            if (!AutoRetainer_IPCSubscriber.IsEnabled)
            {
                if (Configuration.EnableAutoRetainer)
                {
                    Configuration.EnableAutoRetainer = false;
                    Configuration.Save();
                }
                ImGui.Text("* AutoRetainer requires a plugin");
                ImGui.Text("Visit ");
                ImGui.SameLine(0, 0);
                ImGuiEx.TextCopy(ImGuiHelper.LinkColor, @"https://puni.sh/plugin/AutoRetainer");
            }
            if (Configuration.UnhideAM)
            {
                ImGui.SameLine(0, 5);
                if (ImGui.Checkbox("AM", ref Configuration.AM))
                {
                    if (!AM_IPCSubscriber.IsEnabled)
                        MainWindow.ShowPopup("DISCLAIMER", "AM Requires a plugin - Visit https://discord.gg/JzSxThjKnd\nDO NOT ASK ABOUT OR DISCUSS THIS OPTION IN PUNI.SH DISCORD\nYOU HAVE BEEN WARNED!!!!!!!");
                    else if (Configuration.AM)
                        MainWindow.ShowPopup("DISCLAIMER", "By enabling the usage of this option, you are agreeing to NEVER discuss this option within the Puni.sh Discord or to anyone in Puni.sh! \nYou have been warned!!!");
                    Configuration.Save();
                }
                ImGuiComponents.HelpMarker("By enabling the usage of this option, you are agreeing to NEVER discuss this option within the Puni.sh Discord or to anyone in Puni.sh! You have been warned!!!");
            }

            if (Configuration.UnhideAM && !AM_IPCSubscriber.IsEnabled)
            {
                ImGui.SameLine(0, 5);
                if (Configuration.AM)
                {
                    Configuration.AM = false;
                    Configuration.Save();
                }
                ImGui.Text("* AM Requires a plugin");
                ImGui.Text("Visit ");
                ImGui.SameLine(0, 0);
                ImGuiEx.TextCopy(ImGuiHelper.LinkColor, @"https://discord.gg/JzSxThjKnd");
                ImGui.Text("DO NOT ASK ABOUT OR DISCUSS THIS OPTION WITHIN THE PUNI.SH DISCORD");
                ImGui.Text("YOU HAVE BEEN WARNED!!!!!!!");
            }
            if (Configuration.EnableAutoRetainer || Configuration.AM)
            {
                ImGui.Text("Preferred Summoning Bell Location: ");
                ImGuiComponents.HelpMarker("No matter what location is chosen, if there is a summoning bell in the location you are in when this is invoked it will go there instead");
                if (ImGui.BeginCombo("##PreferredBell", EnumString(Configuration.PreferredSummoningBellEnum)))
                {
                    foreach (SummoningBellLocations summoningBells in Enum.GetValues(typeof(SummoningBellLocations)))
                    {
                        if (ImGui.Selectable(EnumString(summoningBells)))
                        {
                            Configuration.PreferredSummoningBellEnum = summoningBells;
                            Configuration.Save();
                        }
                    }
                    ImGui.EndCombo();
                }
            }
        }

        //Loop Termination Settings
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
        var terminationHeader = ImGui.Selectable("Loop Termination Settings", terminationHeaderSelected, ImGuiSelectableFlags.DontClosePopups);
        ImGui.PopStyleVar();
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (terminationHeader)
            terminationHeaderSelected = !terminationHeaderSelected;
        if (terminationHeaderSelected == true)
        {
            ImGui.Separator();

            if (ImGui.Checkbox("Stop Looping @ Level", ref Configuration.StopLevel))

                Configuration.Save();

            using (var stopLevelDisabled = ImRaii.Disabled(!Configuration.StopLevel))
            {
                ImGui.SameLine(0, 10);
                ImGui.PushItemWidth(100 * ImGuiHelpers.GlobalScale);
                if (Configuration.UseSliderInputs)
                {
                    if (ImGui.SliderInt("##Level", ref Configuration.StopLevelInt, 1, 100))
                        Configuration.Save();
                }
                else
                {
                    if (ImGui.InputInt("##Level", ref Configuration.StopLevelInt))
                    {
                        if (Configuration.StopLevelInt < 1) Configuration.StopLevelInt = 1;
                        else if (Configuration.StopLevelInt > 100) Configuration.StopLevelInt = 100;
                        Configuration.Save();
                    }
                }
                ImGui.PopItemWidth();
            }
            ImGuiComponents.HelpMarker("Note that Loop Number takes precedence over this option!");
            if (ImGui.Checkbox("Stop When No Rested XP", ref Configuration.StopNoRestedXP))
                Configuration.Save();

            ImGuiComponents.HelpMarker("Note that Loop Number takes precedence over this option!");
            if (ImGui.Checkbox("Stop Looping When Reach Item Qty", ref Configuration.StopItemQty))
                Configuration.Save();

            ImGuiComponents.HelpMarker("Note that Loop Number takes precedence over this option!");
            using (var stopItemQtyDisabled = ImRaii.Disabled(!Configuration.StopItemQty))
            {
                ImGui.PushItemWidth(250 * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo("Select Item", selectedItem.Value))
                {
                    ImGui.InputTextWithHint("Item Name", "Start typing item name to search", ref stopItemQtyItemNameInput, 1000);
                    foreach (var item in Items.Where(x => x.Value.Contains(stopItemQtyItemNameInput, StringComparison.InvariantCultureIgnoreCase))!)
                    {
                        if (ImGui.Selectable($"{item.Value}"))
                            selectedItem = item;
                    }
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();
                ImGui.PushItemWidth(190 * ImGuiHelpers.GlobalScale);
                if (ImGui.InputInt("Quantity", ref Configuration.StopItemQtyInt))
                    Configuration.Save();

                ImGui.SameLine(0, 5);
                using (var addDisabled = ImRaii.Disabled(selectedItem.Value.IsNullOrEmpty()))
                {
                    if (ImGui.Button("Add Item"))
                    {
                        if (!Configuration.StopItemQtyItemDictionary.TryAdd(selectedItem.Key, new(selectedItem.Value, Configuration.StopItemQtyInt)))
                        {
                            Configuration.StopItemQtyItemDictionary.Remove(selectedItem.Key);
                            Configuration.StopItemQtyItemDictionary.Add(selectedItem.Key, new(selectedItem.Value, Configuration.StopItemQtyInt));
                        }
                        Configuration.Save();
                    }
                }
                ImGui.PopItemWidth();
                if (!ImGui.BeginListBox("##ItemList", new System.Numerics.Vector2(325 * ImGuiHelpers.GlobalScale, 80 * ImGuiHelpers.GlobalScale))) return;

                foreach (var item in Configuration.StopItemQtyItemDictionary)
                {
                    ImGui.Selectable($"{item.Value.Key} (Qty: {item.Value.Value})");
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        Configuration.StopItemQtyItemDictionary.Remove(item);
                        Configuration.Save();
                    }
                }
                ImGui.EndListBox();
            }
            ImGui.Text("On Completion of All Loops: ");
            ImGui.SameLine(0, 10);
            ImGui.PushItemWidth(150 * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo("##ConfigTerminationMethod", EnumString(Configuration.TerminationMethodEnum)))
            {
                foreach (TerminationMode terminationMode in Enum.GetValues(typeof(TerminationMode)))
                {
                    if (terminationMode != TerminationMode.Kill_PC || OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                        if (ImGui.Selectable(EnumString(terminationMode)))
                        {
                            Configuration.TerminationMethodEnum = terminationMode;
                            Configuration.Save();
                        }
                }
                ImGui.EndCombo();
            }

            if (Configuration.TerminationMethodEnum is TerminationMode.Kill_Client or TerminationMode.Kill_PC or TerminationMode.Logout)
            {
                ImGui.Indent();
                if(ImGui.Checkbox("Keep Termination option after execution ", ref Configuration.TerminationKeepActive))
                    Configuration.Save();
                ImGui.Unindent();
            }
        }     
    }

    //BossModConfig
    public static class BossModConfigTab
    {
        private static Configuration Configuration = AutoDuty.Plugin.Configuration;

        public static void Draw()
        {
            if (MainWindow.CurrentTabName != "BossModConfig")
                MainWindow.CurrentTabName = "BossModConfig";
            var followRole = Configuration.FollowRole;
            var maxDistanceToTargetRoleBased = Configuration.MaxDistanceToTargetRoleBased;
            var maxDistanceToTarget = Configuration.MaxDistanceToTarget;
            var maxDistanceToTargetAoE = Configuration.MaxDistanceToTargetAoE;
            var positionalRoleBased = Configuration.PositionalRoleBased;

            if (ImGui.Checkbox("Follow During Combat", ref Configuration.FollowDuringCombat))
                Configuration.Save();

            if (ImGui.Checkbox("Follow During Active BossModule", ref Configuration.FollowDuringActiveBossModule))
                Configuration.Save();

            if (ImGui.Checkbox("Follow Out Of Combat (Not Recommended)", ref Configuration.FollowOutOfCombat))
                Configuration.Save();

            if (ImGui.Checkbox("Follow Target", ref Configuration.FollowTarget))
                Configuration.Save();

            if (ImGui.Checkbox("Follow Self", ref Configuration.followSelf))
            {
                Configuration.FollowSelf = Configuration.followSelf;
                Configuration.Save();
            }

            if (ImGui.Checkbox("Follow Slot", ref Configuration.followSlot))
            {
                Configuration.FollowSlot = Configuration.followSlot;
                Configuration.Save();
            }

            using (var d1 = ImRaii.Disabled(!Configuration.followSlot))
            {
                ImGui.PushItemWidth(270);
                if (ImGui.SliderInt("Follow Slot #", ref Configuration.FollowSlotInt, 1, 4))
                    Configuration.Save();
                ImGui.PopItemWidth();
            }

            if (ImGui.Checkbox("Follow Role", ref Configuration.followRole))
            {
                Configuration.FollowRole = Configuration.followRole;
                Configuration.Save();
            }

            using (var d1 = ImRaii.Disabled(!followRole))
            {
                ImGui.SameLine(0, 10);
                if (ImGui.Button(EnumString(Configuration.FollowRoleEnum)))
                {
                    ImGui.OpenPopup("RolePopup");
                }
                if (ImGui.BeginPopup("RolePopup"))
                {
                    foreach (Role role in Enum.GetValues(typeof(Role)))
                    {
                        if (ImGui.Selectable(EnumString(role)))
                        {
                            Configuration.FollowRoleEnum = role;
                            Configuration.Save();
                        }
                    }
                    ImGui.EndPopup();
                }
            }

            if (ImGui.Checkbox("Set Max Distance To Target Based on Role", ref Configuration.maxDistanceToTargetRoleBased))
            {
                Configuration.MaxDistanceToTargetRoleBased = Configuration.maxDistanceToTargetRoleBased;
                Configuration.Save();
            }

            using (var d1 = ImRaii.Disabled(Configuration.MaxDistanceToTargetRoleBased))
            {
                ImGui.PushItemWidth(195);
                if (ImGui.SliderInt("Max Distance To Target", ref Configuration.MaxDistanceToTarget, 1, 30))
                    Configuration.Save();
                if (ImGui.SliderInt("Max Distance To Target AoE", ref Configuration.MaxDistanceToTargetAoE, 1, 10))
                    Configuration.Save();
                ImGui.PopItemWidth();
            }

            ImGui.PushItemWidth(195);
            if (ImGui.SliderInt("Max Distance To Slot", ref Configuration.MaxDistanceToSlot, 1, 30))
                Configuration.Save();

            ImGui.PopItemWidth();

            if (ImGui.Checkbox("Set Positional Based on Role", ref Configuration.positionalRoleBased))
            {
                Configuration.PositionalRoleBased = Configuration.positionalRoleBased;
                AutoDuty.Plugin.BMRoleChecks();
                Configuration.Save();
            }

            using (var d1 = ImRaii.Disabled(Configuration.positionalRoleBased))
            {
                ImGui.SameLine(0, 10);
                if (ImGui.Button(EnumString(Configuration.PositionalEnum)))
                    ImGui.OpenPopup("PositionalPopup");

                if (ImGui.BeginPopup("PositionalPopup"))
                {
                    foreach (Positional positional in Enum.GetValues(typeof(Positional)))
                    {
                        if (ImGui.Selectable(EnumString(positional)))
                        {
                            Configuration.PositionalEnum = positional;
                            Configuration.Save();
                        }
                    }
                    ImGui.EndPopup();
                }
            }
        }
    }
}
