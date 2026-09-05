using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WindowTool.Model;

namespace WindowTool.Service;

internal static partial class TopMostStateManager {
    private readonly record struct WindowIdentity(IntPtr Handle, uint ProcessId);

    private static readonly ConcurrentDictionary<WindowIdentity, bool> OriginalStates = new();

    public static bool Apply(ProcessInfo process, bool shouldBeTopMost) {
        if (!process.Refresh() || process.MainWindowHandle == IntPtr.Zero) return false;

        IntPtr handle = process.MainWindowHandle;
        uint processId = GetWindowProcessId(handle);
        if (processId == 0) return false;

        var identity = new WindowIdentity(handle, processId);
        bool currentTopMost = ProcessHelper.IsTopMost(handle);
        process.IsTopMost = currentTopMost;

        bool hasOriginalState = OriginalStates.TryGetValue(identity, out bool originalTopMost);
        if (currentTopMost != shouldBeTopMost) {
            if (!hasOriginalState) {
                originalTopMost = currentTopMost;
                OriginalStates.TryAdd(identity, originalTopMost);
                hasOriginalState = true;
            }

            if (!ProcessHelper.SetTopMost(handle, shouldBeTopMost)) {
                if (currentTopMost == originalTopMost) OriginalStates.TryRemove(identity, out _);
                return false;
            }

            currentTopMost = shouldBeTopMost;
        }

        // Once the window is back at the state it had before WindowTool's
        // first change, it no longer needs to be restored during shutdown.
        if (hasOriginalState && currentTopMost == originalTopMost) {
            OriginalStates.TryRemove(identity, out _);
            hasOriginalState = false;
        }

        process.OriginalTopMost = originalTopMost;
        process.HasOriginalTopMost = hasOriginalState;
        process.TopMostManagedByTool = hasOriginalState;
        process.IsTopMost = currentTopMost;
        return true;
    }

    public static int RestoreAll() {
        int failureCount = 0;
        foreach (var entry in OriginalStates.ToArray()) {
            try {
                if (!IsWindow(entry.Key.Handle)
                    || GetWindowProcessId(entry.Key.Handle) != entry.Key.ProcessId) {
                    OriginalStates.TryRemove(entry.Key, out _);
                    continue;
                }

                if (ProcessHelper.IsTopMost(entry.Key.Handle) != entry.Value
                    && !ProcessHelper.SetTopMost(entry.Key.Handle, entry.Value)) {
                    failureCount++;
                    continue;
                }

                OriginalStates.TryRemove(entry.Key, out _);
            }
            catch (Exception ex) {
                failureCount++;
                Debug.WriteLine($"[TopMostRestore] Failed for HWND {entry.Key.Handle}: {ex.Message}");
            }
        }

        return failureCount;
    }

    private static uint GetWindowProcessId(IntPtr handle) {
        _ = GetWindowThreadProcessId(handle, out uint processId);
        return processId;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(IntPtr windowHandle);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}
