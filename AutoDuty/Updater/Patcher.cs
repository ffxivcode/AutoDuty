using ECommons.DalamudServices;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

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
                var xml = await S3.ListObjectsAsync($"https://autoduty.s3.us-west-2.amazonaws.com");
                var xmlDoc = XDocument.Parse(xml);
                XNamespace ns = "http://s3.amazonaws.com/doc/2006-03-01/";
                foreach (var file in xmlDoc.Descendants(ns + "Contents"))
                {
                    var key = file.Element(ns + "Key")?.Value;
                    var etag = file.Element(ns + "ETag")?.Value?.Trim('"');
                    if (key != null && etag != null)
                    {
                        if ((!localFilesDictionary.TryGetValue(key, out string? value) || !value.Equals(etag, StringComparison.OrdinalIgnoreCase) || skipMD5) && !Plugin.Configuration.DoNotUpdatePathFiles.Contains(key))
                        {
                            var result = await S3.DownloadFileAsync($"https://autoduty.s3.us-west-2.amazonaws.com/{key}",$"{Plugin.PathsDirectory.FullName}/{key}");
                            var logger = result ? $"Succesfully downloaded: {key}" : $"Failed to download: {key}";
                            Svc.Log.Info(logger);
                        }
                    }
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
