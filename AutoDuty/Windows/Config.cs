using Dalamud.Configuration;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;

namespace AutoDuty.Windows;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public HashSet<string> DoNotUpdatePathFiles { get; set; } = [];

    public int Version { get; set; } = 56;
    public int AutoRepairPct { get; set; } = 50;
    public int AutoGCTurninSlotsLeft { get; set; } = 5;
    public int LoopTimes { get; set; } = 1;
    public int TreasureCofferScanDistance { get; set; } = 25;

    public bool AutoExitDuty { get; set; } = true;
    public bool AutoKillClient { get; set; } = false;
    public bool AutoLogout { get; set; } = false;
    public bool LootTreasure { get; set; } = true;
    public bool LootBossTreasureOnly { get; set; } = true;
    public bool AutoRepair { get; set; } = false;
    public bool AutoRepairSelf { get; set; } = false;
    public bool AutoRepairCity { get; set; } = true;
    public bool AutoRepairReturnToInn { get; set; } = true;
    public bool AutoRepairReturnToBarracks { get; set; } = false;
    public bool RetireToInnBeforeLoops { get; set; } = true;
    public bool RetireToBarracksBeforeLoops { get; set; } = false;
    public bool AutoDesynth { get; set; } = false;
    public bool AutoGCTurnin { get; set; } = false;
    public bool AutoGCTurninAfterEveryLoop { get; set; } = false;
    public string AutoGCTurninItemToBuyId { get; set; } = "0";
    public bool Support { get; set; } = false;
    public bool Trust { get; set; } = false;
    public bool Squadron { get; set; } = false;
    public bool Regular { get; set; } = false;
    public bool Trial { get; set; } = false;
    public bool Raid { get; set; } = false;
    public bool Unsynced { get; set; } = false;
    public bool HideUnavailableDuties { get; set; } = false;

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
        var autoExitDuty = Configuration.AutoExitDuty;
        var autoKillClient = Configuration.AutoKillClient;
        var autoLogout = Configuration.AutoLogout;
        var lootTreasure = Configuration.LootTreasure;
        var treasureCofferScanDistance = Configuration.TreasureCofferScanDistance;
        var lootBossTreasureOnly = Configuration.LootBossTreasureOnly;
        var autoRepair = Configuration.AutoRepair;
        var autoRepairSelf = Configuration.AutoRepairSelf;
        var autoRepairCity = Configuration.AutoRepairCity;
        var autoRepairReturnToInn = Configuration.AutoRepairReturnToInn;
        var autoRepairReturnToBarracks = Configuration.AutoRepairReturnToBarracks;
        var autoRepairPct = Configuration.AutoRepairPct;
        var retireToInnBeforeLoops = Configuration.RetireToInnBeforeLoops;
        var retireToBarracksBeforeLoops = Configuration.RetireToBarracksBeforeLoops;
        var autoDesynth = Configuration.AutoDesynth;
        var autoGCTurnin = Configuration.AutoGCTurnin;
        var autoGCTurninSlotsLeft = Configuration.AutoGCTurninSlotsLeft;
        var autoGCTurninAfterEveryLoop = Configuration.AutoGCTurninAfterEveryLoop;
        string autoGCTurninItemToBuyId = Configuration.AutoGCTurninItemToBuyId;

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
        if (ImGui.Checkbox("Retire to Inn before Looping", ref retireToInnBeforeLoops))
        {
            Configuration.RetireToInnBeforeLoops = retireToInnBeforeLoops;
            Configuration.RetireToBarracksBeforeLoops = false;
            retireToBarracksBeforeLoops = false;
            Configuration.Save();
        }
        if (ImGui.Checkbox("Retire to Barracks before Looping", ref retireToBarracksBeforeLoops))
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

            using (var d2 = ImRaii.Disabled(!autoRepairCity))
            {
                if (ImGui.Checkbox("Return to Inn", ref autoRepairReturnToInn))
                {
                    Configuration.AutoRepairReturnToInn = autoRepairReturnToInn;
                    Configuration.AutoRepairReturnToBarracks = false;
                    autoRepairReturnToBarracks = false;
                    Configuration.Save();
                }
                if (ImGui.Checkbox("Return to Barracks", ref autoRepairReturnToBarracks))
                {
                    Configuration.AutoRepairReturnToBarracks = autoRepairReturnToBarracks;
                    Configuration.AutoRepairReturnToInn = false;
                    autoRepairReturnToInn = false;
                    Configuration.Save();
                }
            }
        }
        /*/disabled until implemented
        using (var d1 = ImRaii.Disabled(true))
        {
            ImGui.Separator();
            if (ImGui.Checkbox("Auto Desynth", ref autoDesynth))
            {
                Configuration.AutoDesynth = autoDesynth;
                Configuration.AutoGCTurnin = false;
                autoGCTurnin = false;
                Configuration.Save();
            }
        
            if (ImGui.Checkbox("Auto GC Turnin", ref autoGCTurnin))
            {
                Configuration.AutoGCTurnin = autoGCTurnin;
                Configuration.AutoDesynth = false;
                autoDesynth = false;
                Configuration.Save();
            }
            using (var d2 = ImRaii.Disabled(!autoGCTurnin))
            {
                if (ImGui.Checkbox("After Every Loop", ref autoGCTurninAfterEveryLoop))
                {
                    Configuration.AutoGCTurninAfterEveryLoop = autoGCTurninAfterEveryLoop;
                    Configuration.Save();
                }
                //ImGuiEx.SetNextItemWidthScaled(50);
                if (ImGui.InputText("ItemID to Buy when Max Seals", ref autoGCTurninItemToBuyId, 10))
                {
                    Configuration.AutoGCTurninItemToBuyId = autoGCTurninItemToBuyId;
                    Configuration.Save();
                }
                using (var d3 = ImRaii.Disabled(autoGCTurninAfterEveryLoop))
                {
                    if (ImGui.SliderInt("@ Slots Left", ref autoGCTurninSlotsLeft, 1, 180))
                    {
                        Configuration.AutoGCTurninSlotsLeft = autoGCTurninSlotsLeft;
                        Configuration.Save();
                    }
                }
            }
        }*/
    }
}