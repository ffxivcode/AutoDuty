using AutoDuty.External;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static AutoDuty.Helpers.ContentHelper;
using static FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule.Delegates;

namespace AutoDuty.Managers
{
    internal class SquadronManager(TaskManager _taskManager)
    {
        internal unsafe void RegisterSquadron(ContentHelper.Content content)
        {
            if (content.GCArmyIndex < 0)
            {
                Svc.Log.Info("GCArmyIndex was < than 0");
                return;
            }
            _taskManager.Enqueue(() => Svc.Log.Info($"Queueing Squadron: {content.Name}"), "RegisterSquadron");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Step: Queueing Squadron: {content.Name}", "RegisterSquadron");

            AtkUnitBase* addon = null;
            _taskManager.Enqueue(() => { ExecSkipTalk.IsEnabled = true; }, "RegisterSquadron");
            if (!ObjectHelper.IsValid)
            {
                Svc.Log.Info("ObjectHelper was invalid, making it valid.");
                _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterSquadron");
                Svc.Log.Info("Delaying next Enqueue by 2s");
                _taskManager.DelayNext("RegisterSquadron", 2000);
            }
            Svc.Log.Info("Defining addon as GcArmyCapture");
            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("GcArmyCapture"), "RegisterSquadron");
            Svc.Log.Info("Run OpenSquadron(addon)");
            _taskManager.Enqueue(() => OpenSquadron(addon), "RegisterSquadron");
            Svc.Log.Info("Check if the addon (GcArmyCapture) is ready and open");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("GcArmyCapture", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterSquadron");
            Svc.Log.Info("First callback fired to select the dungeon");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 11, content.GCArmyIndex), "RegisterSquadron");
            Svc.Log.Info("Second callback fired to open dungeon");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 13), "RegisterSquadron");
            Svc.Log.Info("ContentsFinderConfirm?, this might initiate the dungeon");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterSquadron");
            Svc.Log.Info("Another callback triggered, what is it?");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterSquadron");
            Svc.Log.Info("Looking at current territory");
            _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == content.TerritoryType, int.MaxValue, "RegisterSquadron");
            Svc.Log.Info("ObjectHelper set to isValid (or theres a check, and a maxValue is defined)");
            _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterSquadron");
            Svc.Log.Info("Duty has started");
            _taskManager.Enqueue(() => Svc.DutyState.IsDutyStarted, int.MaxValue, "RegisterSquadron");
            Svc.Log.Info("check if navmesh is ready");
            _taskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "RegisterSquadron");
            Svc.Log.Info("Skip talk is set to false");
            _taskManager.Enqueue(() => { ExecSkipTalk.IsEnabled = false; }, "RegisterSquadron");
            Svc.Log.Info("Start navigating");
            _taskManager.Enqueue(() => AutoDuty.Plugin.StartNavigation(true), "RegisterSquadron");
        }
        
        internal bool SeenAddon = false;
        internal string? CurrentWindow = null;
        internal unsafe bool OpenSquadron(AtkUnitBase* aub)
        {
            SeenAddon = false;
            AtkUnitBase* sargeantWindow = null;
            Svc.Log.Info($"SeenAddon_STATE : {SeenAddon}");
            Svc.Log.Info("Opening Squadron");
            IGameObject? gameObject;

            if (aub != null)
            {
                Svc.Log.Info($"Value of aub is not equal to null, so we will return");
                return true;
            }
            

            Svc.Log.Info("Running ObjectHelper, it should be enabled here right?");
            Svc.Log.Info($"So what is it? {ObjectHelper.IsValid}");
            Svc.Log.Info("We're going to try get the squadron sergeant, otherwise move closer");
            if ((gameObject = ObjectHelper.GetObjectByPartialName("Squadron Sergeant")) == null || !MovementHelper.Move(gameObject, 0.25f, 6f))
                return false;
            Svc.Log.Info($"Sergeant, {gameObject}");

            if (GenericHelpers.TryGetAddonByName("GcArmyExpeditionResult", out AtkUnitBase* addon))
            {
                Svc.Log.Info("EXPEDITION_RESULT: If we're in the mission success screen, close it");
                AddonHelper.FireCallBack(addon, true, 0);
                return false;
            }

            if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* _))
            {
                Svc.Log.Info("We can SelectString NOW");
                sargeantWindow = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectString");
                AddonHelper.FireCallBack(sargeantWindow, true, 0);
                AddonHelper.ClickSelectString(0);
            }

            
            if (SeenAddon && AddonHelper.ClickSelectString(0))
            {
                Svc.Log.Info("If SeenAddon is true, then click the select string and update state");
                Svc.Log.Info("Clicking a select string, I assume for Command Missions");
                Svc.Log.Info("SeenAddon_STATE: Setting this to false");
                SeenAddon = false;
                return true;
            }

            if (!SeenAddon && !GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* _))
            {
                Svc.Log.Info("Interacting with Sergeant");
                Svc.Log.Info($"Value of CurrentWindow, {CurrentWindow}");
                ObjectHelper.InteractWithObject(gameObject);
                
                // Check if Svc.GameGui is not null before calling ToString() on it
                if (Svc.GameGui != null)
                {
                    string gameGuiString = Svc.GameGui.ToString() ?? "No window";
                    
                    

                    // Check if gameGuiString is not null before assigning it to CurrentWindow
                    if (!string.IsNullOrEmpty(gameGuiString))
                    {
                        CurrentWindow = gameGuiString;
                    }
                    else
                    {

                        CurrentWindow = "No window"; 
                    }
                }
                return false;
            }
            else
                
                Svc.Log.Info("SeenAddon_STATE: True");
                SeenAddon = true;

            return false;
        }

    }
}
