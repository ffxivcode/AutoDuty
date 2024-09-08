using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
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
            public ContentPathContainer(Content content)
            {
                Content = content;
                id      = content.TerritoryType;

                this.ColoredNameString = $"({ImGuiHelper.idColor}{this.id}</>) {ImGuiHelper.dutyColor}{this.Content!.Name}</>";
                this.ColoredNameRegex  = RegexHelper.ColoredTextRegex().Match(this.ColoredNameString);
            }

            public uint id { get; }

            public Content Content { get; }

            public List<DutyPath> Paths { get; } = [];

            public string ColoredNameString { get; }

            public Match ColoredNameRegex { get; private set; }

            public DutyPath? SelectPath(out int pathIndex, Job? job = null)
            {
                job ??= Player.Object.GetJob();

                if (job == null)
                {
                    pathIndex = 0;
                    return Paths[0];
                }

                if (Paths.Count > 1)
                {
                    if (AutoDuty.Plugin.Configuration.PathSelections.TryGetValue(Content.TerritoryType, out Dictionary<Job, int>? jobConfig))
                    {
                        if (jobConfig.TryGetValue((Job) job, out int pathId))
                        {
                            if (pathId < Paths.Count)
                            {
                                pathIndex = pathId;
                                return Paths[pathIndex];
                            }
                        }
                    }

                    if (job.GetRole() == CombatRole.Tank)
                    {
                        for (int index = 0; index < Paths.Count; index++)
                        {
                            string curPath = Paths[index].Name;
                            if (curPath.Contains(PathIdentifiers.W2W))
                            {
                                pathIndex = index;
                                return Paths[index];
                            }
                        }
                    }
                }

                pathIndex = 0;
                return Paths[0];
            }
        }

        internal class DutyPath
        {
            public DutyPath(string filePath)
            {
                FilePath = filePath;
                FileName = Path.GetFileName(filePath);
                Name     = FileName.Replace(".json", string.Empty);

                UpdateColoredNames();
            }

            public void UpdateColoredNames()
            {
                Match pathMatch = RegexHelper.PathFileRegex().Match(FileName);

                string pathFileColor = AutoDuty.Plugin.Configuration.DoNotUpdatePathFiles.Contains(FileName) ? ImGuiHelper.pathFileColorNoUpdate : ImGuiHelper.pathFileColor;
                id = uint.Parse(pathMatch.Groups[2].Value);
                ColoredNameString = pathMatch.Success ?
                                             $"<0.8,0.8,1>{pathMatch.Groups[4]}</>{pathFileColor}{pathMatch.Groups[5]}</>" :
                                             FileName;
                ColoredNameRegex = RegexHelper.ColoredTextRegex().Match(ColoredNameString);
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
                    if (pathFile == null)
                    {
                        try
                        {
                            RevivalFound = false;
                            string json;

                            using (StreamReader streamReader = new(FilePath, Encoding.UTF8))
                                json = streamReader.ReadToEnd();

                            // Backwards compatibility, with instant updating
                            if (!json.Contains("\"createdAt\""))
                            {
                                List<string>? paths;
                                if ((paths = JsonSerializer.Deserialize<List<string>>(json)) != null)
                                {
                                    pathFile         = PathFile.Default;
                                    pathFile.actions = paths.ToArray();
                                }

                                string jsonNew = JsonSerializer.Serialize(PathFile, BuildTab.jsonSerializerOptions);
                                File.WriteAllText(FilePath, jsonNew);
                            }
                            else
                            {
                                pathFile = JsonSerializer.Deserialize<PathFile>(json);
                            }

                            foreach (string action in PathFile.actions)
                                if (action.Split('|')[0].Trim() == "Revival")
                                {
                                    RevivalFound = true;
                                    break;
                                }
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Info($"{FilePath} is not a valid duty path {ex}");
                            DictionaryPaths[id].Paths.Remove(this);
                        }
                    }

                    return pathFile!;
                }
            }

            public string[] Actions => PathFile.actions;
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

            public int LastUpdatedVersion => changelog.Count > 0 ? changelog.Last().version : createdAt;

            public List<string> notes { get; set; }
        }

        internal class PathFileChangelogEntry
        {
            public int    version { get; set; }
            public string change  { get; set; } = string.Empty;
        }
    }
}
