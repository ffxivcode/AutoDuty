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

        [EzIPC] public void Run(uint territoryType, int loops) => AutoDuty.Plugin.Run(territoryType, loops);
        [EzIPC] public void Start(bool startFromZero = true) => AutoDuty.Plugin.StartNavigation(startFromZero);
        [EzIPC] public void Stop() => AutoDuty.Plugin.Stage = Data.Enum.Stage.Stopped;
    }
}
