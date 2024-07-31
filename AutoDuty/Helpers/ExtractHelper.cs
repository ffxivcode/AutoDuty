using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal static class ExtractHelper
    {
        internal static void Invoke()
        {
            if (!QuestManager.IsQuestComplete(66174))
                Svc.Log.Info("Materia Extraction requires having completed quest: Forging the Spirit");
            else if (!ExtractRunning)
            {
                Svc.Log.Info("Extract Materia Started");
                ExtractRunning = true;
                SchedulerHelper.ScheduleAction("ExtractTimeOut", Stop, 300000);
                if (AutoDuty.Plugin.Configuration.AutoExtractAll)
                    stoppingCategory = 6;
                else
                    stoppingCategory = 0;
                AutoDuty.Plugin.Action = "Extracting Materia";
                Svc.Framework.Update += ExtractUpdate;
                if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                    ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(false);
            }
        }

        internal unsafe static void Stop()
        {
            ExtractRunning = false;
            currentCategory = 0;
            switchedCategory = false;
            AutoDuty.Plugin.Action = "";
            SchedulerHelper.DescheduleAction("ExtractTimeOut");
            Svc.Framework.Update -= ExtractUpdate;
            if (GenericHelpers.TryGetAddonByName("MaterializeDialog", out AtkUnitBase* addonMaterializeDialog))
                addonMaterializeDialog->Close(true);
            if (GenericHelpers.TryGetAddonByName("Materialize", out AtkUnitBase* addonMaterialize))
                addonMaterialize->Close(true);
            if (ReflectionHelper.YesAlready_Reflection.IsEnabled)
                ReflectionHelper.YesAlready_Reflection.SetPluginEnabled(true);
        }

        internal static bool ExtractRunning = false;

        private static int currentCategory = 0;

        private static int stoppingCategory;

        private static bool switchedCategory = false;

        internal static unsafe void ExtractUpdate(IFramework framework)
        {
            if (AutoDuty.Plugin.Started || AutoDuty.Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("Extract", 250))
                return;

            if (Conditions.IsMounted)
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                return;
            }

            AutoDuty.Plugin.Action = "Extracting Materia";

            if (InventoryManager.Instance()->GetEmptySlotsInBag() < 1)
            {
                Stop();
                return;
            }

            if (ObjectHelper.IsOccupied)
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
                if (currentCategory <= stoppingCategory)
                {
                    var list = addonMaterialize->GetNodeById(12)->GetAsAtkComponentList();

                    if (list == null) return;

                    var spiritbondTextNode = list->UldManager.NodeList[2]->GetComponent()->GetTextNodeById(5)->GetAsAtkTextNode();
                    var categoryTextNode = addonMaterialize->GetNodeById(4)->GetAsAtkComponentDropdownList()->UldManager.NodeList[1]->GetAsAtkComponentCheckBox()->GetTextNodeById(3)->GetAsAtkTextNode();

                    if (spiritbondTextNode == null || categoryTextNode == null) return;

                    //switch to Category, if not on it
                    if (!switchedCategory)
                    {
                        Svc.Log.Debug($"AutoExtract - Switching to Category: {currentCategory}");
                        AddonHelper.FireCallBack(addonMaterialize, false, 1, currentCategory);
                        switchedCategory = true;
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
                        currentCategory++;
                        switchedCategory = false;
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
