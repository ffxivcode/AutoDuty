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
                    if (Plugin.Configuration.PathSelections.TryGetValue(Content.TerritoryType, out Dictionary<Job, int>? jobConfig))
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

                    if (job.GetCombatRole() == CombatRole.Tank)
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

                string pathFileColor = Plugin.Configuration.DoNotUpdatePathFiles.Contains(FileName) ? ImGuiHelper.pathFileColorNoUpdate : ImGuiHelper.pathFileColor;
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
                                        var name = action[0];
                                        var vector3 = action[1].TryGetVector3(out var v3) ? v3 : Vector3.Zero;
                                        var argument = action[2];
                                        var tag = ActionTag.None;

                                        if (name.StartsWithIgnoreCase("Synced"))
                                        {
                                            tag = ActionTag.Synced;
                                            name = name.Remove(0, 6);
                                        }
                                        if (name.StartsWithIgnoreCase("Unsynced"))
                                        {
                                            tag = ActionTag.Unsynced;
                                            name = name.Remove(0, 8);
                                        }
                                        if (name.EqualsIgnoreCase("<-- Comment -->"))
                                            tag = ActionTag.Comment;
                                        if (name.EqualsIgnoreCase("Revival"))
                                            tag = ActionTag.Revival;
                                        if (name.EqualsIgnoreCase("TreasureCoffer"))
                                            tag = ActionTag.Treasure;
                                        pathActions.Add(new PathAction { Tag = tag, Name = name, Position = v3, Arguments = [argument] });
                                    });

                                    pathFile = new()
                                    {
                                        Actions = [.. pathActions]
                                    };
                                    pathFile.Meta.Changelog.Add(new() { Change = "Converted to JSON Structure with Tags", Version = Plugin.Configuration.Version });
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
                                        var tag = ActionTag.None;

                                        if (x.StartsWithIgnoreCase("Synced"))
                                        {
                                            tag = ActionTag.Synced;
                                            x = x.Remove(0, 6);
                                        }
                                        if (x.StartsWithIgnoreCase("Unsynced"))
                                        {
                                            tag = ActionTag.Unsynced;
                                            x = x.Remove(0, 8);
                                        }
                                        if (x.EqualsIgnoreCase("<-- Comment -->"))
                                            tag = ActionTag.Comment;
                                        if (x.EqualsIgnoreCase("Revival"))
                                            tag = ActionTag.Revival;
                                        if (x.EqualsIgnoreCase("TreasureCoffer"))
                                            tag = ActionTag.Treasure;
                                        var action = x.Split('|');
                                        var pathAction = new PathAction { Tag = tag, Name = action[0] };
                                        if (action.Length > 1)
                                        {
                                            pathAction.Position = action[1].TryGetVector3(out var vector3) ? vector3 : Vector3.Zero;
                                            if (action.Length == 3)
                                            {
                                                var argument = string.Empty;
                                                var note = string.Empty;
                                                if (action[2].Contains(" (") && action[2].Contains(')'))
                                                {
                                                    var argumentArray = action[2].Split(" (");

                                                    if (int.TryParse(argumentArray[0], out _))
                                                    {
                                                        argument = argumentArray[0];
                                                        note = action[2].Replace($"{argument} (", string.Empty).Replace(")", string.Empty);
                                                    }
                                                    else
                                                        argument = action[2];
                                                }
                                                else
                                                    argument = action[2];

                                                pathAction.Arguments = [argument];
                                                pathAction.Note = note;
                                            }
                                        }
                                        else
                                        {
                                            pathAction.Name = "<-- Comment -->";
                                            pathAction.Note = action[0];
                                        }
                                        pathActions.Add(pathAction);
                                    });
                                    json = json.Replace("\"actionsString\": [],", string.Empty);
                                    json = Regex.Replace(json, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
                                    pathFile = JsonSerializer.Deserialize<PathFile>(json);
                                    if (pathFile == null) return new();
                                    pathFile.Actions = [.. pathActions];
                                    pathFile.Meta.Changelog.Add(new() { Change = "Converted to JSON Structure with Tags", Version = Plugin.Configuration.Version });
                                    string jsonNew = JsonSerializer.Serialize(PathFile, BuildTab.jsonSerializerOptions);
                                    File.WriteAllText(FilePath, jsonNew);
                                }
                            }
                            else if (json.Contains("\"argument\"") && !json.Contains("\"arguments\""))
                            {
                                var doc = JsonDocument.Parse(json);
                                var element = doc.RootElement.GetProperty("actions");
                                List<string> arguments = [];
                                element.EnumerateArray().Each(x => arguments.Add(x.GetProperty("argument").Deserialize<string>() ?? string.Empty));

                                pathFile = JsonSerializer.Deserialize<PathFile>(json, BuildTab.jsonSerializerOptions);
                                pathFile?.Actions.Select((Value, Index) => (Value, Index)).Each(x => x.Value.Arguments = [arguments[x.Index]]);
                                pathFile?.Actions.Each(x =>
                                {
                                    var tag = ActionTag.None;

                                    if (x.Name.StartsWithIgnoreCase("Synced"))
                                    {
                                        tag = ActionTag.Synced;
                                        x.Name = x.Name.Remove(0, 6);
                                    }
                                    if (x.Name.StartsWithIgnoreCase("Unsynced"))
                                    {
                                        tag = ActionTag.Unsynced;
                                        x.Name = x.Name.Remove(0, 8);
                                    }
                                    if (x.Name.EqualsIgnoreCase("<-- Comment -->"))
                                        tag = ActionTag.Comment;
                                    if (x.Name.EqualsIgnoreCase("Revival"))
                                        tag = ActionTag.Revival;
                                    if (x.Name.EqualsIgnoreCase("TreasureCoffer"))
                                        tag = ActionTag.Treasure;
                                    x.Tag = tag;
                                });
                                pathFile?.Meta.Changelog.Add(new() { Change = "Converted to JSON Structure with Tags", Version = Plugin.Configuration.Version });
                                string jsonNew = JsonSerializer.Serialize(PathFile, BuildTab.jsonSerializerOptions);
                                File.WriteAllText(FilePath, jsonNew);
                            }
                            else if (!json.Contains("\"tag\""))
                            {

                                pathFile = JsonSerializer.Deserialize<PathFile>(json, BuildTab.jsonSerializerOptions);
                                pathFile?.Actions.Each(x =>
                                {
                                    if (x.Name.StartsWithIgnoreCase("Synced"))
                                    {
                                        x.Tag = ActionTag.Synced;
                                        x.Name = x.Name.Remove(0, 6);
                                    }
                                    if (x.Name.StartsWithIgnoreCase("Unsynced"))
                                    {
                                        x.Tag = ActionTag.Unsynced;
                                        x.Name = x.Name.Remove(0, 8);
                                    }
                                    if (x.Name.EqualsIgnoreCase("<-- Comment -->"))
                                        x.Tag = ActionTag.Comment;
                                    if (x.Name.EqualsIgnoreCase("Revival"))
                                        x.Tag = ActionTag.Revival;
                                    if (x.Name.EqualsIgnoreCase("TreasureCoffer"))
                                        x.Tag = ActionTag.Treasure;
                                });
                                pathFile?.Meta.Changelog.Add(new() { Change = "Converted to JSON Structure with Tags", Version = 164 });
                                string jsonNew = JsonSerializer.Serialize(PathFile, BuildTab.jsonSerializerOptions);
                                File.WriteAllText(FilePath, jsonNew);
                                Svc.Log.Info($"{FilePath}");
                            }
                            else
                            {
                                pathFile = JsonSerializer.Deserialize<PathFile>(json, BuildTab.jsonSerializerOptions);
                            }

                            RevivalFound = PathFile.Actions.Any(x => x.Tag == ActionTag.Revival);
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
