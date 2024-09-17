using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal static class ExtractHelper
    {
        internal static void Invoke()
        {
            if (!QuestManager.IsQuestComplete(66174))
                Svc.Log.Info("Materia Extraction requires having completed quest: Forging the Spirit");
            else if (State != ActionState.Running)
            {
                Svc.Log.Info("Extract Materia Started");
                State = ActionState.Running;
                Plugin.States |= PluginState.Other;
                SchedulerHelper.ScheduleAction("ExtractTimeOut", Stop, 300000);
                if (Plugin.Configuration.AutoExtractAll)
                    _stoppingCategory = 6;
                else
                    _stoppingCategory = 0;
                Plugin.Action = "Extracting Materia";
                Svc.Framework.Update += ExtractUpdate;
            }
        }

        internal unsafe static void Stop()
        {
            _currentCategory = 0;
            _switchedCategory = false;
            Plugin.States |= PluginState.Other;
            Plugin.Action = "";
            if (!Plugin.States.HasFlag(PluginState.Looping))
                Plugin.SetGeneralSettings(false);
            SchedulerHelper.DescheduleAction("ExtractTimeOut");
            Svc.Framework.Update += ExtractStopUpdate;
            Svc.Framework.Update -= ExtractUpdate;
            if (GenericHelpers.TryGetAddonByName("MaterializeDialog", out AtkUnitBase* addonMaterializeDialog))
                addonMaterializeDialog->Close(true);
            if (GenericHelpers.TryGetAddonByName("Materialize", out AtkUnitBase* addonMaterialize))
                addonMaterialize->Close(true);
        }

        internal static ActionState State = ActionState.None;

        private static int _currentCategory = 0;
        private static int _stoppingCategory;
        private static bool _switchedCategory = false;

        internal static unsafe void ExtractStopUpdate(IFramework framework)
        {
            if (GenericHelpers.TryGetAddonByName("MaterializeDialog", out AtkUnitBase* addonMaterializeDialogClose))
                addonMaterializeDialogClose->Close(true);
            else if (GenericHelpers.TryGetAddonByName("Materialize", out AtkUnitBase* addonMaterializeClose))
                addonMaterializeClose->Close(true);
            else
            {
                State = ActionState.None;
                Plugin.States &= ~PluginState.Other;
                if (!Plugin.States.HasFlag(PluginState.Looping))
                    Plugin.SetGeneralSettings(true);
                Svc.Framework.Update -= ExtractStopUpdate;
            }
            return;
        }

        internal static unsafe void ExtractUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating) || Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("Extract", 250))
                return;

            if (Conditions.IsMounted)
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                return;
            }

            Plugin.Action = "Extracting Materia";

            if (InventoryManager.Instance()->GetEmptySlotsInBag() < 1)
            {
                Stop();
                return;
            }

            if (PlayerHelper.IsOccupied)
                return;

            if (GenericHelpers.TryGetAddonByName("MaterializeDialog", out AtkUnitBase* addonMaterializeDialog) && GenericHelpers.IsAddonReady(addonMaterializeDialog))
            {
                Svc.Log.Debug("AutoExtract - Confirming MaterializeDialog");
                new AddonMaster.MaterializeDialog(addonMaterializeDialog).Materialize();
                return;
            }

            if (!GenericHelpers.TryGetAddonByName("Materialize", out AtkUnitBase* addonMaterialize))
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 14);
            else if (GenericHelpers.IsAddonReady(addonMaterialize))
            {
                if (_currentCategory <= _stoppingCategory)
                {
                    var list = addonMaterialize->GetNodeById(12)->GetAsAtkComponentList();

                    if (list == null) return;

                    var spiritbondTextNode = list->UldManager.NodeList[2]->GetComponent()->GetTextNodeById(5)->GetAsAtkTextNode();
                    var categoryTextNode = addonMaterialize->GetNodeById(4)->GetAsAtkComponentDropdownList()->UldManager.NodeList[1]->GetAsAtkComponentCheckBox()->GetTextNodeById(3)->GetAsAtkTextNode();

                    if (spiritbondTextNode == null || categoryTextNode == null) return;

                    //switch to Category, if not on it
                    if (!_switchedCategory)
                    {
                        Svc.Log.Debug($"AutoExtract - Switching to Category: {_currentCategory}");
                        AddonHelper.FireCallBack(addonMaterialize, false, 1, _currentCategory);
                        _switchedCategory = true;
                        return;
                    }

                    if (spiritbondTextNode->NodeText.ToString() == "100%")
                    {
                        Svc.Log.Debug($"AutoExtract - Extracting Materia");
                        AddonHelper.FireCallBack(addonMaterialize, true, 2, 0);
                        return;
                    }
                    else
                    {
                        _currentCategory++;
                        _switchedCategory = false;
                    }
                }
                else
                {
                    addonMaterialize->Close(true);
                    Svc.Log.Info("Extract Materia Finished");
                    Stop();
                }
            }
        }
    }
}
