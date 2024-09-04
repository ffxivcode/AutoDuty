using AutoDuty.IPC;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;
using System.Numerics;

namespace AutoDuty.Helpers
{
    internal static class DeathHelper
    {
        private static PlayerLifeState _deathState = PlayerLifeState.Alive;
        internal static PlayerLifeState DeathState
        {
            get
            {
                if (!AutoDuty.Plugin.Configuration.DutyModeEnum.EqualsAny(DutyMode.Regular, DutyMode.Trial, DutyMode.Raid) || AutoDuty.Plugin.Configuration.Unsynced)
                    return _deathState;
                else if (Player.Object.CurrentHp == 0 && _deathState != PlayerLifeState.Dead)
                {
                    OnDeath();
                    return _deathState = PlayerLifeState.Dead;
                }
                else if (Player.Object.CurrentHp > 0 && _deathState != PlayerLifeState.Revived)
                {
                    _oldIndex = AutoDuty.Plugin.Indexer;
                    BossMod_IPCSubscriber.Presets_ClearActive();
                    _findShortcutStartTime = Environment.TickCount;
                    FindShortcut();
                    return _deathState = PlayerLifeState.Revived;
                }
                else
                    return _deathState;
            }
        }

        private static unsafe void OnDeath()
        {
            if (AutoDuty.Plugin.Configuration.DutyModeEnum.EqualsAny(DutyMode.Regular, DutyMode.Trial, DutyMode.Raid) && !AutoDuty.Plugin.Configuration.Unsynced)
                return;

            AutoDuty.Plugin.StopForCombat = true;
            AutoDuty.Plugin.SkipTreasureCoffer = true;

            if (VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.Path_Stop();

            if (AutoDuty.Plugin.TaskManager.IsBusy)
                AutoDuty.Plugin.TaskManager.Abort();
           
            if (AutoDuty.Plugin.Configuration.DutyModeEnum.EqualsAny(DutyMode.Regular, DutyMode.Trial, DutyMode.Raid))
            {
                if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno))
                    AddonHelper.ClickSelectYesno();
                else
                    SchedulerHelper.ScheduleAction("OnDeath", () => OnDeath(), 500);
            }
        }

        private static int _oldIndex = 0;
        private static IGameObject? _gameObject => ObjectHelper.GetObjectByDataId(2000700);
        private static int _findShortcutStartTime = 0;

