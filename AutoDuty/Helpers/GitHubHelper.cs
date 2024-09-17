using AutoDuty.Windows;
using ECommons;
using ECommons.DalamudServices;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    internal class GitHubHelper
    {
        const string APP_NAME = "autoduty-create-issue";
        const string CLIENT_ID = "Iv23liWV5R21nasKaQjP";
        const string CLIENT_SECRET = "c25b34bd89f0add109cfc5bf1194f76b86522631";

        public class PollResponseClass
        {
            [JsonPropertyName("interval")]
            public int Interval { get; set; } = -1;

            [JsonPropertyName("error")]
            public string Error { get; set; } = string.Empty;

            [JsonPropertyName("error_description")]
            public string Error_Description { get; set; } = string.Empty;

            [JsonPropertyName("error_uri")]
            public string Error_Uri { get; set; } = string.Empty;

            [JsonPropertyName("access_token")]
            public string Access_Token = string.Empty;

            [JsonPropertyName("expires_in")]
            public int Expires_In { get; set; } = 0;

            [JsonPropertyName("refresh_token")]
            public string Refresh_Token = string.Empty;

            [JsonPropertyName("refresh_token_expires_in")]
            public int Refresh_Token_Expires_In = 0;

            [JsonPropertyName("token_type")]
            public string Token_Type = string.Empty;

            [JsonPropertyName("scope")]
            public string Scope = string.Empty;
        }

        public class UserCode
        {
            [JsonPropertyName("device_code")]
            public string Device_Code { get; set; } = string.Empty;

            [JsonPropertyName("expires_in")]
            public int Expires_In { get; set; } = 0;

            [JsonPropertyName("user_code")]
            public string User_Code { get; set; } = string.Empty;

            [JsonPropertyName("verification_uri")]
            public string Verification_Uri { get; set; } = string.Empty;

            [JsonPropertyName("interval")]
            public int Interval { get; set; } = 500;
        }

        public class GitHubIssue
        {
            [JsonPropertyName("title")]
            public string Title { get; set; } = "[Bug] ";

            [JsonPropertyName("body")]
            public string Body { get; set; } = string.Empty;

            [JsonPropertyName("labels")]
            public List<string> Labels = ["bug", "unconfirmed"];

            public static string Version => $"{Plugin.Configuration.Version}";

            public static string LogFile => Plugin.DalamudLogEntries.SelectMulti(x => x.Message).ToList().ToCustomString("\n");

            public static string InstalledPlugins => PluginInterface.InstalledPlugins.Select(x => $"{x.InternalName}, Version= {x.Version}").ToList().ToCustomString("\n");

            public static string ConfigFile => ReadConfigFile().ToCustomString("\n");

            private static List<string> ReadConfigFile()
            {
                using FileStream fs = new(Plugin.ConfigFile.FullName, System.IO.FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader sr = new(fs);
                string? x;
                List<string> strings = [];
                while ((x = sr.ReadLine()) != null)
                {
                    strings.Add(x);
                }
                return strings;
            }
        }

        private static HttpClient? client;

        internal static async Task<UserCode?> GetUserCode()
        {
            try
            {
                client = new();
                var uri = new Uri("https://github.com/login/device/code");
                var parameters = new FormUrlEncodedContent([ new KeyValuePair<string, string>("client_id", CLIENT_ID) ]);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PostAsync(uri, parameters);
                var jsonString = await response.Content.ReadAsStringAsync();
                client.Dispose();
                return JsonSerializer.Deserialize<UserCode>(jsonString, BuildTab.jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"{ex}");
                client?.Dispose();
                return null;
            }
        }

        internal static async Task<PollResponseClass?> PollResponse(UserCode userCode)
        {
            try
            {
                client = new();
                var uri = new Uri("https://github.com/login/oauth/access_token");
                var parameters = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("client_id", CLIENT_ID),
                    new KeyValuePair<string, string>("device_code", userCode.Device_Code),
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                ]);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PostAsync(uri, parameters);
                var jsonString = await response.Content.ReadAsStringAsync();
                client.Dispose();
                return JsonSerializer.Deserialize<PollResponseClass>(jsonString, BuildTab.jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"{ex}");
                client?.Dispose();
                return null;
            }
        }

        internal static async Task<string?> FileIssue(string title, string whatHappened, string reproSteps, string accessToken)
        {
            try 
            {
                client = new();
                var body = $"What Happened?\n\n{whatHappened}\n\nVersion Number\n\n{GitHubIssue.Version}\n\nSteps to reproduce the error\n\n{reproSteps}\n\nRelevant log output\n\n{GitHubIssue.LogFile}\n\nOther relevant plugins installed\n\n{GitHubIssue.InstalledPlugins}\n\nConfig file\n\n{GitHubIssue.ConfigFile}";

                var issue = new GitHubIssue() 
                {
                    Title = title,
                    Body = body
                };

                var json = JsonSerializer.Serialize(issue, BuildTab.jsonSerializerOptions);
                Svc.Log.Info(json);
                client.DefaultRequestHeaders.Add("User-Agent", "AutoDuty");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                var content = new StringContent(json, Encoding.UTF8, "application/vnd.github+json");

                var url = $"https://api.github.com/repos/ffxivcode/AutoDuty/issues";
                var response = await client.PostAsync(url, content);

                var responseString = await response.Content.ReadAsStringAsync();
                Svc.Log.Info(responseString);
                client.Dispose();
                return responseString;
            }
            catch (Exception ex)
            {
                Svc.Log.Error($"{ex}");
                client?.Dispose();
                return null;
            }
        }

    }
}
