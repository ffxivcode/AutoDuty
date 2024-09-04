using AutoDuty.IPC;
using AutoDuty.Windows;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;
using System.Numerics;

namespace AutoDuty.Helpers
{
    internal static class DeathHelper
    {
        private static PlayerState _deathState = PlayerState.Alive;
        internal static PlayerState DeathState
        {
            get
            {
                if (!AutoDuty.Plugin.Configuration.DutyModeEnum.EqualsAny(DutyMode.Regular, DutyMode.Trial, DutyMode.Raid) || AutoDuty.Plugin.Configuration.Unsynced)
                    return _deathState;
                else if (Player.Object.CurrentHp == 0 && _deathState != PlayerState.Dead)
                {
                    OnDeath();
                    return _deathState = PlayerState.Dead;
                }
                else if (Player.Object.CurrentHp > 0 && _deathState != PlayerState.Revived)
                {
                    _oldIndex = AutoDuty.Plugin.Indexer;
                    BossMod_IPCSubscriber.Presets_ClearActive();
                    _findShortcutStartTime = Environment.TickCount;
                    Svc.Framework.Update += OnRevive;
                    return _deathState = PlayerState.Revived;
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
                FindShortcut();

            if ((gameObject = ObjectHelper.GetObjectByDataId(2000700)) == null || !gameObject.IsTargetable)
            {
                Svc.Log.Debug($"OnRevive: Unable to find Shortcut");
                return;
            }
        }

        private static unsafe void OnRevive(IFramework _)
        {
            if (!EzThrottler.Throttle("OnRevive", 500) || !ObjectHelper.IsValid || ObjectHelper.PlayerIsCasting) return;

            if (gameObject == null || !gameObject.IsTargetable)
            {
                if (AutoDuty.Plugin.Indexer == 0) AutoDuty.Plugin.Indexer = FindWaypoint();
                AutoDuty.Plugin.Stage = Stage.Reading_Path;
                _deathState = PlayerState.Alive;
                return;
            }

            if (_oldIndex == AutoDuty.Plugin.Indexer)
                AutoDuty.Plugin.Indexer = FindWaypoint();

            if (!MovementHelper.Move(gameObject, 0.25f, 2))
                Svc.Log.Debug($"OnRevive: Moving to {gameObject.Name} at: {gameObject.Position} which is {ObjectHelper.GetDistanceToPlayer(gameObject)} away");
            else if (ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno") == null)
                Svc.Log.Debug($"OnRevive: Interacting with {gameObject.Name} until SelectYesno Addon appears");
            else if (!AddonHelper.ClickSelectYesno())
                Svc.Log.Debug($"OnRevive: Clicking Yes");

            TaskManager.Enqueue(() => { if (Indexer == 0) Indexer = FindWaypoint(); });
            TaskManager.Enqueue(() => Stage = Stage.Reading_Path);



            
        }

        internal static void Invoke() 
        {
            Svc.Log.Debug("AMHelper.Invoke");
            if (!AM_IPCSubscriber.IsEnabled)
            {
                Svc.Log.Info("AM requires a plugin, visit https://discord.gg/JzSxThjKnd for more info");
                Svc.Log.Info("DO NOT ask in Puni.sh discord about this option");
            }
            else if (State != ActionState.Running)
            {
                Svc.Log.Info("AM Started");
                State = ActionState.Running;
                AutoDuty.Plugin.States |= PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(false);
                SchedulerHelper.ScheduleAction("AMTimeOut", Stop, 600000);
                Svc.Framework.Update += AMUpdate;
            }
        }

        

        internal static void Stop() 
        {
            Svc.Log.Debug("AMHelper.Stop");
            if (State == ActionState.Running)
                Svc.Log.Info("AM Finished");
            GotoInnHelper.Stop();
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("AMTimeOut");
            _aMStarted = false;
            if (AM_IPCSubscriber.IsRunning())
                AM_IPCSubscriber.Stop();
            Svc.Framework.Update += AMStopUpdate;
            Svc.Framework.Update -= AMUpdate;
        }

        internal static ActionState State = ActionState.None;

        private static bool _aMStarted = false;
        private static IGameObject? SummoningBellGameObject => Svc.Objects.FirstOrDefault(x => x.DataId == SummoningBellHelper.SummoningBellDataIds((uint)AutoDuty.Plugin.Configuration.PreferredSummoningBellEnum));

        internal static unsafe void AMStopUpdate(IFramework framework)
        {
            if (!Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                State = ActionState.None;
                AutoDuty.Plugin.States &= ~PluginState.Other;
                if (!AutoDuty.Plugin.States.HasFlag(PluginState.Looping))
                    AutoDuty.Plugin.SetGeneralSettings(true);
                Svc.Framework.Update -= AMStopUpdate;
            }
            else if (Svc.Targets.Target != null)
                Svc.Targets.Target = null;
            else if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno))
                addonSelectYesno->Close(true);
            else if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* addonSelectString))
                addonSelectString->Close(true);
            else if (GenericHelpers.TryGetAddonByName("RetainerList", out AtkUnitBase* addonRetainerList))
                addonRetainerList->Close(true);
            else if (GenericHelpers.TryGetAddonByName("RetainerSellList", out AtkUnitBase* addonRetainerSellList))
                addonRetainerSellList->Close(true);
            else if (GenericHelpers.TryGetAddonByName("RetainerSell", out AtkUnitBase* addonRetainerSell))
                addonRetainerSell->Close(true);
            else if (GenericHelpers.TryGetAddonByName("ItemSearchResult", out AtkUnitBase* addonItemSearchResult))
                addonItemSearchResult->Close(true);
            return;
        }

