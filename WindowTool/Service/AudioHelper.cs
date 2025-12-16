using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Text;
using WindowTool.Model;
using static System.Collections.Specialized.BitVector32;

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
        public static async Task MuteProcess(ProcessInfo process, AudioSessionControl session, int delayMuteSec, int fadeDurationSec, CancellationToken ctsToken) {
            bool shouldBeMuted = process.ShouldBeMuted; // 先取起來 避免外面更改導致race condition
            try { 
                await Task.Delay(delayMuteSec * 1000, ctsToken);

                if (shouldBeMuted) { // 靜音
                    await FadeVolume(process, session, session.SimpleAudioVolume.Volume, 0, fadeDurationSec, ctsToken);
                    lock (process.VolumeLock) process.IsMuted = true;
                }
                else {
                    await FadeVolume(process, session, session.SimpleAudioVolume.Volume, process.OriginalVolume, fadeDurationSec, ctsToken); // 恢復到最一開始的音量
                    lock (process.VolumeLock) process.IsMuted = false;
                }
            } catch (TaskCanceledException) {
                lock (process.VolumeLock) {
                    if (shouldBeMuted) session.SimpleAudioVolume.Volume = process.OriginalVolume;
                    else session.SimpleAudioVolume.Volume = 0; // 取消任務時恢復到原本的音量
                }
                return;
            } finally {
                lock (process.VolumeLock) process.IsProcessingTask = false;
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
        private static async Task FadeVolume(ProcessInfo process, AudioSessionControl session, float fromVolume, float toVolume, int fadeDurationSec, CancellationToken ctsToken) {
            float totalStep = fadeDurationSec * 1000 / 50;
            if (totalStep < 1) totalStep = 1;

            for (int i = 1; i <= totalStep; i++) {
                ctsToken.ThrowIfCancellationRequested();
                float progress = i / totalStep;
                float newVolume = fromVolume + (toVolume - fromVolume) * progress;
                lock (process.VolumeLock) session.SimpleAudioVolume.Volume = newVolume;
                await Task.Delay(50, ctsToken);
            }
            lock (process.VolumeLock) session.SimpleAudioVolume.Volume = toVolume;
        }

        /// <summary>
        /// 重設Process 音量至原本設定值
        /// </summary>
        /// <param name="process"></param>
        public static void ResetVolume(ProcessInfo process) {
            var session = FindAudioSession(process.Id);
            if (session == null) return;
            lock (process.VolumeLock) session.SimpleAudioVolume.Volume = process.OriginalVolume;
        }

        /// <summary>
        /// 設定Process音量至目前Audio Session音量
        /// </summary>
        /// <param name="process"></param>
        public static void SetProcessVolume(ProcessInfo process) {
            if (process == null) return;
            var session = FindAudioSession(process.Id);
            if (session != null) {
                lock (process.VolumeLock) {
                    process.OriginalVolume = session.SimpleAudioVolume.Volume;
                    process.HasOriginalVolume = true;
                }
            }
        }
    }
}
