using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AutoDuty.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    public ConfigWindow(AutoDuty plugin) : base(
        "AutoDuty Config",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.Size = new Vector2(232, 270);
        this.SizeCondition = ImGuiCond.Always;
        this.Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
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

        if (ImGui.Checkbox("Retire to Inn before Looping", ref retireToInnBeforeLoops))
        {
            this.Configuration.RetireToInnBeforeLoops = retireToInnBeforeLoops;
            this.Configuration.RetireToBarracksBeforeLoops = false;
            retireToBarracksBeforeLoops = false;
            this.Configuration.Save();
        }
        if (ImGui.Checkbox("Retire to Baracks before Looping", ref retireToBarracksBeforeLoops))
        {
            this.Configuration.RetireToBarracksBeforeLoops = retireToBarracksBeforeLoops;
            this.Configuration.RetireToInnBeforeLoops = false;
            retireToInnBeforeLoops = false;
            this.Configuration.Save();
        }
        ImGui.Separator();
        if (ImGui.Checkbox("AutoRepair Enabled", ref autoRepair))
        {
            this.Configuration.AutoRepair = autoRepair;
            this.Configuration.Save();
        }

        using (var d1 = ImRaii.Disabled(!autoRepair))
        {
            if (ImGui.SliderInt("Repair@", ref autoRepairPct, 1, 100, "%d%%"))
            {
                this.Configuration.AutoRepairPct = autoRepairPct;
                this.Configuration.Save();
            }

            if (ImGui.Checkbox("Self AutoRepair", ref autoRepairSelf))
            {
                this.Configuration.AutoRepairSelf = autoRepairSelf;
                this.Configuration.AutoRepairCity = false;
                autoRepairCity = false;
                this.Configuration.Save();
            }

            if (ImGui.Checkbox("AutoRepair at City", ref autoRepairCity))
            {
                this.Configuration.AutoRepairCity = autoRepairCity;
                this.Configuration.AutoRepairSelf = false;
                autoRepairSelf = false;
                this.Configuration.Save();
            }

            using (var d2 = ImRaii.Disabled(!autoRepairCity))
            {
                if (ImGui.Checkbox("Return to Inn", ref autoRepairReturnToInn))
                {
                    this.Configuration.AutoRepairReturnToInn = autoRepairReturnToInn;
                    this.Configuration.AutoRepairReturnToBarracks = false;
                    autoRepairReturnToBarracks = false;
                    this.Configuration.Save();
                }
                if (ImGui.Checkbox("Return to Baracks", ref autoRepairReturnToBarracks))
                {
                    this.Configuration.AutoRepairReturnToBarracks = autoRepairReturnToBarracks;
                    this.Configuration.AutoRepairReturnToInn = false;
                    autoRepairReturnToInn = false;
                    this.Configuration.Save();
                }
            }
        }
        //disabled until implemented
        using (var d1 = ImRaii.Disabled(true))
        {
            ImGui.Separator();
            if (ImGui.Checkbox("Auto Desynth", ref autoDesynth))
            {
                this.Configuration.AutoDesynth = autoDesynth;
                this.Configuration.AutoGCTurnin = false;
                autoGCTurnin = false;
                this.Configuration.Save();
            }
            if (ImGui.Checkbox("Auto GC Turnin", ref autoGCTurnin))
            {
                this.Configuration.AutoGCTurnin = autoGCTurnin;
                this.Configuration.AutoDesynth = false;
                autoDesynth = false;
                this.Configuration.Save();
            }
            using (var d2 = ImRaii.Disabled(!autoGCTurnin))
            {
                if (ImGui.SliderInt("@ Slots Left", ref autoGCTurninSlotsLeft, 1, 180))
                {
                    this.Configuration.AutoGCTurninSlotsLeft = autoGCTurninSlotsLeft;
                    this.Configuration.Save();
                }
            }
        }
    }
}
