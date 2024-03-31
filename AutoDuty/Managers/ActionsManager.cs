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

namespace AutoDuty.Managers
{
    public class ActionsManager(AutoDuty _plugin, Chat _chat, TaskManager _taskManager)
    {
        public readonly List<(string, string)> ActionsList =
        [
            ("Wait","how long?"),
            ("WaitFor","for?"),
            ("Boss","move to leash location"),
            ("Interactable","interact with?"),
            ("SelectYesno","yes or no?"),
            ("MoveToObject","Object Name?"),
            ("ExitDuty","false"),
            ("TreasureCoffer","false"),
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
                if (!string.IsNullOrEmpty(action))
                {
                    Type thisType = GetType();
                    MethodInfo? actionTask = thisType.GetMethod(action);
                    _taskManager.Enqueue(() => actionTask?.Invoke(this, p));
                }
                else
                    Svc.Log.Error("no action");
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }
        }

        public void BossMod(string sts) => _chat.ExecuteCommand($"/vbmai {sts}");

        public void Wait(string wait)
        {
            if (AutoDuty.Plugin.Player == null)
                return;
            _taskManager.Enqueue(() => !ObjectHelper.InCombat(AutoDuty.Plugin.Player), int.MaxValue, "Wait");
            _taskManager.Enqueue(() => EzThrottler.Throttle("Wait", Convert.ToInt32(wait)), "Wait");
            _taskManager.Enqueue(() => EzThrottler.Check("Wait"), Convert.ToInt32(wait), "Wait");
            _taskManager.Enqueue(() => !ObjectHelper.InCombat(AutoDuty.Plugin.Player), int.MaxValue, "Wait");
        }

        public unsafe void WaitFor(string waitForWhat)
        {
            switch (waitForWhat)
            {
                case "Combat":
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

        }

        private bool CheckPause() => _plugin.Stage == 5;

        public void ExitDuty(string _)
        {
            _chat.ExecuteCommand($"/rotation cancel");
            exitDuty.Invoke((char)0);
        }

        public unsafe bool IsAddonReady(nint addon) => addon > 0 && GenericHelpers.IsAddonReady((AtkUnitBase*)addon);

        private bool SelectYesnoCheck(nint addon, string Yesno)
        {
            if (addon == 0 || !IsAddonReady(addon))
                return true;

            if (EzThrottler.Check("SelectYesno"))
            {
                EzThrottler.Throttle("SelectYesno", 250);
                if (Yesno.Equals(""))
                    ClickSelectYesNo.Using(addon).Yes();
                else
                {
                    if (Yesno.ToUpper().Equals("YES"))
                        ClickSelectYesNo.Using(addon).Yes();
                    else if (Yesno.ToUpper().Equals("NO"))
                        ClickSelectYesNo.Using(addon).No();
                }
            }
            return false;
        }
        public void SelectYesno(string Yesno)
        {
            nint addon = 0;

            _taskManager.Enqueue(() => (addon = Svc.GameGui.GetAddonByName("SelectYesno", 1)) > 0, "SelectYesno");
            _taskManager.Enqueue(() => IsAddonReady(addon), "SelectYesno");
            _taskManager.Enqueue(() => SelectYesnoCheck(addon, Yesno), int.MaxValue, "SelectYesno");
            _taskManager.DelayNext("SelectYesno", 500);
            _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "SelectYesno");
        }

