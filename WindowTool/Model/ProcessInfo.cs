using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Security.Cryptography.Pkcs;

namespace WindowTool.Model {
    public class ProcessInfo {
        public string Name { get; set; }
        public int Id { get; set; }
        public string MainWindowTitle { get; set; }
        public IntPtr MainWindowHandle { get; set; }
        public float OriginalVolume { get; set; }
        public bool HasOriginalVolume { get; set; }
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

        public readonly Lock VolumeLock = new Lock(); // 用於同步音量相關操作的鎖
        public ProcessInfo(Process process) {
            Name = process.ProcessName;
            Id = process.Id;
            MainWindowHandle = process.MainWindowHandle;
            MainWindowTitle = process.MainWindowTitle;
            OriginalVolume = 1.0f;
            HasOriginalVolume = false;
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
