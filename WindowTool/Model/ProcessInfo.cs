using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography.Pkcs;

namespace WindowTool.Model {
    internal class ProcessInfo {
        public string Name { get; set; }
        public int Id { get; set; }
        public string MainWindowTitle { get; set; }
        public ProcessInfo(Process process) {
            Name = process.ProcessName;
            Id = process.Id;
            MainWindowTitle = process.MainWindowTitle;
        }

        public bool Refresh() {
            try {
                var p = Process.GetProcessById(Id);
                this.MainWindowTitle = p.MainWindowTitle;
                return true;
            }
            catch (Exception) {
                return false;
            }
        }
    }
}
