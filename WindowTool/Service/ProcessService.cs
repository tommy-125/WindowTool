using System.Diagnostics;
using WindowTool.Model;

namespace WindowTool.Service {
    internal partial class ProcessService : IProcessService {
        public List<ProcessInfo> WindowProcessList { get; set; }
        public List<ProcessInfo> MonitorWindowProcessList { get; set; }

        private readonly ProcessTaskRegistry _muteTasks = new();
        private readonly SemaphoreSlim _monitorLock = new(1, 1);
        private readonly ProcessSettingsStore _settingsStore = new();
        private readonly object _shutdownLock = new();
        private Task? _shutdownTask;
        private volatile bool _shuttingDown;
        private int _focusRefreshPending;
        private int _focusRefreshRunning;
        private bool _disposed;

        public ProcessService() {
            WindowProcessList = new List<ProcessInfo>();
            MonitorWindowProcessList = new List<ProcessInfo>();

            ProcessHelper.FocusWindowChanged += OnFocusWindowChanged;
        }

        public void StartMonitoring() {
            ProcessHelper.StartFocusWindowMonitoring();
        }

        public void StopMonitoring() {
            ProcessHelper.StopFocusWindowMonitoring();
        }

        public async Task AddToMonitorListAsync(ProcessInfo processInfo) {
            ThrowIfDisposed();

            await _monitorLock.WaitAsync();
            try {
                ThrowIfShuttingDown();
                if (MonitorWindowProcessList.Any(p => p.Id == processInfo.Id)) return;

                AudioHelper.PrepareProcessVolumeForMonitoring(processInfo);
                _settingsStore.Save(processInfo);
                MonitorWindowProcessList = [.. MonitorWindowProcessList, processInfo];
                Debug.WriteLine($"[ProcessService] Added to monitor list: {processInfo.MainWindowTitle} (PID: {processInfo.Id})");
            }
            finally {
                _monitorLock.Release();
            }
        }

        public async Task RemoveFromMonitorListAsync(ProcessInfo processInfo) {
            ThrowIfDisposed();

            await _monitorLock.WaitAsync();
            try {
                ThrowIfShuttingDown();
                var existingProcess = MonitorWindowProcessList.FirstOrDefault(p => p.Id == processInfo.Id);
                if (existingProcess == null) return;

                await CancelTaskAsync(existingProcess);
                MonitorWindowProcessList = MonitorWindowProcessList
                    .Where(process => process.Id != existingProcess.Id)
                    .ToList();
                AudioHelper.ResetVolume(existingProcess);
                Debug.WriteLine($"[ProcessService] Removed from monitor list: {processInfo.MainWindowTitle} (PID: {processInfo.Id})");
            }
            finally {
                _monitorLock.Release();
            }
        }

        private void OnFocusWindowChanged(object? sender, ProcessInfo? processInfo) {
            if (_shuttingDown) return;

            if (processInfo != null) Debug.WriteLine($"[ProcessService] Focus changed to: {processInfo.MainWindowTitle} (PID: {processInfo.Id})");
            else Debug.WriteLine("[ProcessService] Focus changed to: null");

            Interlocked.Exchange(ref _focusRefreshPending, 1);
            StartPendingFocusRefresh();
        }

        private void StartPendingFocusRefresh() {
            if (Interlocked.CompareExchange(ref _focusRefreshRunning, 1, 0) != 0) return;
            _ = ProcessPendingFocusRefreshesAsync();
        }

        private async Task ProcessPendingFocusRefreshesAsync() {
            try {
                while (!_shuttingDown && Interlocked.Exchange(ref _focusRefreshPending, 0) != 0) {
                    try {
                        await MonitorProcessAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException) when (_shuttingDown || _disposed) {
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"[ProcessService] Monitor failed: {ex.Message}");
                    }
                }
            }
            finally {
                Interlocked.Exchange(ref _focusRefreshRunning, 0);
                if (!_shuttingDown && Volatile.Read(ref _focusRefreshPending) != 0) {
                    StartPendingFocusRefresh();
                }
            }
        }

