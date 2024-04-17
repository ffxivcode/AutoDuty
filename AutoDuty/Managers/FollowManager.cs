using AutoDuty.External;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoDuty.Managers
{
    internal class FollowManager(OverrideMovement _overrideMovement)
    {
        private GameObject? FollowTarget;
        private float FollowDistance;

        private bool Enabled
        {
            get { return Enabled; }    
            set
            {
                if (value)
                {
                    Svc.Framework.Update += Update;
                    _overrideMovement.Enabled = true;
                }
                else
                    _overrideMovement.Enabled = false;
            }
        }

        internal bool GetFollowStatus() => Enabled;

        internal void SetFollowStatus(bool on) => Enabled = on;

        internal void SetFollowTarget(GameObject gameObject) => FollowTarget = gameObject;

        internal void SetFollowDistance(float f) => FollowDistance = f;

        private void Update(IFramework framework)
        {
            if (FollowTarget == null || Svc.ClientState.LocalPlayer == null)
                return;

            if (_overrideMovement.Precision != FollowDistance + 0.1f)
                _overrideMovement.Precision = FollowDistance + 0.1f;

            _overrideMovement.DesiredPosition = FollowTarget.Position;
        }
    }
}
