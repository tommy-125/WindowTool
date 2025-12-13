using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Text;
using WindowTool.Model;

namespace WindowTool.Service {
    internal static class AudioHelper {
        /// <summary>
        /// 根據Process id回傳對應的AudioSessionControl
        /// </summary>
        /// <param name="pid"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 異步靜音程序
        /// </summary>
        /// <param name="process"></param>
        /// <param name="session"></param>
        /// <param name="delayMuteSec"></param>
        /// <param name="fadeDurationSec"></param>
        /// <param name="ctsToken"></param>
        public static async void MuteProcess(ProcessInfo process, AudioSessionControl session, int delayMuteSec, int fadeDurationSec, CancellationToken ctsToken) {
            try {
                process.IsProcessingTask = true;
                await Task.Delay(delayMuteSec * 1000, ctsToken);

                if (process.ShouldBeMuted) {
                    process.Volume = session.SimpleAudioVolume.Volume;
                    await FadeVolume(session, process.Volume, 0, fadeDurationSec, ctsToken);
                    process.IsMuted = true;
                }
                else {
                    float targetVolume = process.Volume;
                    await FadeVolume(session, session.SimpleAudioVolume.Volume, targetVolume, fadeDurationSec, ctsToken);
                    process.IsMuted = false;
                }
            } catch (TaskCanceledException) {
                return;
            } finally {
                process.IsProcessingTask = false;
            }
        }

        /// <summary>
        /// 漸進式調整音量邏輯
        /// </summary>
        /// <param name="session"></param>
        /// <param name="fromVolume"></param>
        /// <param name="toVolume"></param>
        /// <param name="fadeDurationSec"></param>
        /// <param name="ctsToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 重設Process 音量至原本設定值
        /// </summary>
        /// <param name="process"></param>
        public static void ResetVolume(ProcessInfo process) {
            var session = FindAudioSession(process.Id);
            if (session == null) return;
            session.SimpleAudioVolume.Volume = process.Volume;
        }
    }
}
