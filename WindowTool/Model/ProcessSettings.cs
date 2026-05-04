namespace WindowTool.Model {
    public class ProcessSettings {
        public bool EnableUnfocusMute { get; set; }
        public int UnfocusMuteDurationSec { get; set; }
        public int FocusUnmuteDurationSec { get; set; }
        public int FadeMuteDurationSec { get; set; }
        public int FadeUnmuteDurationSec { get; set; }
        public bool ShouldBeTopMost { get; set; }
        public bool HasOriginalVolume { get; set; }
        public float OriginalVolume { get; set; } = 1.0f;
    }
}
