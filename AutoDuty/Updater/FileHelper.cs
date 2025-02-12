using AutoDuty.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ECommons;
using Serilog.Events;
using AutoDuty.Windows;

namespace AutoDuty.Updater
{
    using static Data.Classes;
    internal static class FileHelper
    {
        internal static readonly FileSystemWatcher FileSystemWatcher = new(Plugin.PathsDirectory.FullName)
        {
            NotifyFilter = NotifyFilters.Attributes
                                                                                        | NotifyFilters.CreationTime
                                                                                        | NotifyFilters.DirectoryName
                                                                                        | NotifyFilters.FileName
                                                                                        | NotifyFilters.LastAccess
                                                                                        | NotifyFilters.LastWrite
                                                                                        | NotifyFilters.Security
                                                                                        | NotifyFilters.Size,

            Filter = "*.json",
            IncludeSubdirectories = true
        };

        internal static readonly FileSystemWatcher FileWatcher = new();

        private static readonly object _updateLock = new();


        public static byte[] CalculateMD5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filename);
            return md5.ComputeHash(stream);
        }

        internal static void LogInit()
        {
            var path = $"{Plugin.DalamudDirectory}/dalamud.log";
            if (!File.Exists(path)) return;
            var file = new FileInfo(path);
            if (file == null) return;
            var directory = file.DirectoryName;
            var filename = file.Name;
            if (directory.IsNullOrEmpty() || filename.IsNullOrEmpty()) return;
            var lastMaxOffset = file.Length;

            FileWatcher.Path = directory!;
            FileWatcher.Filter = filename;
            FileWatcher.NotifyFilter = NotifyFilters.LastWrite;

            FileWatcher.Changed += (sender, e) =>
            {
                using FileStream fs = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(lastMaxOffset, SeekOrigin.Begin);
                using StreamReader sr = new(fs);
                var x = string.Empty;
                while ((x = sr.ReadLine()) != null)
                {
                    if (!x.Contains("[AutoDuty]")) continue;

                    var logEntry = new LogMessage() { Message = x };

                    if (x.Contains("[FTL]"))
                        logEntry.LogEventLevel = LogEventLevel.Fatal;
                    else if (x.Contains("[ERR]"))
                        logEntry.LogEventLevel = LogEventLevel.Error;
                    else if (x.Contains("[WRN]"))
                        logEntry.LogEventLevel = LogEventLevel.Warning;
                    else if (x.Contains("[INF]"))
                        logEntry.LogEventLevel = LogEventLevel.Information;
                    else if (x.Contains("[DBG]"))
                        logEntry.LogEventLevel = LogEventLevel.Debug;
                    else if (x.Contains("[VRB]"))
                        logEntry.LogEventLevel = LogEventLevel.Verbose;
                    LogTab.Add(logEntry);
                }
                lastMaxOffset = fs.Position;
            };
            FileWatcher.EnableRaisingEvents = true;
        }

        internal static void Init()
        {
            FileSystemWatcher.Changed += OnChanged;
            FileSystemWatcher.Created += OnCreated;
            FileSystemWatcher.Deleted += OnDeleted;
            FileSystemWatcher.Renamed += OnRenamed;
            FileSystemWatcher.EnableRaisingEvents = true;
            Update();
            LogInit();
        }

        private static void Update()
        {
            lock (_updateLock)
            {
                ContentPathsManager.DictionaryPaths = [];

                MainTab.PathsUpdated();
                PathsTab.PathsUpdated();

                foreach ((uint _, Content? content) in ContentHelper.DictionaryContent)
                {
                    IEnumerable<FileInfo> files = Plugin.PathsDirectory.EnumerateFiles($"({content.TerritoryType})*.json", SearchOption.AllDirectories);

                    foreach (FileInfo file in files)
                    {
                        if (!ContentPathsManager.DictionaryPaths.ContainsKey(content.TerritoryType))
                            ContentPathsManager.DictionaryPaths.Add(content.TerritoryType, new ContentPathsManager.ContentPathContainer(content));

                        ContentPathsManager.DictionaryPaths[content.TerritoryType].AddPath(file.FullName);
                    }
                }
            }
        }

        private static void OnChanged(object sender, FileSystemEventArgs e) => Update();

        private static void OnCreated(object sender, FileSystemEventArgs e) => Update();

        private static void OnDeleted(object sender, FileSystemEventArgs e) => Update();

        private static void OnRenamed(object sender, RenamedEventArgs e) => Update();
    }
}
