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
using static System.Net.WebRequestMethods;



namespace AutoDuty.Windows;

using ECommons.Automation.LegacyTaskManager;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Common.Math;
using Helpers;

[Serializable]
public class Configuration : IPluginConfiguration
{
    //Meta
    public int Version { get => 114; set { } }
    public HashSet<string> DoNotUpdatePathFiles { get; set; } = [];
    public Dictionary<uint, Dictionary<Job, int>> PathSelections { get; set; } = [];


    //General Options
    public int LoopTimes = 1;
    public bool Support { get; set; } = false;
    public bool Trust { get; set; } = false;
    public bool Squadron { get; set; } = false;
    public bool Regular { get; set; } = false;
    public bool Trial { get; set; } = false;
    public bool Raid { get; set; } = false;
    public bool Variant { get; set; } = false;
    public bool Unsynced { get; set; } = false;
    public bool HideUnavailableDuties { get; set; } = false;

    //Overlay Config Options
    public bool ShowOverlay { get; set; } = true;
    public bool HideOverlayWhenStopped { get; set; } = false;
    public bool LockOverlay = false;
    public bool OverlayNoBG = false;
    public bool ShowDutyLoopText { get; set; } = true;
    public bool ShowActionText { get; set; } = true;
    public bool UseSliderInputs = false;

    //Duty Config Options
    public bool AutoExitDuty { get; set; } = true;
    public bool AutoManageRSRState { get; set; } = true;
    public bool AutoManageBossModAISettings { get; set; } = true;
    public bool LootTreasure { get; set; } = true;
    public string LootMethod { get; set; } = "AutoDuty";
    public bool LootBossTreasureOnly { get; set; } = true;
    public int TreasureCofferScanDistance { get; set; } = 25;
    public bool UsingAlternativeRotationPlugin = false;
    public bool UsingAlternativeMovementPlugin = false;
    public bool UsingAlternativeBossPlugin = false;

    //PreLoop Config Options
    public bool RetireMode { get; set; } = false;
    public string RetireLocation { get; set; } = "Inn";
    public bool RetireToInnBeforeLoops { get; set; } = true;
    public bool RetireToBarracksBeforeLoops { get; set; } = false;
    public bool RetireToHomeBeforeLoops { get; set; } = false;
    public bool RetireToFCBeforeLoops { get; set; } = false;
    public bool AutoEquipRecommendedGear { get; set; } = false;
    public bool AutoBoiledEgg = false;
    public bool AutoRepair { get; set; } = false;
    public int AutoRepairPct { get; set; } = 50;
    public bool AutoRepairSelf { get; set; } = false;
    public bool AutoRepairCity { get; set; } = true;

    //Between Loop Config Options
    public int WaitTimeBeforeAfterLoopActions = 0;
    public bool AutoExtract { get; set; } = false;
    public bool AutoExtractEquipped { get; set; } = true;
    public bool AutoExtractAll { get; set; } = false;
    public bool AutoDesynth { get; set; } = false;
    public bool AutoGCTurnin { get; set; } = false;
    public int AutoGCTurninSlotsLeft = 5;
    public bool AutoGCTurninSlotsLeftBool = false;
    public bool EnableAutoRetainer = false;
    public bool AM = false;
    public bool UnhideAM = false;

    //Termination Config Options
    public bool StopLevel { get; set; } = false;
    public int StopLevelInt { get; set; } = 1;
    public bool StopNoRestedXP { get; set; } = false;
    public bool StopItemQty { get; set; } = false;
    public Dictionary<uint, KeyValuePair<string, int>> StopItemQtyItemDictionary { get; set; } = [];
    public int StopItemQtyInt { get; set; } = 1;
    public string TerminationMethod { get; set; } = "Do Nothing";
    public bool AutoLogout { get; set; } = false;
    public bool AutoARMultiEnable { get; set; } = false;
    public bool AutoKillClient { get; set; } = false;

