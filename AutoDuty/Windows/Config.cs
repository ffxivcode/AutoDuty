using AutoDuty.IPC;
using Dalamud.Configuration;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;

namespace AutoDuty.Windows;

using System.Linq;
using ECommons.ExcelServices;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public HashSet<string> DoNotUpdatePathFiles { get; set; } = [];

    public int Version { get; set; } = 85;
    public int AutoRepairPct { get; set; } = 50;
    public int AutoGCTurninSlotsLeft { get; set; } = 5;
    public int LoopTimes { get; set; } = 1;
    public int TreasureCofferScanDistance { get; set; } = 25;

    public bool AutoManageBossModAISettings { get; set; } = true;
    public bool AutoManageRSRState { get; set; } = true;
    public bool AutoExitDuty { get; set; } = true;
    public bool AutoKillClient { get; set; } = false;
    public bool AutoLogout { get; set; } = false;
    public bool AutoARMultiEnable { get; set; } = false;
    public bool LootTreasure { get; set; } = true;
    public bool LootBossTreasureOnly { get; set; } = true;
    public bool AutoRepair { get; set; } = false;
    public bool AutoRepairSelf { get; set; } = false;
    public bool AutoRepairCity { get; set; } = true;
    public bool RetireToInnBeforeLoops { get; set; } = true;
    public bool RetireToBarracksBeforeLoops { get; set; } = false;
    public bool AutoExtract { get; set; } = false;
    public bool AutoExtractEquipped { get; set; } = true;
    public bool AutoExtractAll { get; set; } = false;
    public bool AutoDesynth { get; set; } = false;
    public bool AutoGCTurnin { get; set; } = false;
    public bool Support { get; set; } = false;
    public bool Trust { get; set; } = false;
    public bool Squadron { get; set; } = false;
    public bool Regular { get; set; } = false;
    public bool Trial { get; set; } = false;
    public bool Raid { get; set; } = false;
    public bool Unsynced { get; set; } = false;
    public bool HideUnavailableDuties { get; set; } = false;

    public Dictionary<uint, Dictionary<Job, int>> PathSelections { get; set; } = [];

    [NonSerialized]
    private IDalamudPluginInterface? PluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
    }

    public void Save()
    {
        PluginInterface!.SavePluginConfig(this);
    }
}

public static class ConfigTab
{
    private static Configuration Configuration = AutoDuty.Plugin.Configuration;

