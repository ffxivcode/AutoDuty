using System;

namespace AutoDuty.Data
{
    public class Enum
    {
        public static string EnumString(System.Enum T) => T?.ToString()?.Replace("_", " ") ?? "";

        public enum LootMethod : int
        {
            AutoDuty = 0,
            RotationSolver = 1,
            Pandora = 2,
            All = 3
        }
        public enum Housing : int
        {
            Apartment = 1,
            Personal_Home = 2,
            FC_Estate = 3,
        }
        public enum RetireLocation : int
        {
            Inn = 0,
            Apartment = 1,
            Personal_Home = 2,
            FC_Estate = 3,
            GC_Barracks = 4,
        }
        public enum TerminationMode : int
        {
            Do_Nothing = 0,
            Logout = 1,
            Start_AR_Multi_Mode = 2,
            Kill_Client = 3,
            Kill_PC = 4
        }
        public enum Role : int
        {
            Tank = 0,
            Healer = 1,
            Ranged_DPS = 2,
            Melee_DPS = 3
        }
        public enum Positional : int
        {
            Any = 0,
            Flank = 1,
            Rear = 2,
            Front = 3
        }
        public enum SummoningBellLocations : uint
        {
            Inn = 0,
            Apartment = 1,
            Personal_Home = 2,
            FC_Estate = 3,
            Limsa_Lominsa_Lower_Decks = 129,
            Old_Gridania = 133,
            Uldah_Steps_of_Thal = 131,
            The_Pillars = 419,
            Rhalgrs_Reach = 635,
            Kugane = 628,
            The_Doman_Enclave = 759,
            The_Crystarium = 819,
            Eulmore = 820,
            Old_Sharlayan = 962,
            Radz_at_Han = 963,
            Tuliyollal = 1185,
            Nexus_Arcade = 1186
        }

        public enum DutyState : int
        {
            None = 0,
            DutyStarted = 1,
            DutyWiped = 2,
            DutyRecommenced = 3,
            DutyComplete = 4,
        }

        public enum Stage : int
        {
            Stopped = 0,
            Reading_Path = 1,
            Action = 2,
            Looping = 3,
            Condition = 4,
            Moving = 5,
            Waiting_For_Combat = 6,
            Paused = 7,
            Dead = 8,
            Revived = 9,
            Looting_Treasure = 10
        }

        [Flags]
        public enum State : int
        {
            None = 0,
            Looping = 1,
            Navigating = 2,
            Other = 4
        }

        [Flags]
        public enum SettingsActive : int
        {
            None = 0,
            Vnav_Align_Camera_Off = 1,
            Pandora_Interact_Objects = 2,
            YesAlready = 4
        }

        public enum Sounds : byte
        {
            None = 0x00,
            Unknown = 0x01,
            Sound01 = 0x25,
            Sound02 = 0x26,
            Sound03 = 0x27,
            Sound04 = 0x28,
            Sound05 = 0x29,
            Sound06 = 0x2A,
            Sound07 = 0x2B,
            Sound08 = 0x2C,
            Sound09 = 0x2D,
            Sound10 = 0x2E,
            Sound11 = 0x2F,
            Sound12 = 0x30,
            Sound13 = 0x31,
            Sound14 = 0x32,
            Sound15 = 0x33,
            Sound16 = 0x34,
        }
    }
}

public static class SoundsExtensions
{
    public static string ToName(this Sounds value)
    {
        return value switch
        {
            Sounds.None => "None",
            Sounds.Sound01 => "Sound Effect 1",
            Sounds.Sound02 => "Sound Effect 2",
            Sounds.Sound03 => "Sound Effect 3",
            Sounds.Sound04 => "Sound Effect 4",
            Sounds.Sound05 => "Sound Effect 5",
            Sounds.Sound06 => "Sound Effect 6",
            Sounds.Sound07 => "Sound Effect 7",
            Sounds.Sound08 => "Sound Effect 8",
            Sounds.Sound09 => "Sound Effect 9",
            Sounds.Sound10 => "Sound Effect 10",
            Sounds.Sound11 => "Sound Effect 11",
            Sounds.Sound12 => "Sound Effect 12",
            Sounds.Sound13 => "Sound Effect 13",
            Sounds.Sound14 => "Sound Effect 14",
            Sounds.Sound15 => "Sound Effect 15",
            Sounds.Sound16 => "Sound Effect 16",
            _ => "Unknown",
        };
    }
}
