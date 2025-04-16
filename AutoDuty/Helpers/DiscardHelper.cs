using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using Dalamud.Plugin.Services;
    using ECommons.DalamudServices;
    using IPC;

    internal class DiscardHelper : ActiveHelperBase<DiscardHelper>
    {
        protected override string Name        { get; } = nameof(DiscardHelper);
        protected override string DisplayName { get; } = "Discarding Items";

        private bool started = false;

        internal override void Start()
        {
            base.Start();
            this.started = false;
        }

        protected override unsafe void   HelperUpdate(IFramework framework)
        {
            if (!this.UpdateBase() || !PlayerHelper.IsReadyFull)
                return;
            if (!this.started)
            {
                Plugin.Chat.ExecuteCommand("/discardall");
                this.started = true;
                return;
            }
            if(!DiscardHelper_IPCSubscriber.IsRunning())
                this.Stop();
        }
    }
}
