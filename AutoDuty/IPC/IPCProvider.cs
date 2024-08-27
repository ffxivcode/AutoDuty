using AutoDuty.Helpers;
using ECommons.EzIpcManager;
#nullable disable

namespace AutoDuty.IPC
{
    internal class IPCProvider
    {
        internal IPCProvider()
        {
            EzIPC.Init(this);
        }

        [EzIPC] public void ListConfig() => ConfigHelper.ListConfig();
        [EzIPC] public string GetConfig(string config) => ConfigHelper.GetConfig(config);
        [EzIPC] public void SetConfig (string config, string setting) => ConfigHelper.ModifyConfig(config, setting);
        [EzIPC] public void Run(uint territoryType, int loops) => AutoDuty.Plugin.Run(territoryType, loops);
        [EzIPC] public void Start(bool startFromZero = true) => AutoDuty.Plugin.StartNavigation(startFromZero);
        [EzIPC] public void Stop() => AutoDuty.Plugin.Stage = Stage.Stopped;
        [EzIPC] public bool IsNavigating() => AutoDuty.Plugin.States.HasFlag(State.Navigating);
        [EzIPC] public bool IsLooping() => AutoDuty.Plugin.States.HasFlag(State.Looping);
        [EzIPC] public bool IsStopped() => AutoDuty.Plugin.Stage == Stage.Stopped;
    }
}
