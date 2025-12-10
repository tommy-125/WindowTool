using System;
using System.Collections.Generic;
using System.Text;
using WindowTool.Model;

namespace WindowTool.Service {
    internal interface IProcessService {
        ProcessInfo? GetFocusWindowProcess();

        List<ProcessInfo> GetAllWindowProcesses();


        
    }
}