        internal static unsafe void AMUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.States.HasFlag(PluginState.Paused))
                return;

            if (AutoDuty.Plugin.States.HasFlag(PluginState.Navigating))
            {
                Svc.Log.Debug("AutoDuty is Started, Stopping AMHelper");
                Stop();
            }
            if (!_aMStarted && AM_IPCSubscriber.IsRunning())
            {
                Svc.Log.Info("AM has Started");
                _aMStarted = true;
                return;
            }
            else if (_aMStarted && !AM_IPCSubscriber.IsRunning())
            {
                Svc.Log.Debug("AM is Complete");
                Stop();
                return;
            }

            if (!EzThrottler.Throttle("AM", 250))
                return;

            if (!ObjectHelper.IsValid) return;

            if (GotoHelper.State == ActionState.Running)
            {
                Svc.Log.Debug("Goto Running");
                return;
            }
            AutoDuty.Plugin.Action = "AM Running";

            if (SummoningBellGameObject != null && ObjectHelper.GetDistanceToPlayer(SummoningBellGameObject) > 4)
            {
                Svc.Log.Debug("Moving Closer to Summoning Bell");
                MovementHelper.Move(SummoningBellGameObject, 0.25f, 4);
            }
            else if (SummoningBellGameObject == null && GotoHelper.State != ActionState.Running)
            {
                Svc.Log.Debug("Moving to Summoning Bell Location");
                SummoningBellHelper.Invoke(AutoDuty.Plugin.Configuration.PreferredSummoningBellEnum);
            }
            else if (SummoningBellGameObject != null && ObjectHelper.GetDistanceToPlayer(SummoningBellGameObject) <= 4 && !_aMStarted && !GenericHelpers.TryGetAddonByName("RetainerList", out AtkUnitBase* addonRetainerList) && (ObjectHelper.InteractWithObjectUntilAddon(SummoningBellGameObject, "RetainerList") == null))
            {
                if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
                {
                    Svc.Log.Debug("Starting AM");
                    AM_IPCSubscriber.Start();
                }
                else
                    Svc.Log.Debug("Interacting with SummoningBell");
            }
        }
    }
}
