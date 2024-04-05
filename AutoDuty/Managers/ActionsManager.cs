using System.Reflection;
using System;
using ECommons.DalamudServices;
using ClickLib.Clicks;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using AutoDuty.IPC;
using ECommons;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Throttlers;
using ECommons.GameHelpers;
using AutoDuty.Helpers;
using AutoDuty.External;
using System.Diagnostics;

namespace AutoDuty.Managers
{
    public class ActionsManager(AutoDuty _plugin, Chat _chat, TaskManager _taskManager, OverrideMovement _overrideMovement)
    {
        public readonly List<(string, string)> ActionsList =
        [
            ("Wait","how long?"),
            ("WaitFor","for?"),
            ("Boss","false"),
            ("Interactable","interact with?"),
            ("SelectYesno","yes or no?"),
            ("MoveToObject","Object Name?"),
            ("ExitDuty","false"),
            ("DutySpecificCode","step #?"),
            ("BossMod","on / off"),
            ("Target","Target what?"),
        ];

        private delegate void ExitDutyDelegate(char timeout);
        private readonly ExitDutyDelegate exitDuty = Marshal.GetDelegateForFunctionPointer<ExitDutyDelegate>(Svc.SigScanner.ScanText("40 53 48 83 ec 20 48 8b 05 ?? ?? ?? ?? 0f b6 d9"));

        public void InvokeAction(string action, object?[] p)
        {
            try
            {
                /*try
                {
                    Svc.Log.Info($"InvokeAction: {action} ParamCount: {p.Length} Param: {p[0]}");

                }
                catch(Exception)
                {
                    Svc.Log.Info($"InvokeAction: {action} ParamCount: {p.Length}");
                }*/
                if (!string.IsNullOrEmpty(action))
                {
                    Type thisType = GetType();
                    MethodInfo? actionTask = thisType.GetMethod(action);
                    _taskManager.Enqueue(() => actionTask?.Invoke(this, p));
                }
                else
                    Svc.Log.Error("no action");
            }
            catch (Exception)
            {
                //Svc.Log.Error(ex.ToString());
            }
        }

        public void BossMod(string sts) => _chat.ExecuteCommand($"/vbmai {sts}");

