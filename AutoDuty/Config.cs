using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace AutoDuty;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 30;
    public int AutoRepairPct { get; set; } = 50;
    public int AutoGCTurninSlotsLeft { get; set; } = 5;

    public bool AutoRepair { get; set; } = false;
    public bool AutoRepairSelf { get; set; } = false;
    public bool AutoRepairCity { get; set; } = true;
    public bool AutoRepairReturnToInn { get; set; } = true;
    public bool AutoRepairReturnToBarracks { get; set; } = false;
    public bool RetireToInnBeforeLoops { get; set; } = true;
    public bool RetireToBarracksBeforeLoops { get; set; } = false;
    public bool AutoDesynth { get; set; } = false;
    public bool AutoGCTurnin { get; set; } = false;

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