using System.Diagnostics;
using NAudio.CoreAudioApi;
using WindowTool.Model;

namespace WindowTool.Service {
    internal static class AudioHelper {
        private sealed class AudioSessionHandle : IDisposable {
            public AudioSessionHandle(MMDeviceEnumerator enumerator, MMDevice device, AudioSessionControl session) {
                Enumerator = enumerator;
                Device = device;
                Session = session;
            }

            public AudioSessionControl Session { get; }

            private MMDeviceEnumerator Enumerator { get; }
            private MMDevice Device { get; }

            public void Dispose() {
                Session.Dispose();
                Device.Dispose();
                Enumerator.Dispose();
            }
        }

        private static AudioSessionHandle? FindAudioSession(int pid) {
            var enumerator = new MMDeviceEnumerator();
            try {
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                try {
                    var sessionManager = device.AudioSessionManager;
                    for (int i = 0; i < sessionManager.Sessions.Count; i++) {
                        var session = sessionManager.Sessions[i];
                        if (session.GetProcessID == pid) {
                            return new AudioSessionHandle(enumerator, device, session);
                        }

                        session.Dispose();
                    }
                }
                catch {
                    device.Dispose();
                    throw;
                }

                device.Dispose();
                enumerator.Dispose();
                return null;
            }
            catch {
                enumerator.Dispose();
                throw;
            }
        }

        public static async Task MuteProcess(ProcessInfo process, int delayMuteSec, int fadeDurationSec, CancellationToken ctsToken) {
            using var sessionHandle = FindAudioSession(process.Id);
            if (sessionHandle == null) {
                Debug.WriteLine($"[MuteProcess] Audio session not found for PID: {process.Id}");
                lock (process.VolumeLock) process.IsProcessingTask = false;
                return;
            }

            var session = sessionHandle.Session;
            bool shouldBeMuted = process.ShouldBeMuted;
            try {
                await Task.Delay(delayMuteSec * 1000, ctsToken).ConfigureAwait(false);

                if (shouldBeMuted) {
                    await FadeVolume(process, session, session.SimpleAudioVolume.Volume, 0, fadeDurationSec, ctsToken).ConfigureAwait(false);
                    lock (process.VolumeLock) process.IsMuted = true;
                }
                else {
                    await FadeVolume(process, session, session.SimpleAudioVolume.Volume, process.OriginalVolume, fadeDurationSec, ctsToken).ConfigureAwait(false);
                    lock (process.VolumeLock) process.IsMuted = false;
                }
            }
            catch (TaskCanceledException) {
                Debug.WriteLine($"[MuteProcess] Cancelled task for PID: {process.Id}");
            }
            finally {
                lock (process.VolumeLock) process.IsProcessingTask = false;
            }
        }

        private static async Task FadeVolume(ProcessInfo process, AudioSessionControl session, float fromVolume, float toVolume, int fadeDurationSec, CancellationToken ctsToken) {
            int totalStep = Math.Max(1, fadeDurationSec * 1000 / 50);

            for (int i = 1; i <= totalStep; i++) {
                ctsToken.ThrowIfCancellationRequested();
                float progress = (float)i / totalStep;
                float newVolume = fromVolume + (toVolume - fromVolume) * progress;
                lock (process.VolumeLock) session.SimpleAudioVolume.Volume = newVolume;
                await Task.Delay(50, ctsToken).ConfigureAwait(false);
            }

            lock (process.VolumeLock) session.SimpleAudioVolume.Volume = toVolume;
        }

        public static void ResetVolume(ProcessInfo process) {
            using var sessionHandle = FindAudioSession(process.Id);
            if (sessionHandle == null) return;

            lock (process.VolumeLock) {
                sessionHandle.Session.SimpleAudioVolume.Volume = process.OriginalVolume;
                process.IsMuted = false;
                process.ShouldBeMuted = false;
            }
        }

        public static bool PrepareProcessVolumeForMonitoring(ProcessInfo process) {
            using var sessionHandle = FindAudioSession(process.Id);
            if (sessionHandle == null) return false;

            lock (process.VolumeLock) {
                if (process.HasOriginalVolume) {
                    if (!process.HasRestoredOriginalVolumeForSession) {
                        sessionHandle.Session.SimpleAudioVolume.Volume = process.OriginalVolume;
                        process.IsMuted = false;
                        process.ShouldBeMuted = false;
                        process.HasRestoredOriginalVolumeForSession = true;
                    }

                    return true;
                }

                process.OriginalVolume = sessionHandle.Session.SimpleAudioVolume.Volume;
                process.HasOriginalVolume = true;
                process.HasRestoredOriginalVolumeForSession = true;
                return true;
            }
        }
    }
}
