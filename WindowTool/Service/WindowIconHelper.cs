using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowTool.Model;

namespace WindowTool.Service;

internal static partial class WindowIconHelper {
    private static readonly Dictionary<(int ProcessId, IntPtr Handle), ImageSource> IconCache = new();
    private static readonly Dictionary<(int ProcessId, IntPtr Handle), Task<ImageSource?>> PendingIconLoads = new();
    private static readonly object IconCacheLock = new();
    private static readonly SemaphoreSlim IconLoadLimit = new(4, 4);
    private const uint WmGetIcon = 0x007F;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const int IconSmall2 = 2;
    private const int GclpHIcon = -14;
    private const int GclpHIconSmall = -34;
    private const uint SmtoAbortIfHung = 0x0002;
    private const ushort VtBstr = 8;
    private const ushort VtLpwstr = 31;
    private const uint SiigbfBiggerSizeOk = 0x00000001;
    private const uint SiigbfIconOnly = 0x00000004;
    private const uint SiigbfScaleUp = 0x00000100;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint GaRoot = 2;
    private const uint GaRootOwner = 3;

    private static readonly Guid PropertyStoreInterfaceId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly Guid ShellItemImageFactoryInterfaceId = new("BCC18B79-BA16-442F-80C4-8A59C30C463B");
    private static readonly PropertyKey AppUserModelIdKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        5);

    private static ImageSource? GetIcon(int processId, IntPtr windowHandle) {
        var cacheKey = (processId, windowHandle);
        lock (IconCacheLock) {
            if (IconCache.TryGetValue(cacheKey, out ImageSource? cached)) return cached;
        }

        ImageSource? icon = GetIconCore(processId, windowHandle);
        if (icon != null) lock (IconCacheLock) {
            IconCache[cacheKey] = icon;
            if (IconCache.Count > 512) {
                foreach (var key in IconCache.Keys.Take(128).ToList()) IconCache.Remove(key);
            }
        }

        return icon;
    }

    public static Task<ImageSource?> GetIconAsync(ProcessInfo processInfo) {
        int processId = processInfo.Id;
        IntPtr windowHandle = processInfo.MainWindowHandle;
        var cacheKey = (processId, windowHandle);
        lock (IconCacheLock) {
            if (IconCache.TryGetValue(cacheKey, out ImageSource? cached)) {
                return Task.FromResult<ImageSource?>(cached);
            }

            if (PendingIconLoads.TryGetValue(cacheKey, out Task<ImageSource?>? pending)) {
                return pending;
            }

            Task<ImageSource?> loadTask = LoadIconLimitedAsync(processId, windowHandle);
            PendingIconLoads[cacheKey] = loadTask;
            _ = loadTask.ContinueWith(
                _ => {
                    lock (IconCacheLock) PendingIconLoads.Remove(cacheKey);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return loadTask;
        }
    }

    private static async Task<ImageSource?> LoadIconLimitedAsync(int processId, IntPtr windowHandle) {
        await IconLoadLimit.WaitAsync().ConfigureAwait(false);
        try {
            return await Task.Run(() => GetIcon(processId, windowHandle)).ConfigureAwait(false);
        }
        finally {
            IconLoadLimit.Release();
        }
    }

    private static ImageSource? GetIconCore(int processId, IntPtr windowHandle) {
        ImageSource? packagedAppIcon = GetPackagedAppIcon(windowHandle, processId);
        if (packagedAppIcon != null) {
            return packagedAppIcon;
        }

        foreach (IntPtr handle in GetCandidateWindowHandles(windowHandle)) {
            IntPtr iconHandle = GetWindowMessageIconHandle(handle);
            if (iconHandle != IntPtr.Zero) {
                return CreateImageSource(iconHandle);
            }

            iconHandle = GetWindowClassIconHandle(handle);
            if (iconHandle != IntPtr.Zero) {
                return CreateImageSource(iconHandle);
            }
        }

        return GetExecutableIcon(processId);
    }

    private static IEnumerable<IntPtr> GetCandidateWindowHandles(IntPtr windowHandle) {
        if (windowHandle == IntPtr.Zero) yield break;

        yield return windowHandle;
        IntPtr root = GetAncestor(windowHandle, GaRoot);
        if (root != IntPtr.Zero && root != windowHandle) yield return root;
        IntPtr rootOwner = GetAncestor(windowHandle, GaRootOwner);
        if (rootOwner != IntPtr.Zero && rootOwner != windowHandle && rootOwner != root) yield return rootOwner;
    }

    private static ImageSource? GetPackagedAppIcon(IntPtr windowHandle, int processId) {
        string? appUserModelId = GetWindowAppUserModelId(windowHandle)
            ?? GetProcessAppUserModelId(processId);
        if (string.IsNullOrWhiteSpace(appUserModelId)) return null;

        IntPtr imageFactoryPointer = IntPtr.Zero;
        IShellItemImageFactory? imageFactory = null;
        IntPtr bitmapHandle = IntPtr.Zero;

        try {
            Guid interfaceId = ShellItemImageFactoryInterfaceId;
            int result = SHCreateItemFromParsingName(
                $"shell:AppsFolder\\{appUserModelId}",
                IntPtr.Zero,
                ref interfaceId,
                out imageFactoryPointer);
            if (result < 0 || imageFactoryPointer == IntPtr.Zero) return null;

            imageFactory = (IShellItemImageFactory)Marshal.GetObjectForIUnknown(imageFactoryPointer);
            Marshal.Release(imageFactoryPointer);
            imageFactoryPointer = IntPtr.Zero;

            result = imageFactory.GetImage(
                new NativeSize(48, 48),
                SiigbfIconOnly | SiigbfBiggerSizeOk | SiigbfScaleUp,
                out bitmapHandle);
            if (result < 0 || bitmapHandle == IntPtr.Zero) return null;

            BitmapSource image = Imaging.CreateBitmapSourceFromHBitmap(
                bitmapHandle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        catch (Exception ex) {
            Debug.WriteLine($"[WindowIconHelper] Could not load packaged app icon for {appUserModelId}: {ex.Message}");
            return null;
        }
        finally {
            if (bitmapHandle != IntPtr.Zero) {
                DeleteObject(bitmapHandle);
            }

            if (imageFactory != null) {
                Marshal.FinalReleaseComObject(imageFactory);
            }

            if (imageFactoryPointer != IntPtr.Zero) {
                Marshal.Release(imageFactoryPointer);
            }
        }
    }

    private static string? GetWindowAppUserModelId(IntPtr windowHandle) {
        if (windowHandle == IntPtr.Zero) return null;

        IntPtr propertyStorePointer = IntPtr.Zero;
        IPropertyStore? propertyStore = null;
        PropVariant value = default;

        try {
            Guid interfaceId = PropertyStoreInterfaceId;
            int result = SHGetPropertyStoreForWindow(windowHandle, ref interfaceId, out propertyStorePointer);
            if (result < 0 || propertyStorePointer == IntPtr.Zero) return null;

            propertyStore = (IPropertyStore)Marshal.GetObjectForIUnknown(propertyStorePointer);
            Marshal.Release(propertyStorePointer);
            propertyStorePointer = IntPtr.Zero;

            PropertyKey key = AppUserModelIdKey;
            result = propertyStore.GetValue(ref key, out value);
            return result < 0 ? null : value.GetString();
        }
        catch (Exception ex) {
            Debug.WriteLine($"[WindowIconHelper] Could not resolve AppUserModelID: {ex.Message}");
            return null;
        }
        finally {
            PropVariantClear(ref value);

            if (propertyStore != null) {
                Marshal.FinalReleaseComObject(propertyStore);
            }

            if (propertyStorePointer != IntPtr.Zero) {
                Marshal.Release(propertyStorePointer);
            }
        }
    }

    private static string? GetProcessAppUserModelId(int processId) {
        IntPtr processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (processHandle == IntPtr.Zero) return null;

        try {
            uint characterCount = 0;
            _ = GetApplicationUserModelId(processHandle, ref characterCount, null);
            if (characterCount == 0) return null;

            var appUserModelId = new StringBuilder((int)characterCount);
            int result = GetApplicationUserModelId(processHandle, ref characterCount, appUserModelId);
            return result == 0 ? appUserModelId.ToString() : null;
        }
        catch (Exception ex) {
            Debug.WriteLine($"[WindowIconHelper] Could not resolve process AppUserModelID for PID {processId}: {ex.Message}");
            return null;
        }
        finally {
            CloseHandle(processHandle);
        }
    }

    private static IntPtr GetWindowMessageIconHandle(IntPtr windowHandle) {
        if (windowHandle == IntPtr.Zero) return IntPtr.Zero;

        foreach (int iconType in new[] { IconSmall2, IconSmall, IconBig }) {
            IntPtr result = SendMessageTimeout(
                windowHandle,
                WmGetIcon,
                new IntPtr(iconType),
                IntPtr.Zero,
                SmtoAbortIfHung,
                100,
                out IntPtr iconHandle);

            if (result != IntPtr.Zero && iconHandle != IntPtr.Zero) {
                return iconHandle;
            }
        }

        return IntPtr.Zero;
    }

    private static IntPtr GetWindowClassIconHandle(IntPtr windowHandle) {
        if (windowHandle == IntPtr.Zero) return IntPtr.Zero;

        IntPtr classIcon = IntPtr.Size == 8
            ? GetClassLongPtr(windowHandle, GclpHIconSmall)
            : new IntPtr(GetClassLong(windowHandle, GclpHIconSmall));
        if (classIcon != IntPtr.Zero) return classIcon;

        return IntPtr.Size == 8
            ? GetClassLongPtr(windowHandle, GclpHIcon)
            : new IntPtr(GetClassLong(windowHandle, GclpHIcon));
    }

    private static ImageSource? GetExecutableIcon(int processId) {
        try {
            string? executablePath = GetExecutablePath(processId);
            if (string.IsNullOrWhiteSpace(executablePath)) return null;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
            return icon == null ? null : CreateImageSource(icon.Handle);
        }
        catch (Exception ex) {
            Debug.WriteLine($"[WindowIconHelper] Could not load icon for PID {processId}: {ex.Message}");
            return null;
        }
    }

    private static string? GetExecutablePath(int processId) {
        IntPtr processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (processHandle != IntPtr.Zero) {
            try {
                uint capacity = 32768;
                var path = new StringBuilder((int)capacity);
                if (QueryFullProcessImageName(processHandle, 0, path, ref capacity)) {
                    return path.ToString();
                }
            }
            finally {
                CloseHandle(processHandle);
            }
        }

        try {
            using var process = Process.GetProcessById(processId);
            return process.MainModule?.FileName;
        }
        catch {
            return null;
        }
    }

    private static ImageSource? CreateImageSource(IntPtr iconHandle) {
        try {
            BitmapSource image = Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(24, 24));
            image.Freeze();
            return image;
        }
        catch (Exception ex) {
            Debug.WriteLine($"[WindowIconHelper] Could not convert icon: {ex.Message}");
            return null;
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial IntPtr SendMessageTimeout(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);

    [LibraryImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true)]
    private static partial IntPtr GetClassLongPtr(IntPtr windowHandle, int index);

    [LibraryImport("user32.dll", EntryPoint = "GetClassLongW", SetLastError = true)]
    private static partial int GetClassLong(IntPtr windowHandle, int index);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetAncestor(IntPtr windowHandle, uint flags);

    [LibraryImport("shell32.dll")]
    private static partial int SHGetPropertyStoreForWindow(
        IntPtr windowHandle,
        ref Guid interfaceId,
        out IntPtr propertyStore);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHCreateItemFromParsingName(
        string path,
        IntPtr bindingContext,
        ref Guid interfaceId,
        out IntPtr shellItem);

    [LibraryImport("ole32.dll")]
    private static partial int PropVariantClear(ref PropVariant value);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr objectHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        IntPtr processHandle,
        uint flags,
        StringBuilder executablePath,
        ref uint characterCount);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(
        IntPtr processHandle,
        ref uint applicationUserModelIdLength,
        StringBuilder? applicationUserModelId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr objectHandle);

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore {
        [PreserveSig]
        int GetCount(out uint propertyCount);

        [PreserveSig]
        int GetAt(uint propertyIndex, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory {
        [PreserveSig]
        int GetImage(NativeSize size, uint flags, out IntPtr bitmapHandle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey(Guid formatId, uint propertyId) {
        public readonly Guid FormatId = formatId;
        public readonly uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize(int width, int height) {
        public readonly int Width = width;
        public readonly int Height = height;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant {
        [FieldOffset(0)]
        private ushort _valueType;

        [FieldOffset(8)]
        private IntPtr _pointerValue;

        public readonly string? GetString() {
            return _valueType switch {
                VtBstr => Marshal.PtrToStringBSTR(_pointerValue),
                VtLpwstr => Marshal.PtrToStringUni(_pointerValue),
                _ => null
            };
        }
    }
}
