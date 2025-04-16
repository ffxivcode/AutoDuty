using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoDuty.Helpers
{
    internal class ExtractHelper : ActiveHelperBase<ExtractHelper>
    {
        protected override string Name        => nameof(ExtractHelper);
        protected override string DisplayName => "Extracting Materia";

        protected override string[] AddonsToClose { get; } = ["Materialize", "MaterializeDialog", "SelectYesno", "SelectString"];

        internal override void Start()
        {
            if (!QuestManager.IsQuestComplete(66174))
                Svc.Log.Info("Materia Extraction requires having completed quest: Forging the Spirit");
            else
            {
                base.Start();

                _stoppingCategory = Plugin.Configuration.AutoExtractAll ? 6 : 0;
            }
        }

        internal override unsafe void Stop()
        {
            _currentCategory = 0;
            _switchedCategory = false;
            base.Stop();
        }

        private int _currentCategory = 0;
        private int _stoppingCategory;
        private bool _switchedCategory = false;

        protected override unsafe void HelperUpdate(IFramework framework)
        {
            if (Plugin.States.HasFlag(PluginState.Navigating) || Plugin.InDungeon)
                Stop();

            if (!EzThrottler.Throttle("Extract", 250))
                return;

            if (Conditions.Instance()->Mounted)
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

                    if (spiritbondTextNode->NodeText.ToString().Replace(" ", string.Empty) == "100%")
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