        public async Task MonitorProcessAsync() {
            ThrowIfDisposed();

            await _monitorLock.WaitAsync();
            try {
                ThrowIfShuttingDown();
                await RefreshMonitorWindowProcessListAsync();

                var focusWindowProcess = ProcessHelper.GetFocusWindowProcess();
                foreach (var process in MonitorWindowProcessList.ToList()) {
                    bool isFocused = process.Id == focusWindowProcess?.Id;
                    bool initializedTargetVolume = AudioHelper.PrepareProcessVolumeForMonitoring(process);
                    if (initializedTargetVolume) {
                        _settingsStore.Save(process);
                    }

                    if (MonitorStateMachine.ShouldCancelRunningTask(process, isFocused)) {
                        await CancelTaskAsync(process, syncStateToCancelledTarget: true);
                    }

                    process.ShouldBeMuted = !isFocused;

                    if (MonitorStateMachine.ShouldStartTask(process)) {
                        await StartTaskAsync(process);
                    }
                }
            }
            finally {
                _monitorLock.Release();
            }
        }

        private async Task StartTaskAsync(ProcessInfo process) {
            if (_muteTasks.Contains(process.Id)) {
                await CancelTaskAsync(process);
                Debug.WriteLine($"[StartTask] Replaced existing task for PID: {process.Id}");
            }

            var cts = new CancellationTokenSource();
            int delaySeconds = process.ShouldBeMuted
                ? process.UnfocusMuteDurationSec
                : process.FocusUnmuteDurationSec;

            int fadeDuration = process.ShouldBeMuted
                ? process.FadeMuteDurationSec
                : process.FadeUnmuteDurationSec;

            process.IsProcessingTask = true;
            var task = AudioHelper.SetMuteStateAsync(process, delaySeconds, fadeDuration, cts.Token);
            var entry = _muteTasks.Register(process.Id, task, cts);

            _ = task.ContinueWith(completedTask => {
                if (completedTask.Exception != null) {
                    Debug.WriteLine($"[StartTask] Task failed for PID {process.Id}: {completedTask.Exception.GetBaseException().Message}");
                }

                // Only remove this exact task. A replacement may already be registered
                // after a rapid focus change, and must remain tracked.
                if (_muteTasks.TryTake(process.Id, entry)) {
                    entry.Cancellation.Dispose();
                    Debug.WriteLine($"Auto-cleaned task for PID: {process.Id}");
                }
            }, TaskScheduler.Default);

            Debug.WriteLine($"[StartTask] Started task for PID: {process.Id}, ShouldBeMuted: {process.ShouldBeMuted}");
        }

        private async Task CancelTaskAsync(ProcessInfo process, bool syncStateToCancelledTarget = false) {
            if (!_muteTasks.TryTake(process.Id, out var taskInfo) || taskInfo == null) return;

            taskInfo.Cancellation.Cancel();
            try {
                await taskInfo.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
            }
            catch (Exception ex) {
                Debug.WriteLine($"[CancelTask] Task failed while cancelling PID {process.Id}: {ex.Message}");
            }
            finally {
                taskInfo.Cancellation.Dispose();
            }

            if (syncStateToCancelledTarget) {
                lock (process.VolumeLock) {
                    process.IsMuted = process.ShouldBeMuted;
                }
            }
        }

        public bool RefreshWindowProcessList() {
            return MergeWindowProcessList(ProcessHelper.GetAllWindowProcess());
        }

