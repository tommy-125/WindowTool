using WindowTool.Model;

namespace WindowTool.Service {
    public interface IProcessService : IDisposable {
        public List<ProcessInfo> WindowProcessList { get; set; }
        public List<ProcessInfo> MonitorWindowProcessList { get; set; }

        public void StartMonitoring();
        public void StopMonitoring();
        public Task AddToMonitorListAsync(ProcessInfo processInfo);
        public Task RemoveFromMonitorListAsync(ProcessInfo processInfo);
        public Task MonitorProcessAsync();
        public bool RefreshWindowProcessList();
        public void SaveSettings(ProcessInfo processInfo);
    }
}
