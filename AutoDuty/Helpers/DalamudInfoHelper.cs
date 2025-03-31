using ECommons.DalamudServices;
using ECommons.Reflection;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace AutoDuty.Helpers
{
    using Dalamud.Common;
    using Newtonsoft.Json;

    internal static class DalamudInfoHelper
    {
        private static bool stagingChecked = false;
        private static bool isStaging      = false;

        public static bool IsOnStaging()
        {
            if(Plugin.isDev)
                return false;

            if (stagingChecked) 
                return isStaging;

            if (DalamudReflector.TryGetDalamudStartInfo(out DalamudStartInfo? startinfo, Svc.PluginInterface))
            {
                try
                {
                    HttpClient         client         = new();
                    const string       dalDeclarative = "https://raw.githubusercontent.com/goatcorp/dalamud-declarative/refs/heads/main/config.yaml";
                    using Stream       stream         = client.GetStreamAsync(dalDeclarative).Result;
                    using StreamReader reader         = new(stream);

                    for (int i = 0; i <= 4; i++)
                    {
                        string line = reader.ReadLine().Trim();
                        if (i != 4) continue;
                        string version = line.Split(":").Last().Trim().Replace("'", "");
                        if (version != startinfo.GameVersion.ToString())
                        {
                            stagingChecked = true;
                            isStaging      = false;
                            return false;
                        }
                    }
                }
                catch
                {
                    // Something has gone wrong with checking the Dalamud github file, just allow plugin load anyway
                    stagingChecked = true;
                    isStaging = false;
                    return false;
                }

                if (File.Exists(startinfo.ConfigurationPath))
                {
                    try
                    {
                        string file = File.ReadAllText(startinfo.ConfigurationPath);
                        var ob = JsonConvert.DeserializeObject<dynamic>(file);
                        string type = ob.DalamudBetaKind;
                        if (type is not null && !string.IsNullOrEmpty(type) && type != "release")
                        {
                            stagingChecked = true;
                            isStaging = true;
                            return true;
                        }
                        else
                        {
                            stagingChecked = true;
                            isStaging = false;
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Svc.Chat.PrintError($"Unable to determine Dalamud staging due to file being config being unreadable.");
                        Svc.Log.Error(ex.ToString());
                        stagingChecked = true;
                        isStaging = false;
                        return false;
                    }
                }
                else
                {
                    stagingChecked = true;
                    isStaging = false;
                    return false;
                }
            }
            return false;
        }
    }
}
