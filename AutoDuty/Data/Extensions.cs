namespace AutoDuty.Data
{
    public static class Extensions
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
}
