using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using Serilog.Events;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;

namespace AutoDuty.Data
{
    public class Classes
    {
        public class LogMessage
        {
            public string Message { get; set; } = string.Empty;
            public LogEventLevel LogEventLevel { get; set; }
        }

        public class Message
        {
            public string Sender { get; set; } = string.Empty;
            public List<PathAction> Action { get; set; } = [];
        }

        public class Content
        {
            public uint Id { get; set; }
            public string? Name { get; set; }
            public string? EnglishName { get; set; }
            public uint TerritoryType { get; set; }
            public uint ExVersion { get; set; }
            public byte ClassJobLevelRequired { get; set; }
            public uint ItemLevelRequired { get; set; }
            public int DawnIndex { get; set; } = -1;
            public uint ContentFinderCondition { get; set; }
            public uint ContentType { get; set; }
            public uint ContentMemberType { get; set; }
            public int TrustIndex { get; set; } = -1;
            public bool VariantContent { get; set; } = false;
            public int VVDIndex { get; set; } = -1;
            public bool GCArmyContent { get; set; } = false;
            public int GCArmyIndex { get; set; } = -1;
            public List<TrustMember> TrustMembers { get; set; } = [];
            public DutyMode DutyModes { get; set; } = DutyMode.None;
            public uint UnlockQuest { get; init; }
        }

        public class TrustMember
        {
            public uint Index { get; set; }
            public TrustRole Role { get; set; } // 0 = DPS, 1 = Healer, 2 = Tank, 3 = G'raha All Rounder
            public Lumina.Excel.GeneratedSheets.ClassJob? Job { get; set; } = null;//closest actual job that applies. G'raha gets Blackmage
            public string Name { get; set; } = string.Empty;
            public TrustMemberName MemberName { get; set; }

            public uint Level { get; set; }
            public uint LevelCap { get; set; }
            public uint LevelInit { get; set; }
            public bool LevelIsSet { get; set; }
            public uint UnlockQuest { get; init; }

            public bool Available => this.UnlockQuest <= 0 || QuestManager.IsQuestComplete(this.UnlockQuest);

            public void ResetLevel()
            {
                Level      = LevelInit;
                LevelIsSet = LevelInit == LevelCap;
            }

            public void SetLevel(uint level)
            {
                if (level >= LevelInit-1)
                {
                    LevelIsSet = true;
                    Level = level;
                }
            }
        }

        public class PathFile
        {
            [JsonPropertyName("actions")]
            public List<PathAction> Actions { get; set; } = [];

            [JsonPropertyName("meta")]
            public PathFileMetaData Meta { get; set; } = new()
            {
                CreatedAt = Plugin.Configuration.Version,
                Changelog = [],
                Notes = []
            };
        }

        public class PathAction
        {
            [JsonPropertyName("tag")]
            public ActionTag Tag { get; set; } = ActionTag.None;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("position")]
            public Vector3 Position { get; set; } = Vector3.Zero;

            [JsonPropertyName("arguments")]
            public List<string> Arguments { get; set; } = [];

            [JsonPropertyName("note")]
            public string Note { get; set; } = string.Empty;
        }

        public class PathFileMetaData
        {
            [JsonPropertyName("createdAt")]
            public int CreatedAt { get; set; }

            [JsonPropertyName("changelog")]
            public List<PathFileChangelogEntry> Changelog { get; set; } = [];

            public int LastUpdatedVersion => Changelog.Count > 0 ? Changelog.Last().Version : CreatedAt;

            [JsonPropertyName("notes")]
            public List<string> Notes { get; set; } = [];
        }

        public class PathFileChangelogEntry
        {
            [JsonPropertyName("version")]
            public int Version { get; set; }

            [JsonPropertyName("change")]
            public string Change { get; set; } = string.Empty;
        }

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
                using FileStream fs = new(Plugin.ConfigFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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
    }
}
