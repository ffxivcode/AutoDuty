using System;
using System.Numerics;
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
        this.Size = new Vector2(232, 180);
        this.SizeCondition = ImGuiCond.Always;

        this.Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var autoRepair = Configuration.AutoRepair;
        var autoRepairSelf = Configuration.AutoRepairSelf;
        var autoRepairCity = Configuration.AutoRepairCity;
        var autoRepairLimsa = Configuration.AutoRepairLimsa;
        var autoRepairUldah = Configuration.AutoRepairUldah;
        var autoRepairGridania = Configuration.AutoRepairGridania;
        var autoRepairPct = Configuration.AutoRepairPct;
        if (ImGui.Checkbox("AutoRepair Enabled", ref autoRepair))
        {
            this.Configuration.AutoRepair = autoRepair;
            this.Configuration.Save();
        }
        using (var d1 = ImRaii.Disabled(!autoRepair))
        {
            //ImGui.SameLine(0, 5);
            if (ImGui.SliderInt("Repair@", ref autoRepairPct, 1, 100,"%d%%"))
            {
                this.Configuration.AutoRepairPct = autoRepairPct;
                this.Configuration.Save();
            }
                //("Percent", ref autoRepairPct);
            using (var d2 = ImRaii.Disabled(!autoRepairCity))
            {
                if (ImGui.Checkbox("Self AutoRepair", ref autoRepairSelf))
                {
                    this.Configuration.AutoRepairSelf = autoRepairSelf;
                    this.Configuration.AutoRepairCity = false;
                    autoRepairCity = false;
                    this.Configuration.Save();
                }
            }
            using (var d3 = ImRaii.Disabled(!autoRepairSelf))
            {
                if (ImGui.Checkbox("AutoRepair at City", ref autoRepairCity))
                {
                    this.Configuration.AutoRepairCity = autoRepairCity;
                    this.Configuration.AutoRepairSelf = false;
                    autoRepairSelf = false;
                    this.Configuration.Save();
                }
            }
            using (var d4 = ImRaii.Disabled(!autoRepairCity))
            {
                if (ImGui.Checkbox("AutoRepair at Limsa", ref autoRepairLimsa))
                {
                    this.Configuration.AutoRepairLimsa = autoRepairLimsa;
                    if (autoRepairLimsa)
                    {
                        this.Configuration.AutoRepairUldah = false;
                        this.Configuration.AutoRepairGridania = false;
                        autoRepairUldah = false;
                        autoRepairGridania = false;
                    }
                    this.Configuration.Save();
                }
                if (ImGui.Checkbox("AutoRepair at Uldah", ref autoRepairUldah))
                {
                    this.Configuration.AutoRepairUldah = autoRepairUldah;
                    if (autoRepairUldah)
                    {
                        this.Configuration.AutoRepairLimsa = false;
                        this.Configuration.AutoRepairGridania = false;
                        autoRepairLimsa = false;
                        autoRepairGridania = false;
                    }
                    this.Configuration.Save();
                }
                if (ImGui.Checkbox("AutoRepair at Gridania", ref autoRepairGridania))
                {
                    this.Configuration.AutoRepairGridania = autoRepairGridania;
                    if (autoRepairGridania)
                    {
                        this.Configuration.AutoRepairUldah = false;
                        this.Configuration.AutoRepairLimsa = false;
                        autoRepairUldah = false;
                        autoRepairLimsa = false;
                    }
                    this.Configuration.Save();
                }
            }
        }
    }
}
