using AutoDuty.Helpers;
using ECommons.EzIpcManager;
using System;
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
        [EzIPC] public void Run(uint territoryType, int loops = 0, bool bareMode = false) => Plugin.Run(territoryType, loops, bareMode);
        [EzIPC] public void Start(bool startFromZero = true) => Plugin.StartNavigation(startFromZero);
        [EzIPC] public void Stop() => Plugin.Stage = Stage.Stopped;
        [EzIPC] public bool IsNavigating() => Plugin.States.HasFlag(PluginState.Navigating);
        [EzIPC] public bool IsLooping() => Plugin.States.HasFlag(PluginState.Looping);
        [EzIPC] public bool IsStopped() => Plugin.Stage == Stage.Stopped;
        [EzIPC] public bool ContentHasPath(uint territoryType) => ContentHelper.DictionaryContent.ContainsKey(territoryType);

        //Callback for Wrath Combo Lease Cancel
        [EzIPC] public void WrathComboCallback(int reason, string s) => Wrath_IPCSubscriber.CancelActions(reason, s);
    }
}
