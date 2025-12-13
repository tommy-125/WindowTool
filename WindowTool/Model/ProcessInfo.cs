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
        public IntPtr MainWindowHandle { get; set; }
        public float Volume { get; set; }
        public bool EnableUnfocusMute { get; set; }
        public int UnfocusMuteDurationSec { get; set; }
        public int FocusUnmuteDurationSec { get; set; }
        public int FadeMuteDurationSec { get; set; }
        public int FadeUnmuteDurationSec { get; set; }
        public bool IsMuted { get; set; }
        public bool ShouldBeMuted { get; set; }
        public bool IsTopMost { get; set; }
        public bool ShouldBeTopMost { get; set; }
        public bool IsProcessingTask { get; set; }

        public ProcessInfo(Process process) {
            Name = process.ProcessName;
            Id = process.Id;
            MainWindowHandle = process.MainWindowHandle;
            MainWindowTitle = process.MainWindowTitle;
            Volume = 1.0f;
            EnableUnfocusMute = false;
            EnableUnfocusMute = false;
            ShouldBeMuted = false;
            ShouldBeTopMost = false;
            IsMuted = false;
            IsTopMost = false;
            IsProcessingTask = false;
            UnfocusMuteDurationSec = 0;
            FocusUnmuteDurationSec = 0;
        }

        public bool Refresh() {
            try {
                var p = Process.GetProcessById(Id);
                this.MainWindowTitle = p.MainWindowTitle;
                this.MainWindowHandle = p.MainWindowHandle;
                return true;
            } catch (Exception) {
                return false;
            }
        }
    }
}
