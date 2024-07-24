using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ECommons;

namespace AutoDuty.Helpers
{
    internal static class ExitDutyHelper
    {
        internal unsafe static void Invoke()
        {
            AgentModule.Instance()->GetAgentByInternalId(AgentId.ContentsFinderMenu)->Show();
            if (GenericHelpers.TryGetAddonByName("ContentsFinderMenu", out AtkUnitBase* addonContentsFinderMenu))
            {
                AddonHelper.FireCallBack(addonContentsFinderMenu, true, 0);
                AddonHelper.FireCallBack(addonContentsFinderMenu, false, -2);
                GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno);
                AddonHelper.FireCallBack(addonSelectYesno, true, 0);
            }
        }
    }
}
