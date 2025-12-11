using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WindowTool.Model;

namespace WindowTool.Service {
    internal static partial class ProcessHelper {
        [LibraryImport("user32.dll")]
        private static partial IntPtr GetForegroundWindow();

        [LibraryImport("user32.dll")]
        private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags
        );

        public static ProcessInfo? GetFocusWindowProcess() {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd != IntPtr.Zero) {
                GetWindowThreadProcessId(hwnd, out uint pid);
                try {
                    var process = Process.GetProcessById((int)pid);
                    return new ProcessInfo(process);
                }
                catch (Exception ex) {
                    Debug.WriteLine($"GetFocusWindowProcess failed: {ex.Message}");
                    return null;
                }
            }
            else return null;
        }

        public static List<ProcessInfo> GetAllWindowProcess() {
            return Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
            .Select(p => new ProcessInfo(p))
            .ToList();
        }

        public static bool SetTopMost(IntPtr hwnd, bool topMost) {
            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_SHOWWINDOW = 0x0040;

            IntPtr HWND_TOPMOST;
            if (topMost) HWND_TOPMOST = new IntPtr(-1);
            else HWND_TOPMOST = new IntPtr(-2);

            bool result = SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            if (!result ) {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"SetWindowPos failed with error code: {error}");
            }

            return result;
        }

    }
}
