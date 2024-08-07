using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using AutoDuty.Helpers;
using AutoDuty.Windows;

namespace AutoDuty.Managers
{
    using System;
    using ECommons.DalamudServices;

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
                this.id = uint.Parse(pathMatch.Groups[2].Value);
                this.ColoredNameString = pathMatch.Success ?
                                             $"{pathMatch.Groups[1]}{ImGuiHelper.idColor}{pathMatch.Groups[2]}</>{pathMatch.Groups[3]}<0.8,0.8,1>{pathMatch.Groups[4]}</>{pathFileColor}{pathMatch.Groups[5]}</><0.5,0.5,0.5>{pathMatch.Groups[6]}</>" :
                                             this.FileName;
                this.ColoredNameRegex = RegexHelper.ColoredTextRegex().Match(this.ColoredNameString);
            }

            public uint id;

            public string Name     { get; }
            public string FileName { get; }
            public string FilePath { get; }

            public  string ColoredNameString { get; private set; } = null!;

            public  Match ColoredNameRegex { get; private set; } = null!;

            private PathFile? pathFile = null;
            public PathFile PathFile
            {
                get
                {
                    if (this.pathFile == null)
                    {
                        try
                        {
                            this.RevivalFound = false;
                            string json;

                            using (StreamReader streamReader = new(this.FilePath, Encoding.UTF8))
                                json = streamReader.ReadToEnd();

                            // Backwards compatibility, with instant updating
                            if (!json.Contains("\"createdAt\""))
                            {
                                List<string>? paths;
                                if ((paths = JsonSerializer.Deserialize<List<string>>(json)) != null)
                                {
                                    this.pathFile         = PathFile.Default;
                                    this.pathFile.actions = paths.ToArray();
                                }

                                string jsonNew = JsonSerializer.Serialize(this.PathFile, BuildTab.jsonSerializerOptions);
                                File.WriteAllText(this.FilePath, jsonNew);
                            }
                            else
                            {
                                this.pathFile = JsonSerializer.Deserialize<PathFile>(json);
                            }

                            foreach (string action in this.PathFile.actions)
                                if (action.Split('|')[0].Trim() == "Revival")
                                {
                                    this.RevivalFound = true;
                                    break;
                                }
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Info($"{this.FilePath} is not a valid duty path");
                            DictionaryPaths[this.id].Paths.Remove(this);
                        }
                    }

                    return this.pathFile!;
                }
            }

            public string[] Actions => this.PathFile.actions;
            public         bool                  RevivalFound { get; private set; }
        }

        internal class PathFile
        {
            public string[]         actions { get; set; }
            public PathFileMetaData meta    { get; set; }

            public static PathFile Default => new()
                                              {
                                                  actions = [],
                                                  meta = new PathFileMetaData
                                                         {
                                                             createdAt = AutoDuty.Plugin.Configuration.Version,
                                                             changelog = [],
                                                             notes     = []
                                                         }
                                              };
        }

        internal class PathFileMetaData
        {
            public int                          createdAt { get; set; }
            public List<PathFileChangelogEntry> changelog { get; set; }

            public int LastUpdatedVersion => this.changelog.Count > 0 ? this.changelog.Last().version : this.createdAt;

            public List<string> notes { get; set; }
        }

        internal class PathFileChangelogEntry
        {
            public int    version { get; set; }
            public string change  { get; set; } = string.Empty;
        }
    }
}
