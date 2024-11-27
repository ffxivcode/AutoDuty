using ECommons.DalamudServices;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AutoDuty.Updater
{
    public class Patcher
    {
        internal static ActionState PatcherState => PatcherTask != null && !PatcherTask.IsCompleted && !PatcherTask.IsCanceled && !PatcherTask.IsFaulted ? ActionState.Running : ActionState.None;
            
        internal static Task<bool>? PatcherTask = null;

        internal static void Patch(bool skipMD5 = false)
        {
            if (PatcherTask == null)
            {
                PatcherTask = Task.Run(() => PatchTask(skipMD5));
                PatcherTask.ContinueWith(t => {
                    OnPatcherTaskCompleted(t.IsCompletedSuccessfully);
                });
            }
        }

        private static void OnPatcherTaskCompleted(bool success)
        {
            if (PatcherTask != null && success && PatcherTask.Result)
                Svc.Log.Info("Patching Complete");
            PatcherTask = null;
        }

        public static async Task<bool> PatchTask(bool skipMD5)
        {
            Svc.Log.Info("Patching Started");
            try
            {
                var localFileInfos = Plugin.PathsDirectory.EnumerateFiles("*.json", SearchOption.AllDirectories);
                var localFilesDictionary = localFileInfos.ToDictionary(
                    fileInfo => fileInfo.Name,
                    fileInfo => BitConverter.ToString(FileHelper.CalculateMD5(fileInfo.FullName)).Replace("-", "")
                );
                var list = await GitHubHelper.GetPathFileListAsync();
                if (list == null) return false;

                var downloadList = list.Where(kvp => !localFilesDictionary.ContainsKey(kvp.Key) || !localFilesDictionary[kvp.Key].Equals(kvp.Value, StringComparison.OrdinalIgnoreCase));

                foreach (var file in downloadList)
                {
                    var result = await GitHubHelper.DownloadFileAsync($"https://raw.githubusercontent.com/ffxivcode/AutoDuty/refs/heads/master/AutoDuty/Paths/{file.Key}",$"{Plugin.PathsDirectory.FullName}/{file.Key}");
                    var logger = result ? $"Succesfully downloaded: {file.Key}" : $"Failed to download: {file.Key}";
                    Svc.Log.Info(logger);
                }
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"error patching path files: {ex}");
                return false;
            }
        }
    }
}
