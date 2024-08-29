using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using AutoDuty.IPC;
using System.Numerics;
using System.Collections.Generic;
using ECommons.Throttlers;

namespace AutoDuty.Helpers
{
    internal static class FollowHelper
    {
        private static IGameObject? _followTarget = null;
        private static float _followDistance = 0.25f;
        private static bool _updateHooked = false;
        private static bool _enabled = false;

        internal static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (value && !_updateHooked) {
                    _updateHooked = true;
                    Svc.Framework.Update += FollowUpdate;
                }
                else if (!value && _updateHooked)
                {
                    _updateHooked = false;
                    Svc.Framework.Update -= FollowUpdate;
                    VNavmesh_IPCSubscriber.Path_Stop();
                }
            }
        }

        internal static void SetFollow(IGameObject? gameObject, float followDistance = 0)
        {
            if (gameObject != null)
            {
                _followTarget = gameObject;
                Enabled = true;
            }
            else
            {
                _followTarget = null;
                Enabled = false;
            }
            if (followDistance > 0)
                _followDistance = followDistance;
        } 

        internal static void SetFollowTarget(IGameObject? gameObject) => _followTarget = gameObject;

        internal static void SetFollowDistance(float f) => _followDistance = f + 0.1f;

        private static void FollowUpdate(IFramework framework)
        {
            if (_followTarget == null || Svc.ClientState.LocalPlayer == null || !EzThrottler.Throttle("FollowUpdate", 50))
                return;

            if (ObjectHelper.GetDistanceToPlayer(_followTarget) >= _followDistance)
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                List<Vector3> _followTargetList = [_followTarget.Position];
                VNavmesh_IPCSubscriber.Path_MoveTo(_followTargetList, false);
            }
        }
    }
}
