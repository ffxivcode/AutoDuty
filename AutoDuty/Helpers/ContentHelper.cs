using ECommons;
using ECommons.DalamudServices;
using System.Collections.Generic;
using System.Linq;
using ECommons.GameFunctions;
using global::AutoDuty.Managers;
using Lumina.Data;
using Lumina.Text;

namespace AutoDuty.Helpers
{
    using Lumina.Excel.GeneratedSheets;
    using ContentFinderCondition = Lumina.Excel.GeneratedSheets2.ContentFinderCondition;
    using DawnContent = Lumina.Excel.GeneratedSheets2.DawnContent;
    using DawnMemberUIParam = Lumina.Excel.GeneratedSheets2.DawnMemberUIParam;

    internal static class ContentHelper
    {
        internal static Dictionary<uint, Content> DictionaryContent { get; set; } = [];

        private static List<uint> ListGCArmyContent { get; set; } = [162, 1039, 1041, 1042, 171, 172, 159, 160, 349, 362, 188, 1064, 1066, 430, 510];
        
        private static List<uint> ListVVDContent { get; set; } = [1069, 1137, 1176]; //[1069, 1075, 1076, 1137, 1155, 1156, 1176, 1179, 1180]; *Criterions


        internal class Content
        {
            internal string? Name { get; set; }

            internal string? DisplayName { get; set; }

            internal uint TerritoryType { get; set; }

            internal uint ExVersion { get; set; }

            internal byte ClassJobLevelRequired { get; set; }

            internal uint ItemLevelRequired { get; set; }

            internal bool DawnContent { get; set; } = false;

            internal int DawnIndex { get; set; } = -1;

            internal uint ContentFinderCondition { get; set; }

            internal uint ContentType { get; set; }

            internal uint ContentMemberType { get; set; }

            internal bool TrustContent { get; set; } = false;

            internal bool VariantContent { get; set; } = false;

            internal int VVDIndex { get; set; } = -1;

            internal bool GCArmyContent { get; set; } = false;

            internal int GCArmyIndex { get; set; } = -1;

            internal List<TrustMember> TrustMembers { get; set; } = new();
        }