    //BMAI Config Options
    public bool HideBossModAIConfig { get; set; } = false;
    public bool FollowDuringCombat { get; set; } = true;
    public bool FollowDuringActiveBossModule { get; set; } = true;
    public bool FollowOutOfCombat { get; set; } = false;
    public bool FollowTarget { get; set; } = true;
    public bool FollowSelf { get; set; } = true;
    public bool FollowSlot { get; set; } = false;
    public int FollowSlotInt { get; set; } = 1;
    public bool FollowRole { get; set; } = false;
    public string FollowRoleStr { get; set; } = "Healer";
    public bool MaxDistanceToTargetRoleRange { get; set; } = true;
    public int MaxDistanceToTarget { get; set; } = 3;
    public int MaxDistanceToTargetAoE { get; set; } = 12;
    public int MaxDistanceToSlot { get; set; } = 1;
    public bool PositionalRoleBased { get; set; } = true;
    public string PositionalCustom { get; set; } = "Any";

    public void Save()
    {
        AutoDuty.PluginInterface.SavePluginConfig(this);
    }

    public TrustMember?[] SelectedTrusts = new TrustMember?[3];
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
    private static string lootMethodAD = "AutoDuty";
    private static string lootMethodRSR = "Rotation Solver";
    private static string lootMethodPandora = "Pandora";
    private static string lootMethodAll = "All";

