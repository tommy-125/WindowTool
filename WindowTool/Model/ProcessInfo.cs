using System.Diagnostics;

namespace WindowTool.Model {
    public class ProcessInfo {
        public string Name { get; set; }
        public int Id { get; set; }
        public string MainWindowTitle { get; set; }
        public IntPtr MainWindowHandle { get; set; }
        public float OriginalVolume { get; set; }
        public bool HasOriginalVolume { get; set; }
        public bool HasRestoredOriginalVolumeForSession { get; set; }
        public bool EnableUnfocusMute { get; set; }
        public int UnfocusMuteDurationSec { get; set; }
        public int FocusUnmuteDurationSec { get; set; }
        public int FadeMuteDurationSec { get; set; }
        public int FadeUnmuteDurationSec { get; set; }
        public bool IsMuted { get; set; }
        public bool ShouldBeMuted { get; set; }
        public bool IsTopMost { get; set; }
        public bool ShouldBeTopMost { get; set; }
        public bool OriginalTopMost { get; set; }
        public bool HasOriginalTopMost { get; set; }
        public bool TopMostManagedByTool { get; set; }
        public bool IsProcessingTask { get; set; }

        public readonly Lock VolumeLock = new Lock();

        public ProcessInfo(Process process) {
            Name = process.ProcessName;
            Id = process.Id;
            MainWindowHandle = process.MainWindowHandle;
            MainWindowTitle = process.MainWindowTitle;
            OriginalVolume = 1.0f;
            HasOriginalVolume = false;
            HasRestoredOriginalVolumeForSession = false;
            EnableUnfocusMute = false;
            ShouldBeMuted = false;
            ShouldBeTopMost = false;
            IsMuted = false;
            IsTopMost = false;
            IsProcessingTask = false;
            OriginalTopMost = false;
            HasOriginalTopMost = false;
            TopMostManagedByTool = false;
            UnfocusMuteDurationSec = 0;
            FocusUnmuteDurationSec = 0;
        }

        public bool Refresh() {
            try {
                using var process = Process.GetProcessById(Id);
                MainWindowTitle = process.MainWindowTitle;
                MainWindowHandle = process.MainWindowHandle;
                return true;
            }
            catch (Exception) {
                return false;
            }
        }
    }
}
