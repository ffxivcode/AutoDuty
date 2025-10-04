using AutoDuty.Helpers;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AtkReaderImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ECommons.GameFunctions;
    using static Data.Classes;
    //on Rewrite need to check for sufficient seals
    internal class SquadronManager(TaskManager _taskManager)
    {

        internal bool InteractedWithSergeant = false;
        internal bool OpeningMissions = false;
        internal bool ViewingMissions = false;
        internal unsafe void RegisterSquadron(Content content)
        {
            if (content.GCArmyIndex < 0)
            {
                _taskManager.Enqueue(() => Svc.Log.Info("GCArmyIndex was < than 0"), "RegisterSquadron");
                return;
            }
            _taskManager.Enqueue(() => Svc.Log.Info($"Queueing Squadron: {content.Name}"), "RegisterSquadron");
            _taskManager.Enqueue(() => Plugin.Action = $"Queueing Squadron: {content.Name}", "RegisterSquadron");

            AtkUnitBase* captureAddon = null;

            //Check if player is valid
            if (!PlayerHelper.IsValid)
            {
                Svc.Log.Info("player was invalid, waiting for it to be valid.");
                _taskManager.Enqueue(() => PlayerHelper.IsValid, int.MaxValue, "RegisterSquadron");
                Svc.Log.Info("Delaying next Enqueue by 2s");
                _taskManager.DelayNext("RegisterSquadron", 2000);
            }

            //Defining the GUI for the squadron duty finder
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("GcArmyCapture", out captureAddon), "RegisterSquadron");
            
            // Run logic to open the squadron duty finder
            _taskManager.Enqueue(() => OpenSquadron(captureAddon), "RegisterSquadron");

            // Check if we're viewing missions to select (dungeons)
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("GcArmyCapture", out captureAddon) && GenericHelpers.IsAddonReady(captureAddon), "RegisterSquadron");
            
            // Select Mission
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(captureAddon, true, 11, content.GCArmyIndex), "RegisterSquadron");


            _taskManager.Enqueue(() => Svc.Log.Warning(content.GCArmyIndex.ToString()));

            // Open member list
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(captureAddon, true, 12, content.GCArmyIndex), "RegisterSquadron-MemberList");

            AtkUnitBase* memberListAddon = null;
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("GcArmyMemberList", out memberListAddon), "RegisterSquadron-GetMemberList");

            ReaderGCArmyMemberList armyMemberList = null!;
            _taskManager.Enqueue(() => armyMemberList = new ReaderGCArmyMemberList(memberListAddon), "RegisterSquadron-GetReader");

            // disable active members
            _taskManager.Enqueue(() =>
                                 {
                                     IEnumerable<(ReaderGCArmyMemberList.MemberInfo Value, int Index)> activeMembers = armyMemberList.Entries.WithIndex().Where((tuple) => tuple.Value.Selected);

                                     foreach ((ReaderGCArmyMemberList.MemberInfo? mi, int index) in activeMembers) 
                                         _taskManager.EnqueueImmediate(() => AddonHelper.FireCallBack(memberListAddon, true, 11, index));
                                 }, "RegisterSquadron-DisableMembers");

            // select lowest fitting members
            _taskManager.Enqueue(() =>
                                 {
                                     CombatRole role = Player.Job.GetCombatRole();

                                     int neededTanks = role == CombatRole.Tank ? 0 : 1;
                                     int neededDPS   = role == CombatRole.DPS ? 1 : 2;
                                     int neededHeal  = role == CombatRole.Healer ? 0 : 1;

                                     foreach ((ReaderGCArmyMemberList.MemberInfo? member, int index) in armyMemberList.Entries.WithIndex().OrderBy(tu => tu.Value.Level).Where(tu => tu.Value.Level >= content.ClassJobLevelRequired))
                                     {
                                         switch (member.ClassType)
                                         {
                                             case ReaderGCArmyMemberList.SquadronClassType.Marauder:
                                             case ReaderGCArmyMemberList.SquadronClassType.Gladiator:
                                                 if(neededTanks > 0)
                                                 {
                                                     _taskManager.EnqueueImmediate(() => AddonHelper.FireCallBack(memberListAddon, true, 11, index));
                                                     neededTanks--;
                                                 }
                                                 break;
                                             case ReaderGCArmyMemberList.SquadronClassType.Pugilist:
                                             case ReaderGCArmyMemberList.SquadronClassType.Lancer:
                                             case ReaderGCArmyMemberList.SquadronClassType.Archer:
                                             case ReaderGCArmyMemberList.SquadronClassType.Thaumaturge:
                                             case ReaderGCArmyMemberList.SquadronClassType.Arcanist:
                                             case ReaderGCArmyMemberList.SquadronClassType.Rogue:
                                                 if (neededDPS > 0)
                                                 {
                                                     _taskManager.EnqueueImmediate(() => AddonHelper.FireCallBack(memberListAddon, true, 11, index));
                                                     neededDPS--;
                                                 }
                                                 break;
                                             case ReaderGCArmyMemberList.SquadronClassType.Conjurer:
                                                 if (neededHeal > 0)
                                                 {
                                                     _taskManager.EnqueueImmediate(() => AddonHelper.FireCallBack(memberListAddon, true, 11, index));
                                                     neededHeal--;
                                                 }
                                                 break;
                                             default:
                                                 throw new ArgumentOutOfRangeException();
                                         }
                                     }
                                 }, "RegisterSquadron-SelectMembers");
            
            // click ok
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(captureAddon, true, 13), "RegisterSquadron-Queue");

            AtkUnitBase* contentFinderAddon = null;

            // retrieve the ContentsFinderConfirm addon
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out contentFinderAddon) && GenericHelpers.IsAddonReady(contentFinderAddon), "RegisterSquadron-ContentFinder");

            // Confirm Duty
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(contentFinderAddon, true, 8), "RegisterSquadron-ConfirmDuty");

            // Check if we're in a valid map for the dungeon / paths
            _taskManager.Enqueue(() => Svc.ClientState.TerritoryType == content.TerritoryType, int.MaxValue, "RegisterSquadron-WaitForMap");

            _taskManager.Enqueue(() => {
                if (Svc.ClientState.TerritoryType == content.TerritoryType)
                {
                    // Reset states because we queued the correct duty, this is for looping
                    Svc.Log.Info("Resetting states for loop.");
                    InteractedWithSergeant = false;
                    OpeningMissions = false;
                    ViewingMissions = false;
                    return true; // Return true to continue the task sequence
                }
                return false; // Return false if we are not in correct duty
            }, "RegisterSquadron-ResetState");
        }
        

        // Try to open the squadron menu by finding the squadron manager until specific GUI window checks are passed
        internal unsafe bool OpenSquadron(AtkUnitBase* aub)
        {
            ViewingMissions = false;
            OpeningMissions = false;
            InteractedWithSergeant = false;

            if (aub != null)
            {
                return true;
            }

            if (GenericHelpers.TryGetAddonByName("Talk", out AtkUnitBase* addonTalk) && GenericHelpers.IsAddonReady(addonTalk))
            {
                // Talk window up ClickIt
                AddonHelper.ClickTalk();
                Svc.Log.Info("Clicking Talk");
                return false;
            }

            if (GenericHelpers.TryGetAddonByName("GcArmyCapture", out AtkUnitBase* _))
            {
                // Viewing missions, move on to the next step for registering
                ViewingMissions = true;
                Svc.Log.Info("ViewingMissions: TRUE");
                return true;
            }

            // Attempt to get the squadron sergeant once and reuse the result --- This still sets this every call I will change this to one and done later.
            IGameObject? gameObject = ObjectHelper.GetObjectByDataId(1016924u) ?? ObjectHelper.GetObjectByDataId(1016986u) ?? ObjectHelper.GetObjectByDataId(1016987u);
            if (gameObject == null || !MovementHelper.Move(gameObject, 0.25f, 6f))
            {
                return false;
            }

            // Check if the GcArmyExpeditionResult addon is open
            if (GenericHelpers.TryGetAddonByName("GcArmyExpeditionResult", out AtkUnitBase* expeditionResultScreen))
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
            if (GenericHelpers.TryGetAddonByName("SelectString", out AtkUnitBase* sergeantListMenu))
            {
                // Successfully interacted with the Sergeant
                InteractedWithSergeant = true;
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
                if (GenericHelpers.TryGetAddonByName("GcArmyCapture", out AtkUnitBase* _))
                {
                    InteractedWithSergeant = true;
                }
                    
                return false;
            }

            return false;
        }

    }
}
