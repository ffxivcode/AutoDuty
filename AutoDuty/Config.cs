using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace AutoDuty;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 20;

    public bool AutoDuty { get; set; } = true;

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