        public void MoveToObject(string objectName)
        {
            GameObject? gameObject = null;
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(objectName)) != null, "MoveToObject");
            _taskManager.Enqueue(() => { if (gameObject != null) VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(gameObject.Position, false); }, "MoveToObject");
            _taskManager.Enqueue(() => !VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0, int.MaxValue, "MoveToObject");
        }

        public void TreasureCoffer(string _) => Interactable("Treasure Coffer");

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
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(objectName)) != null, "Target");
            _taskManager.Enqueue(() => TargetCheck(gameObject), "Target");
        }

        private bool InteractableCheck(GameObject? gameObject)
        {
            nint addon = Svc.GameGui.GetAddonByName("SelectYesno", 1);
            if (addon > 0)
            {
                SelectYesno("Yes");
                return true;
            }
            if (gameObject == null || !gameObject.IsTargetable || !gameObject.IsValid())
                return true;

            if (EzThrottler.Check("Interactable"))
            {
                EzThrottler.Throttle("Interactable", 10);
                ObjectHelper.InteractWithObject(gameObject);
            }
            return false;
        }
        public unsafe void Interactable(string objectName)
        {
            GameObject? gameObject = null;
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByNameAndRadius(objectName)) != null, "Interactable");
            _taskManager.Enqueue(() => InteractableCheck(gameObject), "Interactable");
            _taskManager.Enqueue(() => Player.Character->IsCasting, 500, "Interactable");
            _taskManager.Enqueue(() => !Player.Character->IsCasting, "Interactable");
        }

        private bool BossCheck(GameObject? bossObject)
        {

            if (!Svc.Condition[ConditionFlag.InCombat] || (bossObject?.IsDead ?? false))
                return true;
            if (EzThrottler.Check("Boss"))
            {
                EzThrottler.Throttle("Boss", 10);

                if (BossMod_IPCSubscriber.ForbiddenZonesCount() > 0)
                    SetFollowStatus(false);
                else if (BossMod_IPCSubscriber.ForbiddenZonesCount() == 0)
                    SetFollowStatus(true);
            }
            return false;
        }
        
        GameObject FollowTarget;
        float FollowDistance;

        public void Boss(string x, string y, string z)
        {
            GameObject? followTargetObject = null;
            BattleChara? bossObject = null;
            AutoDuty.Plugin.StopForCombat = false;
            VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(new Vector3(float.Parse(x), float.Parse(y), float.Parse(z)), false);
            _taskManager.Enqueue(() => (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0), int.MaxValue, "Boss");
            _taskManager.DelayNext("Boss", 5000);

            //get our BossObject
            _taskManager.Enqueue(() => bossObject = GetBossObject(), "Boss");

            //switch our class type
            _taskManager.Enqueue(() =>
            {
                switch (Player.Object.ClassJob.GameData?.Role)
                {
                    //tank - follow healer
                    case 1:
                        //get our healer object
                        followTargetObject = GetTrustHealerMemberObject();
                        break;
                    //everyone else - follow tank
                    default:
                        //get our tank object
                        followTargetObject = GetTrustTankMemberObject();
                        break;
                }
                if (followTargetObject != null)
                {
                    SetFollowTarget(followTargetObject);
                    SetFollowDistance(0);
                    SetFollowStatus(true);
                }
            }, "Boss");
            _taskManager.Enqueue(() => BossCheck(bossObject), int.MaxValue, "Boss");
            _taskManager.Enqueue(() => AutoDuty.Plugin.StopForCombat = true, "Boss");
            _taskManager.Enqueue(() => SetFollowStatus(false), "Boss");
        }

        private OverrideMovement _overrideMovement = new();

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

        private static BattleChara? GetBossObject()
        {
            var battleCharas = ObjectHelper.GetObjectsByRadius(30)?.OfType<BattleChara>();
            if (battleCharas == null)
                return null;
            BattleChara? bossObject = default;
            foreach (var battleChara in battleCharas)
            {
                if (ObjectHelper.IsBossFromIcon(battleChara))
                    bossObject = battleChara;
            }

            return bossObject;
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

        public void DutySpecificCode(string stage)
        {
            switch (Svc.ClientState.TerritoryType)
            {
                //Sastasha - From BossMod
                case 1036:
                    switch (stage)
                    {
                        case "1":
                            var b = Svc.Objects.FirstOrDefault(a => a.IsTargetable && (OID)a.DataId is OID.Blue or OID.Red or OID.Green);
                            if (b != null)
                            {
                                GlobalStringStore = ((OID)b.DataId).ToString();
                                Svc.Log.Info(((OID)b.DataId).ToString());
                            }
                            break;
                        case "2":
                            var a = Svc.Objects.Where(a => a.Name.TextValue.Equals(GlobalStringStore + " Coral Formation")).FirstOrDefault();
                            if (a != null)
                            {
                                VNavmesh_IPCSubscriber.Path_SetTolerance(2.5f);
                                VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(a.Position, false);
                                _taskManager.Enqueue(() => (!VNavmesh_IPCSubscriber.SimpleMove_PathfindInProgress() && VNavmesh_IPCSubscriber.Path_NumWaypoints() == 0), int.MaxValue, "DutySpecificCode");
                                _taskManager.Enqueue(() => VNavmesh_IPCSubscriber.Path_SetTolerance(0.25f), "DutySpecificCode");
                                _taskManager.Enqueue(() => Interactable(a.Name.TextValue), "DutySpecificCode");
                            }
                            break;
                        default: break;
                    }
                    break;
                default: break;
            }
        }
    }
}
