using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using NAudio.Wave;
using System;

namespace AutoDuty.Helpers
{
    public static class SoundHelper
    {
        private static AudioFileReader? _audioFile;
        private static WaveOutEvent? _audioEvent;

        public static bool SoundReady
            => _audioFile != null && _audioEvent != null;

        public static bool StartSound(bool PlayEndSound, bool CustomSound, Sounds SoundEnum = Sounds.None)
        {
            if (!PlayEndSound)
                return false;

            if (CustomSound)
            {
                if (_audioFile == null || _audioEvent == null)
                {
                    Svc.Log.Error("AudioFileAudioEventNull.");
                    return false;
                }

                _audioEvent!.Stop();
                _audioFile!.Position = 0;
                _audioEvent!.Play();
                //Svc.Log.Error("DidYouHearAnything?");
                return true;
            }

            UIModule.PlaySound((uint)SoundEnum);
            return true;
        }

        public static void DisposeAudio()
        {
            _audioFile?.Dispose();
            _audioFile = null;
            _audioEvent?.Dispose();
            _audioEvent = null;
        }

        public static void UpdateAudio(bool PlayEndSound, bool CustomSound, string SoundPath, float CustomSoundVolume = 0.5f)
        {
            if (!(PlayEndSound && CustomSound))
            {
                DisposeAudio();
                return;
            }

            try
            {
                if (_audioFile?.FileName != SoundPath)
                    DisposeAudio();

                _audioFile = new AudioFileReader(SoundPath) { Volume = CustomSoundVolume };
                _audioEvent = new WaveOutEvent();
                _audioEvent.Init(_audioFile);
            }
            catch (Exception ex)
            {
                //Dalamud.Log.Error(ex, "Error attempting to setup sound.");
                DisposeAudio();
            }
        }
    }
}
