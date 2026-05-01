using System.Diagnostics;
using WindowTool.Model;
using WindowTool.Service;

namespace WindowTool.Tests;

public class MonitorStateMachineTests {
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShouldCancelRunningTask_WhenFocusDirectionReverses(bool isFocused, bool shouldBeMuted, bool expected) {
        using var currentProcess = Process.GetCurrentProcess();
        var process = new ProcessInfo(currentProcess) {
            IsProcessingTask = true,
            ShouldBeMuted = shouldBeMuted,
        };

        bool result = MonitorStateMachine.ShouldCancelRunningTask(process, isFocused);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldCancelRunningTask_ReturnsFalse_WhenNoTaskIsRunning() {
        using var currentProcess = Process.GetCurrentProcess();
        var process = new ProcessInfo(currentProcess) {
            IsProcessingTask = false,
            ShouldBeMuted = true,
        };

        bool result = MonitorStateMachine.ShouldCancelRunningTask(process, isFocused: true);

        Assert.False(result);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    public void ShouldStartTask_WhenTargetMuteStateDiffersAndNoTaskIsRunning(
        bool shouldBeMuted,
        bool isMuted,
        bool isProcessingTask,
        bool expected
    ) {
        using var currentProcess = Process.GetCurrentProcess();
        var process = new ProcessInfo(currentProcess) {
            ShouldBeMuted = shouldBeMuted,
            IsMuted = isMuted,
            IsProcessingTask = isProcessingTask,
        };

        bool result = MonitorStateMachine.ShouldStartTask(process);

        Assert.Equal(expected, result);
    }
}
