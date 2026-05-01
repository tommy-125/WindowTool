namespace WindowTool.Model {
    public class ProcessSettings {
        public bool EnableUnfocusMute { get; set; }
        public int UnfocusMuteDurationSec { get; set; }
        public int FocusUnmuteDurationSec { get; set; }
        public int FadeMuteDurationSec { get; set; }
        public int FadeUnmuteDurationSec { get; set; }
        public bool ShouldBeTopMost { get; set; }
    }
}
