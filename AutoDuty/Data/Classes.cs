using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;

namespace AutoDuty.Data
{
    public class Classes
    {
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

            public void ResetLevel()
            {
                LevelIsSet = false;
                Level = LevelInit;
            }

            public void SetLevel(uint level)
            {
                if (level >= LevelInit)
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

            [JsonPropertyName("interactables")]
            public uint[] Interactables { get; set; } = [];

            [JsonPropertyName("meta")]
            public PathFileMetaData Meta { get; set; } = new()
            {
                CreatedAt = AutoDuty.Plugin.Configuration.Version,
                Changelog = [],
                Notes = []
            };
        }

        public class PathAction
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("position")]
            public Vector3 Position { get; set; } = Vector3.Zero;

            [JsonPropertyName("argument")]
            public string Argument { get; set; } = string.Empty;

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
    }
}
