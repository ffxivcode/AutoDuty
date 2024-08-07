namespace AutoDuty.Managers
{
    public class TrustMember
    {
        public uint Index { get; set; }
        public uint Role { get; set; } // 0 = DPS, 1 = Healer, 2 = Tank, 3 = G'raha All Rounder
        public string Name { get; set; }
    }
}
