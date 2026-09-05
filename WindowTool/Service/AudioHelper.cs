using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using WindowTool.Model;

namespace WindowTool.Service {
    internal static class AudioHelper {
        private readonly record struct AudioSessionKey(string EndpointId, string SessionInstanceId);

        private sealed record AudioSessionSnapshot(
            AudioSessionKey Key,
            int ProcessId,
            float Volume,
            bool Mute);

        private readonly record struct ManagedAudioSession(
            AudioSessionControl Session,
            AudioSessionKey Key);

        private sealed class AudioSessionHandle : IDisposable {
            public AudioSessionHandle(
                MMDeviceEnumerator enumerator,
                MMDevice device,
                IReadOnlyList<AudioSessionControl> sessions) {
                Enumerator = enumerator;
                Device = device;
                EndpointId = device.ID;
                Sessions = sessions;
            }

            public string EndpointId { get; }
            public IReadOnlyList<AudioSessionControl> Sessions { get; }
            public AudioSessionControl PrimarySession => Sessions.FirstOrDefault(
                session => session.State == AudioSessionState.AudioSessionStateActive)
                ?? Sessions[0];

            private MMDeviceEnumerator Enumerator { get; }
            private MMDevice Device { get; }

            public void Dispose() {
                foreach (AudioSessionControl session in Sessions) {
                    session.Dispose();
                }

                Device.Dispose();
                Enumerator.Dispose();
            }
        }

        private static readonly object OriginalSessionStatesLock = new();
        private static readonly Dictionary<AudioSessionKey, AudioSessionSnapshot> OriginalSessionStates = new();
        private static readonly HashSet<AudioSessionKey> AppliedTargetVolumeSessions = new();

        private static AudioSessionHandle? FindAudioSessions(int pid) {
            HashSet<int> processGroupIds = ProcessTreeHelper.GetProcessGroupIds(pid);
            var enumerator = new MMDeviceEnumerator();
            MMDevice? device = null;
            var matchedSessions = new List<AudioSessionControl>();

            try {
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessionManager = device.AudioSessionManager;
                for (int i = 0; i < sessionManager.Sessions.Count; i++) {
                    var session = sessionManager.Sessions[i];
                    if (processGroupIds.Contains((int)session.GetProcessID)) {
                        matchedSessions.Add(session);
                    }
                    else {
                        session.Dispose();
                    }
                }

                if (matchedSessions.Count == 0) {
                    device.Dispose();
                    enumerator.Dispose();
                    return null;
                }

                return new AudioSessionHandle(enumerator, device, matchedSessions);
            }
            catch {
                foreach (AudioSessionControl session in matchedSessions) {
                    session.Dispose();
                }

                device?.Dispose();
                enumerator.Dispose();
                throw;
            }
        }

