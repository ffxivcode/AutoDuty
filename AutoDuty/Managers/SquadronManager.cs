using AutoDuty.External;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;


namespace AutoDuty.Managers
{
    internal class SquadronManager(TaskManager _taskManager)
    {

        internal bool InteractedWithSergeant = false;
        internal bool OpeningMissions = false;
        internal bool ViewingMissions = false;
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

            //Fallback or check if the ObjectHelper functionality is unavailable
            if (!ObjectHelper.IsValid)
            {
                Svc.Log.Info("ObjectHelper was invalid, making it valid.");
                _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterSquadron");
                Svc.Log.Info("Delaying next Enqueue by 2s");
                _taskManager.DelayNext("RegisterSquadron", 2000);
            }

            //Defining the GUI for the squadron duty finder
            _taskManager.Enqueue(() => addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("GcArmyCapture"), "RegisterSquadron");
            
            // Run logic to open the squadron duty finder
            _taskManager.Enqueue(() => OpenSquadron(addon), "RegisterSquadron");

            // Check if we're viewing missions to select (dungeons)
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("GcArmyCapture", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterSquadron");
            
            // Not sure on what this callback does.
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 11, content.GCArmyIndex), "RegisterSquadron");

            // Not sure on what this callback does.
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 13), "RegisterSquadron");
            
            // Not sure what this does but I assume its selecting the desired dungeon
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterSquadron");

            // Callback fired for what I assume is registering for the duty
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterSquadron");

            // Check if we're in a valid map for the dungeon / paths
            _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == content.TerritoryType, int.MaxValue, "RegisterSquadron");

            // Idk what this does
            _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "RegisterSquadron");

            // Check if we've started the Duty
            _taskManager.Enqueue(() => {
                if (Svc.DutyState.IsDutyStarted)
                {
                    // Reset states because duty has started, this is for looping
                    Svc.Log.Info("Resetting states for loop.");
                    InteractedWithSergeant = false;
                    OpeningMissions = false;
                    ViewingMissions = false;
                    return true; // Return true to continue the task sequence
                }
                return false; // Return false if duty has not started
            }, "RegisterSquadron");

            // Check if Navmesh is ready
            _taskManager.Enqueue(() => VNavmesh_IPCSubscriber.Nav_IsReady(), int.MaxValue, "RegisterSquadron");

            // Reset ExecSkipTalk to false
            _taskManager.Enqueue(() => { ExecSkipTalk.IsEnabled = false; }, "RegisterSquadron");

            // Start the dungeon!
            _taskManager.Enqueue(() => AutoDuty.Plugin.StartNavigation(true), "RegisterSquadron");
            
        }
        

        // Try to open the squadron menu by finding the squadron manager until specific GUI window checks are passed
        internal unsafe bool OpenSquadron(AtkUnitBase* aub)
        {
            ViewingMissions = false;
            OpeningMissions = false;
            InteractedWithSergeant = false;
            AtkUnitBase* sergeantListMenu = null;
            AtkUnitBase* expeditionResultScreen = null;

            if (aub != null)
            {
                return true;
            }

            if (GenericHelpers.TryGetAddonByName("GcArmyCapture", out AtkUnitBase* addon))
            {
                // Viewing missions, move on to the next step for registering
                ViewingMissions = true;
                Svc.Log.Info("ViewingMissions: TRUE");
                return true;
            }

            // Attempt to get the squadron sergeant once and reuse the result
            IGameObject? gameObject = ObjectHelper.GetObjectByPartialName("Squadron Sergeant");
            if (gameObject == null || !MovementHelper.Move(gameObject, 0.25f, 6f))
            {
                return false;
            }

            // Check if the GcArmyExpeditionResult addon is open
            if (GenericHelpers.TryGetAddonByName("GcArmyExpeditionResult", out expeditionResultScreen))
            {
                Svc.Log.Info("Viewing expedition result");
                // Close the expedition result menu
                AddonHelper.FireCallBack(expeditionResultScreen, true, 0);
                // Reset states so we try to open the squadron view again until we hit the squadron duty GUI
                OpeningMissions = false;
                InteractedWithSergeant = false;
                ViewingMissions = false;
                return false; // Exit to retry interaction
            }

            // Check if the SelectString addon is open (List Menu for "Command Missions", "Squadron Missions", etc.)
            if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* _))
            {
                // Successfully interacted with the Sergeant
                InteractedWithSergeant = true;
                sergeantListMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectString");
                AddonHelper.FireCallBack(sergeantListMenu, true, 0);
                AddonHelper.ClickSelectString(0);
                OpeningMissions = true; // Set the opened missions state to true
                return false;
            }

            // Continuously check if we've interacted with the sergeant until we open up the SelectString list menu
            // Check if we have interacted with the sergeant
            if (!InteractedWithSergeant)
            {
                ObjectHelper.InteractWithObject(gameObject);
                if (GenericHelpers.TryGetAddonByName("GcArmyCapture", out addon))
                {
                    InteractedWithSergeant = true;
                }
                    
                return false;
            }

            return false;
        }

    }
}
