using WindowTool.Service;

namespace WindowTool.Tests;

public class ProcessTaskRegistryTests {
    [Fact]
    public void CompletedOldTask_CannotRemoveReplacementTask() {
        const int processId = 42;
        var registry = new ProcessTaskRegistry();
        using var firstCancellation = new CancellationTokenSource();
        using var replacementCancellation = new CancellationTokenSource();

        var first = registry.Register(processId, Task.CompletedTask, firstCancellation);
        var replacement = registry.Register(processId, Task.CompletedTask, replacementCancellation);

        Assert.False(registry.TryTake(processId, first));
        Assert.True(registry.Contains(processId));
        Assert.True(registry.TryTake(processId, replacement));
        Assert.False(registry.Contains(processId));
    }
}
