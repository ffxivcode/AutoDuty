using System.Reflection;
using System;
using ECommons.DalamudServices;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using AutoDuty.IPC;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Throttlers;
using ECommons.GameHelpers;
using AutoDuty.Helpers;
using ECommons.Automation;
using AutoDuty.Windows;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoDuty.Managers
{
    internal class ActionsManager(AutoDuty _plugin, Chat _chat, TaskManager _taskManager)
    {
        public readonly List<(string, string)> ActionsList =
        [
            ("Wait","how long?"),
            ("WaitFor","for?"),
            ("Boss","false"),
            ("Interactable","interact with?"),
            ("TreasureCoffer","false"),
            ("SelectYesno","yes or no?"),
            ("MoveToObject","Object Name?"),
            ("DutySpecificCode","step #?"),
            ("BossMod","on / off"),
            ("Target","Target what?"),
            ("ChatCommand","Command with args?"),
            ("StopForCombat","True/False")
        ];

        //private delegate void ExitDutyDelegate(char timeout);
        //private readonly ExitDutyDelegate exitDuty = Marshal.GetDelegateForFunctionPointer<ExitDutyDelegate>(Svc.SigScanner.ScanText("40 53 48 83 ec 20 48 8b 05 ?? ?? ?? ?? 0f b6 d9"));

        public void InvokeAction(string action, object?[] p)
        {
            try
            {
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

        public void StopForCombat(string TrueFalse)
        {
            if (AutoDuty.Plugin.Player == null)
                return;
            AutoDuty.Plugin.Action = $"StopForCombat: {TrueFalse}";
            AutoDuty.Plugin.StopForCombat = TrueFalse.Equals("true", StringComparison.InvariantCultureIgnoreCase);
            //AutoDuty.Plugin.Action = "";
        }

        public void ChatCommand(string commandAndArgs)
        {
            if (AutoDuty.Plugin.Player == null)
                return;
            AutoDuty.Plugin.Action = $"ChatCommand: {commandAndArgs}";
            _taskManager.Enqueue(() => _chat.ExecuteCommand(commandAndArgs), "ChatCommand");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void Wait(string wait)
        {
            if (AutoDuty.Plugin.Player == null)
                return;
            AutoDuty.Plugin.Action = $"Wait: {wait}";
            _taskManager.Enqueue(() => _chat.ExecuteCommand($"/rotation auto"), "Wait");
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

        public unsafe void ExitDuty(string _)
        {
            _chat.ExecuteCommand($"/rotation cancel");
            //exitDuty.Invoke((char)0);
            AtkUnitBase* addon = null;
            TaskManager exitDutyTaskManager = new();
            exitDutyTaskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContentsFinderMenu"), "ExitDuty");
            exitDutyTaskManager.Enqueue(() => { if (addon == null) OpenContentsFinderMenu(); }, "ExitDuty");
            exitDutyTaskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderMenu", out addon) && GenericHelpers.IsAddonReady(addon), "ExitDuty");
            exitDutyTaskManager.DelayNext("ExitDuty", 50);
            exitDutyTaskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 0), "ExitDuty");
            exitDutyTaskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("SelectYesno", out addon) && GenericHelpers.IsAddonReady(addon), "ExitDuty");
            exitDutyTaskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 0), "ExitDuty");
        }

        private unsafe void OpenContentsFinderMenu() => AgentModule.Instance()->GetAgentByInternalId(AgentId.ContentsFinderMenu)->Show();

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
            IGameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"MoveToObject: {objectName}";
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(objectName)) != null, "MoveToObject");
            _taskManager.Enqueue(() => MovementHelper.Move(gameObject), int.MaxValue, "MoveToObject");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void TreasureCoffer(string _) 
        {
            if (AutoDuty.Plugin.Configuration.LootTreasure && !AutoDuty.Plugin.Configuration.LootBossTreasureOnly)
                Interactable("Treasure Coffer");
            return;
        }

        private bool TargetCheck(IGameObject? gameObject)
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
            IGameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"Target: {objectName}";
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByPartialName(objectName)) != null, "Target");
            _taskManager.Enqueue(() => TargetCheck(gameObject), "Target");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        private bool InteractableCheck(IGameObject? gameObject)
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
            {
                if (ObjectHelper.GetBattleDistanceToPlayer(gameObject) > 2f)
                    MovementHelper.Move(gameObject, 0.25f, 2f, false);
                else if (!VNavmesh_IPCSubscriber.Path_IsRunning())
                    ObjectHelper.InteractWithObject(gameObject);
            }

            return false;
        }
        public unsafe void Interactable(string objectName)
        {
            IGameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"Interactable: {objectName}";
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(objectName)) != null, "Interactable");
            _taskManager.Enqueue(() => InteractableCheck(gameObject), "Interactable");
            _taskManager.Enqueue(() => Player.Character->IsCasting, 500, "Interactable");
            _taskManager.Enqueue(() => !Player.Character->IsCasting, "Interactable");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        private bool BossCheck(bool hasModule, Vector3 bossV3, int numForbiddenZonesToIgnore, IGameObject? followTargetObject)
        {
            if (((AutoDuty.Plugin.BossObject?.IsDead ?? true) && !Svc.Condition[ConditionFlag.InCombat]) || !Svc.Condition[ConditionFlag.InCombat])
                return true;
            if (EzThrottler.Throttle("Boss", 10))
            {
                if (AutoDuty.Plugin.BossObject == null && Svc.Targets.Target != null)
                {
                    AutoDuty.Plugin.BossObject = (IBattleChara?)Svc.Targets.Target;
                    hasModule = BossMod_IPCSubscriber.HasModuleByDataId(AutoDuty.Plugin.BossObject!.DataId);
                }
                if (BossMod_IPCSubscriber.ForbiddenZonesCount() > numForbiddenZonesToIgnore)
                    FollowHelper.SetFollow(null);
                else if (BossMod_IPCSubscriber.ForbiddenZonesCount() <= numForbiddenZonesToIgnore)
                {
                    if (!hasModule)
                        FollowHelper.SetFollow(followTargetObject);
                    else
                    {
                        IGameObject? healerGameObject;
                        switch (Player.Object.ClassJob.GameData?.Role)
                        {
                            //tank - try to stay within 10 of boss waypoint, or move to boss if loose aggro
                            case 1:
                                if (AutoDuty.Plugin.BossObject != null && AutoDuty.Plugin.BossObject.TargetObject != Player.Object)
                                    FollowHelper.SetFollow(AutoDuty.Plugin.BossObject, 3f);
                                else if (ObjectHelper.GetDistanceToPlayer(bossV3) > 10)
                                    MovementHelper.Move(bossV3, 0.25f, 10f);
                                break;
                            //healer - try to stay in range of anyone needing heals, priority to tank, else
                            //stay in range and try to stay behind/flanked (later implementation)
                            case 4:
                                var tankGameObject = ObjectHelper.GetTankPartyMember();
                                if (tankGameObject != null && ObjectHelper.GetDistanceToPlayer(tankGameObject) > 15)
                                    FollowHelper.SetFollow(tankGameObject, 15);
                                else if (AutoDuty.Plugin.BossObject != null && ECommons.GameFunctions.ObjectFunctions.GetAttackableEnemyCountAroundPoint(AutoDuty.Plugin.BossObject.Position, 8) <= 2)
                                    FollowHelper.SetFollow(AutoDuty.Plugin.BossObject, 15);
                                else if (AutoDuty.Plugin.BossObject != null)
                                    FollowHelper.SetFollow(AutoDuty.Plugin.BossObject, 3);
                                break;
                            //everyone else - stay in range of healer then try to stay behind/flanked (later implementation)
                            case 2:
                                healerGameObject = ObjectHelper.GetHealerPartyMember();
                                if (healerGameObject != null && ObjectHelper.GetDistanceToPlayer(healerGameObject) > 15)
                                    FollowHelper.SetFollow(healerGameObject, 15);
                                else if (AutoDuty.Plugin.BossObject != null)
                                    FollowHelper.SetFollow(AutoDuty.Plugin.BossObject, 3);
                                break;
                            default:
                                healerGameObject = ObjectHelper.GetHealerPartyMember();
                                if (healerGameObject != null && ObjectHelper.GetDistanceToPlayer(healerGameObject) > 15)
                                    FollowHelper.SetFollow(healerGameObject, 15);
                                else if (AutoDuty.Plugin.BossObject != null)
                                    FollowHelper.SetFollow(AutoDuty.Plugin.BossObject, 15);
                                break;
                        }
                    }
                }
            }
            return false;
        }
        private bool BossMoveCheck(Vector3 bossV3)
        {
            if (AutoDuty.Plugin.BossObject != null && AutoDuty.Plugin.BossObject.InCombat())
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                return true;
            }
            return MovementHelper.Move(bossV3);
        }
            
        public void Boss(Vector3 bossV3)
        {
            Svc.Log.Info($"Starting Action Boss: {AutoDuty.Plugin.BossObject?.Name.TextValue ?? "null"}");
            ReflectionHelper.RotationSolver_Reflection.RotationAuto();
            IGameObject? followTargetObject = null;
            IGameObject? treasureCofferObject = null;
            var hasModule = false;
            var numForbiddenZonesToIgnore = 0;
            _taskManager.Enqueue(() => BossMoveCheck(bossV3), "Boss");
            if (AutoDuty.Plugin.BossObject == null)
                _taskManager.Enqueue(() => (AutoDuty.Plugin.BossObject = ObjectHelper.GetBossObject()) != null, "Boss");
            //check if our Boss has a Module
            _taskManager.Enqueue(() =>
            {
                if (AutoDuty.Plugin.BossObject != null)
                {
                    hasModule = BossMod_IPCSubscriber.HasModuleByDataId(AutoDuty.Plugin.BossObject!.DataId);
                }
                else if (Svc.Targets.Target != null)
                {
                    AutoDuty.Plugin.BossObject = (IBattleChara)Svc.Targets.Target;
                    hasModule = BossMod_IPCSubscriber.HasModuleByDataId(AutoDuty.Plugin.BossObject!.DataId);
                }
                if (hasModule)
                {
                    if (BossMod_IPCSubscriber.ActiveModuleHasComponent("Cleave"))
                        numForbiddenZonesToIgnore++;
                    if (BossMod_IPCSubscriber.ActiveModuleHasComponent("Positioning"))
                        numForbiddenZonesToIgnore++;
                    if (BossMod_IPCSubscriber.ActiveModuleHasComponent("RonkanFire"))
                        numForbiddenZonesToIgnore = 4;
                }
            }, "Boss");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Boss: {AutoDuty.Plugin.BossObject?.Name.TextValue ?? ""}", "Boss");
            //switch our class type
            _taskManager.Enqueue(() =>
            {
                if (hasModule)
                {
                    followTargetObject = AutoDuty.Plugin.BossObject;
                    return;
                }
                switch (Player.Object.ClassJob.GameData?.Role)
                {
                    //tank
                    case 1:
                        followTargetObject = GetTrustMeleeDpsMemberObject() ?? GetTrustRangedDpsMemberObject();
                        break;
                    //melee
                    case 2:
                        followTargetObject = GetTrustMeleeDpsMemberObject() ?? GetTrustTankMemberObject();
                        break;
                    //ranged or healer
                    case 3 or 4:
                        followTargetObject = GetTrustRangedDpsMemberObject() ?? GetTrustMeleeDpsMemberObject();
                        break;
                }
                FollowHelper.SetFollow(followTargetObject, 0);
            }, "Boss");
            _taskManager.Enqueue(() => Svc.Condition[ConditionFlag.InCombat], "Boss");
            _taskManager.Enqueue(() => BossCheck(hasModule, bossV3, numForbiddenZonesToIgnore, followTargetObject), int.MaxValue, "Boss");
            _taskManager.Enqueue(() => { AutoDuty.Plugin.StopForCombat = true; }, "Boss");
            _taskManager.Enqueue(() => FollowHelper.SetFollow(null), "Boss");
            _taskManager.Enqueue(() => { AutoDuty.Plugin.BossObject = null; }, "Boss");
            if (AutoDuty.Plugin.Configuration.LootTreasure)
            {
                _taskManager.Enqueue(() => (treasureCofferObject = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure)?.FirstOrDefault(o => ObjectHelper.GetDistanceToPlayer(o) < 50)) != null, "Boss");
                _taskManager.Enqueue(() => MovementHelper.Move(treasureCofferObject, 0.25f, 1f), int.MaxValue, "Boss");
                _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilNotTargetable(treasureCofferObject), "Boss");
            }
            _taskManager.DelayNext("Boss", 500);
            _taskManager.Enqueue(() => { AutoDuty.Plugin.Action = ""; }, "Boss");
        }

        public IGameObject? GetTrustTankMemberObject() => Svc.Buddies.FirstOrDefault(s => s.GameObject is ICharacter chara && chara.ClassJob.GameData?.Role == 1)?.GameObject;

        public IGameObject? GetTrustHealerMemberObject() => Svc.Buddies.FirstOrDefault(s => s.GameObject is ICharacter chara && chara.ClassJob.GameData?.Role == 4)?.GameObject;

        public IGameObject? GetTrustRangedDpsMemberObject() => Svc.Buddies.FirstOrDefault(s => s.GameObject is ICharacter chara && chara.ClassJob.GameData?.Role == 3)?.GameObject;

        public IGameObject? GetTrustMeleeDpsMemberObject() => Svc.Buddies.FirstOrDefault(s => s.GameObject is ICharacter chara && chara.ClassJob.GameData?.Role == 2)?.GameObject;

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
                    IGameObject? gameObject = null;
                    switch (stage)
                    {
                        case "1":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj)?.FirstOrDefault(a => a.IsTargetable && (OID)a.DataId is OID.Blue or OID.Red or OID.Green)) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() =>
                            {
                                if (gameObject != null)
                                {
                                    GlobalStringStore = ((OID)gameObject.DataId).ToString();
                                    Svc.Log.Info(((OID)gameObject.DataId).ToString());
                                }
                            }, "DutySpecificCode");
                            break;
                        case "2":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(GlobalStringStore + " Coral Formation")) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2.5f), "DutySpecificCode");
                            _taskManager.Enqueue(() => ObjectHelper.InteractWithObjectUntilAddon(gameObject, "SelectYesno") != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(), "DutySpecificCode");
                            break;
                        case "3":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName("Inconspicuous Switch")) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2.5f), "DutySpecificCode");
                            _taskManager.DelayNext("DutySpecificCode", 1000);
                            _taskManager.Enqueue(() => ObjectHelper.InteractWithObject(gameObject), "DutySpecificCode");
                            break;
                        default: break;
                    }
                    break;
                default: break;
            }
        }
    }
}