        public static async Task SetMuteStateAsync(
            ProcessInfo process,
            int delayMuteSec,
            int fadeDurationSec,
            CancellationToken cancellationToken) {
            bool shouldBeMuted = process.ShouldBeMuted;
            try {
                await Task.Delay(TimeSpan.FromSeconds(delayMuteSec), cancellationToken).ConfigureAwait(false);

                using var sessionHandle = FindAudioSessions(process.Id);
                if (sessionHandle == null) {
                    Debug.WriteLine($"[SetMuteState] Audio session not found for PID: {process.Id}");
                    return;
                }

                float targetVolume = shouldBeMuted ? 0.0f : process.TargetVolume;
                await FadeVolume(
                    process,
                    sessionHandle,
                    targetVolume,
                    fadeDurationSec,
                    cancellationToken).ConfigureAwait(false);

                lock (process.VolumeLock) {
                    process.IsMuted = shouldBeMuted;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                Debug.WriteLine($"[SetMuteState] Cancelled task for PID: {process.Id}");
            }
            finally {
                lock (process.VolumeLock) process.IsProcessingTask = false;
            }
        }

        private static async Task FadeVolume(
            ProcessInfo process,
            AudioSessionHandle sessionHandle,
            float toVolume,
            int fadeDurationSec,
            CancellationToken cancellationToken) {
            List<ManagedAudioSession> sessions = GetRestorableSessions(sessionHandle, process.Id);
            if (sessions.Count == 0) return;

            int totalStep = Math.Max(1, fadeDurationSec * 1000 / 50);
            float[] fromVolumes;
            lock (process.VolumeLock) {
                fromVolumes = sessions
                    .Select(session => session.Session.SimpleAudioVolume.Volume)
                    .ToArray();
            }

            for (int i = 1; i <= totalStep; i++) {
                cancellationToken.ThrowIfCancellationRequested();
                float progress = (float)i / totalStep;
                lock (process.VolumeLock) {
                    for (int sessionIndex = 0; sessionIndex < sessions.Count; sessionIndex++) {
                        float fromVolume = fromVolumes[sessionIndex];
                        sessions[sessionIndex].Session.SimpleAudioVolume.Volume =
                            fromVolume + (toVolume - fromVolume) * progress;
                    }
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }

            lock (process.VolumeLock) {
                foreach (ManagedAudioSession session in sessions) {
                    session.Session.SimpleAudioVolume.Volume = toVolume;
                    MarkTargetVolumeApplied(session.Key);
                }
            }
        }

        public static void ResetVolume(ProcessInfo process) {
            try {
                using var sessionHandle = FindAudioSessions(process.Id);
                if (sessionHandle == null) return;

                List<ManagedAudioSession> sessions = GetRestorableSessions(sessionHandle, process.Id);
                lock (process.VolumeLock) {
                    foreach (ManagedAudioSession session in sessions) {
                        session.Session.SimpleAudioVolume.Volume = process.TargetVolume;
                        MarkTargetVolumeApplied(session.Key);
                    }
                }
            }
            finally {
                lock (process.VolumeLock) {
                    process.IsMuted = false;
                    process.ShouldBeMuted = false;
                }
            }
        }

        // Returns true only when a persisted target volume was initialized.
        // This lets focus changes avoid rewriting the settings file when nothing changed.
        public static bool PrepareProcessVolumeForMonitoring(ProcessInfo process) {
            using var sessionHandle = FindAudioSessions(process.Id);
            if (sessionHandle == null) return false;

            bool initializedTargetVolume = false;
            float volumeToApply;
            lock (process.VolumeLock) {
                if (!process.HasTargetVolume) {
                    process.TargetVolume = sessionHandle.PrimarySession.SimpleAudioVolume.Volume;
                    process.HasTargetVolume = true;
                    initializedTargetVolume = true;
                }

                bool remainMuted = process.EnableUnfocusMute && process.ShouldBeMuted;
                volumeToApply = remainMuted ? 0.0f : process.TargetVolume;
            }

            List<ManagedAudioSession> sessions = GetRestorableSessions(sessionHandle, process.Id);
            bool allCurrentSessionsApplied = sessions.Count > 0;
            foreach (ManagedAudioSession session in sessions) {
                if (HasAppliedTargetVolume(session.Key)) continue;

                try {
                    lock (process.VolumeLock) {
                        session.Session.SimpleAudioVolume.Volume = volumeToApply;
                    }

                    MarkTargetVolumeApplied(session.Key);
                }
                catch (Exception ex) {
                    allCurrentSessionsApplied = false;
                    Debug.WriteLine(
                        $"[AudioState] Could not apply target volume to session " +
                        $"{session.Key.SessionInstanceId} (PID {process.Id}): {ex.Message}");
                }
            }

            lock (process.VolumeLock) {
                process.HasAppliedTargetVolumeForSession = allCurrentSessionsApplied;
            }

            return initializedTargetVolume;
        }

        public static bool TryGetVolume(ProcessInfo process, out float volume) {
            using var sessionHandle = FindAudioSessions(process.Id);
            if (sessionHandle == null) {
                volume = process.TargetVolume;
                return false;
            }

            lock (process.VolumeLock) {
                volume = sessionHandle.PrimarySession.SimpleAudioVolume.Volume;
                return true;
            }
        }

        public static bool SetTargetVolume(ProcessInfo process, float volume) {
            float clampedVolume = Math.Clamp(volume, 0.0f, 1.0f);
            lock (process.VolumeLock) {
                process.TargetVolume = clampedVolume;
                process.HasTargetVolume = true;
                process.HasAppliedTargetVolumeForSession = false;
            }

            using var sessionHandle = FindAudioSessions(process.Id);
            if (sessionHandle == null) return false;

            List<ManagedAudioSession> sessions = GetRestorableSessions(sessionHandle, process.Id);
            if (sessions.Count == 0) return false;

            lock (process.VolumeLock) {
                bool remainMuted = process.EnableUnfocusMute && process.ShouldBeMuted;
                foreach (ManagedAudioSession session in sessions) {
                    session.Session.SimpleAudioVolume.Mute = false;
                    session.Session.SimpleAudioVolume.Volume = remainMuted ? 0.0f : clampedVolume;
                    MarkTargetVolumeApplied(session.Key);
                }

                process.IsMuted = remainMuted;
                process.HasAppliedTargetVolumeForSession = true;
            }

            return true;
        }

        public static int RestoreAllManagedSessions() {
            List<AudioSessionSnapshot> snapshots;
            lock (OriginalSessionStatesLock) {
                snapshots = OriginalSessionStates.Values.ToList();
            }

            if (snapshots.Count == 0) return 0;

            using var enumerator = new MMDeviceEnumerator();
            foreach (IGrouping<string, AudioSessionSnapshot> endpointGroup in
                snapshots.GroupBy(snapshot => snapshot.Key.EndpointId, StringComparer.Ordinal)) {
                MMDevice? device = null;
                try {
                    device = enumerator.GetDevice(endpointGroup.Key);
                    Dictionary<string, AudioSessionSnapshot> endpointSnapshots = endpointGroup
                        .ToDictionary(
                            snapshot => snapshot.Key.SessionInstanceId,
                            StringComparer.Ordinal);
                    var sessions = device.AudioSessionManager.Sessions;
                    int sessionCount = sessions.Count;
                    for (int i = 0; i < sessionCount; i++) {
                        AudioSessionControl? session = null;
                        try {
                            // A session can disappear between reading Count and indexing the
                            // COM collection. Keep that failure local so later sessions on the
                            // same endpoint still get a chance to restore.
                            session = sessions[i];
                            string? instanceId = TryGetSessionInstanceId(session);
                            if (instanceId == null
                                || !endpointSnapshots.TryGetValue(instanceId, out AudioSessionSnapshot? snapshot)) {
                                continue;
                            }

                            try {
                                session.SimpleAudioVolume.Volume = snapshot.Volume;
                                session.SimpleAudioVolume.Mute = snapshot.Mute;
                                RemoveSnapshot(snapshot);
                            }
                            catch (Exception ex) {
                                Debug.WriteLine(
                                    $"[AudioRestore] Failed to restore session {instanceId} " +
                                    $"(PID {snapshot.ProcessId}): {ex.Message}");
                            }
                        }
                        catch (Exception ex) {
                            Debug.WriteLine(
                                $"[AudioRestore] Failed to inspect session index {i} " +
                                $"on endpoint {endpointGroup.Key}: {ex.Message}");
                        }
                        finally {
                            session?.Dispose();
                        }
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"[AudioRestore] Failed to open endpoint {endpointGroup.Key}: {ex.Message}");
                }
                finally {
                    device?.Dispose();
                }
            }

            lock (OriginalSessionStatesLock) {
                return OriginalSessionStates.Count;
            }
        }

        private static List<ManagedAudioSession> GetRestorableSessions(
            AudioSessionHandle sessionHandle,
            int processId) {
            var sessions = new List<ManagedAudioSession>(sessionHandle.Sessions.Count);
            foreach (AudioSessionControl session in sessionHandle.Sessions) {
                if (TryCaptureOriginalState(
                    sessionHandle.EndpointId,
                    session,
                    processId,
                    out AudioSessionKey key)) {
                    sessions.Add(new ManagedAudioSession(session, key));
                }
            }

            return sessions;
        }

        private static bool TryCaptureOriginalState(
            string endpointId,
            AudioSessionControl session,
            int processId,
            out AudioSessionKey key) {
            key = default;
            try {
                string? instanceId = TryGetSessionInstanceId(session);
                if (instanceId == null) return false;

                key = new AudioSessionKey(endpointId, instanceId);
                lock (OriginalSessionStatesLock) {
                    if (!OriginalSessionStates.ContainsKey(key)) {
                        OriginalSessionStates[key] = new AudioSessionSnapshot(
                            key,
                            processId,
                            session.SimpleAudioVolume.Volume,
                            session.SimpleAudioVolume.Mute);
                    }
                }

                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"[AudioState] Could not snapshot session for PID {processId}: {ex.Message}");
                return false;
            }
        }

        private static string? TryGetSessionInstanceId(AudioSessionControl session) {
            try {
                string instanceId = session.GetSessionInstanceIdentifier;
                return string.IsNullOrWhiteSpace(instanceId) ? null : instanceId;
            }
            catch (Exception ex) {
                Debug.WriteLine($"[AudioState] Could not read session identifier: {ex.Message}");
                return null;
            }
        }

        private static void RemoveSnapshot(AudioSessionSnapshot snapshot) {
            lock (OriginalSessionStatesLock) {
                if (OriginalSessionStates.TryGetValue(snapshot.Key, out AudioSessionSnapshot? current)
                    && ReferenceEquals(current, snapshot)) {
                    OriginalSessionStates.Remove(snapshot.Key);
                    AppliedTargetVolumeSessions.Remove(snapshot.Key);
                }
            }
        }

        private static bool HasAppliedTargetVolume(AudioSessionKey key) {
            lock (OriginalSessionStatesLock) {
                return AppliedTargetVolumeSessions.Contains(key);
            }
        }

        private static void MarkTargetVolumeApplied(AudioSessionKey key) {
            lock (OriginalSessionStatesLock) {
                AppliedTargetVolumeSessions.Add(key);
            }
        }
    }
}
