using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowTool.Service;

internal static class ProcessTreeHelper {
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public static HashSet<int> GetProcessGroupIds(int rootProcessId) {
        IntPtr snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == InvalidHandleValue) {
            Debug.WriteLine($"[ProcessTreeHelper] Could not create process snapshot: {Marshal.GetLastWin32Error()}");
            return [rootProcessId];
        }

        try {
            var relationships = new List<ProcessRelationship>();
            var entry = new ProcessEntry32 {
                Size = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            if (Process32First(snapshot, ref entry)) {
                do {
                    relationships.Add(new ProcessRelationship(
                        (int)entry.ProcessId,
                        (int)entry.ParentProcessId));
                    entry.Size = (uint)Marshal.SizeOf<ProcessEntry32>();
                }
                while (Process32Next(snapshot, ref entry));
            }

            return CollectProcessGroupIds(rootProcessId, relationships);
        }
        finally {
            CloseHandle(snapshot);
        }
    }

    internal static HashSet<int> CollectProcessGroupIds(
        int rootProcessId,
        IEnumerable<ProcessRelationship> relationships) {
        var childrenByParent = relationships
            .GroupBy(relationship => relationship.ParentProcessId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(relationship => relationship.ProcessId).ToArray());

        var processIds = new HashSet<int> { rootProcessId };
        var pending = new Queue<int>();
        pending.Enqueue(rootProcessId);

        while (pending.TryDequeue(out int parentProcessId)) {
            if (!childrenByParent.TryGetValue(parentProcessId, out int[]? childProcessIds)) continue;

            foreach (int childProcessId in childProcessIds) {
                if (processIds.Add(childProcessId)) {
                    pending.Enqueue(childProcessId);
                }
            }
        }

        return processIds;
    }

    internal readonly record struct ProcessRelationship(int ProcessId, int ParentProcessId);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32 {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr objectHandle);
}
