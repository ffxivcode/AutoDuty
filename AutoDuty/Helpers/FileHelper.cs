using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using static AutoDuty.AutoDuty;

namespace AutoDuty.Helpers
{
    using Windows;
    using Managers;

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
                var files = Plugin.AssemblyDirectoryInfo.EnumerateFiles("*.json", SearchOption.TopDirectoryOnly).Where(s => s.Name.StartsWith('('));

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

        internal static void Init()
        {
            FileSystemWatcher.Changed += OnChanged;
            FileSystemWatcher.Created += OnCreated;
            FileSystemWatcher.Deleted += OnDeleted;
            FileSystemWatcher.Renamed += OnRenamed;
            FileSystemWatcher.EnableRaisingEvents = true;

            Update();
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
