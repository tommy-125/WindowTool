using System.Collections.Concurrent;
using System.Diagnostics;
using WindowTool.Model;

namespace WindowTool.Service {
    internal partial class ProcessService : IProcessService {
        public List<ProcessInfo> WindowProcessList { get; set; }
        public List<ProcessInfo> MonitorWindowProcessList { get; set; }

        private readonly ConcurrentDictionary<int, (Task Task, CancellationTokenSource Cts)> _muteTasks = new();
        private readonly SemaphoreSlim _monitorLock = new(1, 1);
        private readonly ProcessSettingsStore _settingsStore = new();
        private bool _disposed;

        public ProcessService() {
            WindowProcessList = ProcessHelper.GetAllWindowProcess();
            foreach (var process in WindowProcessList) {
                _settingsStore.Apply(process);
            }

            MonitorWindowProcessList = new List<ProcessInfo>();

            ProcessHelper.FocusWindowChanged += OnFocusWindowChanged;
        }

        public void StartMonitoring() {
            ProcessHelper.StartFocusWindowMonitoring();
        }

        public void StopMonitoring() {
            ProcessHelper.StopFocusWindowMonitoring();
            ProcessHelper.FocusWindowChanged -= OnFocusWindowChanged;
        }

        public async Task AddToMonitorListAsync(ProcessInfo processInfo) {
            ThrowIfDisposed();

            await _monitorLock.WaitAsync();
            try {
                if (MonitorWindowProcessList.Any(p => p.Id == processInfo.Id)) return;

                AudioHelper.SetProcessVolume(processInfo);
                MonitorWindowProcessList.Add(processInfo);
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
                var existingProcess = MonitorWindowProcessList.FirstOrDefault(p => p.Id == processInfo.Id);
                if (existingProcess == null) return;

                await CancelTaskAsync(existingProcess);
                MonitorWindowProcessList.Remove(existingProcess);
                AudioHelper.ResetVolume(existingProcess);
                Debug.WriteLine($"[ProcessService] Removed from monitor list: {processInfo.MainWindowTitle} (PID: {processInfo.Id})");
            }
            finally {
                _monitorLock.Release();
            }
        }

        private async void OnFocusWindowChanged(object? sender, ProcessInfo? processInfo) {
            if (processInfo != null) Debug.WriteLine($"[ProcessService] Focus changed to: {processInfo.MainWindowTitle} (PID: {processInfo.Id})");
            else Debug.WriteLine("[ProcessService] Focus changed to: null");

            try {
                await MonitorProcessAsync();
            }
            catch (ObjectDisposedException) {
            }
            catch (Exception ex) {
                Debug.WriteLine($"[ProcessService] Monitor failed: {ex.Message}");
            }
        }

        public async Task MonitorProcessAsync() {
            ThrowIfDisposed();

            await _monitorLock.WaitAsync();
            try {
                await RefreshMonitorWindowProcessListAsync();

                var focusWindowProcess = ProcessHelper.GetFocusWindowProcess();
                foreach (var process in MonitorWindowProcessList.ToList()) {
                    bool isFocused = process.Id == focusWindowProcess?.Id;

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
            if (_muteTasks.ContainsKey(process.Id)) {
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
            var task = AudioHelper.MuteProcess(process, delaySeconds, fadeDuration, cts.Token);
            _muteTasks[process.Id] = (task, cts);

            _ = task.ContinueWith(completedTask => {
                if (completedTask.Exception != null) {
                    Debug.WriteLine($"[StartTask] Task failed for PID {process.Id}: {completedTask.Exception.GetBaseException().Message}");
                }

                if (_muteTasks.TryRemove(process.Id, out var removed)) {
                    removed.Cts.Dispose();
                    Debug.WriteLine($"Auto-cleaned task for PID: {process.Id}");
                }
            }, TaskScheduler.Default);

            Debug.WriteLine($"[StartTask] Started task for PID: {process.Id}, ShouldBeMuted: {process.ShouldBeMuted}");
        }

        private async Task CancelTaskAsync(ProcessInfo process, bool syncStateToCancelledTarget = false) {
            if (!_muteTasks.TryRemove(process.Id, out var taskInfo)) return;

            taskInfo.Cts.Cancel();
            try {
                await taskInfo.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
            }
            catch (Exception ex) {
                Debug.WriteLine($"[CancelTask] Task failed while cancelling PID {process.Id}: {ex.Message}");
            }
            finally {
                taskInfo.Cts.Dispose();
            }

            if (syncStateToCancelledTarget) {
                lock (process.VolumeLock) {
                    process.IsMuted = process.ShouldBeMuted;
                }
            }
        }

        public bool RefreshWindowProcessList() {
            var currentProcesses = ProcessHelper.GetAllWindowProcess();
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

        private async Task RefreshMonitorWindowProcessListAsync() {
            var removedProcesses = MonitorWindowProcessList.Where(p => !p.Refresh() || !p.EnableUnfocusMute).ToList();
            foreach (var process in removedProcesses) {
                await CancelTaskAsync(process);
                Debug.WriteLine($"[RefreshMonitorWindowProcessList] Removed closed process PID: {process.Id}");
            }

            MonitorWindowProcessList.RemoveAll(removedProcesses.Contains);
        }

        public void Dispose() {
            if (_disposed) return;

            StopMonitoring();

            try {
                CancelAllTasksAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex) {
                Debug.WriteLine($"[Dispose] Error cancelling tasks: {ex.Message}");
            }

            _monitorLock.Dispose();
            _disposed = true;

            Debug.WriteLine("[ProcessService] Disposed");
            GC.SuppressFinalize(this);
        }

        private async Task CancelAllTasksAsync() {
            foreach (var process in MonitorWindowProcessList.ToList()) {
                await CancelTaskAsync(process);
            }

            foreach (var kvp in _muteTasks.ToArray()) {
                if (!_muteTasks.TryRemove(kvp.Key, out var taskInfo)) continue;

                taskInfo.Cts.Cancel();
                try {
                    await taskInfo.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                }
                catch (Exception ex) {
                    Debug.WriteLine($"[CancelAllTasks] Task failed while cancelling PID {kvp.Key}: {ex.Message}");
                }
                finally {
                    taskInfo.Cts.Dispose();
                }
            }
        }

        private void ThrowIfDisposed() {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
