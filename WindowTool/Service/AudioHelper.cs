using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Text;
using WindowTool.Model;

namespace WindowTool.Service {
    internal static class AudioHelper {
        public static AudioSessionControl? FindAudioSession(int pid) {
            var enumerator = new MMDeviceEnumerator();
            MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessionManager = device.AudioSessionManager;
            for (int i = 0; i < sessionManager.Sessions.Count; i++) {
                var session = sessionManager.Sessions[i];
                if (session.GetProcessID == pid) {
                    return session;
                }
            }
            return null;
        }
        public static async Task MuteProcess(ProcessInfo process, AudioSessionControl session, bool isMute, int delayMuteSec, int fadeDurationSec, CancellationToken ctsToken) {
            try {
                await Task.Delay(delayMuteSec * 1000, ctsToken);
                if (isMute) {
                    process.Volume = session.SimpleAudioVolume.Volume;
                    await FadeVolume(session, process.Volume.Value, 0, fadeDurationSec, ctsToken);
                } else {
                    float targetVolume = process.Volume ?? 1.0f;
                    await FadeVolume(session, session.SimpleAudioVolume.Volume, targetVolume, fadeDurationSec, ctsToken);
                }
            }
            catch (TaskCanceledException) {
                return;
            }
        }

        private static async Task FadeVolume(AudioSessionControl session, float fromVolume, float toVolume, int fadeDurationSec, CancellationToken ctsToken) {
            float totalStep = fadeDurationSec * 1000 / 50;
            if (totalStep < 1) totalStep = 1;

            for (int i = 1; i <= totalStep; i++) {
                ctsToken.ThrowIfCancellationRequested();
                float progress = i / totalStep;
                float newVolume = fromVolume + (toVolume - fromVolume) * progress;
                session.SimpleAudioVolume.Volume = newVolume;
                await Task.Delay(50, ctsToken);
            }
            session.SimpleAudioVolume.Volume = toVolume;
        }
    }
}
