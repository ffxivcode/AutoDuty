namespace AutoDuty.Managers
{
    using ECommons;
    using Lumina.Excel.GeneratedSheets;
    using ClassJob = Lumina.Excel.GeneratedSheets2.ClassJob;

    public class TrustMember
    {
        public uint                                  Index      { get; set; }
        public TrustRole                             Role       { get; set; } // 0 = DPS, 1 = Healer, 2 = Tank, 3 = G'raha All Rounder
        public Lumina.Excel.GeneratedSheets.ClassJob Job        { get; set; } //closest actual job that applies. G'raha gets Blackmage
        public string                                Name       { get; set; }
        public TrustMemberName                       MemberName { get; set; }

        public uint Level    { get; set; }
        public uint LevelCap { get; set; }
    }

    public enum TrustMemberName : byte
    {
        Alphinaud = 1,
        Alisaie   = 2,
        Thancred  = 3,
        Urianger  = 5,
        Yshtola   = 6,
        Ryne      = 7,
        Estinien  = 12,
        Graha     = 10,
        Zero      = 41,
        Krile     = 60
    }

    public enum TrustRole : byte
    {
        DPS        = 0,
        Healer     = 1,
        Tank       = 2,
        AllRounder = 3
    }
}