        public void Wait(string wait)
        {
            if (AutoDuty.Plugin.Player == null)
                return;
            AutoDuty.Plugin.Action = $"Wait: {wait}";
            _taskManager.Enqueue(() => _chat.ExecuteCommand($"/rotation auto"), int.MaxValue, "Wait");
            _taskManager.Enqueue(() => !ObjectHelper.InCombat(AutoDuty.Plugin.Player), int.MaxValue, "Wait");
            _taskManager.Enqueue(() => EzThrottler.Throttle("Wait", Convert.ToInt32(wait)), "Wait");
            _taskManager.Enqueue(() => EzThrottler.Check("Wait"), Convert.ToInt32(wait), "Wait");
            _taskManager.Enqueue(() => _chat.ExecuteCommand($"/rotation auto"), int.MaxValue, "Wait");
            _taskManager.Enqueue(() => !ObjectHelper.InCombat(AutoDuty.Plugin.Player), int.MaxValue, "Wait");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public unsafe void WaitFor(string waitForWhat)
        {
            AutoDuty.Plugin.Action = $"WaitFor: {waitForWhat}";
            switch (waitForWhat)
            {
                case "Combat":
                    _taskManager.Enqueue(() => _chat.ExecuteCommand($"/rotation auto"), int.MaxValue, "WaitFor");
                    _taskManager.Enqueue(() => !Player.Character->InCombat, int.MaxValue, "WaitFor");
                    break;
                case "IsValid":
                    _taskManager.Enqueue(() => !ObjectHelper.IsValid, 500, "WaitFor");
                    _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "WaitFor");
                    break;
                case "IsOccupied":
                    _taskManager.Enqueue(() => !ObjectHelper.IsOccupied, 500, "WaitFor");
                    _taskManager.Enqueue(() => ObjectHelper.IsOccupied, int.MaxValue, "WaitFor");
                    break;
                case "IsReady":
                    _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "WaitFor");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "WaitFor");
                    break;
            }
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");

        }

        private bool CheckPause() => _plugin.Stage == 5;

        public void ExitDuty(string _)
        {
            _chat.ExecuteCommand($"/rotation cancel");
            exitDuty.Invoke((char)0);
        }

        public unsafe bool IsAddonReady(nint addon) => addon > 0 && GenericHelpers.IsAddonReady((AtkUnitBase*)addon);

        public void SelectYesno(string Yesno)
        {
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"SelectYesno: {Yesno}", "SelectYesno");
            _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(Yesno.ToUpper().Equals("YES")), "SelectYesno");
            _taskManager.DelayNext("SelectYesno", 500);
            _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "SelectYesno");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void MoveToObject(string objectName)
        {
            GameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"MoveToObject: {objectName}";
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(objectName)) != null, "MoveToObject");
            _taskManager.Enqueue(() => { if (gameObject != null) VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(gameObject.Position, false); }, "MoveToObject");
            _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0, int.MaxValue, "MoveToObject");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void TreasureCoffer(string _) 
        {
            return;
        }

        private bool TargetCheck(GameObject? gameObject)
        {
            if (gameObject == null || gameObject.IsTargetable || gameObject.IsValid() || Svc.Targets.Target == gameObject)
                return true;

            if (EzThrottler.Check("TargetCheck"))
            {
                EzThrottler.Throttle("TargetCheck", 25);
                Svc.Targets.Target = gameObject;
            }
            return false;
        }

        public void Target(string objectName)
        {
            GameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"Target: {objectName}";
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(objectName)) != null, "Target");
            _taskManager.Enqueue(() => TargetCheck(gameObject), "Target");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        private bool InteractableCheck(GameObject? gameObject)
        {
            nint addon = Svc.GameGui.GetAddonByName("SelectYesno", 1);
            if (addon > 0)
            {
                SelectYesno("Yes");
                return true;
            }
            if (gameObject == null || !gameObject.IsTargetable || !gameObject.IsValid() || !ObjectHelper.IsValid)
                return true;

            if (EzThrottler.Throttle("Interactable", 250))
                ObjectHelper.InteractWithObject(gameObject);

            return false;
        }
        public unsafe void Interactable(string objectName)
        {
            GameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"Interactable: {objectName}";
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByNameAndRadius(objectName)) != null, "Interactable");
            _taskManager.Enqueue(() => InteractableCheck(gameObject), "Interactable");
            _taskManager.Enqueue(() => Player.Character->IsCasting, 500, "Interactable");
            _taskManager.Enqueue(() => !Player.Character->IsCasting, "Interactable");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        private bool BossCheck(bool hasModule, Vector3 bossV3, int numForbiddenZonesToIgnore)
        {
            if (!Svc.Condition[ConditionFlag.InCombat] && (AutoDuty.Plugin.BossObject?.IsDead ?? true) || !Svc.Condition[ConditionFlag.InCombat])
                return true;
            if (EzThrottler.Throttle("Boss", 10))
            {
                if (AutoDuty.Plugin.BossObject == null && Svc.Targets.Target != null)
                {
                    AutoDuty.Plugin.BossObject = (BattleChara?)Svc.Targets.Target;
                    hasModule = BossMod_IPCSubscriber.HasModule(AutoDuty.Plugin.BossObject);
                }
                if (BossMod_IPCSubscriber.ForbiddenZonesCount() > numForbiddenZonesToIgnore)
                    SetFollowStatus(false);
                else if (BossMod_IPCSubscriber.ForbiddenZonesCount() <= numForbiddenZonesToIgnore && (FollowTarget != null || hasModule))
                {
                    if (!hasModule)
                        SetFollowStatus(true);
                    else
                    {
                        GameObject? healerGameObject;
                        switch (Player.Object.ClassJob.GameData?.Role)
                        {
                            //tank - try to stay within 10 of boss waypoint, or move to boss if loose aggro
                            case 1:
                                if (AutoDuty.Plugin.BossObject != null && AutoDuty.Plugin.BossObject.TargetObject != Player.Object)
                                    MoveTo(bossV3, 3);
                                else if (ObjectHelper.GetDistanceToPlayer(bossV3) > 10)
                                    MoveTo(bossV3, 10);
                                break;
                            //healer - try to stay in range of anyone needing heals, priority to tank, else
                            //stay in range and try to stay behind/flanked (later implementation)
                            case 4:
                                var tankGameObject = ObjectHelper.GetTankPartyMember();
                                if (tankGameObject != null && ObjectHelper.GetDistanceToPlayer(tankGameObject) > 15)
                                    MoveTo(tankGameObject.Position, 15);
                                else if (AutoDuty.Plugin.BossObject != null && ECommons.GameFunctions.ObjectFunctions.GetAttackableEnemyCountAroundPoint(AutoDuty.Plugin.BossObject.Position, 8) <= 2)
                                    MoveTo(AutoDuty.Plugin.BossObject.Position, 15);
                                else if (AutoDuty.Plugin.BossObject != null)
                                    MoveTo(AutoDuty.Plugin.BossObject.Position, 3);
                                break;
                            //everyone else - stay in range of healer then try to stay behind/flanked (later implementation)
                            case 2:
                                healerGameObject = ObjectHelper.GetHealerPartyMember();
                                if (healerGameObject != null && ObjectHelper.GetDistanceToPlayer(healerGameObject) > 15)
                                    MoveTo(healerGameObject.Position, 15);
                                else if (AutoDuty.Plugin.BossObject != null)
                                        MoveTo(AutoDuty.Plugin.BossObject.Position, 3);
                                break;
                            default:
                                healerGameObject = ObjectHelper.GetHealerPartyMember();
                                if (healerGameObject != null && ObjectHelper.GetDistanceToPlayer(healerGameObject) > 15)
                                    MoveTo(healerGameObject.Position, 15);
                                else if (AutoDuty.Plugin.BossObject != null)
                                    MoveTo(AutoDuty.Plugin.BossObject.Position, 15);
                                break;
                        }
                    }
                }
            }
            return false;
        }
        
        GameObject FollowTarget;
        float FollowDistance;

        public void Boss(Vector3 bossV3)
        {
            Svc.Log.Info($"Starting Action Boss: {AutoDuty.Plugin.BossObject?.Name.TextValue ?? "null"}");
            
            //need to check boss for HasModule IPC and turn off follow and engage where we want to be checks (tanks near center, dps and healers behind or side, and melee same but in range)
            _chat.ExecuteCommand($"/rotation auto");
            GameObject? followTargetObject = null;
            GameObject? treasureCofferObject = null;
            var hasModule = false;
            var numForbiddenZonesToIgnore = 0;
            //AutoDuty.Plugin.StopForCombat = false;
            _taskManager.Enqueue(() => MovementHelper.PathfindAndMove(bossV3), int.MaxValue, "Boss");
            _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0, int.MaxValue, "Boss");
            if (AutoDuty.Plugin.BossObject == null)
                _taskManager.Enqueue(() => (AutoDuty.Plugin.BossObject = ObjectHelper.GetBossObject()) != null, "Boss");

            //check if our Boss has a Module
            _taskManager.Enqueue(() =>
            {
                if (AutoDuty.Plugin.BossObject != null)
                {
                    hasModule = BossMod_IPCSubscriber.HasModule(AutoDuty.Plugin.BossObject);
                }
                else if (Svc.Targets.Target != null)
                {
                    AutoDuty.Plugin.BossObject = (BattleChara)Svc.Targets.Target;
                    hasModule = BossMod_IPCSubscriber.HasModule(AutoDuty.Plugin.BossObject);
                }
                if (hasModule)
                {
                    if (BossMod_IPCSubscriber.ActiveModuleHasComponent("Cleave"))
                        numForbiddenZonesToIgnore++;
                    if (BossMod_IPCSubscriber.ActiveModuleHasComponent("Positioning"))
                        numForbiddenZonesToIgnore++;
                }
            });
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Boss: {AutoDuty.Plugin.BossObject?.Name.TextValue ?? ""} : VBM Module: {hasModule}");
            //switch our class type
            _taskManager.Enqueue(() =>
            {
                if (hasModule)
                {
                    followTargetObject = AutoDuty.Plugin.BossObject;
                    return;
                }
                followTargetObject = GetTrustMeleeDpsMemberObject() ? GetTrustRangedDpsMemberObject() : GetTrustMeleeDpsMemberObject();
                if (followTargetObject != null)
                {
                    SetFollowTarget(followTargetObject);
                    SetFollowDistance(0);
                    SetFollowStatus(true);
                }
            }, "Boss");
            _taskManager.Enqueue(() => BossCheck(hasModule, bossV3, numForbiddenZonesToIgnore), int.MaxValue, "Boss");
            _taskManager.Enqueue(() => AutoDuty.Plugin.StopForCombat = true, "Boss");
            _taskManager.Enqueue(() => SetFollowStatus(false), "Boss");
            _taskManager.Enqueue(() => AutoDuty.Plugin.BossObject = null, "Boss");
            if (AutoDuty.Plugin.Configuration.LootTreasure)
            {
                _taskManager.Enqueue(() => (treasureCofferObject = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure)?.FirstOrDefault(o => ObjectHelper.GetDistanceToPlayer(o) < 50)) != null);
                _taskManager.Enqueue(() => MovementHelper.PathfindAndMove(treasureCofferObject, 0.25f, 1f));
                _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilNotTargetable(treasureCofferObject));
            }
            _taskManager.Enqueue(() => Svc.Log.Info("Done Boss Action"));
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        private void MoveTo(Vector3 position, float tollerance)
        {
            if (ObjectHelper.GetDistanceToPlayer(position) <= tollerance)
                return;

            if (_overrideMovement.Precision != tollerance + 0.1f)
                _overrideMovement.Precision = tollerance + 0.1f;

            _overrideMovement.DesiredPosition = position;
        }

        private void SetFollowStatus(bool on)
        {
            if (on)
            {
                if (_overrideMovement.Precision != FollowDistance + 0.1f)
                    _overrideMovement.Precision = FollowDistance + 0.1f;

                _overrideMovement.DesiredPosition = FollowTarget.Position;
            }
            else
            {
                if (_overrideMovement.DesiredPosition != null)
                    _overrideMovement.DesiredPosition = null;
            }
        }

        private void SetFollowTarget(GameObject gameObject)
        {
            FollowTarget = gameObject;
        }

        private void SetFollowDistance(float f)
        {
            FollowDistance = f;
        }

        public GameObject? GetTrustTankMemberObject() => Svc.Buddies.FirstOrDefault(s => s.GameObject is Character chara && chara.ClassJob.GameData?.Role == 1)?.GameObject;

        public GameObject? GetTrustHealerMemberObject() => Svc.Buddies.FirstOrDefault(s => s.GameObject is Character chara && chara.ClassJob.GameData?.Role == 4)?.GameObject;

        public GameObject? GetTrustRangedDpsMemberObject() => Svc.Buddies.FirstOrDefault(s => s.GameObject is Character chara && chara.ClassJob.GameData?.Role == 3)?.GameObject;

        public GameObject? GetTrustMeleeDpsMemberObject() => Svc.Buddies.FirstOrDefault(s => s.GameObject is Character chara && chara.ClassJob.GameData?.Role == 2)?.GameObject;

        public enum OID : uint
        {
            Blue = 0x1E8554,
            Red = 0x1E8A8C,
            Green = 0x1E8A8D,
        }

        private string? GlobalStringStore;

        public unsafe void DutySpecificCode(string stage)
        {
            switch (Svc.ClientState.TerritoryType)
            {
                //Sastasha - From BossMod
                case 1036:
                    GameObject? gameObject = null;
                    switch (stage)
                    {
                        case "1":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj)?.FirstOrDefault(a => a.IsTargetable && (OID)a.DataId is OID.Blue or OID.Red or OID.Green)) != null);
                            _taskManager.Enqueue(() =>
                            {
                                if (gameObject != null)
                                {
                                    GlobalStringStore = ((OID)gameObject.DataId).ToString();
                                    Svc.Log.Info(((OID)gameObject.DataId).ToString());
                                }
                            });
                            break;
                        case "2":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(GlobalStringStore + " Coral Formation")) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => MovementHelper.PathfindAndMove(gameObject, 0.25f, 2.5f), "DutySpecificCode");
                            _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno") != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), "DutySpecificCode");
                            break;
                        case "3":
                            _taskManager.Enqueue(() => Svc.Log.Info("getting obj"));
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Inconspicuous Switch")) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => Svc.Log.Info("done getting obj, pathinfinding"));
                            _taskManager.Enqueue(() => MovementHelper.PathfindAndMove(gameObject, 0.25f, 2.5f), "DutySpecificCode");
                            _taskManager.DelayNext("DutySpecificCode", 1000);

                            _taskManager.Enqueue(() => Svc.Log.Info("done pathfinding, interacting"));
                            _taskManager.Enqueue(() => ObjectHelper.InteractWithObject(gameObject), "DutySpecificCode");
                            _taskManager.Enqueue(() => Svc.Log.Info("done"));
                            break;
                        default: break;
                    }
                    break;
                default: break;
            }
        }
    }
}
