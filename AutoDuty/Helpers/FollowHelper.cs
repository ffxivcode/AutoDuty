using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using static Dalamud.Game.ClientState.Keys.VirtualKey;
using static FFXIVClientStructs.FFXIV.Client.UI.KeyStateFlags;
using System.Numerics;
using System.Collections.Generic; 

namespace AutoDuty.Helpers
{
    internal static class FollowHelper
    {
        private static GameObject? _followTarget = null;
        private static float _followDistance = 0.25f;

        private static bool _enabled
        {
            get { return _enabled; }    
            set
            {
                if (value)
                    Svc.Framework.Update += FollowUpdate;
                else
                {
                    Svc.Framework.Update -= FollowUpdate;
                    InputHelper.SetKeyValue(W, Released);
                }
            }
        }

        internal static bool IsFollowing => _enabled;

        internal static void SetFollow(GameObject? gameObject, float followDistance = 0)
        {
            _enabled = _followTarget = gameObject;
            if (gameObject != null)
                _followTarget = gameObject;
            if (followDistance > 0)
                _followDistance = followDistance;
        } 

        internal static void SetFollowTarget(GameObject? gameObject) => _followTarget = gameObject;

        internal static void SetFollowDistance(float f) => _followDistance = f + 0.1f;

        private static void FollowUpdate(IFramework framework)
        {
            if (_followTarget == null || Svc.ClientState.LocalPlayer == null)
                return;

            if (ObjectHelper.GetDistanceToPlayer(_followTarget) >= _followDistance)
            {
                //AutoDuty.Plugin.OverrideCamera.Face(_followTarget.Position);
                //InputHelper.SetKeyValue(W, Down);
                List<Vector3> _followTargetList = [_followTarget.Position];
                VNavmesh_IPCSubscriber.Path_MoveTo(_followTargetList,false);
            }
            //else
                //InputHelper.SetKeyValue(W, Released);
        }
    }
}
