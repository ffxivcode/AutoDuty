using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using System.Numerics;
using System;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using AutoDuty.IPC;

namespace AutoDuty.Helpers
{
    internal unsafe static class GotoBarracksHelper
    {
        internal static void Invoke()
        {
            if (!GotoBarracksRunning && Svc.ClientState.TerritoryType != BarracksTerritoryType(UIState.Instance()->PlayerState.GrandCompany))
            {
                Svc.Log.Info($"Goto Barracks Started");
                GotoBarracksRunning = true;
                Svc.Framework.Update += GotoBarracksUpdate;
                YesAlready_IPCSubscriber.SetPluginEnabled(false);
            }
        }

        internal static void Stop() 
        {
            if (GotoBarracksRunning)
                Svc.Log.Info($"Goto Barracks Finished");
            Svc.Framework.Update -= GotoBarracksUpdate;
            GotoBarracksRunning = false;
            AutoDuty.Plugin.Action = "";
            YesAlready_IPCSubscriber.SetPluginEnabled(true);
        }

        internal static bool GotoBarracksRunning = false;
        internal static uint BarracksTerritoryType(uint _grandCompany) => _grandCompany == 1 ? 536u : (_grandCompany == 2 ? 534u : 535u);
        internal static uint ExitBarracksDoorDataId(uint _grandCompany) => _grandCompany == 1 ? 2007528u : (_grandCompany == 2 ? 2006963u : 0u);
        private static IGameObject? barracksDoorGameObject = null;
        private static Vector3 barracksDoorLocation => UIState.Instance()->PlayerState.GrandCompany == 1 ? new Vector3(98.00867f, 41.275635f, 62.790894f) : (UIState.Instance()->PlayerState.GrandCompany == 2 ? new Vector3(-80.00789f, -0.5001702f, -6.6672616f) : new Vector3(-153.30743f, 5.2338257f, -98.039246f));
        private static uint _barracksDoorDataId => UIState.Instance()->PlayerState.GrandCompany == 1 ? 2007527u : (UIState.Instance()->PlayerState.GrandCompany == 2 ? 2006962u : 0u);

        internal static unsafe void GotoBarracksUpdate(IFramework framework)
        {
            if (!EzThrottler.Check("GotoBarracks"))
                return;

            EzThrottler.Throttle("GotoBarracks", 50);

            if (Svc.ClientState.LocalPlayer == null)
                return;

            if (GotoHelper.GotoRunning)
                return;

            if (Svc.ClientState.TerritoryType == BarracksTerritoryType(UIState.Instance()->PlayerState.GrandCompany))
            {
                Stop();
                return;
            }

            if (Svc.ClientState.TerritoryType != ObjectHelper.GrandCompanyTerritoryType(UIState.Instance()->PlayerState.GrandCompany) || (barracksDoorGameObject = ObjectHelper.GetObjectByDataId(Convert.ToUInt32(_barracksDoorDataId))) == null || Vector3.Distance(Svc.ClientState.LocalPlayer.Position, barracksDoorGameObject.Position) > 3f)
            {
                GotoHelper.Invoke(ObjectHelper.GrandCompanyTerritoryType(UIState.Instance()->PlayerState.GrandCompany), [barracksDoorLocation], 0.25f, 3f);
                return;
            }
            else if (ObjectHelper.IsValid)
            {
                ObjectHelper.InteractWithObject(barracksDoorGameObject);
                AddonHelper.ClickSelectYesno();
            }
        }
    }
}
