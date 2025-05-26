using AutoDuty.Windows;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Networking.Http;

namespace AutoDuty.Updater
{
    internal static class GitHubHelper
    {
        const string CLIENT_ID = "Iv23liWV5R21nasKaQjP";

        private static readonly SocketsHttpHandler _handler = new() { AutomaticDecompression = DecompressionMethods.All, ConnectCallback = new HappyEyeballsCallback().ConnectCallback };

        private static readonly HttpClient _client = new(_handler) { Timeout = TimeSpan.FromSeconds(20) };

        internal static async Task<bool> DownloadFileAsync(string url, string localPath)
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(localPath, content);
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"{ex}");
                return false;
            }
        }

        internal static async Task<Dictionary<string, string>?> GetPathFileListAsync()
        {
            try
            {
                using HttpClient client = new(_handler) { Timeout = TimeSpan.FromSeconds(20) };
                var md5List = await client.GetFromJsonAsync<Dictionary<string, string>>("https://raw.githubusercontent.com/ffxivcode/AutoDuty/refs/heads/master/AutoDuty/Resources/md5s.json");
                return md5List ?? [];
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"{ex}");
                return null;
            }
        }

        internal static async Task<UserCode?> GetUserCode()
        {
            try
            {
                var uri = new Uri("https://github.com/login/device/code");
                var parameters = new FormUrlEncodedContent([new KeyValuePair<string, string>("client_id", CLIENT_ID)]);
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await _client.PostAsync(uri, parameters);
                var jsonString = await response.Content.ReadAsStringAsync();
                _client.Dispose();
                return JsonSerializer.Deserialize<UserCode>(jsonString, BuildTab.jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"{ex}");
                return null;
            }
        }

        internal static async Task<PollResponseClass?> PollResponse(UserCode userCode)
        {
            try
            {
                var uri = new Uri("https://github.com/login/oauth/access_token");
                var parameters = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("client_id", CLIENT_ID),
                    new KeyValuePair<string, string>("device_code", userCode.Device_Code),
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                ]);
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await _client.PostAsync(uri, parameters);
                var jsonString = await response.Content.ReadAsStringAsync();
                _client.Dispose();
                return JsonSerializer.Deserialize<PollResponseClass>(jsonString, BuildTab.jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"{ex}");
                return null;
            }
        }

        internal static async Task<string?> FileIssue(string title, string whatHappened, string reproSteps, string accessToken)
        {
            try
            {
                var body = $"What Happened?\n\n{whatHappened}\n\nVersion Number\n\n{GitHubIssue.Version}\n\nSteps to reproduce the error\n\n{reproSteps}\n\nRelevant log output\n\n{GitHubIssue.LogFile}\n\nOther relevant plugins installed\n\n{GitHubIssue.InstalledPlugins}\n\nConfig file\n\n{GitHubIssue.ConfigFile}";

                var issue = new GitHubIssue()
                {
                    Title = title,
                    Body = body
                };

                var json = JsonSerializer.Serialize(issue, BuildTab.jsonSerializerOptions);
                Svc.Log.Info(json);
                _client.DefaultRequestHeaders.Add("User-Agent", "AutoDuty");
                _client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                _client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                var content = new StringContent(json, Encoding.UTF8, "application/vnd.github+json");

                var url = $"https://api.github.com/repos/ffxivcode/AutoDuty/issues";
                var response = await _client.PostAsync(url, content);

                var responseString = await response.Content.ReadAsStringAsync();
                return responseString;
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"{ex}");
                return null;
            }
        }

        internal static void Dispose() => _client.Dispose();
    }
}
