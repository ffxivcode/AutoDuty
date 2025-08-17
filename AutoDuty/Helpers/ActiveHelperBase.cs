using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    using System;
    using System.Collections.Generic;
    using static FFXIVClientStructs.FFXIV.Client.Game.GcArmyManager.Delegates;

    public static class ActiveHelper
    {
        internal static HashSet<IActiveHelper> activeHelpers = [];
    }

    internal interface IActiveHelper
    {
        internal        void        StopIfRunning();
    }

    internal abstract class ActiveHelperBase<T> : IActiveHelper where T : ActiveHelperBase<T>, new()
    {
        protected abstract string   Name          { get; }
        protected abstract string   DisplayName   { get; }

        protected virtual string[] AddonsToClose { get; } = [];

        protected virtual int TimeOut { get; set; } = 300_000;

        private static T? instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    T helper = new();
                    ActiveHelper.activeHelpers.Add(helper);
                    instance = helper;
                }
                return instance;
            }
        }


        internal static void Invoke()
        {
            Instance.Start();
        }

        internal virtual void Start()
        {
            if(State == ActionState.Running)
            {
                this.DebugLog(this.Name + " already running");
                return;
            }
            this.InfoLog(this.Name + " started");
            State         =  ActionState.Running;
            Plugin.States |= PluginState.Other;

            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(false);

            if(this.TimeOut > 0)
                SchedulerHelper.ScheduleAction($"Helper_{this.Name}_TimeOut", this.Stop, this.TimeOut);

            if (this.DisplayName != string.Empty)
                Plugin.Action = this.DisplayName;
            Svc.Framework.Update += this.HelperUpdate;
        }

        internal static ActionState State  = ActionState.None;

        internal static void ForceStop()
        {
            instance?.Stop();
        }

        public void StopIfRunning()
        {
            if(State == ActionState.Running)
                this.Stop();
        }

        internal virtual void Stop()
        {
            if (State == ActionState.Running)
                this.InfoLog(this.Name + " finished");

            if (this.DisplayName != string.Empty)
                Plugin.Action = string.Empty;

            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(false);

            SchedulerHelper.DescheduleAction($"Helper_{this.Name}_TimeOut");

            Svc.Framework.Update += this.HelperStopUpdate;
            Svc.Framework.Update -= this.HelperUpdate;
        }

        protected abstract unsafe void HelperUpdate(IFramework framework);

        protected virtual int UpdateBaseThrottle { get; set; } = 500;

        protected bool UpdateBase()
        {
            if (Plugin.States.HasFlag(PluginState.Navigating) || Plugin.InDungeon)
            {
                this.Stop();
                return false;
            }

            if (!EzThrottler.Throttle(this.Name, this.UpdateBaseThrottle))
                return false;

            if (GotoHelper.State == ActionState.Running)
            {
                //Svc.Log.Debug("Goto Running");
                return false;
            }

            return true;
        }

        protected virtual unsafe void HelperStopUpdate(IFramework framework)
        {
            if (!this.CloseAddons())
                return;

            State         =  ActionState.None;
            Plugin.States &= ~PluginState.Other;

            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(true);
            Svc.Framework.Update -= this.HelperStopUpdate;
        }

        public unsafe bool CloseAddons()
        {
            for (int i = 0; i < this.AddonsToClose.Length; i++)
            {
                if (GenericHelpers.TryGetAddonByName(this.AddonsToClose[i], out AtkUnitBase* atkUnitBase) && atkUnitBase->IsVisible)
                {
                    this.DebugLog("Closing Addon " + this.AddonsToClose[i]);
                    atkUnitBase->Close(true);
                    return false;
                }
            }

            return true;
        }

        protected void DebugLog(string s)
        {
            Svc.Log.Debug($"{this.Name}: {s}");
        }

        protected void InfoLog(string s)
        {
            Svc.Log.Info($"{this.Name}: {s}");
        }
    }
}
