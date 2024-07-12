using ECommons;
using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;

namespace AutoDuty.Helpers
{
    internal static class ContentHelper
    {
        internal static Dictionary<uint, Content> DictionaryContent { get; set; } = [];

        private static List<uint> ListGCArmyContent { get; set; } = [162, 1039, 1041, 1042, 171, 172, 159, 160, 349, 362, 188, 1064, 1066, 430, 510];

        internal class Content
        {
            internal string? Name { get; set; }

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

            internal bool GCArmyContent { get; set; } = false;

            internal int GCArmyIndex { get; set; } = -1;
        }

        internal static void PopulateDuties()
        {
            var listContentFinderCondition = Svc.Data.GameData.GetExcelSheet<ContentFinderCondition>();
            var listDawnContent = Svc.Data.GameData.GetExcelSheet<DawnContent>();
            if (listContentFinderCondition == null || listDawnContent == null) return;

            foreach (var contentFinderCondition in listContentFinderCondition)
            {
                if (contentFinderCondition.ContentType.Value == null || contentFinderCondition.TerritoryType.Value == null || contentFinderCondition.TerritoryType.Value.ExVersion.Value == null || ( contentFinderCondition.ContentType.Value.RowId != 2 && contentFinderCondition.ContentType.Value.RowId != 4 && contentFinderCondition.ContentType.Value.RowId != 5 ) || contentFinderCondition.Name.ToString().IsNullOrEmpty())
                    continue;

                var content = new Content
                {
                    Name = contentFinderCondition.Name.ToString()[..3].Equals("the") ? contentFinderCondition.Name.ToString().ReplaceFirst("the", "The").Replace("--", "-").Replace("<italic(0)>","").Replace("<italic(1)>", "") : contentFinderCondition.Name.ToString().Replace("--", "-").Replace("<italic(0)>", "").Replace("<italic(1)>", ""),
                    TerritoryType = contentFinderCondition.TerritoryType.Value.RowId,
                    ContentType = contentFinderCondition.ContentType.Value.RowId,
                    ContentMemberType = contentFinderCondition.ContentMemberType.Value?.RowId ?? 0,
                    ContentFinderCondition = contentFinderCondition.RowId,
                    ExVersion = contentFinderCondition.TerritoryType.Value.ExVersion.Value.RowId,
                    ClassJobLevelRequired = contentFinderCondition.ClassJobLevelRequired,
                    ItemLevelRequired = contentFinderCondition.ItemLevelRequired,
                    DawnContent = listDawnContent.Any(dawnContent => dawnContent.Content.Value == contentFinderCondition),
                    TrustContent = listDawnContent.Any(dawnContent => dawnContent.Content.Value == contentFinderCondition) && contentFinderCondition.TerritoryType.Value.ExVersion.Value.RowId > 2,
                    GCArmyContent = ListGCArmyContent.Any(gcArmyContent => gcArmyContent == contentFinderCondition.TerritoryType.Value.RowId),
                    GCArmyIndex = ListGCArmyContent.FindIndex(gcArmyContent => gcArmyContent == contentFinderCondition.TerritoryType.Value.RowId)
                };
                if (content.DawnContent && listDawnContent.Where(dawnContent => dawnContent.Content.Value == contentFinderCondition).Any())
                    content.DawnIndex = listDawnContent.Where(dawnContent => dawnContent.Content.Value == contentFinderCondition).First().RowId < 32 ? (int)listDawnContent.Where(dawnContent => dawnContent.Content.Value == contentFinderCondition).First().RowId : (int)listDawnContent.Where(dawnContent => dawnContent.Content.Value == contentFinderCondition).First().RowId - 192;
                DictionaryContent.Add(contentFinderCondition.TerritoryType.Value.RowId, content);
            }

            DictionaryContent = DictionaryContent.OrderBy(content => content.Value.ExVersion).ThenBy(content => content.Value.ClassJobLevelRequired).ThenBy(content => content.Value.TerritoryType).ToDictionary();
        }
    }
}
