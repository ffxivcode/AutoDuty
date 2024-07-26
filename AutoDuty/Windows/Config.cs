using AutoDuty.IPC;
using Dalamud.Configuration;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.ExcelServices;
using AutoDuty.Helpers;
using ECommons.GameHelpers;

namespace AutoDuty.Windows;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public HashSet<string> DoNotUpdatePathFiles { get; set; } = [];

    public int Version { get; set; } = 86;
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
    public int MaxDistanceToTargetCustom { get; set; } = 3;
    public int MaxDistanceToSlot { get; set; } = 1;
    public bool PositionalRoleBased { get; set; } = true;
    public string PositionalCustom { get; set; } = "Any";
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
            var maxDistanceToTargetRoleRange = Configuration.MaxDistanceToTargetRoleRange;
            var maxDistanceToTargetCustom = Configuration.MaxDistanceToTargetCustom;
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
                ImGui.PushItemWidth(300);
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

            if (ImGui.Checkbox("Set Max Distance To Target Based on Role", ref maxDistanceToTargetRoleRange))
            {
                Configuration.MaxDistanceToTargetRoleRange = maxDistanceToTargetRoleRange;
                Configuration.Save();
            }

            using (var d1 = ImRaii.Disabled(maxDistanceToTargetRoleRange))
            {
                ImGui.PushItemWidth(200);
                if (ImGui.SliderInt("Max Distance To Target", ref maxDistanceToTargetCustom, 1, 30))
                {
                    Configuration.MaxDistanceToTargetCustom = maxDistanceToTargetCustom;
                    Configuration.Save();
                }
                ImGui.PopItemWidth();
            }
            ImGui.PushItemWidth(200);
            if (ImGui.SliderInt("Max Distance To Slot", ref maxDistanceToSlot, 1, 30))
            {
                Configuration.MaxDistanceToSlot = maxDistanceToSlot;
                Configuration.Save();
            }
            ImGui.PopItemWidth();

            if (ImGui.Checkbox("Set Positional Based on Role", ref positionalRoleBased))
            {
                Configuration.PositionalRoleBased = positionalRoleBased;
                Configuration.Save();
            }

            if (positionalRoleBased && positionalCustom != (ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Melee ? "Rear" : "Any"))
            {
                positionalCustom = (ObjectHelper.GetJobRole(Player.Object.ClassJob.GameData!) == ObjectHelper.JobRole.Melee ? "Rear" : "Any");
                Configuration.PositionalCustom = positionalCustom;
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
