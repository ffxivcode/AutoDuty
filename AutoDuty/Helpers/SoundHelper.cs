using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
//using NAudio.Wave;
using System;

namespace AutoDuty.Helpers
{
    public static class SoundHelper
    {
        public static bool StartSound(bool PlayEndSound, bool CustomSound, Sounds SoundEnum = Sounds.None)
        {
            if (!PlayEndSound)
                return false;

            UIModule.PlaySound((uint)SoundEnum);
            return true;
        }
    }
}
