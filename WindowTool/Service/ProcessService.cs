using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WindowTool.Model;

namespace WindowTool.Service {
    internal partial class ProcessService : IProcessService, IDisposable {
        public List<ProcessInfo> WindowProcessList { get; set; }
        public List<ProcessInfo> MonitorWindowProcessList { get; set; }
        private Dictionary<int, (Task Task,CancellationTokenSource Cts)> _muteTasks { get; set; } = new Dictionary<int, (Task,CancellationTokenSource)>();
        private bool _disposed = false;

        public ProcessService() {
            WindowProcessList = ProcessHelper.GetAllWindowProcess();
            MonitorWindowProcessList = new List<ProcessInfo>();

            // 訂閱焦點視窗改變事件
            ProcessHelper.FocusWindowChanged += OnFocusWindowChanged;
        }

        /// <summary>
        /// 啟動監控
        /// </summary>
        public void StartMonitoring() {
            ProcessHelper.StartFocusWindowMonitoring();
        }

        /// <summary>
        /// 停止監控
        /// </summary>
        public void StopMonitoring() {
            ProcessHelper.StopFocusWindowMonitoring();
            ProcessHelper.FocusWindowChanged -= OnFocusWindowChanged;  // 取消訂閱
        }

        /// <summary>
        /// 將進程加入監控列表
        /// </summary>
        /// <param name="processInfo"></param>
        public void AddToMonitorList(ProcessInfo processInfo) {
            if (!MonitorWindowProcessList.Any(p => p.Id == processInfo.Id)) {
                MonitorWindowProcessList.Add(processInfo);
                Debug.WriteLine($"[ProcessService] Added to monitor list: {processInfo.MainWindowTitle} (PID: {processInfo.Id})");
            }
        }

        /// <summary>
        /// 將進程從監控列表中移除
        /// </summary>
        /// <param name="processInfo"></param>
        public void RemoveFromMonitorList(ProcessInfo processInfo) {
            var existingProcess = MonitorWindowProcessList.FirstOrDefault(p => p.Id == processInfo.Id);
            if (existingProcess != null) {
                CancelTask(existingProcess); // 取消任何正在進行的任務
                MonitorWindowProcessList.Remove(existingProcess);
                AudioHelper.ResetVolume(existingProcess); // 重設音量
                Debug.WriteLine($"[ProcessService] Removed from monitor list: {processInfo.MainWindowTitle} (PID: {processInfo.Id})");
            }
        }

        /// <summary>
        /// 當焦點視窗改變時執行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="processInfo"></param>
        private void OnFocusWindowChanged(object? sender, ProcessInfo? processInfo) {
            if (processInfo != null) Debug.WriteLine($"[ProcessService] Focus changed to: {processInfo.MainWindowTitle} (PID: {processInfo.Id})");
            else Debug.WriteLine($"[ProcessService] Focus changed to: null");

            MonitorProcess();
        }
        
        /// <summary>
        /// 監控進程核心邏輯
        /// </summary>
        public void MonitorProcess() {
            CleanupCompletedTasks();
            RefreshMonitorWindowProcessList();

            var FocusWindowProcess = ProcessHelper.GetFocusWindowProcess();
            foreach (var process in MonitorWindowProcessList) {
                bool isFocused = process.Id == FocusWindowProcess?.Id;

                // 如果正在執行任務且目標狀態改變，取消任務
                if (process.IsProcessingTask) {
                    bool shouldBeFocusedButMuting = isFocused && process.ShouldBeMuted;
                    bool shouldBeUnfocusedButUnmuting = !isFocused && !process.ShouldBeMuted;

                    if (shouldBeFocusedButMuting || shouldBeUnfocusedButUnmuting) {
                        CancelTask(process);
                    }
                }

                // 更新應該的狀態
                process.ShouldBeMuted = !isFocused;

                // 如果狀態需要改變且沒有任務在執行，啟動新任務
                if (process.ShouldBeMuted != process.IsMuted && !process.IsProcessingTask) {
                    StartTask(process);
                }
            }
        }
        
        /// <summary>
        /// 清理已完成的任務
        /// </summary>
        private void CleanupCompletedTasks() {
            var completedPids = new List<int>();

            foreach (var kvp in _muteTasks) {
                var pid = kvp.Key;
                var (task, cts) = kvp.Value;

                if (task.IsCompleted) {
                    completedPids.Add(pid);
                    cts.Dispose();

                    // 更新 ProcessInfo 狀態
                    var process = MonitorWindowProcessList.FirstOrDefault(p => p.Id == pid);
                    if (process != null) {
                        process.IsProcessingTask = false;
                    }

                    Debug.WriteLine($"[CleanupCompletedTasks] Removed completed task for PID: {pid}, Status: {task.Status}");
                }
            }

            // 從字典中移除
            foreach (var pid in completedPids) {
                _muteTasks.Remove(pid);
            }
        }

