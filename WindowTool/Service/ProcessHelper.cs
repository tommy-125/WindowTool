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

        /// <summary>
        /// 定義回調函式的委派型別
        /// </summary>
        /// <param name="hWinEventHook"></param>
        /// <param name="eventType"></param>
        /// <param name="hwnd"></param>
        /// <param name="idObject"></param>
        /// <param name="idChild"></param>
        /// <param name="dwEventThread"></param>
        /// <param name="dwmsEventTime"></param>
        // 這是 Windows 會呼叫的函式簽章
        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,     // Hook 句柄
            uint eventType,           // 事件類型
            IntPtr hwnd,              // 視窗句柄
            int idObject,             // 物件 ID
            int idChild,              // 子物件 ID
            uint dwEventThread,       // 執行緒 ID
            uint dwmsEventTime        // 時間戳記
        );

        /// <summary>
        /// 宣告 SetWinEventHook API
        /// </summary>
        /// <param name="eventMin"></param>
        /// <param name="eventMax"></param>
        /// <param name="hmodWinEventProc"></param>
        /// <param name="lpfnWinEventProc"></param>
        /// <param name="idProcess"></param>
        /// <param name="idThread"></param>
        /// <param name="dwFlags"></param>
        /// <returns></returns>
        // 用來向 Windows 註冊 Hook
        [LibraryImport("user32.dll")]
        private static partial IntPtr SetWinEventHook(
            uint eventMin,                          // 最小事件代碼
            uint eventMax,                          // 最大事件代碼
            IntPtr hmodWinEventProc,                // DLL 句柄
            WinEventDelegate lpfnWinEventProc,     // 回調函式
            uint idProcess,                         // 進程 ID
            uint idThread,                          // 執行緒 ID
            uint dwFlags                            // 旗標
        );

        /// <summary>
        /// 宣告 UnhookWinEvent API
        /// </summary>
        /// <param name="hWinEventHook"></param>
        /// <returns></returns>
        // 用來取消 Hook
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnhookWinEvent(IntPtr hWinEventHook);

        /// <summary>
        /// 定義常數
        /// </summary>
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;  // 焦點改變事件
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;    // 不注入 DLL

        /// <summary>
        /// 儲存 Hook 相關資料
        /// </summary>
        private static IntPtr _hookHandle = IntPtr.Zero;      // Hook 句柄（用來取消 Hook）
        private static WinEventDelegate? _hookDelegate;       // 保持 delegate 存活（防止被 GC 回收）

        /// <summary>
        /// 定義事件：讓其他類別可以訂閱焦點改變
        /// </summary>
        public static event EventHandler<ProcessInfo?>? FocusWindowChanged;

        /// <summary>
        /// 啟動監聽的方法
        /// </summary>
        public static void StartFocusWindowMonitoring() {
            // 檢查是否已經在監聽
            if (_hookHandle != IntPtr.Zero) {
                Debug.WriteLine("Already monitoring focus window");
                return;
            }

            // 建立 delegate 實例（必須保持引用，否則會被 GC 回收）
            _hookDelegate = new WinEventDelegate(WinEventProc);

            // 向 Windows 註冊 Hook
            _hookHandle = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,     // eventMin: 只監聽焦點改變
                EVENT_SYSTEM_FOREGROUND,     // eventMax: 只監聽焦點改變
                IntPtr.Zero,                 // hmodWinEventProc: 不使用 DLL
                _hookDelegate,               // lpfnWinEventProc: 回調函式
                0,                           // idProcess: 監聽所有進程
                0,                           // idThread: 監聽所有執行緒
                WINEVENT_OUTOFCONTEXT        // dwFlags: Out-of-context 模式
            );

            // 檢查是否成功註冊
            if (_hookHandle == IntPtr.Zero) {
                Debug.WriteLine("Failed to set WinEvent hook");
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Error code: {error}");
            }
            else {
                Debug.WriteLine($"Successfully set hook: {_hookHandle}");
            }
        }

        /// <summary>
        /// 停止監聽的方法
        /// </summary>
        public static void StopFocusWindowMonitoring() {
            if (_hookHandle != IntPtr.Zero) {
                bool success = UnhookWinEvent(_hookHandle);
                if (success) {
                    Debug.WriteLine("Successfully unhooked");
                }
                else {
                    Debug.WriteLine("Failed to unhook");
                }
                _hookHandle = IntPtr.Zero;
                _hookDelegate = null;
            }
        }

        /// <summary>
        /// 回調函式：Windows 會在焦點改變時呼叫此方法
        /// </summary>
        /// <param name="hWinEventHook"></param>
        /// <param name="eventType"></param>
        /// <param name="hwnd"></param>
        /// <param name="idObject"></param>
        /// <param name="idChild"></param>
        /// <param name="dwEventThread"></param>
        /// <param name="dwmsEventTime"></param>
        private static void WinEventProc(
            IntPtr hWinEventHook,      // Hook 句柄
            uint eventType,            // 事件類型
            IntPtr hwnd,               // 視窗句柄
            int idObject,              // 物件 ID
            int idChild,               // 子物件 ID
            uint dwEventThread,        // 執行緒 ID
            uint dwmsEventTime)        // 時間戳記
        {
            // 確認是焦點改變事件且視窗句柄有效
            if (eventType == EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero) {

                // 根據視窗句柄取得進程資訊
                var processInfo = GetProcessInfoByWindowHandle(hwnd);

                if (processInfo != null) {

                    // 觸發事件，通知所有訂閱者
                    FocusWindowChanged?.Invoke(null, processInfo);
                }
                else {
                    Debug.WriteLine("Failed to get process info");
                }
            }
        }

        /// <summary>
        /// 根據視窗句柄取得進程資訊
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        private static ProcessInfo? GetProcessInfoByWindowHandle(IntPtr hwnd) {
            // 取得視窗所屬的進程 ID
            GetWindowThreadProcessId(hwnd, out uint pid);

            try {
                // 根據 PID 取得 Process 物件
                var process = Process.GetProcessById((int)pid);

                // 轉換成 ProcessInfo
                return new ProcessInfo(process);
            }
            catch (ArgumentException) {
                // 進程不存在或已結束
                Debug.WriteLine($"Process {pid} not found");
                return null;
            }
            catch (Exception ex) {
                Debug.WriteLine($"GetProcessInfoByWindowHandle failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 獲取焦點視窗Process
        /// </summary>
        /// <returns></returns>
        public static ProcessInfo? GetFocusWindowProcess() {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd != IntPtr.Zero) {
                GetWindowThreadProcessId(hwnd, out uint pid);
                try {
                    var process = Process.GetProcessById((int)pid);
                    return new ProcessInfo(process);
                } catch (Exception ex) {
                    Debug.WriteLine($"GetFocusWindowProcess failed: {ex.Message}");
                    return null;
                }
            }
            else return null;
        }

        /// <summary>
        /// 獲取全部的視窗進程
        /// </summary>
        /// <returns></returns>
        public static List<ProcessInfo> GetAllWindowProcess() {
            return Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
            .Select(p => new ProcessInfo(p))
            .ToList();
        }

        /// <summary>
        /// 置頂視窗邏輯
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="topMost"></param>
        /// <returns></returns>
        public static bool 
            SetTopMost(IntPtr hwnd, bool topMost) {
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