        public async Task<bool> RefreshWindowProcessListAsync(CancellationToken cancellationToken = default) {
            var currentProcesses = await Task.Run(
                ProcessHelper.GetAllWindowProcess,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return MergeWindowProcessList(currentProcesses);
        }

        private bool MergeWindowProcessList(List<ProcessInfo> currentProcesses) {
            var currentPidSet = currentProcesses.ToDictionary(p => p.Id);
            bool hasChanges = false;

            int removedCount = WindowProcessList.RemoveAll(p =>
                !currentPidSet.ContainsKey(p.Id)
                || (currentPidSet.TryGetValue(p.Id, out var currentProcess) && p.Name != currentProcess.Name));
            hasChanges = removedCount > 0;

            var existingPidDict = WindowProcessList.ToDictionary(p => p.Id);

            foreach (var process in WindowProcessList) {
                if (currentPidSet.TryGetValue(process.Id, out var currentProcess)) {
                    bool changed = process.Name != currentProcess.Name
                        || process.MainWindowTitle != currentProcess.MainWindowTitle
                        || process.MainWindowHandle != currentProcess.MainWindowHandle;

                    if (changed) {
                        process.Name = currentProcess.Name;
                        process.MainWindowTitle = currentProcess.MainWindowTitle;
                        process.MainWindowHandle = currentProcess.MainWindowHandle;
                        hasChanges = true;
                    }
                }
            }

            var newProcesses = currentProcesses.Where(p => !existingPidDict.ContainsKey(p.Id)).ToList();
            foreach (var process in newProcesses) {
                _settingsStore.Apply(process);
                WindowProcessList.Add(process);
            }

            return hasChanges || newProcesses.Count > 0;
        }

        public void SaveSettings(ProcessInfo processInfo) {
            _settingsStore.Save(processInfo);
        }

        public async Task<bool> SetProcessVolumeAsync(ProcessInfo processInfo, float volume) {
            ThrowIfDisposed();

            await _monitorLock.WaitAsync();
            try {
                ThrowIfShuttingDown();
                await CancelTaskAsync(processInfo);
                bool updatedSession = AudioHelper.SetTargetVolume(processInfo, volume);
                _settingsStore.Save(processInfo);
                return updatedSession;
            }
            finally {
                _monitorLock.Release();
            }
        }

        private async Task RefreshMonitorWindowProcessListAsync() {
            var removedProcesses = MonitorWindowProcessList.Where(p => !p.Refresh() || !p.EnableUnfocusMute).ToList();
            foreach (var process in removedProcesses) {
                await CancelTaskAsync(process);
                Debug.WriteLine($"[RefreshMonitorWindowProcessList] Removed closed process PID: {process.Id}");
            }

            if (removedProcesses.Count > 0) {
                HashSet<int> removedIds = removedProcesses.Select(process => process.Id).ToHashSet();
                MonitorWindowProcessList = MonitorWindowProcessList
                    .Where(process => !removedIds.Contains(process.Id))
                    .ToList();
            }
        }

        public Task ShutdownAsync() {
            lock (_shutdownLock) {
                return _shutdownTask ??= ShutdownCoreAsync();
            }
        }

        private async Task ShutdownCoreAsync() {
            _shuttingDown = true;
            StopMonitoring();
            ProcessHelper.FocusWindowChanged -= OnFocusWindowChanged;

            await _monitorLock.WaitAsync().ConfigureAwait(false);
            try {
                try {
                    await CancelAllTasksAsync().ConfigureAwait(false);
                }
                catch (Exception ex) {
                    Debug.WriteLine($"[Shutdown] Error cancelling audio tasks: {ex.Message}");
                }

                try {
                    int failedRestoreCount = AudioHelper.RestoreAllManagedSessions();
                    if (failedRestoreCount > 0) {
                        Debug.WriteLine($"[ProcessService] {failedRestoreCount} audio session(s) could not be restored.");
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"[Shutdown] Error restoring audio state: {ex.Message}");
                }
            }
            finally {
                _monitorLock.Release();
            }
        }

        public void Dispose() {
            if (_disposed) return;

            // App normally awaits ShutdownAsync before OnExit. This remains an
            // idempotent fallback for non-UI callers and exceptional exits.
            ShutdownAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            _monitorLock.Dispose();
            _disposed = true;

            Debug.WriteLine("[ProcessService] Disposed");
            GC.SuppressFinalize(this);
        }

        private async Task CancelAllTasksAsync() {
            foreach (var process in MonitorWindowProcessList.ToList()) {
                await CancelTaskAsync(process).ConfigureAwait(false);
            }

            foreach (var kvp in _muteTasks.Snapshot()) {
                if (!_muteTasks.TryTake(kvp.Key, out var taskInfo) || taskInfo == null) continue;

                taskInfo.Cancellation.Cancel();
                try {
                    await taskInfo.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                }
                catch (Exception ex) {
                    Debug.WriteLine($"[CancelAllTasks] Task failed while cancelling PID {kvp.Key}: {ex.Message}");
                }
                finally {
                    taskInfo.Cancellation.Dispose();
                }
            }
        }

        private void ThrowIfDisposed() {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private void ThrowIfShuttingDown() {
            if (_shuttingDown) {
                throw new ObjectDisposedException(nameof(ProcessService), "WindowTool is shutting down.");
            }
        }
    }
}
