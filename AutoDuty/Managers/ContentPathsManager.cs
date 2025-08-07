using AutoDuty.Helpers;
using AutoDuty.Windows;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoDuty.Managers
{
    using Data;
    using Newtonsoft.Json;
    using static Data.Classes;

    internal static class ContentPathsManager
    {
        internal static Dictionary<uint, ContentPathContainer> DictionaryPaths = [];

        internal class ContentPathContainer
        {
            public ContentPathContainer(Content content)
            {
                Content = content;
                id      = content.TerritoryType;

                ColoredNameString = $"({ImGuiHelper.idColor}{this.id}</>) {ImGuiHelper.dutyColor}{this.Content!.Name}</>";
                ColoredNameRegex  = RegexHelper.ColoredTextRegex().Match(this.ColoredNameString);
            }

            public uint id { get; }

            public Content Content { get; }

            public List<DutyPath> Paths { get; } = [];

            public string ColoredNameString { get; }

            public Match ColoredNameRegex { get; private set; }

            public DutyPath? SelectPath(out int pathIndex, Job? job = null)
            {
                job ??= PlayerHelper.GetJob();

                DutyPath defaultPath = this.Paths[0];

                if (job == null)
                {
                    pathIndex = 0;
                    return defaultPath;
                }

                if (this.Paths.Count > 1)
                {
                    if (Plugin.Configuration.PathSelectionsByPath.TryGetValue(this.Content.TerritoryType, out Dictionary<string, JobWithRole>? jobConfig))
                    {
                        foreach ((string? pathName, JobWithRole pathJobs) in jobConfig)
                        {
                            if (pathJobs.HasJob((Job)job))
                            {
                                int pInx = this.Paths.IndexOf(dp => dp.FileName.Equals(pathName));

                                if (pInx < this.Paths.Count)
                                {
                                    pathIndex = pInx;
                                    return this.Paths[pathIndex];
                                }
                            }
                        }
                    }

                    //temporary while w2w gets integrated
                    if (!defaultPath.W2WFound && Plugin.Configuration.W2WJobs.HasJob(job.Value))
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
                return defaultPath;
            }

            public void AddPath(string name)
            {
                this.Paths.Add(new DutyPath(name, this));
            }
        }

        internal class DutyPath
        {
            public DutyPath(string filePath, ContentPathContainer container)
            {
                FilePath  = filePath;
                FileName  = Path.GetFileName(filePath);
                Name      = FileName.Replace(".json", string.Empty);
                this.container = container;


                UpdateColoredNames();
            }

            public void UpdateColoredNames()
            {
                Match pathMatch = RegexHelper.PathFileRegex().Match(FileName);

                string pathFileColor = Plugin.Configuration.DoNotUpdatePathFiles.Contains(FileName) ? ImGuiHelper.pathFileColorNoUpdate : ImGuiHelper.pathFileColor;
                id = uint.Parse(pathMatch.Groups[2].Value);
                ColoredNameString = pathMatch.Success ?
                                             $"<0.8,0.8,1>{pathMatch.Groups[4]}</>{pathFileColor}{pathMatch.Groups[5]}</>" :
                                             FileName;
                ColoredNameRegex = RegexHelper.ColoredTextRegex().Match(ColoredNameString);
            }

            public readonly ContentPathContainer container;

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
                            W2WFound     = false;

                            string json;

                            using (StreamReader streamReader = new(FilePath, Encoding.UTF8))
                                json = streamReader.ReadToEnd();


                            pathFile = JsonConvert.DeserializeObject<PathFile>(json, ConfigurationMain.JsonSerializerSettings);

                            RevivalFound = PathFile.Actions.Any(x => x.Tag.HasFlag(ActionTag.Revival));
                            W2WFound     = PathFile.Actions.Any(x => x.Tag.HasFlag(ActionTag.W2W));
                            
                            /*
                            if (this.pathFile.Meta.LastUpdatedVersion < 189)
                            {

                                pathFile.Meta.Changelog.Add(new PathFileChangelogEntry
                                                            {
                                                                Version = 189,
                                                                Change  = "Adjusted tags to string values"
                                                            });

                                json = JsonSerializer.Serialize(pathFile, BuildTab.jsonSerializerOptions);
                                File.WriteAllText(FilePath, json);
                            }*/
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Info($"{FilePath} is not a valid duty path: {ex}");
                            DictionaryPaths[id].Paths.Remove(this);
                        }
                    }

                    return pathFile!;
                }
            }

            public List<PathAction> Actions      => PathFile.Actions;
            public bool             RevivalFound { get; private set; }
            public bool             W2WFound { get; private set; }
        }
    }

    internal static class ContentPathContainerExtensions
    {
        public static bool IsFirstPath(this ContentPathsManager.ContentPathContainer container, ContentPathsManager.DutyPath dp) => 
            container.Paths[0] == dp;
    }
}
