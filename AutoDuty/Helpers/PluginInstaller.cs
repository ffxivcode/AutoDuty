using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using Dalamud.Plugin.Services;
    using ECommons.Automation;
    using ECommons.Reflection;
    using ECommons.Throttlers;
    using global::AutoDuty.IPC;

    internal class PluginInstaller : ActiveHelperBase<PluginInstaller>
    {
        protected override string Name        { get; } = "Plugin Installer";
        protected override string DisplayName { get; } = "Plugin Installer";

        private static ExternalPlugin? pluginToInstall;
        private Task<bool>?     installTask;
        private        int             retries = 0;


        internal static void InstallPlugin(ExternalPlugin plugin)
        {
            pluginToInstall = plugin;
            Invoke();
        }

        internal override void Start()
        {
            if(this.installTask?.Status == TaskStatus.Running)
            {
                this.DebugLog("Plugin installation already in progress");
                return;
            }
            if(pluginToInstall == null)
            {
                this.DebugLog("No plugin specified for installation");
                return;
            }

            base.Start();
        }

        protected override void HelperUpdate(IFramework framework)
        {
            if(!pluginToInstall.HasValue)
            {
                this.Stop();
                return;
            }

            if (!EzThrottler.Throttle(this.Name, this.UpdateBaseThrottle))
                return;

            if (this.installTask == null)
            {
                this.DebugLog("Getting plugin data");
                (string url, string name) = pluginToInstall.Value.GetExternalPluginData();
                this.DebugLog($"{url} | {name}");
                this.installTask             = DalamudReflector.AddPlugin(url, name);
                return;
            }
            if(this.installTask.IsCompleted)
            {
                this.DebugLog("task completed");
                string pluginName = pluginToInstall.Value.GetExternalPluginData().name;
                if (this.installTask.Result || IPCSubscriber_Common.IsReady(pluginName))
                {
                    this.DebugLog("Successfully installed");
                    this.Stop();
                    return;
                } else
                {
                    if(PluginInterface.InstalledPlugins.Any(iep => iep.InternalName == pluginName))
                    {
                        this.DebugLog("Plugin already installed but not ready, stopping installation");
                        this.Stop();
                        return;
                    }

                    this.DebugLog("Failed to install plugin");
                    this.retries++;
                    if(this.retries > 5)
                        this.Stop();
                    else
                        this.installTask = null;
                }
            }
        }

        internal override void Stop()
        {
            pluginToInstall  = null;
            this.installTask = null;
            this.retries     = 0;
            base.Stop();
        }
    }
}