    public static void Draw()
    {
        if (MainWindow.CurrentTabName != "Config")
            MainWindow.CurrentTabName = "Config";
        var autoManageRSRState = Configuration.AutoManageRSRState;
        var autoManageBossModAISettings = Configuration.AutoManageBossModAISettings;
        var autoExitDuty = Configuration.AutoExitDuty;
        var autoKillClient = Configuration.AutoKillClient;
        var autoLogout = Configuration.AutoLogout;
        var autoARMultiEnable = Configuration.AutoARMultiEnable;
        var lootTreasure = Configuration.LootTreasure;
        var treasureCofferScanDistance = Configuration.TreasureCofferScanDistance;
        var lootBossTreasureOnly = Configuration.LootBossTreasureOnly;
        var autoRepair = Configuration.AutoRepair;
        var autoRepairSelf = Configuration.AutoRepairSelf;
        var autoRepairCity = Configuration.AutoRepairCity;
        var autoRepairPct = Configuration.AutoRepairPct;
        var retireToInnBeforeLoops = Configuration.RetireToInnBeforeLoops;
        var retireToBarracksBeforeLoops = Configuration.RetireToBarracksBeforeLoops;
        var autoExtract = Configuration.AutoExtract;
        var autoExtractEquipped = Configuration.AutoExtractEquipped;
        var autoExtractAll = Configuration.AutoExtractAll;
        var autoDesynth = Configuration.AutoDesynth;
        var autoGCTurnin = Configuration.AutoGCTurnin;
        
        if (ImGui.Checkbox("Auto Manage Rotation Solver State", ref autoManageRSRState))
        {
            Configuration.AutoManageRSRState = autoManageRSRState;
            Configuration.Save();
        }
        if (ImGui.Checkbox("Auto Manage BossMod AI Settings", ref autoManageBossModAISettings))
        {
            Configuration.AutoManageBossModAISettings = autoManageBossModAISettings;
            Configuration.Save();
        }
        if (ImGui.Checkbox("Auto Kill Client on Completion of Looping", ref autoKillClient))
        {
            Configuration.AutoKillClient = autoKillClient;
            Configuration.AutoLogout = false;
            Configuration.Save();
        }
        if (ImGui.Checkbox("Auto Logout on Completion of Looping", ref autoLogout))
        {
            Configuration.AutoLogout = autoLogout;
            Configuration.AutoKillClient = false;
            Configuration.Save();
        }
        if (ImGui.Checkbox("Enable AutoRetainer Multi on Completion of Looping", ref autoARMultiEnable))
        {
            Configuration.AutoARMultiEnable = autoARMultiEnable;
            Configuration.Save();
        }
        if (ImGui.Checkbox("Auto Exit Duty on Completion of Dungeon", ref autoExitDuty))
        {
            Configuration.AutoExitDuty = autoExitDuty;
            Configuration.Save();
        }

        if (ImGui.Checkbox("Loot Treasure Coffers", ref lootTreasure))
        {
            Configuration.LootTreasure = lootTreasure;
            Configuration.Save();
        }
        /*/disabled for now
        using (var d0 = ImRaii.Disabled(true))
        {
            if (ImGui.SliderInt("Scan Distance", ref treasureCofferScanDistance, 1, 100))
            {
                Configuration.TreasureCofferScanDistance = treasureCofferScanDistance;
                Configuration.Save();
            }
        }*/
        using (var d1 = ImRaii.Disabled(!lootTreasure))
        {
            if (ImGui.Checkbox("Loot Boss Treasure Only", ref lootBossTreasureOnly))
            {
                Configuration.LootBossTreasureOnly = lootBossTreasureOnly;
                Configuration.Save();
            }
        }
        if (ImGui.Checkbox("Retire to Inn before each Loop", ref retireToInnBeforeLoops))
        {
            Configuration.RetireToInnBeforeLoops = retireToInnBeforeLoops;
            Configuration.RetireToBarracksBeforeLoops = false;
            retireToBarracksBeforeLoops = false;
            Configuration.Save();
        }
        if (ImGui.Checkbox("Retire to Barracks before each Loop", ref retireToBarracksBeforeLoops))
        {
            Configuration.RetireToBarracksBeforeLoops = retireToBarracksBeforeLoops;
            Configuration.RetireToInnBeforeLoops = false;
            retireToInnBeforeLoops = false;
            Configuration.Save();
        }
        ImGui.Separator();
        if (ImGui.Checkbox("AutoRepair Enabled", ref autoRepair))
        {
            Configuration.AutoRepair = autoRepair;
            Configuration.Save();
        }

        using (var d1 = ImRaii.Disabled(!autoRepair))
        {
            if (ImGui.SliderInt("Repair@", ref autoRepairPct, 1, 100, "%d%%"))
            {
                Configuration.AutoRepairPct = autoRepairPct;
                Configuration.Save();
            }

            if (ImGui.Checkbox("Self AutoRepair", ref autoRepairSelf))
            {
                Configuration.AutoRepairSelf = autoRepairSelf;
                Configuration.AutoRepairCity = false;
                autoRepairCity = false;
                Configuration.Save();
            }

            if (ImGui.Checkbox("AutoRepair at City", ref autoRepairCity))
            {
                Configuration.AutoRepairCity = autoRepairCity;
                Configuration.AutoRepairSelf = false;
                autoRepairSelf = false;
                Configuration.Save();
            }
        }
        ImGui.Separator();
        if (ImGui.Checkbox("Auto Extract", ref autoExtract))
        {
            Configuration.AutoExtract = autoExtract;
            Configuration.Save();
        }
        if (ImGui.Checkbox("Extract Equipped Only", ref autoExtractEquipped))
        {
            if (autoExtractEquipped)
            {
                Configuration.AutoExtractEquipped = true;
                Configuration.AutoExtractAll = false;
                autoExtractAll = false;
            }
            else
            {
                Configuration.AutoExtractAll = true;
                Configuration.AutoExtractEquipped = false;
                autoExtractAll = true;
            }
            Configuration.Save();
        }
        if (ImGui.Checkbox("Extract All", ref autoExtractAll))
        {
            if (autoExtractAll)
            {
                Configuration.AutoExtractAll = true;
                Configuration.AutoExtractEquipped = false;
                autoExtractEquipped = false;
            }
            else
            {
                Configuration.AutoExtractAll = false;
                Configuration.AutoExtractEquipped = true;
                autoExtractEquipped = true;
            }
            Configuration.Save();
        }
        if (ImGui.Checkbox("Auto Desynth", ref autoDesynth))
        {
            Configuration.AutoDesynth = autoDesynth;
            Configuration.AutoGCTurnin = false;
            autoGCTurnin = false;
            Configuration.Save();
        }
        using (var autoGcTurninDisabled = ImRaii.Disabled(!Deliveroo_IPCSubscriber.IsEnabled))
        {
            if (ImGui.Checkbox("Auto GC Turnin", ref autoGCTurnin))
            {
                Configuration.AutoGCTurnin = autoGCTurnin;
                Configuration.AutoDesynth = false;
                autoDesynth = false;
                Configuration.Save();
            }
            if (!Deliveroo_IPCSubscriber.IsEnabled)
            {
                Configuration.AutoGCTurnin = false;
                autoGCTurnin = false;
                Configuration.Save();
                ImGui.Text("* Auto GC Turnin Requires Deliveroo plugin");
                ImGui.Text("Get @ https://git.carvel.li/liza/plugin-repo");
            }
        }

        using (var savedPathsDisabled = ImRaii.Disabled(!Configuration.PathSelections.Any(kvp => kvp.Value.Any())))
        {
            if (ImGui.Button("Clear all saved path selections"))
            {
                Configuration.PathSelections.Clear();
                Configuration.Save();
            }
        }
    }
}
