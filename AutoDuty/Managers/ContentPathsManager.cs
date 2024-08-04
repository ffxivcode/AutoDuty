using System.Collections.Generic;
using System.Text;

namespace AutoDuty.Managers
{
    using System.IO;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using ECommons.ExcelServices;
    using ECommons.GameFunctions;
    using ECommons.GameHelpers;
    using Helpers;

    internal static class ContentPathsManager
    {
        internal static Dictionary<uint, ContentPathContainer> DictionaryPaths = [];

        internal class ContentPathContainer
        {
            public ContentPathContainer(ContentHelper.Content content)
            {
                this.Content = content;
                this.id      = content.TerritoryType;

                this.ColoredNameString = $"({ImGuiHelper.idColor}{this.id}</>) {ImGuiHelper.dutyColor}{this.Content!.DisplayName}</>";
                this.ColoredNameRegex  = RegexHelper.ColoredTextRegex().Match(this.ColoredNameString);
            }

            public uint id { get; }

            public ContentHelper.Content Content { get; }

            public List<DutyPath> Paths { get; } = [];

            public string ColoredNameString { get; }

            public Match ColoredNameRegex { get; private set; }

            public DutyPath? SelectPath(out int pathIndex, Job? job = null)
            {
                job ??= AutoDuty.Plugin.Player?.GetJob();

                if (job == null)
                {
                    pathIndex = 0;
                    return this.Paths[0];
                }

                if (this.Paths.Count > 1)
                {
                    if (AutoDuty.Plugin.Configuration.PathSelections.TryGetValue(this.Content.TerritoryType, out Dictionary<Job, int>? jobConfig))
                    {
                        if (jobConfig.TryGetValue((Job) job, out int pathId))
                        {
                            if (pathId < this.Paths.Count)
                            {
                                pathIndex = pathId;
                                return this.Paths[pathIndex];
                            }
                        }
                    }

                    if (job.GetRole() == CombatRole.Tank)
                    {
                        for (int index = 0; index < this.Paths.Count; index++)
                        {
                            string curPath = this.Paths[index].Name;
                            if (curPath.Contains(PathIdentifiers.W2W))
                            {
                                pathIndex = index;
                                return this.Paths[index];
                            }
                        }
                    }
                }

                pathIndex = 0;
                return this.Paths[0];
            }
        }

        internal class DutyPath
        {
            public DutyPath(string filePath)
            {
                this.FilePath = filePath;
                this.FileName = Path.GetFileName(filePath);
                this.Name     = this.FileName.Replace(".json", string.Empty);

                this.UpdateColoredNames();
            }

            public void UpdateColoredNames()
            {
                Match pathMatch = RegexHelper.PathFileRegex().Match(this.FileName);

                string pathFileColor = AutoDuty.Plugin.Configuration.DoNotUpdatePathFiles.Contains(this.FileName) ? ImGuiHelper.pathFileColorNoUpdate : ImGuiHelper.pathFileColor;

                this.ColoredNameString = pathMatch.Success ?
                                             $"{pathMatch.Groups[1]}{ImGuiHelper.idColor}{pathMatch.Groups[2]}</>{pathMatch.Groups[3]}<0.8,0.8,1>{pathMatch.Groups[4]}</>{pathFileColor}{pathMatch.Groups[5]}</><0.5,0.5,0.5>{pathMatch.Groups[6]}</>" :
                                             this.FileName;
                this.ColoredNameRegex = RegexHelper.ColoredTextRegex().Match(this.ColoredNameString);
            }

            public string Name     { get; }
            public string FileName { get; }
            public string FilePath { get; }

            public  string ColoredNameString { get; private set; } = null!;

            public  Match ColoredNameRegex { get; private set; } = null!;


            private string[] actions = [];

            public string[] Actions
            {
                get
                {
                    if (this.actions.Length <= 0)
                    {
                        this.RevivalFound = false;
                        using StreamReader streamReader = new(this.FilePath, Encoding.UTF8);
                        string             json         = streamReader.ReadToEnd();
                        List<string>?      paths;
                        if ((paths = JsonSerializer.Deserialize<List<string>>(json)) != null)
                            this.actions = paths.ToArray();

                        foreach (string action in this.actions)
                            if (action.Split('|')[0].Trim() == "Revival")
                            {
                                this.RevivalFound = true;
                                break;
                            }
                    }

                    return this.actions;
                }
            }

            public bool RevivalFound { get; private set; }
        }
    }
}
