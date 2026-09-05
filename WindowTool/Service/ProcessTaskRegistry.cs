using System.Collections.Concurrent;

namespace WindowTool.Service;

internal sealed class ProcessTaskRegistry {
    internal sealed record Entry(Task Task, CancellationTokenSource Cancellation);

    private readonly ConcurrentDictionary<int, Entry> _entries = new();

    public bool Contains(int processId) => _entries.ContainsKey(processId);

    public Entry Register(int processId, Task task, CancellationTokenSource cancellation) {
        var entry = new Entry(task, cancellation);
        _entries[processId] = entry;
        return entry;
    }

    public bool TryTake(int processId, out Entry? entry) {
        bool removed = _entries.TryRemove(processId, out var value);
        entry = value;
        return removed;
    }

    public bool TryTake(int processId, Entry expected) {
        var pair = new KeyValuePair<int, Entry>(processId, expected);
        return ((ICollection<KeyValuePair<int, Entry>>)_entries).Remove(pair);
    }

    public KeyValuePair<int, Entry>[] Snapshot() => _entries.ToArray();
}