        internal static void PopulateDuties()
        {
            var listContentFinderCondition = Svc.Data.GameData.GetExcelSheet<ContentFinderCondition>(Language.English);
            var listContentFinderConditionDisplay =
                Svc.Data.GameData.Options.DefaultExcelLanguage == Language.English ?
                                                        listContentFinderCondition :
                                                        Svc.Data.GameData.GetExcelSheet<ContentFinderCondition>() ?? listContentFinderCondition;

            var listDawnContent = Svc.Data.GameData.GetExcelSheet<DawnContent>(Language.English);


            if (listContentFinderCondition == null || listDawnContent == null) return;


            foreach (var contentFinderCondition in listContentFinderCondition)
            {
                if (contentFinderCondition.ContentType.Value == null || contentFinderCondition.TerritoryType.Value == null || contentFinderCondition.TerritoryType.Value.ExVersion.Value == null || (contentFinderCondition.ContentType.Value.RowId != 2 && contentFinderCondition.ContentType.Value.RowId != 4 && contentFinderCondition.ContentType.Value.RowId != 5 && contentFinderCondition.ContentType.Value.RowId != 30) || contentFinderCondition.Name.RawString.IsNullOrEmpty())
                    continue;

                string CleanName(string name)
                {
                    string result = name;
                    if (result[.. 3].Equals("the"))
                        result = result.ReplaceFirst("the", "The");
                    return result.Replace("--", "-").Replace("<italic(0)>", "").Replace("<italic(1)>", "");
                }

                var content = new Content
                {
                    Name = CleanName(contentFinderCondition.Name.RawString),
                    TerritoryType = contentFinderCondition.TerritoryType.Value.RowId,
                    ContentType = contentFinderCondition.ContentType.Value.RowId,
                    ContentMemberType = contentFinderCondition.ContentMemberType.Value?.RowId ?? 0,
                    ContentFinderCondition = contentFinderCondition.RowId,
                    ExVersion = contentFinderCondition.TerritoryType.Value.ExVersion.Value.RowId,
                    ClassJobLevelRequired = contentFinderCondition.ClassJobLevelRequired,
                    ItemLevelRequired = contentFinderCondition.ItemLevelRequired,
                    DawnContent = listDawnContent.Any(dawnContent => dawnContent.Content.Value == contentFinderCondition),
                    TrustContent = listDawnContent.Any(dawnContent => dawnContent.Content.Value == contentFinderCondition) && contentFinderCondition.TerritoryType.Value.ExVersion.Value.RowId > 2,
                    VariantContent = ListVVDContent.Any(variantContent => variantContent == contentFinderCondition.TerritoryType.Value.RowId),
                    VVDIndex = ListVVDContent.FindIndex(variantContent => variantContent == contentFinderCondition.TerritoryType.Value.RowId),
                    GCArmyContent = ListGCArmyContent.Any(gcArmyContent => gcArmyContent == contentFinderCondition.TerritoryType.Value.RowId),
                    GCArmyIndex = ListGCArmyContent.FindIndex(gcArmyContent => gcArmyContent == contentFinderCondition.TerritoryType.Value.RowId)
                };

                SeString? displayName = listContentFinderConditionDisplay?.GetRow(contentFinderCondition.RowId)?.Name;
                content.DisplayName = displayName != null ? CleanName(displayName) : content.Name;

                if (content.DawnContent && listDawnContent.Where(dawnContent => dawnContent.Content.Value == contentFinderCondition).Any())
                    content.DawnIndex = listDawnContent.Where(dawnContent => dawnContent.Content.Value == contentFinderCondition).First().RowId < 32 ? (int)listDawnContent.Where(dawnContent => dawnContent.Content.Value == contentFinderCondition).First().RowId : (int)listDawnContent.Where(dawnContent => dawnContent.Content.Value == contentFinderCondition).First().RowId - 200;
                
                if (content.TrustContent)
                {
                    content.TrustMembers.Add(TrustManager.members[TrustMemberName.AlisaieBlue]);
                    content.TrustMembers.Add(TrustManager.members[TrustMemberName.Alisaie]);
                    content.TrustMembers.Add(TrustManager.members[TrustMemberName.Thancred]);
                    content.TrustMembers.Add(TrustManager.members[TrustMemberName.Urianger]);
                    content.TrustMembers.Add(TrustManager.members[TrustMemberName.BestCatGirl]);
                    content.TrustMembers.Add(TrustManager.members[content.ExVersion == 3 ?
                                                                      TrustMemberName.Ryne :
                                                                      TrustMemberName.Estinien
                                                                 ]);
                    content.TrustMembers.Add(TrustManager.members[TrustMemberName.Graha]);
                    if (content.TerritoryType is >= 1097 and <= 1164)
                        content.TrustMembers.Add(TrustManager.members[TrustMemberName.Zero]);
                    if (content.ExVersion == 5)
                        content.TrustMembers.Add(TrustManager.members[TrustMemberName.Krile]);
                }

                DictionaryContent.Add(contentFinderCondition.TerritoryType.Value.RowId, content);
            }

            DictionaryContent = DictionaryContent.OrderBy(content => content.Value.ExVersion).ThenBy(content => content.Value.ClassJobLevelRequired).ThenBy(content => content.Value.TerritoryType).ToDictionary();
        }

        public static bool CanRun(this Content content, short level = -1, short ilvl = -1)
        {
            if ((AutoDuty.Plugin.Player?.GetRole() ?? CombatRole.NonCombat) == CombatRole.NonCombat)
                return false;

            if (level < 0) 
                level = PlayerHelper.GetCurrentLevelFromSheet();

            if (ilvl < 0) 
                ilvl = PlayerHelper.GetCurrentItemLevelFromGearSet();

            return content.ClassJobLevelRequired <= level                                 &&
                   ContentPathsManager.DictionaryPaths.ContainsKey(content.TerritoryType) &&
                   content.ItemLevelRequired <= ilvl;
        }
    }
}
