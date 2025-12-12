using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography.Pkcs;

namespace WindowTool.Model {
    public class ProcessInfo {
        public string Name { get; set; }
        public int Id { get; set; }
        public string MainWindowTitle { get; set; }
        public float? Volume { get; set; }
        public bool EnableUnfocusMute { get; set; }
        public bool EnableTopMost { get; set; }
        public int UnfocusMuteDuration { get; set; }
        public int FocusUnmuteDuration { get; set; }
        public int FadeMuteDuration { get; set; }
        public int FadeUnmuteDuration { get; set; }
        public bool IsMuted { get; set; }
        public bool shouldBeMute { get; set; }

        public ProcessInfo(Process process) {
            Name = process.ProcessName;
            Id = process.Id;
            MainWindowTitle = process.MainWindowTitle;
            Volume = null;

        }

        public bool Refresh() {
            try {
                var p = Process.GetProcessById(Id);
                this.MainWindowTitle = p.MainWindowTitle;
                return true;
            } catch (Exception) {
                return false;
            }
        }
    }
}
