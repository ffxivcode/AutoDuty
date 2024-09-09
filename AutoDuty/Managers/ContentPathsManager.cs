using AutoDuty.Helpers;
using AutoDuty.Windows;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoDuty.Managers
{
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
                                    List<PathAction> pathActions = [];
                                    paths.Each(x => 
                                    {
                                        var action = x.Split('|');
                                        var position = action[1].Split(", ");
                                        Vector3 v3Position = new(float.Parse(position[0]), float.Parse(position[1]), float.Parse(position[2]));

                                        pathActions.Add(new PathAction { Name = action[0], Position = v3Position, Argument = action[2] });
                                    });
                                    pathFile = new()
                                    {
                                        Actions = [.. pathActions]
                                    };
                                    pathFile.Meta.Changelog.Add(new() { Change = "Converted to new JSON Structure", Version = AutoDuty.Plugin.Configuration.Version });
                                }
                                
                                string jsonNew = JsonSerializer.Serialize(PathFile, BuildTab.jsonSerializerOptions);
                                File.WriteAllText(FilePath, jsonNew);
                            }
                            else if (!json.Contains("\"name\"") && !json.Contains("\"position\"") && !json.Contains("\"argument\""))
                            {
                                json = json.Replace("actions", "actionsString");
                                var doc = JsonDocument.Parse(json);
                                var element = doc.RootElement.GetProperty("actionsString");
                                var paths = element.Deserialize<List<string>>();
                                List<PathAction> pathActions = [];
                                if (paths != null && paths.Count != 0)
                                {
                                    paths.Each(x =>
                                    {
                                        var action = x.Split('|');
                                        var pathAction = new PathAction { Name = action[0] };
                                        if (action.Length > 1)
                                        {
                                            var position = action[1].Replace(" ", string.Empty).Split(",");
                                            
                                            pathAction.Position = new(float.Parse(position[0]), float.Parse(position[1]), float.Parse(position[2]));

                                            
                                            if (action.Length == 3)
                                                pathAction.Argument = action[2];
                                        }
                                        pathActions.Add(pathAction);
                                    });
                                    json = json.Replace("\"actionsString\": [],", string.Empty);
                                    json = Regex.Replace(json, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
                                    pathFile = JsonSerializer.Deserialize<PathFile>(json);
                                    if (pathFile == null) return new();
                                    pathFile.Actions = [.. pathActions];
                                    pathFile.Meta.Changelog.Add(new() { Change = "Converted to new JSON Structure", Version = AutoDuty.Plugin.Configuration.Version });
                                    string jsonNew = JsonSerializer.Serialize(PathFile, BuildTab.jsonSerializerOptions);
                                    File.WriteAllText(FilePath, jsonNew);
                                }
                            }
                            else
                            {
                                pathFile = JsonSerializer.Deserialize<PathFile>(json, BuildTab.jsonSerializerOptions);
                            }

                            RevivalFound = PathFile.Actions.Any(x => x.Name.Equals("Revival", StringComparison.CurrentCultureIgnoreCase));
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

            public List<PathAction> Actions => PathFile.Actions;
            public uint[] Interactables => PathFile.Interactables;
            public bool RevivalFound { get; private set; }
        }
    }
}
