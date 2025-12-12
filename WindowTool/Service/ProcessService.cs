using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WindowTool.Model;

namespace WindowTool.Service {
    internal partial class ProcessService : IProcessService {
        public List<ProcessInfo> WindowProcessList { get; set; }
        public List<ProcessInfo> MonitorWindowProcessList { get; set; }
        private Dictionary<int, CancellationTokenSource> _muteTaskCts = new Dictionary<int, CancellationTokenSource>();
        public ProcessService() {
            WindowProcessList = ProcessHelper.GetAllWindowProcess();
            MonitorWindowProcessList = new List<ProcessInfo>();

            // 訂閱焦點視窗改變事件
            ProcessHelper.FocusWindowChanged += OnFocusWindowChanged;
        }

        // 啟動監控
        public void StartMonitoring() {
            ProcessHelper.StartFocusWindowMonitoring();
        }

        // 停止監控
        public void StopMonitoring() {
            ProcessHelper.StopFocusWindowMonitoring();
            ProcessHelper.FocusWindowChanged -= OnFocusWindowChanged;  // 取消訂閱
        }

        // 當焦點視窗改變時執行
        private void OnFocusWindowChanged(object? sender, ProcessInfo? processInfo) {
            MonitorProcess();
            if (processInfo != null) Debug.WriteLine($"[ProcessService] Focus changed to: {processInfo.MainWindowTitle} (PID: {processInfo.Id})");
            else Debug.WriteLine($"[ProcessService] Focus changed to: null");
        }

        public void MonitorProcess() {
            var FocusWindowProcess = ProcessHelper.GetFocusWindowProcess();
            if (FocusWindowProcess != null && MonitorWindowProcessList.Any(p => p.Id == FocusWindowProcess.Id)) {
                if (_muteTaskCts.TryGetValue(FocusWindowProcess.Id, out var cts)) {
                    // 取消靜音任務
                    cts.Cancel();
                    _muteTaskCts.Remove(FocusWindowProcess.Id);
                    Debug.WriteLine($"[ProcessService] Cancel mute task for PID: {FocusWindowProcess.Id}");
                }
            } else {
                foreach (var process in MonitorWindowProcessList) {
                    // 檢查每個進程是否被靜音
                }
            }
        }


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


    }
}
