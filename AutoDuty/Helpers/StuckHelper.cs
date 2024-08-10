using AutoDuty.IPC;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.Toast;
using ECommons;
using ECommons.Automation;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Logging;
using ECommons.MathHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static AutoDuty.Helpers.ReaderTelepotTown;

namespace AutoDuty.Helpers
{
    internal static class StuckHelper
    {
        internal static Vector3 LastPosition = Vector3.Zero;
        internal static long LastPositionUpdate = 0;

        internal unsafe static bool IsStuck()
        {
            if (!Player.Available) return false;
            if (!VNavmesh_IPCSubscriber.Path_IsRunning())
            {
                LastPositionUpdate = Environment.TickCount64;
            }
            else
            {
                if (Vector3.Distance(LastPosition, Player.Position) > 0.5f)
                {
                    LastPositionUpdate = Environment.TickCount64;
                    LastPosition = Player.Position;
                }
            }

            if (Environment.TickCount64 - LastPositionUpdate > 500 && EzThrottler.Throttle("RequeueMoveTo", 1000))
            {
                Svc.Log.Debug($"Stuck pathfinding.");
                return true;
            }

            return false;
        }
    }
}