    public static void Draw()
    {
        if (MainWindow.CurrentTabName != "Config")
            MainWindow.CurrentTabName = "Config";
        
        //OverlaySettings
        var showOverlay = Configuration.ShowOverlay;
        var hideOverlayWhenStopped = Configuration.HideOverlayWhenStopped;
        var showDutyLoopText = Configuration.ShowDutyLoopText;
        var showActionText = Configuration.ShowActionText;

        //DutySettings
        var autoExitDuty = Configuration.AutoExitDuty;
        var autoManageRSRState = Configuration.AutoManageRSRState;
        var autoManageBossModAISettings = Configuration.AutoManageBossModAISettings;
        var lootTreasure = Configuration.LootTreasure;
        var lootMethod = Configuration.LootMethod;
        var lootBossTreasureOnly = Configuration.LootBossTreasureOnly;
        var treasureCofferScanDistance = Configuration.TreasureCofferScanDistance;

        //PreLoopSettings
        var autoEquipRecommended = Configuration.AutoEquipRecommendedGear;
        var autoRepair = Configuration.AutoRepair;
        var autoRepairSelf = Configuration.AutoRepairSelf;
        var autoRepairCity = Configuration.AutoRepairCity;
        var autoRepairPct = Configuration.AutoRepairPct;
        var retireMode = Configuration.RetireMode;
        var retireLocation = Configuration.RetireLocation;
        var retireToInnBeforeLoops = Configuration.RetireToInnBeforeLoops;
        var retireToBarracksBeforeLoops = Configuration.RetireToBarracksBeforeLoops;
        var retireToHomeBeforeLoops = Configuration.RetireToHomeBeforeLoops;
        var retireToFCBeforeLoops = Configuration.RetireToFCBeforeLoops;

        //BetweenLoopSettings
        var autoExtract = Configuration.AutoExtract;
        var autoExtractEquipped = Configuration.AutoExtractEquipped;
        var autoExtractAll = Configuration.AutoExtractAll;
        var autoDesynth = Configuration.AutoDesynth;
        var autoGCTurnin = Configuration.AutoGCTurnin;

        //BossModAISettings
        var hideBossModAIConfig = Configuration.HideBossModAIConfig;
        var stopLevel = Configuration.StopLevel;
        var stopLevelInt = Configuration.StopLevelInt;
        var stopNoRestedXP = Configuration.StopNoRestedXP;
        var stopItemQty = Configuration.StopItemQty;
        var stopItemQtyItemDictionary = Configuration.StopItemQtyItemDictionary;
        var stopItemQtyInt = Configuration.StopItemQtyInt;

        //LoopTerminationSettings
        var terminationMethod = Configuration.TerminationMethod;


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
            if (ImGui.Checkbox("Show Overlay", ref showOverlay))
            {
                AutoDuty.Plugin.Overlay.IsOpen = showOverlay;
                Configuration.ShowOverlay = showOverlay;
                Configuration.Save();
            }

            using (var openOverlayDisable = ImRaii.Disabled(!showOverlay))
            {
                ImGui.SameLine(0, 53);
                if (ImGui.Checkbox("Hide When Stopped", ref hideOverlayWhenStopped))
                {
                    if (hideOverlayWhenStopped && !AutoDuty.Plugin.Running && !AutoDuty.Plugin.Started)
                        AutoDuty.Plugin.Overlay.IsOpen = false;
                    else
                        AutoDuty.Plugin.Overlay.IsOpen = true;
                    Configuration.HideOverlayWhenStopped = hideOverlayWhenStopped;
                    Configuration.Save();
                }

                if (ImGui.Checkbox("Lock Overlay", ref Configuration.LockOverlay))
                {
                    if (!Configuration.LockOverlay)
                        AutoDuty.Plugin.Overlay.Flags -= ImGuiWindowFlags.NoMove;
                    else
                        AutoDuty.Plugin.Overlay.Flags |= ImGuiWindowFlags.NoMove;

                    Configuration.Save();
                }
                ImGui.SameLine(0, 57);
                if (ImGui.Checkbox("Use Transparent BG", ref Configuration.OverlayNoBG))
                {
                    if (!Configuration.OverlayNoBG)
                        AutoDuty.Plugin.Overlay.Flags -= ImGuiWindowFlags.NoBackground;
                    else
                        AutoDuty.Plugin.Overlay.Flags |= ImGuiWindowFlags.NoBackground;

                    Configuration.Save();
                }

                if (ImGui.Checkbox("Show Duty/Loops Text", ref showDutyLoopText))
                {
                    Configuration.ShowDutyLoopText = showDutyLoopText;
                    Configuration.Save();
                }

                ImGui.SameLine(0, 5);
                if (ImGui.Checkbox("Show AD Action Text", ref showActionText))
                {
                    Configuration.ShowActionText = showActionText;
                    Configuration.Save();
                }
                if (ImGui.Checkbox("Use Slider Inputs", ref Configuration.UseSliderInputs))
                    Configuration.Save();
            }
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
            if (ImGui.Checkbox("Auto Leave Duty", ref autoExitDuty))
            {
                Configuration.AutoExitDuty = autoExitDuty;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Will automatically exit the dungeon upon completion of the path.");
            if (ImGui.Checkbox("Auto Manage Rotation Solver State", ref autoManageRSRState))
            {
                Configuration.AutoManageRSRState = autoManageRSRState;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Autoduty will enable RS Auto States at the start of each duty.");
            if (ImGui.Checkbox("Auto Manage BossMod AI Settings", ref autoManageBossModAISettings))
            {
                Configuration.AutoManageBossModAISettings = autoManageBossModAISettings;
                hideBossModAIConfig = !autoManageBossModAISettings;
                Configuration.HideBossModAIConfig = hideBossModAIConfig;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Autoduty will enable BMAI and any options you configure at the start of each duty.");
            ImGui.SameLine(0, 5);
            using (var autoManageBossModAISettingsDisable = ImRaii.Disabled(!autoManageBossModAISettings))
            {
                if (ImGui.Button(hideBossModAIConfig ? "Show" : "Hide"))
                {
                    hideBossModAIConfig = !hideBossModAIConfig;
                    Configuration.HideBossModAIConfig = hideBossModAIConfig;
                    Configuration.Save();
                }
            }
            if (ImGui.Checkbox("Loot Treasure Coffers", ref lootTreasure))
            {
                Configuration.LootTreasure = lootTreasure;
                Configuration.Save();
                if (Configuration.LootTreasure == false && PandorasBox_IPCSubscriber.IsEnabled)
                {
                    PandorasBox_IPCSubscriber.SetFeatureEnabled("Automatically Open Chests", false);
                }
                if (Configuration.LootTreasure == false && ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
                {
                    //NYI
                }
            }
            using (var d1 = ImRaii.Disabled(!lootTreasure))
            {
                ImGui.Text("Select Method: ");
                ImGui.SameLine(0, 5);
                ImGui.PushItemWidth(150 * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo(" ", lootMethod))
                {
                    if (ImGui.Selectable($"{lootMethodAD}"))
                    {
                        Configuration.LootMethod = lootMethodAD;
                        {
                            if (PandorasBox_IPCSubscriber.IsEnabled)
                            {
                                PandorasBox_IPCSubscriber.SetFeatureEnabled("Automatically Open Chests", false);
                            }
                            if (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled) 
                            {
                                //NYI 
                            }
                        }
                        Svc.Log.Info(lootMethod);
                        Configuration.Save();
                    }
                    else if (ImGui.Selectable($"{lootMethodRSR}"))
                    {
                        Configuration.LootMethod = lootMethodRSR;
                        {
                            if (PandorasBox_IPCSubscriber.IsEnabled)
                            {
                                PandorasBox_IPCSubscriber.SetFeatureEnabled("Automatically Open Chests", false);
                            }
                            if (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
                            {
                                //NYI
                            }
                        }
                        Svc.Log.Info(lootMethod);
                        Configuration.Save();
                    }
                    else if (ImGui.Selectable($"{lootMethodPandora}"))
                    {
                        Configuration.LootMethod = lootMethodPandora;
                        {
                            if (PandorasBox_IPCSubscriber.IsEnabled)
                            {
                                PandorasBox_IPCSubscriber.SetFeatureEnabled("Automatically Open Chests", true);
                                PandorasBox_IPCSubscriber.SetConfigEnabled("Automatically Open Chests", "OpenInHighEndDuty", true);
                            }
                            if (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
                            {
                                //NYI
                            }
                        }
                        Svc.Log.Info(lootMethod);
                        Configuration.Save();
                    }
                    else if (ImGui.Selectable($"{lootMethodAll}"))
                    {
                        Configuration.LootMethod = lootMethodAll;
                        {
                            if (PandorasBox_IPCSubscriber.IsEnabled)
                            {
                                PandorasBox_IPCSubscriber.SetFeatureEnabled("Automatically Open Chests", true);
                                PandorasBox_IPCSubscriber.SetConfigEnabled("Automatically Open Chests", "OpenInHighEndDuty", true);
                            }
                            if (ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
                            {
                                //NYI
                            }
                        }
                        Svc.Log.Info(lootMethod);
                        Configuration.Save();
                    }
                    ImGui.EndCombo();
                }
                ImGuiComponents.HelpMarker("RSR Toggles Not Yet Implemented");
                using (var d2 = ImRaii.Disabled(lootMethod != "AutoDuty"))
                {

                    if (ImGui.Checkbox("Loot Boss Treasure Only", ref lootBossTreasureOnly))
                    {
                        Configuration.LootBossTreasureOnly = lootBossTreasureOnly;
                        Configuration.Save();
                    }
                }
            }
            ImGuiComponents.HelpMarker("AutoDuty will ignore all non-boss chests, and only loot boss chests. (Only works with AD Looting)");         
            /*/disabled for now
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
            if (ImGui.Checkbox("Retire To ", ref retireMode))
            {
                Configuration.RetireMode = retireMode;
                Configuration.Save();
            }
            using (var d1 = ImRaii.Disabled(!retireMode))
            {
                ImGui.SameLine(0, 5);
                ImGui.PushItemWidth(125 * ImGuiHelpers.GlobalScale);
                if (ImGui.BeginCombo(" Before Each Loop", retireLocation))
                {
                    if (ImGui.Selectable($"Inn"))
                    {
                        Configuration.RetireLocation = "Inn";
                        Configuration.RetireToInnBeforeLoops = retireToInnBeforeLoops;
                        Configuration.RetireToBarracksBeforeLoops = false;
                        Configuration.RetireToHomeBeforeLoops = false;
                        Configuration.RetireToFCBeforeLoops = false;
                        Configuration.Save();
                    }
                    else if (ImGui.Selectable($"GC Barracks"))
                    {
                        Configuration.RetireLocation = "GC Barracks";
                        Configuration.RetireToInnBeforeLoops = false;
                        Configuration.RetireToBarracksBeforeLoops = retireToBarracksBeforeLoops;
                        Configuration.RetireToHomeBeforeLoops = false;
                        Configuration.RetireToFCBeforeLoops = false;
                        Configuration.Save();
                    }
                    /* Not Yet Implemented
                    else if (ImGui.Selectable($"Private Home"))
                    {
                        Configuration.RetireLocation = "Private Home";
                        Configuration.RetireToInnBeforeLoops = false;
                        Configuration.RetireToBarracksBeforeLoops = false;
                        Configuration.RetireToHomeBeforeLoops = retireToHomeBeforeLoops;
                        Configuration.RetireToFCBeforeLoops = false;
                        Configuration.Save();
                    }
                    else if (ImGui.Selectable($"FC House"))
                    {
                        Configuration.RetireLocation = "FC House";
                        Configuration.RetireToInnBeforeLoops = false;
                        Configuration.RetireToBarracksBeforeLoops = false;
                        Configuration.RetireToHomeBeforeLoops = false;
                        Configuration.RetireToFCBeforeLoops = retireToFCBeforeLoops;
                        Configuration.Save();
                    }*/
                    ImGui.EndCombo();
                }
            }
            if (ImGui.Checkbox("Auto Equip Recommended Gear", ref autoEquipRecommended))
            {
                Configuration.AutoEquipRecommendedGear = autoEquipRecommended;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Uses Gear from Armory Chest Only");
            if (ImGui.Checkbox("Auto Consume Boiled Eggs", ref Configuration.AutoBoiledEgg))
                Configuration.Save();
            ImGuiComponents.HelpMarker("Will use Boiled Eggs in inventory for +3% Exp.");




            if (ImGui.Checkbox("Auto Repair via Self", ref autoRepairSelf))
            {
                Configuration.AutoRepairSelf = autoRepairSelf;
                Configuration.AutoRepairCity = false;
                autoRepairCity = false;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Will use DarkMatter to Self Repair (Requires Leveled Crafters!)");
            if (ImGui.Checkbox("Auto Repair via CityNpc", ref autoRepairCity))
            {
                Configuration.AutoRepairCity = autoRepairCity;
                Configuration.AutoRepairSelf = false;
                autoRepairSelf = false;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Will use Npc near Inn to Repair.");
            using (var d1 = ImRaii.Disabled(!autoRepairSelf && !autoRepairCity))
            {
                if (ImGui.Checkbox("Trigger Auto Repair @", ref autoRepair))
                {
                    Configuration.AutoRepair = autoRepair;
                    Configuration.Save();
                }
                ImGui.SameLine(0, 5);
                ImGui.PushItemWidth(150 * ImGuiHelpers.GlobalScale);
                if (ImGui.SliderInt("##Repair@", ref autoRepairPct, 1, 99, "%d%%"))
                {
                    Configuration.AutoRepairPct = autoRepairPct;
                    Configuration.Save();
                }
                ImGui.PopItemWidth();

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
                if (Configuration.WaitTimeBeforeAfterLoopActions < 0) Configuration.WaitTimeBeforeAfterLoopActions = 0;
            Configuration.Save();
            ImGui.PopItemWidth();
            ImGuiComponents.HelpMarker("Will delay all AutoDuty between-loop Processes for X seconds.");
            ImGui.Separator();
            if (ImGui.Checkbox("Auto Extract", ref autoExtract))
            {
                Configuration.AutoExtract = autoExtract;
                Configuration.Save();
            }
            ImGui.SameLine(0, 10);
            using (var d1 = ImRaii.Disabled(!autoExtract))
            {
                if (ImGui.Checkbox("Extract Equipped", ref autoExtractEquipped))
                {
                    if (autoExtractEquipped)
                    {
                        Configuration.AutoExtractEquipped = true;
                        Configuration.AutoExtractAll = false;
                        Configuration.Save();
                        autoExtractAll = false;
                    }
                }

                ImGui.SameLine(0, 5);
                if (ImGui.Checkbox("Extract All", ref autoExtractAll))
                {
                    if (autoExtractAll)
                    {
                        Configuration.AutoExtractAll = true;
                        Configuration.AutoExtractEquipped = false;
                        Configuration.Save();
                        autoExtractEquipped = false;
                    }

                }
            }
            if (ImGui.Checkbox("Auto Desynth", ref autoDesynth))
            {
                Configuration.AutoDesynth = autoDesynth;
                Configuration.AutoGCTurnin = false;
                autoGCTurnin = false;
                Configuration.Save();
            }
            ImGui.SameLine(0, 5);
            using (var autoGcTurninDisabled = ImRaii.Disabled(!Deliveroo_IPCSubscriber.IsEnabled))
            {
                if (ImGui.Checkbox("Auto GC Turnin", ref autoGCTurnin))
                {
                    Configuration.AutoGCTurnin = autoGCTurnin;
                    Configuration.AutoDesynth = false;
                    autoDesynth = false;
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
                    autoGCTurnin = false;
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
            ImGui.SameLine(0, 5);
            if (Configuration.UnhideAM && !AM_IPCSubscriber.IsEnabled)
            {
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

            if (ImGui.Checkbox("Stop Looping @ Level", ref stopLevel))
            {
                Configuration.StopLevel = stopLevel;
                Configuration.Save();
            }
            using (var d1 = ImRaii.Disabled(!stopLevel))
            {
                ImGui.SameLine(0, 10);
                ImGui.PushItemWidth(100 * ImGuiHelpers.GlobalScale);
                if (Configuration.UseSliderInputs)
                {
                    if (ImGui.SliderInt("##Level", ref stopLevelInt, 1, 100))
                    {
                        Configuration.StopLevelInt = stopLevelInt;
                        Configuration.Save();
                    }
                }
                else
                {
                    if (ImGui.InputInt("##Level", ref stopLevelInt))
                    {
                        if (stopLevelInt < 1) stopLevelInt = 1;
                        else if (stopLevelInt > 100) stopLevelInt = 100;
                        Configuration.StopLevelInt = stopLevelInt;
                        Configuration.Save();
                    }
                }
                ImGui.PopItemWidth();
            }
            ImGuiComponents.HelpMarker("Note that Loop Number takes precedence over this option!");
            if (ImGui.Checkbox("Stop When No Rested XP", ref stopNoRestedXP))
            {
                Configuration.StopNoRestedXP = stopNoRestedXP;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Note that Loop Number takes precedence over this option!");
            if (ImGui.Checkbox("Stop Looping When Reach Item Qty", ref stopItemQty))
            {
                Configuration.StopItemQty = stopItemQty;
                Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Note that Loop Number takes precedence over this option!");
            using (var d1 = ImRaii.Disabled(!stopItemQty))
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
                if (ImGui.InputInt("Quantity", ref stopItemQtyInt))
                {
                    Configuration.StopItemQtyInt = stopItemQtyInt;
                    Configuration.Save();
                }
                ImGui.SameLine(0, 5);
                using (var addDisabled = ImRaii.Disabled(selectedItem.Value.IsNullOrEmpty()))
                {
                    if (ImGui.Button("Add Item"))
                    {
                        if (!stopItemQtyItemDictionary.TryAdd(selectedItem.Key, new(selectedItem.Value, stopItemQtyInt)))
                        {
                            stopItemQtyItemDictionary.Remove(selectedItem.Key);
                            stopItemQtyItemDictionary.Add(selectedItem.Key, new(selectedItem.Value, stopItemQtyInt));
                        }
                        Configuration.Save();
                    }
                }
                ImGui.PopItemWidth();
                if (!ImGui.BeginListBox("##ItemList", new System.Numerics.Vector2(325 * ImGuiHelpers.GlobalScale, 80 * ImGuiHelpers.GlobalScale))) return;

                foreach (var item in stopItemQtyItemDictionary)
                {
                    ImGui.Selectable($"{item.Value.Key} (Qty: {item.Value.Value})");
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        stopItemQtyItemDictionary.Remove(item);
                        Configuration.Save();
                    }
                }
                ImGui.EndListBox();
            }
            ImGui.Text("On Completion of All Loops: ");
            ImGui.SameLine(0, 10);
            ImGui.PushItemWidth(150 * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo(" ", terminationMethod))
            {
                if (ImGui.Selectable($"Do Nothing"))
                {
                    Configuration.TerminationMethod = "Do Nothing";
                    Configuration.AutoLogout = false;
                    Configuration.AutoARMultiEnable = false;
                    Configuration.AutoKillClient = false;
                    Configuration.Save();
                }
                else if (ImGui.Selectable($"Logout"))
                {
                    Configuration.TerminationMethod = "Logout";
                    Configuration.AutoLogout = true;
                    Configuration.AutoARMultiEnable = false;
                    Configuration.AutoKillClient = false;
                    Configuration.Save();
                }
                else if (ImGui.Selectable($"Start AR Multi Mode"))
                {
                    Configuration.TerminationMethod = "Start AR Multi Mode";
                    Configuration.AutoLogout = false;
                    Configuration.AutoARMultiEnable = true;
                    Configuration.AutoKillClient = false;
                    Configuration.Save();
                }
                else if (ImGui.Selectable($"Kill Client"))
                {
                    Configuration.TerminationMethod = "Kill Client";
                    Configuration.AutoLogout = false;
                    Configuration.AutoARMultiEnable = false;
                    Configuration.AutoKillClient = true;
                    Configuration.Save();
                }
                ImGui.EndCombo();
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
            var followDuringCombat = Configuration.FollowDuringCombat;
            var followDuringActiveBossModule = Configuration.FollowDuringActiveBossModule;
            var followOutOfCombat = Configuration.FollowOutOfCombat;
            var followTarget = Configuration.FollowTarget;
            var followSelf = Configuration.FollowSelf;
            var followSlot = Configuration.FollowSlot;
            var followSlotInt = Configuration.FollowSlotInt;
            var followRole = Configuration.FollowRole;
            var followRoleStr = Configuration.FollowRoleStr;
            var maxDistanceToTargetRoleBased = Configuration.MaxDistanceToTargetRoleRange;
            var maxDistanceToTarget = Configuration.MaxDistanceToTarget;
            var maxDistanceToTargetAoE = Configuration.MaxDistanceToTargetAoE;
            var maxDistanceToSlot = Configuration.MaxDistanceToSlot;
            var positionalRoleBased = Configuration.PositionalRoleBased;
            var positionalCustom = Configuration.PositionalCustom;

            if (ImGui.Checkbox("Follow During Combat", ref followDuringCombat))
            {
                Configuration.FollowDuringCombat = followDuringCombat;
                Configuration.Save();
            }

            if (ImGui.Checkbox("Follow During Active BossModule", ref followDuringActiveBossModule))
            {
                Configuration.FollowDuringActiveBossModule = followDuringActiveBossModule;
                Configuration.Save();
            }

            if (ImGui.Checkbox("Follow Out Of Combat (Not Recommended)", ref followOutOfCombat))
            {
                Configuration.FollowOutOfCombat = followOutOfCombat;
                Configuration.Save();
            }

            if (ImGui.Checkbox("Follow Target", ref followTarget))
            {
                Configuration.FollowTarget = followTarget;
                Configuration.Save();
            }

            if (ImGui.Checkbox("Follow Self", ref followSelf))
            {
                Configuration.FollowSelf = followSelf;
                Configuration.FollowSlot = false;
                Configuration.FollowRole = false;
                followRole = false;
                Configuration.Save();
            }

            if (ImGui.Checkbox("Follow Slot", ref followSlot))
            {
                Configuration.FollowSelf = false;
                followSelf = false;
                Configuration.FollowSlot = followSlot;
                followSlot = false;
                Configuration.FollowRole = false;
                followRole = false;
                Configuration.Save();
            }

            using (var d1 = ImRaii.Disabled(!followSlot))
            {
                ImGui.PushItemWidth(270);
                if (ImGui.SliderInt("Follow Slot #", ref followSlotInt, 1, 4))
                {
                    Configuration.FollowSlotInt = followSlotInt;
                    Configuration.Save();
                }
                ImGui.PopItemWidth();
            }

            if (ImGui.Checkbox("Follow Role", ref followRole))
            {
                Configuration.FollowSelf = false;
                followSelf = false;
                Configuration.FollowSlot = false;
                followSlot = false;
                Configuration.FollowRole = followRole;
                AutoDuty.Plugin.BMRoleChecks();
                Configuration.Save();
            }

            using (var d1 = ImRaii.Disabled(!followRole))
            {
                ImGui.SameLine(0, 10);
                if (ImGui.Button(followRoleStr))
                {
                    ImGui.OpenPopup("RolePopup");
                }
                if (ImGui.BeginPopup("RolePopup"))
                {
                    if (ImGui.Selectable("Tank"))
                    {
                        Configuration.FollowRoleStr = "Tank";
                        followRoleStr = "Tank";
                        Configuration.Save();
                    }
                    if (ImGui.Selectable("Melee"))
                    {
                        Configuration.FollowRoleStr = "Melee";
                        followRoleStr = "Melee";
                        Configuration.Save();
                    }
                    if (ImGui.Selectable("Ranged"))
                    {
                        Configuration.FollowRoleStr = "Ranged";
                        followRoleStr = "Ranged";
                        Configuration.Save();
                    }
                    if (ImGui.Selectable("Healer"))
                    {
                        Configuration.FollowRoleStr = "Healer";
                        followRoleStr = "Healer";
                        Configuration.Save();
                    }
                    ImGui.EndPopup();
                }
            }

            if (ImGui.Checkbox("Set Max Distance To Target Based on Role", ref maxDistanceToTargetRoleBased))
            {
                Configuration.MaxDistanceToTargetRoleRange = maxDistanceToTargetRoleBased;
                AutoDuty.Plugin.BMRoleChecks();
                Configuration.Save();
            }

            using (var d1 = ImRaii.Disabled(maxDistanceToTargetRoleBased))
            {
                ImGui.PushItemWidth(195);
                if (ImGui.SliderInt("Max Distance To Target", ref maxDistanceToTarget, 1, 30))
                {
                    Configuration.MaxDistanceToTarget = maxDistanceToTarget;
                    Configuration.Save();
                }
                if (ImGui.SliderInt("Max Distance To Target AoE", ref maxDistanceToTargetAoE, 1, 10))
                {
                    Configuration.MaxDistanceToTargetAoE = maxDistanceToTargetAoE;
                    Configuration.Save();
                }
                ImGui.PopItemWidth();
            }

            ImGui.PushItemWidth(195);
            if (ImGui.SliderInt("Max Distance To Slot", ref maxDistanceToSlot, 1, 30))
            {
                Configuration.MaxDistanceToSlot = maxDistanceToSlot;
                Configuration.Save();
            }
            ImGui.PopItemWidth();

            if (ImGui.Checkbox("Set Positional Based on Role", ref positionalRoleBased))
            {
                Configuration.PositionalRoleBased = positionalRoleBased;
                AutoDuty.Plugin.BMRoleChecks();
                Configuration.Save();
            }

            using (var d1 = ImRaii.Disabled(positionalRoleBased))
            {
                ImGui.SameLine(0, 10);
                if (ImGui.Button(positionalCustom))
                    ImGui.OpenPopup("PositionalPopup");

                if (ImGui.BeginPopup("PositionalPopup"))
                {
                    if (ImGui.Selectable("Any"))
                    {
                        positionalCustom = "Any";
                        Configuration.PositionalCustom = positionalCustom;
                        Configuration.Save();
                    }
                    if (ImGui.Selectable("Rear"))
                    {
                        positionalCustom = "Rear";
                        Configuration.PositionalCustom = positionalCustom;
                        Configuration.Save();
                    }
                    if (ImGui.Selectable("Flank"))
                    {
                        positionalCustom = "Flank";
                        Configuration.PositionalCustom = positionalCustom;
                        Configuration.Save();
                    }
                    if (ImGui.Selectable("Front"))
                    {
                        positionalCustom = "Front";
                        Configuration.PositionalCustom = positionalCustom;
                        Configuration.Save();
                    }
                    ImGui.EndPopup();
                }
            }
        }
    }
}
