using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace AutoDuty;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 23;
    public int AutoRepairPct { get; set; } = 50;

    public bool AutoRepair { get; set; } = true;
    public bool AutoRepairSelf { get; set; } = false;
    public bool AutoRepairCity { get; set; } = true;
    public bool AutoRepairLimsa { get; set; } = true;
    public bool AutoRepairUldah { get; set; } = false;
    public bool AutoRepairGridania { get; set; } = false;

    [NonSerialized]
    private DalamudPluginInterface? PluginInterface;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
    }

    public void Save()
    {
        PluginInterface!.SavePluginConfig(this);
    }
}