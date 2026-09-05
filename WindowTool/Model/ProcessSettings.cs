namespace WindowTool.Model {
    public class ProcessSettings {
        public bool EnableUnfocusMute { get; set; }
        public int UnfocusMuteDurationSec { get; set; }
        public int FocusUnmuteDurationSec { get; set; }
        public int FadeMuteDurationSec { get; set; }
        public int FadeUnmuteDurationSec { get; set; }
        public bool ShouldBeTopMost { get; set; }
        public bool HasTargetVolume { get; set; }
        public float TargetVolume { get; set; } = 1.0f;

        // Kept nullable so settings written by older versions can be migrated once.
        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? HasOriginalVolume { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public float? OriginalVolume { get; set; }
    }
}
