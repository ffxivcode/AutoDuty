using FFXIVClientStructs.FFXIV.Client.UI;

namespace AutoDuty.Helpers
{
    public static class SoundHelper
    {
        public static bool StartSound(bool PlayEndSound, bool CustomSound, Sounds SoundEnum = Sounds.None)
        {
            if (!PlayEndSound)
                return false;
            UIGlobals.PlaySoundEffect((uint)SoundEnum);
            return true;
        }
    }
}
