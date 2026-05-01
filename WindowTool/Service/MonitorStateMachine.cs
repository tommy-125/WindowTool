using WindowTool.Model;

namespace WindowTool.Service {
    internal static class MonitorStateMachine {
        public static bool ShouldCancelRunningTask(ProcessInfo process, bool isFocused) {
            bool shouldBeFocusedButMuting = isFocused && process.ShouldBeMuted;
            bool shouldBeUnfocusedButUnmuting = !isFocused && !process.ShouldBeMuted;
            return process.IsProcessingTask && (shouldBeFocusedButMuting || shouldBeUnfocusedButUnmuting);
        }

        public static bool ShouldStartTask(ProcessInfo process) {
            return process.ShouldBeMuted != process.IsMuted && !process.IsProcessingTask;
        }
    }
}
