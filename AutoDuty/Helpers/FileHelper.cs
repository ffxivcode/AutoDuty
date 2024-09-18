using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AutoDuty.Helpers
{
    using Windows;
    using Managers;
    using ECommons;
    using Serilog.Events;

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

                                                                           Filter                = "*.json",
                                                                           IncludeSubdirectories = true
                                                                       };

        internal static readonly FileSystemWatcher FileWatcher = new();

        public static byte[] CalculateMD5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filename);
            return md5.ComputeHash(stream);
        }

        internal static void OnStart()
        {
            //Move all the paths to the Paths folder on first install or update
            if (Plugin.AssemblyDirectoryInfo == null)
                return;
            try
            {
                int i = 0;
                var files = Plugin.AssemblyDirectoryInfo.EnumerateFiles("*.json", SearchOption.AllDirectories).Where(s => s.Name.StartsWith('('));

                foreach (var file in files)
                {
                    if (!Plugin.Configuration.DoNotUpdatePathFiles.Contains(file.Name) && 
                        (!File.Exists($"{Plugin.PathsDirectory.FullName}/{file.Name}") || 
                         !BitConverter.ToString(CalculateMD5(file.FullName)).Replace("-", "").Equals(BitConverter.ToString(CalculateMD5($"{Plugin.PathsDirectory.FullName}/{file.Name}")).Replace("-", ""), 
                                                                                                     StringComparison.InvariantCultureIgnoreCase)))
                    {
                        file.MoveTo($"{Plugin.PathsDirectory.FullName}/{file.Name}", true);
                        Svc.Log.Info($"Moved: {file.Name}");
                        i++;
                    }
                }
                Svc.Log.Info($"Moved: {i} Paths to the Paths Folder: {Plugin.PathsDirectory.FullName}");
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"Error copying paths from {Plugin.AssemblyDirectoryInfo.FullName} to {Plugin.PathsDirectory.FullName}\n{ex}");
            }
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

                    ContentPathsManager.DictionaryPaths[content.TerritoryType].Paths.Add(new ContentPathsManager.DutyPath(file.FullName));
                }
            }
        }

        private static void OnChanged(object sender, FileSystemEventArgs e) => Update();

        private static void OnCreated(object sender, FileSystemEventArgs e) => Update();

        private static void OnDeleted(object sender, FileSystemEventArgs e) => Update();

        private static void OnRenamed(object sender, RenamedEventArgs e) => Update();
    }
}
