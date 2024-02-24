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

namespace AutoDuty.Managers
{
    public class ActionsManager()
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
            ("DutySpecificCode","step #?")
        ];

        public CancellationTokenSource? TokenSource;
        public CancellationToken Token;
        private delegate void ExitDutyDelegate(char timeout);
        private readonly ExitDutyDelegate exitDuty = Marshal.GetDelegateForFunctionPointer<ExitDutyDelegate>(Svc.SigScanner.ScanText("40 53 48 83 ec 20 48 8b 05 ?? ?? ?? ?? 0f b6 d9"));

        public async Task InvokeAction(string action, object?[] p)
        {
            try
            {
                //Svc.Log.Info($"InvokeAction: Action: {action} Params: {p.Length}");

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
                Svc.Log.Error(ex.ToString());
            }
        }

        public async Task Wait(string wait)
        {
            //Svc.Log.Info($"Wait: {wait}");
            await Task.Delay(Convert.ToInt32(wait), Token);
            //Svc.Log.Info($"Done Wait: {wait}");
        }

        public void ExitDuty(string _)
        {
            exitDuty.Invoke((char)0);
        }

        public async Task SelectYesno(string YesorNo)
        {
            //Svc.Log.Info($"YesorNo: {YesorNo}");
            try
            {
                nint addon;
                int cnt = 0;
                //Svc.Log.Info("Waiting for YesNo");
                while ((addon = Svc.GameGui.GetAddonByName("SelectYesno", 1)) == 0 && cnt++ < 500 && !Token.IsCancellationRequested)
                    await Task.Delay(10, Token);

                if (addon == 0 || Token.IsCancellationRequested)
                    return;
                //Svc.Log.Info("Done Waiting for YesNo");
                await Task.Delay(25, Token);

                if (Token.IsCancellationRequested)
                    return;

                if (YesorNo.Equals(""))
                    ClickSelectYesNo.Using(addon).Yes();
                else
                {
                    if (YesorNo.Equals("YES"))
                    {
                        //Svc.Log.Info("Clicking Yes");
                        ClickSelectYesNo.Using(addon).Yes();
                    }
                    else if (YesorNo.Equals("NO"))
                    {
                        ClickSelectYesNo.Using(addon).No();
                    }
                }
                await Task.Delay(500, Token);
                while (ObjectManager.PlayerIsCasting && !Token.IsCancellationRequested)
                    await Task.Delay(10, Token);
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
                return;
            }
            await Task.Delay(50, Token);
            //Svc.Log.Info("Done");
        }

        public async Task MoveToObject(string objectName)
        {
            PlayerCharacter? _player;
            if ((_player = Svc.ClientState.LocalPlayer) is null)
                return;

            try
            {
                GameObject? gameObject;
                List<GameObject>? listGameObject;

                if ((listGameObject = ObjectManager.GetObjectsByName([.. Svc.Objects], objectName)) is null)
                    return;

                if ((gameObject = listGameObject.OrderBy(o => Vector3.Distance(_player.Position, o.Position)).FirstOrDefault()) is null)
                    return;

                IPCManager.VNavmesh_SetMovementAllowed(true);
                IPCManager.VNavmesh_MoveTo(gameObject.Position);

                while (IPCManager.VNavmesh_WaypointsCount > 0)
                    await Task.Delay(10, Token);

            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }
        }

        public async Task TreasureCoffer(string s)
        {
            //Svc.Log.Info("Treasure Coffer Start");
            await Interactable("Treasure Coffer");
            //Svc.Log.Info("Treasure Coffer Done");
        }

        public async Task Interactable(string objectName)
        {
            //Svc.Log.Info($"Interactable: {objectName}");
            PlayerCharacter? _player;
            if ((_player = Svc.ClientState.LocalPlayer) is null)
                return;
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
                    if ((listGameObject = ObjectManager.GetObjectsByName([.. Svc.Objects], objectName)) is null)
                        return;

                    if ((gameObject = listGameObject.OrderBy(o => Vector3.Distance(_player.Position, o.Position)).FirstOrDefault()) is null)
                        return;

                    ObjectManager.InteractWithObject(gameObject);

                    await Task.Delay(1000, Token);
                }
                while (cnt++ < 4 && !Token.IsCancellationRequested && gameObject.IsTargetable && gameObject.IsValid());
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }

            await Task.Delay(50, Token);
            //Svc.Log.Info($"Done Interactable: {objectName}");
        }

        public async Task Boss(string x, string y, string z)
        {
            PlayerCharacter? _player;
            if ((_player = Svc.ClientState.LocalPlayer) is null)
                return;

            GameObject followTargetObject;
            var chat = new ECommons.Automation.Chat();
            AutoDuty.Plugin.StopForCombat = false;
            IPCManager.VNavmesh_MoveTo(new Vector3(float.Parse(x), float.Parse(y), float.Parse(z)));
            while (IPCManager.VNavmesh_WaypointsCount > 0 && !Token.IsCancellationRequested)
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
            //chat.ExecuteCommand("/vbmai off"); // for now until vbm IPC
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
                IPCManager.VNavmesh_MoveTo(followTargetObject.Position);
            }
            if (bossObject != null)
            {
                //Svc.Log.Info("Boss: waiting while InCombat and while !" + bossObject.Name + ".IsDead");
                while (Svc.Condition[ConditionFlag.InCombat] && !bossObject.IsDead)
                {
                    if (Vector3.Distance(_player.Position, followTargetObject.Position) > IPCManager.VNavmesh_Tolerance && !IPCManager.BossMod_IsMoving && IPCManager.BossMod_ForbiddenZonesCount == 0)
                        IPCManager.VNavmesh_MoveTo(followTargetObject.Position);
                    if ((IPCManager.BossMod_IsMoving || IPCManager.BossMod_ForbiddenZonesCount > 0) && IPCManager.VNavmesh_MovementAllowed)
                        IPCManager.VNavmesh_SetMovementAllowed(false);
                    else if (IPCManager.VNavmesh_MovementAllowed)
                        IPCManager.VNavmesh_SetMovementAllowed(true);

                    await Task.Delay(5);
                }
            }
            else
            {
                //Svc.Log.Info("Boss: We were unable to determine our Boss Object waiting while InCombat");
                while (Svc.Condition[ConditionFlag.InCombat])
                {
                    if (Vector3.Distance(_player.Position, followTargetObject.Position) > IPCManager.VNavmesh_Tolerance && !IPCManager.BossMod_IsMoving && IPCManager.BossMod_ForbiddenZonesCount == 0)
                        IPCManager.VNavmesh_MoveTo(followTargetObject.Position);
                    if ((IPCManager.BossMod_IsMoving || IPCManager.BossMod_ForbiddenZonesCount > 0) && IPCManager.VNavmesh_MovementAllowed)
                        IPCManager.VNavmesh_SetMovementAllowed(false);
                    else if (IPCManager.VNavmesh_MovementAllowed)
                        IPCManager.VNavmesh_SetMovementAllowed(true);

                    await Task.Delay(5);
                }
            }
            //chat.ExecuteCommand("/vbmai on");
            AutoDuty.Plugin.StopForCombat = true;
        }
        private static BattleChara? GetBossObject()
        {
            var battleCharaObjs = ObjectManager.GetObjectsByRadius([.. Svc.Objects], 30).OfType<BattleChara>();
            BattleChara bossObject = default;
            foreach (var obj in battleCharaObjs)
            {
                //Svc.Log.Info("Checking: " + obj.Name.ToString());
                if (ObjectManager.IsBossFromIcon(obj))
                    bossObject = obj;
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
                //Sastasha
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
                                //Svc.Log.Info("Found Obj (" + a.Name.ToString() + ")- Moving");
                                IPCManager.VNavmesh_SetTolerance(2.5f);
                                IPCManager.VNavmesh_MoveTo(a.Position);
                                while (IPCManager.VNavmesh_WaypointsCount != 0 && !Token.IsCancellationRequested)
                                    await Task.Delay(5, Token);

                                if (Token.IsCancellationRequested)
                                    return;

                                //Svc.Log.Info("Done Moving - Interacting with Obj");
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

                                //Svc.Log.Info($"Done Interacting with Obj - Selecting Yes on ({addon})");

                                await SelectYesno("YES");

                                if (Token.IsCancellationRequested)
                                    return;

                                //Svc.Log.Info("Done Selecting Yes");
                                IPCManager.VNavmesh_SetTolerance(0.5f);
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
