using ECommons.DalamudServices;
using System.Collections.Generic;
using System.IO;
using static AutoDuty.AutoDuty;

namespace AutoDuty.Helpers
{
    internal static class FileHelper
    {
        internal static Dictionary<uint, bool> PathFileExists = [];
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
            IncludeSubdirectories = false
        };

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
            PathFileExists = [];
            foreach (var t in ContentHelper.ListContent)
            {
                PathFileExists.TryAdd(t.TerritoryType, File.Exists($"{Plugin.PathsDirectory.FullName}/{t.TerritoryType}.json"));
            }
        }

        private static void OnChanged(object sender, FileSystemEventArgs e) => Update();

        private static void OnCreated(object sender, FileSystemEventArgs e) => Update();

        private static void OnDeleted(object sender, FileSystemEventArgs e) => Update();

        private static void OnRenamed(object sender, RenamedEventArgs e) => Update();
    }
}
