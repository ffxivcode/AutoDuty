using System.Reflection;
using System;
using System.Threading;
using System.Threading.Tasks;
using ECommons.DalamudServices;
using ClickLib.Clicks;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using AutoDuty.IPC;

namespace AutoDuty.Managers
{
    public class ActionsManager(VNavmesh_IPCSubscriber _vnavIPC, BossMod_IPCSubscriber _vbmIPC, MBT_IPCSubscriber _mbtIPC, ECommons.Automation.Chat _chat)
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
            ("Target","Target what?")
        ];

        public CancellationTokenSource? TokenSource;
        public CancellationToken Token;
        private delegate void ExitDutyDelegate(char timeout);
        private readonly ExitDutyDelegate exitDuty = Marshal.GetDelegateForFunctionPointer<ExitDutyDelegate>(Svc.SigScanner.ScanText("40 53 48 83 ec 20 48 8b 05 ?? ?? ?? ?? 0f b6 d9"));

        public async Task InvokeAction(string action, object?[] p)
        {
            try
            {
                if (!string.IsNullOrEmpty(action))
                {
                    Type thisType = GetType();
                    MethodInfo? actionTask = thisType.GetMethod(action);
                    if (actionTask != null)
                        await (Task)actionTask.Invoke(this, p);
                }
            }
            catch (Exception ex)
            {
                //Svc.Log.Error(ex.ToString());
            }
        }

        private async Task WaitForCombat(BattleChara player)
        {
            while (ObjectManager.InCombat(player) && !Token.IsCancellationRequested)
                await Task.Delay(50, Token);
        }

        public void BossMod(string sts) => _chat.ExecuteCommand($"/vbmai {sts}");

        public async Task Wait(string wait) => await Task.Delay(Convert.ToInt32(wait), Token);

        public void ExitDuty(string _) => exitDuty.Invoke((char)0);

        public async Task SelectYesno(string YesorNo)
        {
            try
            {
                nint addon;
                int cnt = 0;
                while ((addon = Svc.GameGui.GetAddonByName("SelectYesno", 1)) == 0 && cnt++ < 500 && !Token.IsCancellationRequested)
                    await Task.Delay(10, Token);

                if (addon == 0 || Token.IsCancellationRequested)
                    return;

                await Task.Delay(25, Token);

                if (Token.IsCancellationRequested)
                    return;

                if (YesorNo.Equals(""))
                    ClickSelectYesNo.Using(addon).Yes();
                else
                {
                    if (YesorNo.Equals("YES"))
                        ClickSelectYesNo.Using(addon).Yes();
                    else if (YesorNo.Equals("NO"))
                        ClickSelectYesNo.Using(addon).No();
                }
                await Task.Delay(500, Token);
                while (ObjectManager.PlayerIsCasting && !Token.IsCancellationRequested)
                    await Task.Delay(10, Token);
            }
            catch (Exception ex)
            {
                //Svc.Log.Error(ex.ToString());
                return;
            }
            await Task.Delay(50, Token);
        }

        public async Task MoveToObject(string objectName)
        {
            PlayerCharacter? player;
            if ((player = Svc.ClientState.LocalPlayer) is null)
                return;

            await WaitForCombat(player);

            try
            {
                GameObject? gameObject;
                List<GameObject>? listGameObject;

                if ((listGameObject = ObjectManager.GetObjectsByName([.. Svc.Objects], objectName)) is null)
                    return;

                if ((gameObject = listGameObject.OrderBy(o => Vector3.Distance(player.Position, o.Position)).FirstOrDefault()) is null)
                    return;

                _vnavIPC.Path_SetMovementAllowed(true);
                _vnavIPC.SimpleMove_PathfindAndMoveTo(gameObject.Position, false);

                while (_vnavIPC.SimpleMove_PathfindInProgress() || _vnavIPC.Path_NumWaypoints() > 0)
                    await Task.Delay(10, Token);

            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }
        }

        public async Task TreasureCoffer(string _) => await Interactable("Treasure Coffer");

        public async Task Target(string objectName)
        {
            PlayerCharacter? _player;
            if ((_player = Svc.ClientState.LocalPlayer) is null) return;

            if (Token.IsCancellationRequested)
                return;

            await Task.Delay(5, Token);

            try
            {
                var cnt = 0;
                GameObject? gameObject;
                List<GameObject>? listGameObject;
                do
                {
                    if ((listGameObject = ObjectManager.GetObjectsByName([.. Svc.Objects], objectName)) is null)
                        return;

                    if ((gameObject = listGameObject.OrderBy(o => Vector3.Distance(_player.Position, o.Position)).FirstOrDefault()) is null)
                        return;

                    if (!gameObject.IsTargetable || !gameObject.IsValid())
                        return;

                    Svc.Targets.Target = gameObject;

                    await Task.Delay(5, Token);
                }
                while (cnt++ < 4 && !Token.IsCancellationRequested && gameObject.IsTargetable && gameObject.IsValid());
            }
            catch (Exception ex)
            {
                //Svc.Log.Error(ex.ToString());
            }

            await Task.Delay(5, Token);
        }
        public async Task Interactable(string objectName)
        {
            PlayerCharacter? player;
            if ((player = Svc.ClientState.LocalPlayer) is null) return;

            await WaitForCombat(player);

            await Task.Delay(2000, Token);

            if (Token.IsCancellationRequested)
                return;

            try
            {
                var cnt = 0;
                GameObject? gameObject;
                List<GameObject>? listGameObject;
                do
                {
                    if ((listGameObject = ObjectManager.GetObjectsByRadius([.. Svc.Objects], 10)) is null)
                        return;

                    if ((listGameObject = ObjectManager.GetObjectsByName([.. Svc.Objects], objectName)) is null)
                        return;

                    if ((gameObject = listGameObject.OrderBy(o => Vector3.Distance(player.Position, o.Position)).FirstOrDefault()) is null)
                        return;

                    if (!gameObject.IsTargetable || !gameObject.IsValid())
                        return;

                    ObjectManager.InteractWithObject(gameObject);

                    await Task.Delay(1000, Token);
                }
                while (cnt++ < 4 && !Token.IsCancellationRequested && gameObject.IsTargetable && gameObject.IsValid());
            }
            catch (Exception ex)
            {
                //Svc.Log.Error(ex.ToString());
            }

            await Task.Delay(1000, Token);
        }

        public async Task Boss(string x, string y, string z)
        {
            PlayerCharacter? _player;
            if ((_player = Svc.ClientState.LocalPlayer) is null)
                return;

            GameObject followTargetObject;
            AutoDuty.Plugin.StopForCombat = false;
            _vnavIPC.SimpleMove_PathfindAndMoveTo(new Vector3(float.Parse(x), float.Parse(y), float.Parse(z)), false);
            while ((_vnavIPC.SimpleMove_PathfindInProgress() || _vnavIPC.Path_NumWaypoints() > 0) && !Token.IsCancellationRequested)
                await Task.Delay(10, Token);
            await Task.Delay(5000, Token);
            if (Token.IsCancellationRequested)
                return;
            //get our BossObject
            var bossObject = GetBossObject();
            if (bossObject != null)
            {
                Svc.Log.Info("Boss: " + bossObject.Name);
            }
            else
            {
                Svc.Log.Info("Boss: We were unable to determine our Boss Object");
            }
            //switch our class type
            switch (_player.ClassJob.GameData.Role)
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
                _mbtIPC.SetFollowTarget(followTargetObject.Name.TextValue);
                _mbtIPC.SetFollowDistance(0);
                _mbtIPC.SetFollowStatus(true);
            }
            if (bossObject != null)
            {
                while (Svc.Condition[ConditionFlag.InCombat] && !bossObject.IsDead)
                {
                    if ((_vbmIPC.IsMoving() || _vbmIPC.ForbiddenZonesCount() > 0) && _mbtIPC.GetFollowStatus() )
                        _mbtIPC.SetFollowStatus(false);
                    else if (!_mbtIPC.GetFollowStatus() && !_vbmIPC.IsMoving() && _vbmIPC.ForbiddenZonesCount() == 0)
                        _mbtIPC.SetFollowStatus(true);

                    await Task.Delay(5);
                }
            }
            else
            {
                while (Svc.Condition[ConditionFlag.InCombat])
                {
                    if ((_vbmIPC.IsMoving() || _vbmIPC.ForbiddenZonesCount() > 0) && _mbtIPC.GetFollowStatus())
                        _mbtIPC.SetFollowStatus(false);
                    else if (!_mbtIPC.GetFollowStatus() && !_vbmIPC.IsMoving() && _vbmIPC.ForbiddenZonesCount() == 0)
                        _mbtIPC.SetFollowStatus(true);

                    await Task.Delay(5);
                }
            }
            AutoDuty.Plugin.StopForCombat = true;
            _mbtIPC.SetFollowStatus(false);
        }
        private static BattleChara? GetBossObject()
        {
            var battleCharas = ObjectManager.GetObjectsByRadius([.. Svc.Objects], 30).OfType<BattleChara>();
            BattleChara? bossObject = default;
            foreach (var battleChara in battleCharas)
            {
                if (ObjectManager.IsBossFromIcon(battleChara))
                    bossObject = battleChara;
            }

            return bossObject;
        }
        private static GameObject? GetTrustTankMemberObject()
        {
            try
            {
                return Svc.Buddies.First(s => s.GameObject.Name.ToString().Contains("Marauder") || s.GameObject.Name.ToString().Contains("") || s.GameObject.Name.ToString().Contains("Ysayle") || s.GameObject.Name.ToString().Contains("Temple Knight") || s.GameObject.Name.ToString().Contains("Haurchefant") || s.GameObject.Name.ToString().Contains("Pero Roggo") || s.GameObject.Name.ToString().Contains("Aymeric") || s.GameObject.Name.ToString().Contains("House Fortemps Knight") || s.GameObject.Name.ToString().Contains("Carvallain") || s.GameObject.Name.ToString().Contains("Gosetsu") || s.GameObject.Name.ToString().Contains("Hien") || s.GameObject.Name.ToString().Contains("Resistance Fighter") || s.GameObject.Name.ToString().Contains("Arenvald") || s.GameObject.Name.ToString().Contains("Emet-Selch") || s.GameObject.Name.ToString().Contains("Venat") || s.GameObject.Name.ToString().Contains("Varshahn") || s.GameObject.Name.ToString().Contains("Thancred") || s.GameObject.Name.ToString().Contains("G'raha Tia") || s.GameObject.Name.ToString().Contains("Crystal Exarch")).GameObject;
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
                return null;
            }
        }
        private static GameObject? GetTrustHealerMemberObject()
        {
            try
            {
                return Svc.Buddies.First(s => s.GameObject.Name.ToString().Contains("Conjurer") || s.GameObject.Name.ToString().Contains("Temple Chirurgeon") || s.GameObject.Name.ToString().Contains("Mol Youth") || s.GameObject.Name.ToString().Contains("Doman Shaman") || s.GameObject.Name.ToString().Contains("Venat") || s.GameObject.Name.ToString().Contains("Alphinaud") || s.GameObject.Name.ToString().Contains("Urianger") || s.GameObject.Name.ToString().Contains("Y'shtola") || s.GameObject.Name.ToString().Contains("Crystal Exarch") || s.GameObject.Name.ToString().Contains("G'raha Tia")).GameObject;
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
                return null;
            }
        }
        public enum OID : uint
        {
            Blue = 0x1E8554,
            Red = 0x1E8A8C,
            Green = 0x1E8A8D,
        }
        public string GlobalStringStore;

        public async Task DutySpecificCode(string stage)
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
                            var a = Svc.Objects.Where(a => a.Name.ToString().Equals(GlobalStringStore + " Coral Formation")).First();
                            if (a != null)
                            {
                                _vnavIPC.Path_SetTolerance(2.5f);
                                _vnavIPC.SimpleMove_PathfindAndMoveTo(a.Position, false);
                                while ((_vnavIPC.SimpleMove_PathfindInProgress() || _vnavIPC.Path_NumWaypoints() > 0) && !Token.IsCancellationRequested)
                                    await Task.Delay(5, Token);

                                if (Token.IsCancellationRequested)
                                    return;

                                nint addon;
                                int cnt = 0;

                                do
                                {
                                    ObjectManager.InteractWithObject(a);
                                    await Task.Delay(10, Token);
                                }
                                while ((addon = Svc.GameGui.GetAddonByName("SelectYesno", 1)) == nint.Zero && cnt++ < 500 && !Token.IsCancellationRequested);

                                if (addon == nint.Zero || Token.IsCancellationRequested)
                                    return;

                                await SelectYesno("YES");

                                if (Token.IsCancellationRequested)
                                    return;

                                _vnavIPC.Path_SetTolerance(0.25f);
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
