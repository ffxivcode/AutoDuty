using AutoDuty.IPC;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System;
using System.Numerics;

namespace AutoDuty.Helpers
{
    internal static class StuckHelper
    {
        internal static Vector3 LastPosition = Vector3.Zero;
        internal static long LastPositionUpdate = 0;

        internal static Vector3 LastStuckPosition       = Vector3.Zero;
        internal static long    LastStuckPositionUpdate = 0;

        private static byte counter = 0;

        internal static bool IsStuck(out byte count)
        {
            count = 0;
            if (!Player.Available) return false;
            if (!VNavmesh_IPCSubscriber.Path_IsRunning())
            {
                LastPositionUpdate = Environment.TickCount64;
            }
            else
            {
                if (Vector3.DistanceSquared(LastPosition, Player.Position) > 1f)
                {
                    LastPositionUpdate = Environment.TickCount64;
                    LastPosition       = Player.Position;
                }
            }


            if (Environment.TickCount64 - LastPositionUpdate > Plugin.Configuration.MinStuckTime && EzThrottler.Throttle("RequeueMoveTo", 1000))
            {
                LastStuckPosition       = Player.Position;
                LastStuckPositionUpdate = Environment.TickCount64;

                count                   = counter++;
                Svc.Log.Debug($"Stuck pathfinding: " + count);
                return true;
            }

            if (Environment.TickCount64 - LastStuckPositionUpdate > Plugin.Configuration.MinStuckTime * 10)
            {
                count = counter = 0;
            }

            return false;
        }
    }
}