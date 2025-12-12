using System;
using System.Collections.Generic;
using System.Text;
using WindowTool.Model;

namespace WindowTool.Service {
    public interface IProcessService {
        public List<ProcessInfo> WindowProcessList { get; set; }
        public List<ProcessInfo> MonitorWindowProcessList { get; set; }

        public void MonitorProcess();
        public void RefreshWindowProcessList();
    }
}