        private static int FindWaypoint()
        {
            if (AutoDuty.Plugin.Indexer == 0)
            {
                //Svc.Log.Info($"Finding Closest Waypoint {ListBoxPOSText.Count}");
                float closestWaypointDistance = float.MaxValue;
                int closestWaypointIndex = -1;
                float currentDistance = 0;

                for (int i = 0; i < AutoDuty.Plugin.ListBoxPOSText.Count; i++)
                {
                    string node = AutoDuty.Plugin.ListBoxPOSText[i];

                    if (node.Contains("Boss|") && node.Replace("Boss|", "").All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                    {
                        currentDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(AutoDuty.Plugin.ListBoxPOSText[AutoDuty.Plugin.Indexer].Replace("Boss|", "").Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(AutoDuty.Plugin.ListBoxPOSText[AutoDuty.Plugin.Indexer].Replace("Boss|", "").Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(AutoDuty.Plugin.ListBoxPOSText[AutoDuty.Plugin.Indexer].Replace("Boss|", "").Split(',')[2], System.Globalization.CultureInfo.InvariantCulture)));

                        if (currentDistance < closestWaypointDistance)
                        {
                            closestWaypointDistance = currentDistance;
                            closestWaypointIndex = i;
                        }
                    }
                    else if (node.All(c => char.IsDigit(c) || c == ',' || c == ' ' || c == '-' || c == '.'))
                    {
                        currentDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(AutoDuty.Plugin.ListBoxPOSText[AutoDuty.Plugin.Indexer].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(AutoDuty.Plugin.ListBoxPOSText[AutoDuty.Plugin.Indexer].Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(AutoDuty.Plugin.ListBoxPOSText[AutoDuty.Plugin.Indexer].Split(',')[2], System.Globalization.CultureInfo.InvariantCulture)));
                        //Svc.Log.Info($"cd: {currentDistance}");
                        if (currentDistance < closestWaypointDistance)
                        {
                            closestWaypointDistance = ObjectHelper.GetDistanceToPlayer(new Vector3(float.Parse(AutoDuty.Plugin.ListBoxPOSText[AutoDuty.Plugin.Indexer].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(AutoDuty.Plugin.ListBoxPOSText[AutoDuty.Plugin.Indexer].Split(',')[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(AutoDuty.Plugin.ListBoxPOSText[AutoDuty.Plugin.Indexer].Split(',')[2], System.Globalization.CultureInfo.InvariantCulture)));
                            closestWaypointIndex = i;
                        }
                    }
                }
                //Svc.Log.Info($"Closest Waypoint was {closestWaypointIndex}");
                return closestWaypointIndex + 1;
            }

            if (AutoDuty.Plugin.Indexer != -1)
            {
                bool revivalFound = ContentPathsManager.DictionaryPaths[AutoDuty.Plugin.CurrentTerritoryType].Paths[AutoDuty.Plugin.CurrentPath].RevivalFound;

                //Svc.Log.Info("Finding Last Boss");
                for (int i = AutoDuty.Plugin.Indexer; i >= 0; i--)
                {
                    if (revivalFound)
                    {
                        if (AutoDuty.Plugin.ListBoxPOSText[i].Contains("Revival|") && i != AutoDuty.Plugin.Indexer)
                            return i;
                    }
                    else
                    {
                        if (AutoDuty.Plugin.ListBoxPOSText[i].Contains("Boss|") && i != AutoDuty.Plugin.Indexer)
                            return i + 1;
                    }
                }
            }

            return 0;
        }

        private static void FindShortcut()
        {
            if (_gameObject == null && Environment.TickCount <= (_findShortcutStartTime + 5000))
            {
                Svc.Log.Debug($"OnRevive: Searching for shortcut");
                SchedulerHelper.ScheduleAction("FindShortcut", FindShortcut, 500);
                return;
            }

            if (_gameObject == null || !_gameObject!.IsTargetable)
            {
                Svc.Log.Debug($"OnRevive: Couldn't find shortcut");
                Stop();
                return;
            }

            Svc.Log.Debug("OnRevive: Found shortcut");
            Svc.Framework.Update += OnRevive;
        }

        private static void Stop()
        {
            Svc.Framework.Update -= OnRevive;
            AutoDuty.Plugin.Stage = Stage.Reading_Path;
            _deathState = PlayerLifeState.Alive;
        }

        private static unsafe void OnRevive(IFramework _)
        {
            if (!EzThrottler.Throttle("OnRevive", 500) || !ObjectHelper.IsValid || ObjectHelper.PlayerIsCasting) return;

            if (_gameObject == null || !_gameObject.IsTargetable)
            {
                Svc.Log.Debug("OnRevive: Done");
                if (AutoDuty.Plugin.Indexer == 0) AutoDuty.Plugin.Indexer = FindWaypoint();
                Stop();
                return;
            }

            if (_oldIndex == AutoDuty.Plugin.Indexer)
                AutoDuty.Plugin.Indexer = FindWaypoint();

            if (!MovementHelper.Move(_gameObject, 0.25f, 2))
                Svc.Log.Debug($"OnRevive: Moving to {_gameObject.Name} at: {_gameObject.Position} which is {ObjectHelper.GetDistanceToPlayer(_gameObject)} away");
            else if (ObjectHelper.InteractWithObjectUntilAddon(_gameObject, "SelectYesno") == null)
                Svc.Log.Debug($"OnRevive: Interacting with {_gameObject.Name} until SelectYesno Addon appears");
            else if (!AddonHelper.ClickSelectYesno())
                Svc.Log.Debug($"OnRevive: Clicking Yes");
        }
    }
}
