using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
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
        public readonly List<(string, string, string)> ActionsList =
        [
            ("<-- Comment -->","comment?","Adds a comment to the path, AutoDuty will do nothing but display them"),
            ("Wait","how long?", "Adds a Wait (x seconds) step to the path, after moving to the position AutoDuty will wait x seconds"),
            ("WaitFor","for?","Adds a WaitFor (Condition) step to the path, after moving to the position AutoDuty will wait for a condition from the following list:\nCombat - waits until in combat\nIsReady - waits until the player is ready\nIsValid - waits until the player is valid\nIsOccupied - waits until the player is occupied\nBNpcInRadius - waits until a battle npc either spawns or path's into the radius specified"),
            ("Boss","false", "Add a Boss step to the path, after and while moving to the position AutoDuty will attempt to find the boss object, if not found will wait 10s at the position for the boss to spawn and will then Invoke the Boss Action"),
            ("Interactable","interact with?", "Adds a Interactable step to the path, after moving to within 2 of the position AutoDuty will interact with the object specified (Recommended to input DataId) until the object is no longer targetable, you meet certain conditions or a YesNo/Talk addon appears"),
            ("TreasureCoffer","false", "Adds a TreasureCoffer flag to the path, AutoDuty will loot any treasure coffers automatically if it gets within interact range of one (While Config Loop Option is on), this is just a flag to mark the positions of Treasure Coffers, note: AutoDuty will ignore this Path entry when Looting is disabled or Boss LootOnly is enabled"),
            ("SelectYesno","yes or no?", "Adds a SelectYesno step to the path, after moving to the position AutoDuty will click Yes or no on this addon"),
            ("MoveToObject","Object Name?", "Adds a MoveToObject step to the path, AutoDuty will will move the object specified (Recommend input DataId)"),
            ("DutySpecificCode","step #?", "Adds a DutySpecificCode step to the path, after moving to the position AutoDuty will invoke the DutySpecific Action for this TerritoryType and the step # specified"),
            ("BossMod", "on / off", "Adds a BossMod step to the path, after moving to the position AutoDuty will turn BossMod on or off"),
            ("Rotation", "on / off", "Adds a Rotation step to the path, after moving to the position AutoDuty will turn Rotation Plugin on or off"),
            ("Target", "Target what?", "Adds a Target step to the path, after moving to the position AutoDuty will Target the object specified (Recommend inputing DataId)"),
            ("AutoMoveFor", "how long?", "Adds a AutoMoveFor step to the path, AutoDuty will turn on Standard Mode and AutoMoveFor time specified or until player is not ready"),
            ("ChatCommand","Command with args?", "Adds a ChatCommand step to the path, after moving to the position AutoDuty will execute the Command specified"),
            ("StopForCombat","true/false", "Adds a StopForCombat step to the path, after moving to the position AutoDuty will turn StopForCombat on or off"),
            ("Revival", "false", "Adds a Revive flag to the pathh, is is just a flag to mark the positions of Revival Points, note: AutoDuty will ignore this path entry during navigation"),
            ("ForceAttack",  "false", "Adds a ForceAttack step to the path, after moving to the position AutoDuty will ForceAttack the closest mob"),
            ("Jump", "automove for how long before", "Adds a jump step to the path, after AutoMoving AutoDuty will Jump"),
            //("PausePandora", "Which feature | how long"),
            ("CameraFacing", "Face which Coords?", "Adds a CameraFacing step to the path, after moving to the position AutoDuty will face the coordinates specified"),
            ("ClickTalk", "false", "Adds a ClickTalk step to the path, after moving to the position AutoDuty will Click the talk addon"),
            ("ConditionAction","condition;args,action;args", "Adds a ConditionAction step to the path, after moving to the position AutoDuty will check the condition specified and invoke Action"),
            ("ModifyIndex", "what number (0-based)", "Adds a ModifyIndex step to the path, after moving to the position AutoDuty will Modify the index to the number specified")
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
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }
        }
        
        public void Follow(string who) => FollowHelper.SetFollow(ObjectHelper.GetObjectByName(who));

        public void SetBMSettings(string defaultSettings) => AutoDuty.Plugin.SetBMSettings(bool.TryParse(defaultSettings, out bool defaultsettings) && defaultsettings);

        public unsafe void ConditionAction(string conditionAction)
        {
            if (!conditionAction.Any(x => x.Equals(','))) return;

            AutoDuty.Plugin.Action = $"ConditionAction: {conditionAction}";
            var condition = conditionAction.Split(",")[0];
            var conditionArray = condition.Split(";");
            var action = conditionAction.Split(',')[1];
            var actionArray = action.Split(";");
            var invokeAction = false;
            Svc.Log.Debug($"{condition} {conditionArray.Length} {action} {actionArray.Length}");
            switch (conditionArray[0])
            {
                case "ItemGreaterThan":
                    uint itemIdgt = conditionArray.Length > 1 && uint.TryParse(conditionArray[1], out var idgt) ? idgt : 0;
                    uint itemqtygt = conditionArray.Length > 2 && uint.TryParse(conditionArray[2], out var qtygt) ? qtygt : 1;
                    Svc.Log.Debug($"ConditionAction: Checking Item: {itemIdgt} Qty: {InventoryHelper.ItemCount(itemIdgt)} >= {itemqtygt}");
                    if (itemIdgt != 0 && InventoryHelper.ItemCount(itemIdgt) >= itemqtygt)
                            invokeAction = true;
                    break;
                case "ItemLessThan":
                    uint itemIdlt = conditionArray.Length > 1 && uint.TryParse(conditionArray[1], out var idlt) ? idlt : 0;
                    uint itemqtylt = conditionArray.Length > 2 && uint.TryParse(conditionArray[2], out var qtylt) ? qtylt : 1;
                    Svc.Log.Debug($"ConditionAction: Checking Item: {itemIdlt} Qty: {InventoryHelper.ItemCount(itemIdlt)} < {itemqtylt}");
                    if (itemIdlt != 0 && InventoryHelper.ItemCount(itemIdlt) < itemqtylt)
                        invokeAction = true;
                    break;
                case "ObjectNotTargetable":
                    if (conditionArray.Length > 1)
                    {
                        IGameObject? gameObject = null;
                        if ((gameObject = ObjectHelper.GetObjectByDataId(uint.TryParse(conditionArray[1], out uint dataId) ? dataId : 0)) != null && !gameObject.IsTargetable)
                            invokeAction = true;
                    }
                    break;
                case "ObjectTargetable":
                    if (conditionArray.Length > 1)
                    {
                        IGameObject? gameObject = null;
                        if ((gameObject = ObjectHelper.GetObjectByDataId(uint.TryParse(conditionArray[1], out uint dataId) ? dataId : 0)) != null && gameObject.IsTargetable)
                            invokeAction = true;
                    }
                    break;
            }
            if (invokeAction)
            {
                var actionActual = actionArray[0];
                string actionArguments = actionArray.Length > 1 ? actionArray[1] : "";
                Svc.Log.Debug($"ConditionAction: Invoking Action: {actionActual} with Arguments: {actionArguments}");
                InvokeAction(actionActual, [actionArguments]);
            }
        }

        public void BossMod(string sts) => _chat.ExecuteCommand($"/vbmai {sts}");

        public void ModifyIndex(string index)
        {
            if (!int.TryParse(index, out int _index)) return;
            AutoDuty.Plugin.Indexer = _index;
            AutoDuty.Plugin.Stage = Stage.Reading_Path;
        }

        private bool _autoManageRotationPluginState = false;
        public void Rotation(string sts)
        {
            if (sts.Equals("off", StringComparison.InvariantCultureIgnoreCase))
            {
                if (AutoDuty.Plugin.Configuration.AutoManageRotationPluginState)
                {
                    _autoManageRotationPluginState = true;
                    AutoDuty.Plugin.Configuration.AutoManageRotationPluginState = false;
                }
                AutoDuty.Plugin.SetRotationPluginSettings(false, true);
            }
            else if (sts.Equals("on", StringComparison.InvariantCultureIgnoreCase))
            {
                if (_autoManageRotationPluginState)
                    AutoDuty.Plugin.Configuration.AutoManageRotationPluginState = true;

                AutoDuty.Plugin.SetRotationPluginSettings(true, true);
            }
        }

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
            _taskManager.Enqueue(() => { if (movementMode == 1) Svc.GameConfig.UiControl.Set("MoveMode", 0); }, "AutoMove-MoveMode");
            _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove on"), "AutoMove-On");
            _taskManager.Enqueue(() => EzThrottler.Throttle("AutoMove", Convert.ToInt32(wait)), "AutoMove-Throttle");
            _taskManager.Enqueue(() => EzThrottler.Check("AutoMove") || !ObjectHelper.IsReady, Convert.ToInt32(wait), "AutoMove-CheckThrottleOrNotReady");
            _taskManager.Enqueue(() => { if (movementMode == 1) Svc.GameConfig.UiControl.Set("MoveMode", 1); }, "AutoMove-MoveMode2");
            _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "AutoMove-WaitIsReady");
            _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove off"), "AutoMove-Off");
        }

        public unsafe void Wait(string wait)
        {
            AutoDuty.Plugin.Action = $"Wait: {wait}";
            if (AutoDuty.Plugin.StopForCombat)
                _taskManager.Enqueue(() => !Player.Character->InCombat, int.MaxValue, "Wait");
            _taskManager.Enqueue(() => EzThrottler.Throttle("Wait", Convert.ToInt32(wait)), "Wait");
            _taskManager.Enqueue(() => EzThrottler.Check("Wait"), Convert.ToInt32(wait), "Wait");
            if (AutoDuty.Plugin.StopForCombat)
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
                    _taskManager.Enqueue(() => Player.Character->InCombat, "WaitFor-Combat");
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
            _taskManager.Enqueue(() => ExitDutyHelper.State != ActionState.Running, "ExitDuty-WaitExitDutyRunning");
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
            if (Conditions.IsMounted || Conditions.IsMounted2)
                return true;

            if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno) && !AddonHelper.ClickSelectYesno(true))
                return false;
            else if (AddonHelper.ClickSelectYesno(true))
                return true;

            if (GenericHelpers.TryGetAddonByName("Talk", out AtkUnitBase* addonTalk) && GenericHelpers.IsAddonReady(addonTalk) && !AddonHelper.ClickTalk())
                return false;
            else if (AddonHelper.ClickTalk())
                return true;

            if (gameObject == null || !gameObject.IsTargetable || !gameObject.IsValid() || !ObjectHelper.IsValid)
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
        private unsafe void Interactable(IGameObject? gameObject)
        {
            _taskManager.Enqueue(() => InteractableCheck(gameObject), "Interactable-InteractableCheck");
            _taskManager.Enqueue(() => ObjectHelper.PlayerIsCasting, 500, "Interactable-WaitPlayerIsCasting");
            _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Interactable-WaitNotPlayerIsCasting");
            _taskManager.DelayNext("Interactable-DelayNext100", 100);
            _taskManager.Enqueue(() =>
            {
                var boolAddonSelectYesno = GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno);

                var boolAddonTalk = GenericHelpers.TryGetAddonByName("Talk", out AtkUnitBase* addonTalk) && GenericHelpers.IsAddonReady(addonTalk);

                if (!boolAddonSelectYesno && !boolAddonTalk && (!(gameObject?.IsTargetable ?? false) ||
                Conditions.IsMounted ||
                Conditions.IsMounted2 ||
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
                gameObject?.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj))
                {
                    AutoDuty.Plugin.Action = "";
                }
                else
                {
                    Interactable(gameObject);
                }
            }, "Interactable-LoopCheck");
        }
        public unsafe void Interactable(string objectName)
        {
            IGameObject? gameObject = null;

            AutoDuty.Plugin.Action = $"Interactable: {objectName}";

            Match match = RegexHelper.InteractionObjectIdRegex().Match(objectName);
            string id = match.Success ? match.Captures.First().Value : string.Empty;

            _taskManager.Enqueue(() => (gameObject = (match.Success ? ObjectHelper.GetObjectByDataId(Convert.ToUInt32(id)) : null) ?? ObjectHelper.GetObjectByName(objectName)) != null || Player.Character->InCombat, "Interactable-GetGameObjectUnlessInCombat");
            _taskManager.Enqueue(() =>
            {
                if (Player.Character->InCombat)
                {
                    _taskManager.Abort();
                    _taskManager.Enqueue(() => !Player.Character->InCombat, int.MaxValue, "Interactable-InCombatWait");
                    Interactable(objectName);
                }
            }, "Interactable-InCombatCheck");
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

        private void BossLoot(List<IGameObject>? gameObjects, int index)
        {
            if (gameObjects == null || gameObjects.Count < 1)
            {
                _taskManager.DelayNext("Boss-WaitASecToLootChest", 1000);
                return;
            }

            _taskManager.Enqueue(() => MovementHelper.Move(gameObjects[index], 0.25f, 1f));
            _taskManager.Enqueue(() =>
            {
                index++;
                if (gameObjects.Count > index)
                    BossLoot(gameObjects, index);
                else
                    _taskManager.DelayNext("Boss-WaitASecToLootChest", 1000);
            });
        }

        public void Boss(Vector3 bossV3)
        {
            Svc.Log.Info($"Starting Action Boss: {AutoDuty.Plugin.BossObject?.Name.TextValue ?? "null"}");
            int index = 0;
            List<IGameObject>? treasureCofferObjects = null;
            AutoDuty.Plugin.SkipTreasureCoffer = false;
            _taskManager.Enqueue(() => BossMoveCheck(bossV3), "Boss-MoveCheck");
            if (AutoDuty.Plugin.BossObject == null)
                _taskManager.Enqueue(() => (AutoDuty.Plugin.BossObject = ObjectHelper.GetBossObject()) != null, "Boss-GetBossObject");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Boss: {AutoDuty.Plugin.BossObject?.Name.TextValue ?? ""}", "Boss-SetActionVar");
            _taskManager.Enqueue(() => Svc.Condition[ConditionFlag.InCombat], "Boss-WaitInCombat");
            _taskManager.Enqueue(() => BossCheck(), int.MaxValue, "Boss-BossCheck");
            _taskManager.Enqueue(() => { AutoDuty.Plugin.StopForCombat = true; }, "Boss-SetStopForCombatTrue");
            _taskManager.Enqueue(() => { AutoDuty.Plugin.BossObject = null; }, "Boss-ClearBossObject");
            
            if (AutoDuty.Plugin.Configuration.LootTreasure)
            {
                _taskManager.DelayNext("Boss-TreasureDelay", 1000);
                _taskManager.Enqueue(() => treasureCofferObjects = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure)?.Where(x => ObjectHelper.GetDistanceToPlayer(x) <= 30).ToList(), "Boss-GetTreasureChests");
                _taskManager.Enqueue(() => BossLoot(treasureCofferObjects, index), "Boss-LootCheck");
            }
        }

        public void PausePandora(string featureName, string intMs)
        {
            return;
            //disable for now until we have a need other than interact objects
            //if (PandorasBox_IPCSubscriber.IsEnabled)
            //_taskManager.Enqueue(() => PandorasBox_IPCSubscriber.PauseFeature(featureName, int.Parse(intMs)));
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
                    Vector3 facingPos = new(float.Parse(v[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[2], System.Globalization.CultureInfo.InvariantCulture));
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

        private unsafe void PraeUpdate(IFramework _)
        {
            if (!EzThrottler.Throttle("PraeUpdate", 50))
                return;

            var objects = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc);

            if (objects != null)
            {
                var protoArmOrDoor = objects.FirstOrDefault(x => x.IsTargetable && x.DataId is 14566 or 14616 && ObjectHelper.GetDistanceToPlayer(x) <= 25);
                if (protoArmOrDoor != null)
                    Svc.Targets.Target = protoArmOrDoor;
            }

            if (Svc.Targets.Target != null && Svc.Targets.Target.IsHostile())
            {
                var dir = Vector2.Normalize(new Vector2(Svc.Targets.Target.Position.X, Svc.Targets.Target.Position.Z) - new Vector2(Player.Position.X, Player.Position.Z));
                float rot = (float)Math.Atan2(dir.X, dir.Y);

                Player.Object.Struct()->SetRotation(rot);

                var targetPosition = Svc.Targets.Target.Position;
                ActionManager.Instance()->UseActionLocation(ActionType.Action, 1128, Player.Object.GameObjectId, &targetPosition);
            }
        }

        public unsafe void DutySpecificCode(string stage)
        {
            IGameObject? gameObject = null;
            switch (Svc.ClientState.TerritoryType)
            {
                //Prae
                case 1044:
                    switch (stage)
                    {
                        case "1":
                            AutoDuty.Plugin.Chat.ExecuteCommand($"/vbm cfg AIConfig OverridePositional false");
                            Svc.Framework.Update += PraeUpdate;
                            Interactable("2012819");
                            break;
                        case "2":
                            AutoDuty.Plugin.Chat.ExecuteCommand($"/vbm cfg AIConfig OverridePositional true");
                            Svc.Framework.Update -= PraeUpdate;
                            break;
                    }
                    break;
                //Sastasha
                //Blue -  2000213
                //Red -  2000214
                //Green - 2000215
                case 1036:
                    switch (stage)
                    {
                        case "1":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj)?.FirstOrDefault(a => a.IsTargetable && (OID)a.DataId is OID.Blue or OID.Red or OID.Green)) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() =>
                            {
                                if (gameObject != null)
                                {
                                    switch ((OID)gameObject.DataId)
                                    {
                                        case OID.Blue:
                                            GlobalStringStore = "2000213";
                                            break;
                                        case OID.Red:
                                            GlobalStringStore = "2000214";
                                            break;
                                        case OID.Green:
                                            GlobalStringStore = "2000215";
                                            break;
                                    }
                                }
                            }, "DutySpecificCode");
                            break;
                        case "2":
                            _taskManager.Enqueue(() => Interactable(GlobalStringStore ?? ""), "DutySpecificCode");
                            break;
                        case "3":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByDataId(2000216)) != null, "DutySpecificCode");
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
