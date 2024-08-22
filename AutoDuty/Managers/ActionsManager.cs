using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;

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
            ("AutoMoveFor", "how long?"),
            ("ChatCommand","Command with args?"),
            ("StopForCombat","True/False"),
            ("Revival",  "false"),
            ("ForceAttack",  "false"),
            ("Jump", "automove for how long before"),
            ("PausePandora", "Which feature | how long"),
            ("CameraFacing", "Face which Coords?"),
            ("ClickTalk", "false")
        ];

        public void InvokeAction(string action, object?[] p)
        {
            try
            {
                if (!string.IsNullOrEmpty(action))
                {
                    Type thisType = GetType();
                    MethodInfo? actionTask = thisType.GetMethod(action);
                    _taskManager.Enqueue(() => actionTask?.Invoke(this, p), $"InvokeAction-{actionTask?.Name}");
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
            if (!Player.Available)
                return;

            var boolTrueFalse = TrueFalse.Equals("true", StringComparison.InvariantCultureIgnoreCase);
            AutoDuty.Plugin.Action = $"StopForCombat: {TrueFalse}";
            AutoDuty.Plugin.StopForCombat = boolTrueFalse;
            _taskManager.Enqueue(() => _chat.ExecuteCommand($"/vbmai followtarget {(boolTrueFalse ? "on" : "off")}"), "StopForCombat");
        }

        public unsafe void ForceAttack(string timeoutTime)
        {
            var tot = timeoutTime.IsNullOrEmpty() ? 10000 : int.TryParse(timeoutTime, out int time) ? time : 0;
            if (timeoutTime.IsNullOrEmpty())
                timeoutTime = "10000";
            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "ForceAttack-GA16");
            _taskManager.Enqueue(() => Svc.Targets.Target != null, 500, "ForceAttack-GA1");
            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 1), "ForceAttack-GA1");
            _taskManager.Enqueue(() => Player.Object.InCombat(), tot, "ForceAttack-WaitForCombat");
        }

        public unsafe void Jump(string automoveTime)
        {
            AutoDuty.Plugin.Action = $"Jumping";

            if (int.TryParse(automoveTime, out int wait) && wait > 0)
            {
                _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove on"), "Jump");
                _taskManager.Enqueue(() => EzThrottler.Throttle("AutoMove", Convert.ToInt32(wait)), "Jump");
                _taskManager.Enqueue(() => EzThrottler.Check("AutoMove"), Convert.ToInt32(wait), "Jump");
            }

            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2), "Jump");

            if (wait > 0)
            {
                _taskManager.Enqueue(() => EzThrottler.Throttle("AutoMove", Convert.ToInt32(100)), "Jump");
                _taskManager.Enqueue(() => EzThrottler.Check("AutoMove"), Convert.ToInt32(100), "AutoMove");
                _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove off"), "Jump");
            }
        }

        public void ChatCommand(string commandAndArgs)
        {
            if (!Player.Available)
                return;
            AutoDuty.Plugin.Action = $"ChatCommand: {commandAndArgs}";
            _taskManager.Enqueue(() => _chat.ExecuteCommand(commandAndArgs), "ChatCommand");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void AutoMoveFor(string wait)
        {
            if (!Player.Available)
                return;
            AutoDuty.Plugin.Action = $"AutoMove For {wait}";
            var movementMode = Svc.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) ? mode : 0;
            _taskManager.Enqueue(() => { if (movementMode == 1) Svc.GameConfig.UiControl.Set("MoveMode", 0); });
            _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove on"), "AutoMove");
            _taskManager.Enqueue(() => EzThrottler.Throttle("AutoMove", Convert.ToInt32(wait)), "AutoMove");
            _taskManager.Enqueue(() => EzThrottler.Check("AutoMove") || !ObjectHelper.IsReady, Convert.ToInt32(wait), "AutoMove");
            _taskManager.Enqueue(() => { if (movementMode == 1) Svc.GameConfig.UiControl.Set("MoveMode", 1); });
            _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "AutoMove");
            _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove off"), "AutoMove");
        }

        public unsafe void Wait(string wait)
        {
            AutoDuty.Plugin.Action = $"Wait: {wait}";
            _taskManager.Enqueue(() => !Player.Character->InCombat, int.MaxValue, "Wait");
            _taskManager.Enqueue(() => EzThrottler.Throttle("Wait", Convert.ToInt32(wait)), "Wait");
            _taskManager.Enqueue(() => EzThrottler.Check("Wait"), Convert.ToInt32(wait), "Wait");
            _taskManager.Enqueue(() => !Player.Character->InCombat, int.MaxValue, "Wait");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public unsafe void WaitFor(string waitForWhat)
        {
            AutoDuty.Plugin.Action = $"WaitFor: {waitForWhat}";
            var waitForWhats = waitForWhat.Split('|');
                
            switch (waitForWhats[0])
            {
                case "Combat":
                    _taskManager.Enqueue(() => Player.Character->InCombat, int.MaxValue, "WaitFor-Combat");
                    break;
                case "IsValid":
                    _taskManager.Enqueue(() => !ObjectHelper.IsValid, 500, "WaitFor-NotIsValid");
                    _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "WaitFor-IsValid");
                    break;
                case "IsOccupied":
                    _taskManager.Enqueue(() => !ObjectHelper.IsOccupied, 500, "WaitFor-NotIsOccupied");
                    _taskManager.Enqueue(() => ObjectHelper.IsOccupied, int.MaxValue, "WaitFor-IsOccupied");
                    break;
                case "IsReady":
                    _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "WaitFor-NotIsReady");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "WaitFor-IsReady");
                    break;
                case "BNpcInRadius":
                    if (waitForWhats.Length == 1)
                        return;
                    _taskManager.Enqueue(() => !(ObjectHelper.GetObjectsByRadius(int.TryParse(waitForWhats[1], out var radius) ? radius : 0)?.Count > 0), $"WaitFor-BNpcInRadius{waitForWhats[1]}");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "WaitFor");
                    break;
            }
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");

        }

        private bool CheckPause() => _plugin.Stage == Stage.Paused;

        public unsafe void ExitDuty(string _)
        {
            _taskManager.Enqueue(() => { ExitDutyHelper.Invoke(); }, "ExitDuty-Invoke");
            _taskManager.Enqueue(() => !ExitDutyHelper.ExitDutyRunning, "ExitDuty-WaitExitDutyRunning");
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
            IGameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"MoveToObject: {objectName}";
            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByName(objectName)) != null, "MoveToObject");
            _taskManager.Enqueue(() => MovementHelper.Move(gameObject), int.MaxValue, "MoveToObject");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void TreasureCoffer(string _)
        {
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

        public void ClickTalk(string _) => _taskManager.Enqueue(() => AddonHelper.ClickTalk(), "ClickTalk");

        private unsafe bool InteractableCheck(IGameObject? gameObject)
        {
            if (AddonHelper.ClickSelectYesno(true))
                return true;

            if (gameObject == null || !gameObject.IsTargetable || !gameObject.IsValid() || !ObjectHelper.IsValid)
                return true;

            if (AddonHelper.ClickTalk())
                return true;
            
            if (EzThrottler.Throttle("Interactable", 250))
            {
                if (ObjectHelper.GetBattleDistanceToPlayer(gameObject) > 2f)
                    MovementHelper.Move(gameObject, 0.25f, 2f, false);
                else
                {
                    if (VNavmesh_IPCSubscriber.Path_IsRunning())
                        VNavmesh_IPCSubscriber.Path_Stop();
                    ObjectHelper.InteractWithObject(gameObject);
                };
            }

            return false;
        }
        private void Interactable(IGameObject? gameObject)
        {
            _taskManager.Enqueue(() => InteractableCheck(gameObject), "Interactable-InteractableCheck");
            _taskManager.Enqueue(() => ObjectHelper.PlayerIsCasting, 500, "Interactable-WaitPlayerIsCasting");
            _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Interactable-WaitNotPlayerIsCasting");
            _taskManager.DelayNext("Interactable-DelayNext100",100);
            _taskManager.Enqueue(() =>
            {
                if (!(gameObject?.IsTargetable ?? false) ||
                Svc.Condition[ConditionFlag.BetweenAreas] ||
                Svc.Condition[ConditionFlag.BetweenAreas51] ||
                Svc.Condition[ConditionFlag.BeingMoved] ||
                Svc.Condition[ConditionFlag.Jumping61] ||
                Svc.Condition[ConditionFlag.CarryingItem] ||
                Svc.Condition[ConditionFlag.CarryingObject] ||
                Svc.Condition[ConditionFlag.Occupied] ||
                Svc.Condition[ConditionFlag.Occupied30] ||
                Svc.Condition[ConditionFlag.Occupied33] ||
                Svc.Condition[ConditionFlag.Occupied38] ||
                Svc.Condition[ConditionFlag.Occupied39] ||
                gameObject?.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj)
                {
                    AutoDuty.Plugin.Action = "";
                }
                else
                {
                    Interactable(gameObject);
                }
            }, "Interactable-LoopCheck");
            _taskManager.Enqueue(() =>
            {
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    SchedulerHelper.ScheduleAction("InteractableEnableYesAlready",() => ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true), 5000);
                if (PandorasBox_IPCSubscriber.IsEnabled)
                    SchedulerHelper.ScheduleAction("InteractableEnablePandora", () => PandorasBox_IPCSubscriber.SetFeatureEnabled("Auto-interact with Objects in Instances", true), 15000);
            }, "Interactable-YesAlreadyPandoraSetEnableTrueAfter15s");
        }
        public unsafe void Interactable(string objectName)
        {
            Svc.Log.Debug("Interactable-YesAlreadySetEnableFalse");
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);

            Svc.Log.Debug("Interactable-PandoraSetEnableFalse");
            if (PandorasBox_IPCSubscriber.IsEnabled)
                PandorasBox_IPCSubscriber.SetFeatureEnabled("Auto-interact with Objects in Instances", false);

            IGameObject? gameObject = null;

            AutoDuty.Plugin.Action = $"Interactable: {objectName}";

            Match match = RegexHelper.InteractionObjectIdRegex().Match(objectName);
            string id = match.Success ? match.Captures.First().Value : string.Empty;

            _taskManager.Enqueue(() => (gameObject = (match.Success ? ObjectHelper.GetObjectByDataId(Convert.ToUInt32(id)) : null) ?? ObjectHelper.GetObjectByName(objectName)) != null, "Interactable-GetGameObject");
            _taskManager.Enqueue(() => gameObject?.IsTargetable ?? true, "Interactable-WaitGameObjectTargetable");
            _taskManager.Enqueue(() => Interactable(gameObject), "Interactable-InteractableLoop");
        }

        private bool BossCheck()
        {
            if (((AutoDuty.Plugin.BossObject?.IsDead ?? true) && !Svc.Condition[ConditionFlag.InCombat]) || !Svc.Condition[ConditionFlag.InCombat])
                return true;


            if (EzThrottler.Throttle("PositionalChecker", 25) && ReflectionHelper.Avarice_Reflection.PositionalChanged(out Positional positional))
                AutoDuty.Plugin.Chat.ExecuteCommand($"/vbm cfg AIConfig DesiredPositional {positional}");

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
            IGameObject? treasureCofferObject = null;
            _taskManager.Enqueue(() => BossMoveCheck(bossV3), "Boss-MoveCheck");
            if (AutoDuty.Plugin.BossObject == null)
                _taskManager.Enqueue(() => (AutoDuty.Plugin.BossObject = ObjectHelper.GetBossObject()) != null, "Boss-GetBossObject");
            if (AutoDuty.Plugin.Configuration.AutoManageRotationPluginState && !AutoDuty.Plugin.Configuration.UsingAlternativeRotationPlugin && ReflectionHelper.RotationSolver_Reflection.RotationSolverEnabled)
                _taskManager.Enqueue(() => ReflectionHelper.RotationSolver_Reflection.RotationAuto(), "Boss-ReenableRotations");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Boss: {AutoDuty.Plugin.BossObject?.Name.TextValue ?? ""}", "Boss-SetActionVar");
            _taskManager.Enqueue(() => Svc.Condition[ConditionFlag.InCombat], "Boss-WaitInCombat");
            _taskManager.Enqueue(() => BossCheck(), int.MaxValue, "Boss-BossCheck");
            _taskManager.Enqueue(() => { AutoDuty.Plugin.StopForCombat = true; }, "Boss-SetStopForCombatTrue");
            _taskManager.Enqueue(() => { AutoDuty.Plugin.BossObject = null; }, "Boss-ClearBossObject");
            if (AutoDuty.Plugin.Configuration.LootTreasure)
            {
                _taskManager.DelayNext("Boss-TreasureDelay", 1000);
                _taskManager.Enqueue(() => (treasureCofferObject = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure)?.FirstOrDefault(o => ObjectHelper.GetDistanceToPlayer(o) < 50)) != null, 1000, "Boss-FindTreasure");
                _taskManager.Enqueue(() => MovementHelper.Move(treasureCofferObject, 0.25f, 1f), 10000, "Boss-MoveTreasure");
                _taskManager.DelayNext("Boss-WaitASecToLootChest", 1000);
            }
            _taskManager.DelayNext("Boss-Delay500", 500);
            _taskManager.Enqueue(() => { AutoDuty.Plugin.Action = ""; }, "Boss-ClearActionVar");
            _taskManager.Enqueue(() =>
            {
                if (IPCSubscriber_Common.IsReady("BossModReborn") && AutoDuty.Plugin.Configuration.AutoManageBossModAISettings)
                    AutoDuty.Plugin.SetBMSettings();
            }, "Boss-SetFollowTargetBMR");
        }

        public void PausePandora(string featureName, string intMs)
        {
            if (PandorasBox_IPCSubscriber.IsEnabled)
                _taskManager.Enqueue(() => PandorasBox_IPCSubscriber.PauseFeature(featureName, int.Parse(intMs)));
        }

        public void Revival(string _)
        {
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void CameraFacing(string coords)
        {
            if (coords != null)
            {
                string[] v = coords.Split(", ");
                if (v.Length == 3)
                {
                    Vector3 facingPos = new Vector3(float.Parse(v[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[2], System.Globalization.CultureInfo.InvariantCulture));
                    AutoDuty.Plugin.OverrideCamera.Face(facingPos);
                }
            }
        }

        public enum OID : uint
        {
            Blue = 0x1E8554,
            Red = 0x1E8A8C,
            Green = 0x1E8A8D,
        }

        private string? GlobalStringStore;

        public unsafe void DutySpecificCode(string stage)
        {
            IGameObject? gameObject = null;
            switch (Svc.ClientState.TerritoryType)
            {
                //Sastasha - From BossMod
                case 1036:
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
                //Mount Rokkon
                case 1137:
                    switch (stage)
                    {
                        case "5":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByDataId(16140)) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2.5f), "DutySpecificCode");
                            _taskManager.DelayNext("DutySpecificCode", 1000);
                            _taskManager.Enqueue(() => ObjectHelper.InteractWithObject(gameObject), "DutySpecificCode");
                            if (ObjectHelper.IsValid)
                            {
                                _taskManager.Enqueue(() => ObjectHelper.InteractWithObject(gameObject), "DutySpecificCode");
                                _taskManager.Enqueue(() => AddonHelper.ClickSelectString(0));
                            }
                            break;
                        case "6":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByDataId(16140)) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2.5f), "DutySpecificCode");
                            _taskManager.DelayNext("DutySpecificCode", 1000);
                            if (ObjectHelper.IsValid)
                            {
                                _taskManager.Enqueue(() => ObjectHelper.InteractWithObject(gameObject), "DutySpecificCode");
                                _taskManager.Enqueue(() => AddonHelper.ClickSelectString(1));
                            }
                            break;
                        case "12":
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/rotation Settings AoEType Off"), "DutySpecificCode");
                            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(500)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(500), "DutySpecificCode");
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/mk ignore1"), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(100)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(100), "DutySpecificCode");

                            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(500)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(500), "DutySpecificCode");
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/mk ignore2"), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(100)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(100), "DutySpecificCode");

                            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(500)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(500), "DutySpecificCode");
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/mk attack1"), "DutySpecificCode");
                            break;
                        case "13":
                            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(500)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(500), "DutySpecificCode");
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/mk attack1"), "DutySpecificCode");
                            break;

                        default: break;
                    }
                    break;
                default: break;
            }
        }
    }
}
