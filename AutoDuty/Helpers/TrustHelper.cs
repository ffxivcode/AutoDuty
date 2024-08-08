using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using ECommons;
    using ECommons.Automation;
    using ECommons.DalamudServices;
    using ECommons.Throttlers;
    using FFXIVClientStructs.FFXIV.Component.GUI;
    using static Dalamud.Interface.Utility.Raii.ImRaii;
    using static global::AutoDuty.Helpers.ContentHelper;

    internal static class TrustHelper
    {
        internal static unsafe uint GetLevelFromTrustWindow(AtkUnitBase* addon)
        {
            if (addon == null)
                return 0;

            AtkComponentNode* atkResNode       = addon->GetComponentNodeById(88);
            AtkResNode*       resNode          = atkResNode->Component->UldManager.NodeList[5];
            AtkResNode*       resNodeChildNode = resNode->GetComponent()->UldManager.NodeList[0];
            return Convert.ToUInt32(resNodeChildNode->GetAsAtkCounterNode()->NodeText.ExtractText());
        }


        internal static bool CanTrustRun(this Content content)
        {
            if (!content.TrustContent)
                return false;

            if (content.TrustMembers.Any(tm => tm.Level <= 0))
            {
                AutoDuty.Plugin._trustManager.GetLevels(content);
                return false;
            }

            return content.TrustMembers.TrueForAll(tm => tm.Level >= content.ClassJobLevelRequired);
        }
    }
}
