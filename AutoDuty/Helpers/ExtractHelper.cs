using AutoDuty.IPC;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal unsafe static class ExtractHelper
    {
        internal static void Invoke()
        {
            if (!QuestManager.IsQuestComplete(66174))
                Svc.Log.Info("Materia Extraction requires having completed quest: Forging the Spirit");
            else if (!ExtractRunning)
            {
                Svc.Log.Info("Extract Materia Started");
                ExtractRunning = true;
                if (AutoDuty.Plugin.Configuration.AutoExtractAll)
                    stoppingCategory = 6;
                else
                    stoppingCategory = 0;
                Svc.Framework.Update += ExtractUpdate;
            }
        }

        internal static void Stop()
        {
            ExtractRunning = false;
            currentCategory = 0;
            switchedCategory = false;
            Svc.Framework.Update -= ExtractUpdate;
        }

        internal static bool ExtractRunning = false;

        private static int currentCategory = 0;

        private static int stoppingCategory;

        private static bool switchedCategory = false;

        internal static unsafe void ExtractUpdate(IFramework framework)
        {
            if (!EzThrottler.Throttle("Extract", 250))
                return;

            if (ObjectHelper.IsOccupied)
                return;

            if (GenericHelpers.TryGetAddonByName("MaterializeDialog", out AtkUnitBase* addonSalvageDialog) && GenericHelpers.IsAddonReady(addonSalvageDialog))
            {
                Svc.Log.Debug("AutoExtract - Confirming MaterializeDialog");
                new AddonMaster.MaterializeDialog(addonSalvageDialog).Materialize();
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
