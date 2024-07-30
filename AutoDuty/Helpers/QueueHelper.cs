using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Diagnostics;
using System.Linq;
using static AutoDuty.Helpers.ContentHelper;

namespace AutoDuty.Helpers
{
    internal unsafe static class QueueHelper
    {
        internal static void Invoke(string content)
        {
            _content = content;
            QueueRunning = true;
            Svc.Framework.Update += QueueUpdate;
        }

        internal static void Stop()
        {
            _content = null;
            QueueRunning = false;
            Svc.Framework.Update -= QueueUpdate;
        }

        internal static bool QueueRunning = false;

        private static string? _content = null;
        private static AtkUnitBase* addon = null;

        internal static void QueueUpdate(IFramework _)
        {
            if (_content == null)
            {
                Stop();
                return;
            }
            AutoDuty.Plugin.Action = $"Step: Queueing Duty: {_content}";


            if (!ObjectHelper.IsValid)
                return;

            if (ContentsFinder.Instance()->IsUnrestrictedParty != AutoDuty.Plugin.Configuration.Unsynced)
            {
                ContentsFinder.Instance()->IsUnrestrictedParty = AutoDuty.Plugin.Configuration.Unsynced;
                return;
            }

            if (!GenericHelpers.TryGetAddonByName("ContentsFinder", out addon) || !GenericHelpers.IsAddonReady(addon))
            {
                AgentContentsFinder.Instance()->OpenRegularDuty(1);
                return;
            }

            if (((AddonContentsFinder*)addon)->DutyList->Items.LongCount == 0)
                return;

            var a = ((AddonContentsFinder*)addon)->DutyList->Items.ToList();
            //a.Where(x => x.Value->UIntValues[0] != 0);
            Svc.Log.Info($"{a.Count}");
            foreach (var x in a)
            {
                Svc.Log.Info("_______");
                var aas = x.Value->Renderer->GetTextNodeById(5);
                if (aas != null)
                {
                    var aat = aas->GetAsAtkTextNode();
                    if (aat != null)
                        Svc.Log.Info($"{aat->NodeText}");
                }
                var xarr = x.Value->StringValues.ToArray();
                var yarr = x.Value->UIntValues.ToArray();
                foreach (var y in xarr)
                {
                    if (y.Value != null)
                        Svc.Log.Info($"{xarr.Length} {y.Value->ToString()}");
                }
                foreach (var z in yarr)
                {
                    Svc.Log.Info($"{yarr.Length} {z}");
                }
            }
            Svc.Framework.Update -= QueueUpdate;
            /*_taskManager.Enqueue(() => ((AddonContentsFinder*)addon)->DutyList->Items.LongCount > 0, "RegisterRegularDuty");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 12, 1), "RegisterRegularDuty");
            _taskManager.DelayNext("RegisterRegularDuty", 50);
            _taskManager.Enqueue(() => SelectDuty(content, (AddonContentsFinder*)addon), "RegisterRegularDuty");
            _taskManager.DelayNext("RegisterRegularDuty", 50);
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 12, 0), "RegisterRegularDuty");
            _taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out addon) && GenericHelpers.IsAddonReady(addon), "RegisterRegularDuty");
            _taskManager.Enqueue(() => AddonHelper.FireCallBack(addon, true, 8), "RegisterRegularDuty");*/
        }

        private static unsafe bool SelectDuty(Content content, AddonContentsFinder* addon)
        {
            if (content.Name == null) return false;
            if (GenericHelpers.IsAddonReady(&addon->AtkUnitBase))
            {
                var list = addon->AtkUnitBase.GetNodeById(52)->GetAsAtkComponentList();
                for (var i = 3; i < 19; i++)
                {
                    var componentNode = list->UldManager.NodeList[i]->GetComponent();
                    if (componentNode is null) continue;
                    var textNode = componentNode->GetTextNodeById(5)->GetAsAtkTextNode();
                    var buttonNode = componentNode->UldManager.NodeList[16]->GetAsAtkComponentCheckBox();
                    if (textNode is null || buttonNode is null) continue;
                    if (textNode->NodeText.ToString().EndsWith("..."))
                    {
                        var textnode = textNode->NodeText.ToString().Replace("...", "");
                        var len = textnode.Length;
                        if (content.Name.Substring(0, len).Equals(textnode))
                            buttonNode->ClickCheckboxButton(componentNode, 0);
                    }
                    else if (textNode->NodeText.ToString() == content.Name)
                        buttonNode->ClickCheckboxButton(componentNode, 0);
                }
                return true;
            }
            return false;
        }
    }
}
