namespace AutoDuty.Managers
{
    public class TrustMember
    {
        public uint Index { get; set; }
        public TrustRole Role { get; set; } // 0 = DPS, 1 = Healer, 2 = Tank, 3 = G'raha All Rounder
        public string Name { get; set; }

        public uint Level { get; set; }
    }

    public enum TrustMemberName : byte
    {
        AlisaieBlue = 1,
        Alisaie = 2,
        Thancred = 3,
        Urianger = 5,
        BestCatGirl = 6,
        Ryne = 7,
        Estinien = 12,
        Graha = 10,
        Zero = 41,
        Krile = 60
    }

    public enum TrustRole : byte
    {
        DPS = 0,
        Healer = 1,
        Tank = 2,
        Graha = 3
    }
}