        /// <summary>
        /// 開始指定進程的靜音任務
        /// </summary>
        /// <param name="process"></param>
        private void StartTask(ProcessInfo process) {
            var session = AudioHelper.FindAudioSession(process.Id);
            if (session == null) {
                Debug.WriteLine($"[StartTask] Audio session not found for PID: {process.Id}");
                return;
            }

            var cts = new CancellationTokenSource();

            int delaySeconds = process.ShouldBeMuted
                ? process.UnfocusMuteDurationSec
                : process.FocusUnmuteDurationSec;

            int fadeDuration = process.ShouldBeMuted
                ? process.FadeMuteDurationSec
                : process.FadeUnmuteDurationSec;

            var task = Task.Run(async () => AudioHelper.MuteProcess(
                process,
                session,
                delaySeconds,
                fadeDuration,
                cts.Token
            ));

            _muteTasks[process.Id] = (task, cts);
            Debug.WriteLine($"[StartTask] Started task for PID: {process.Id}, ShouldBeMuted: {process.ShouldBeMuted}");
        }

        /// <summary>
        /// 取消指定進程的靜音任務
        /// </summary>
        /// <param name="process"></param>
        private void CancelTask(ProcessInfo process) {
            if (_muteTasks.TryGetValue(process.Id, out var taskInfo)) {
                taskInfo.Cts.Cancel();
                taskInfo.Cts.Dispose();
                _muteTasks.Remove(process.Id);
                process.IsProcessingTask = false;
                Debug.WriteLine($"[CancelTask] Cancelled task for PID: {process.Id}");
            }
        }

        /// <summary>
        /// 刷新系統中所有視窗進程列表
        /// </summary>
        public void RefreshWindowProcessList() {
            // 取得目前系統中所有視窗進程
            var currentProcesses = ProcessHelper.GetAllWindowProcess();

            // 建立字典以 PID 為鍵，方便快速查找（O(1)）
            var currentPidSet = currentProcesses.ToDictionary(p => p.Id);
            var existingPidDict = WindowProcessList.ToDictionary(p => p.Id);

            // 移除已經不存在的進程
            WindowProcessList.RemoveAll(p => !currentPidSet.ContainsKey(p.Id));

            // 加入新的進程（只加入原本列表中不存在的 PID）
            var newProcesses = currentProcesses.Where(p => !existingPidDict.ContainsKey(p.Id));
            WindowProcessList.AddRange(newProcesses);
        }

        /// <summary>
        /// 將已關閉或不啟用靜音邏輯的進程從監控列表中移除
        /// </summary>
        private void RefreshMonitorWindowProcessList() {
            // 在移除前先取消任務
            var removedProcesses = MonitorWindowProcessList?.Where(p => !p.Refresh() || !p.EnableUnfocusMute).ToList();
            if (removedProcesses != null) {
                foreach (var process in removedProcesses) {
                    CancelTask(process);
                    Debug.WriteLine($"[RefreshMonitorWindowProcessList] Removed closed process PID: {process.Id}");
                }
            }

            MonitorWindowProcessList?.RemoveAll(p => !p.Refresh());
            if (MonitorWindowProcessList == null || MonitorWindowProcessList.Count == 0) return;

            foreach (var process in MonitorWindowProcessList) AudioHelper.GetProcessVolume(process);
        }
        public void Dispose() {
            // 防止重複清理
            if (_disposed) return;

            // 1. 停止監控（會取消事件訂閱）
            StopMonitoring();

            // 2. 取消並清理所有任務
            foreach (var kvp in _muteTasks) {
                try {
                    kvp.Value.Cts.Cancel();  // 取消任務
                    kvp.Value.Cts.Dispose(); // 釋放 CancellationTokenSource
                }
                catch (Exception ex) {
                    Debug.WriteLine($"[Dispose] Error disposing task {kvp.Key}: {ex.Message}");
                }
            }
            _muteTasks.Clear();

            // 3. 標記為已清理
            _disposed = true;

            Debug.WriteLine("[ProcessService] Disposed");
            GC.SuppressFinalize(this);
        }
    }
}
