using System.Diagnostics;
using System.Runtime.InteropServices;
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

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        );

        [LibraryImport("user32.dll")]
        private static partial IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags
        );

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnhookWinEvent(IntPtr hWinEventHook);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOPMOST = 0x00000008;

        private static IntPtr _hookHandle = IntPtr.Zero;
        private static WinEventDelegate? _hookDelegate;

        public static event EventHandler<ProcessInfo?>? FocusWindowChanged;

        public static void StartFocusWindowMonitoring() {
            if (_hookHandle != IntPtr.Zero) {
                Debug.WriteLine("Already monitoring focus window");
                return;
            }

            _hookDelegate = new WinEventDelegate(WinEventProc);
            _hookHandle = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _hookDelegate,
                0,
                0,
                WINEVENT_OUTOFCONTEXT
            );

            if (_hookHandle == IntPtr.Zero) {
                Debug.WriteLine("Failed to set WinEvent hook");
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Error code: {error}");
            }
            else {
                Debug.WriteLine($"Successfully set hook: {_hookHandle}");
            }
        }

        public static void StopFocusWindowMonitoring() {
            if (_hookHandle == IntPtr.Zero) return;

            bool success = UnhookWinEvent(_hookHandle);
            Debug.WriteLine(success ? "Successfully unhooked" : "Failed to unhook");
            _hookHandle = IntPtr.Zero;
            _hookDelegate = null;
        }

        private static void WinEventProc(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        ) {
            if (eventType != EVENT_SYSTEM_FOREGROUND || hwnd == IntPtr.Zero) return;

            var processInfo = GetProcessInfoByWindowHandle(hwnd);
            if (processInfo != null) {
                FocusWindowChanged?.Invoke(null, processInfo);
            }
            else {
                Debug.WriteLine("Failed to get process info");
            }
        }

        private static ProcessInfo? GetProcessInfoByWindowHandle(IntPtr hwnd) {
            GetWindowThreadProcessId(hwnd, out uint pid);

            try {
                using var process = Process.GetProcessById((int)pid);
                return new ProcessInfo(process);
            }
            catch (ArgumentException) {
                Debug.WriteLine($"Process {pid} not found");
                return null;
            }
            catch (Exception ex) {
                Debug.WriteLine($"GetProcessInfoByWindowHandle failed: {ex.Message}");
                return null;
            }
        }

        public static ProcessInfo? GetFocusWindowProcess() {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            GetWindowThreadProcessId(hwnd, out uint pid);
            try {
                using var process = Process.GetProcessById((int)pid);
                return new ProcessInfo(process);
            }
            catch (Exception ex) {
                Debug.WriteLine($"GetFocusWindowProcess failed: {ex.Message}");
                return null;
            }
        }

        public static List<ProcessInfo> GetAllWindowProcess() {
            var processInfos = new List<ProcessInfo>();
            foreach (var process in Process.GetProcesses()) {
                using (process) {
                    try {
                        if (!string.IsNullOrEmpty(process.MainWindowTitle)) {
                            processInfos.Add(new ProcessInfo(process));
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine($"GetAllWindowProcess skipped PID {SafeProcessId(process)}: {ex.Message}");
                    }
                }
            }

            var nativeWindowTitles = processInfos
                .Where(process => !IsApplicationFrameHost(process))
                .Select(process => process.MainWindowTitle)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

            return processInfos
                .Where(process => !IsApplicationFrameHost(process)
                    || !nativeWindowTitles.Contains(process.MainWindowTitle))
                .ToList();
        }

        private static bool IsApplicationFrameHost(ProcessInfo process) {
            return string.Equals(
                process.Name,
                "ApplicationFrameHost",
                StringComparison.OrdinalIgnoreCase);
        }

        private static int SafeProcessId(Process process) {
            try {
                return process.Id;
            }
            catch {
                return -1;
            }
        }

        public static bool SetTopMost(IntPtr hwnd, bool topMost) {
            if (hwnd == IntPtr.Zero) return false;

            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_SHOWWINDOW = 0x0040;
            const uint SWP_NOACTIVATE = 0x0010;

            IntPtr hwndInsertAfter = topMost ? new IntPtr(-1) : new IntPtr(-2);

            bool result = SetWindowPos(
                hwnd,
                hwndInsertAfter,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
            if (!result) {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"SetWindowPos failed with error code: {error}");
                return false;
            }

            bool applied = IsTopMost(hwnd) == topMost;
            if (!applied) {
                Debug.WriteLine($"SetTopMost did not apply the requested state ({topMost}) for HWND {hwnd}.");
            }

            return applied;
        }

        public static bool IsTopMost(IntPtr hwnd) {
            if (hwnd == IntPtr.Zero) return false;

            long extendedStyle = IntPtr.Size == 8
                ? GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64()
                : GetWindowLong(hwnd, GWL_EXSTYLE);

            return (extendedStyle & WS_EX_TOPMOST) == WS_EX_TOPMOST;
        }
    }
}
