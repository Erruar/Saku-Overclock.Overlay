using System.Runtime.InteropServices;

namespace Saku_Overclock.Overlay.Helpers;

#region Structs

[StructLayout(LayoutKind.Sequential)]
internal struct Wndclassex
{
    public uint cbSize;
    public uint style;
    public nint lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public nint hInstance;
    public nint hIcon;
    public nint hCursor;
    public nint hbrBackground;
    public nint lpszMenuName;
    public nint lpszClassName;
    public nint hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Point { public int X, Y; }

[StructLayout(LayoutKind.Sequential)]
internal struct Size { public int CX, CY; }

[StructLayout(LayoutKind.Sequential)]
internal struct Rect { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential)]
internal struct Msg
{
    public nint hwnd;
    public uint message;
    public nuint wParam;
    public nint lParam;
    public uint time;
    public Point pt;
    public uint lPrivate;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Monitorinfo
{
    public uint cbSize;
    public Rect rcMonitor;
    public Rect rcWork;
    public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Blendfunction
{
    public byte BlendOp;
    public byte BlendFlags;
    public byte SourceConstantAlpha;
    public byte AlphaFormat;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Bitmapinfoheader
{
    public uint biSize;
    public int biWidth;
    public int biHeight;
    public ushort biPlanes;
    public ushort biBitCount;
    public uint biCompression;
    public uint biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public uint biClrUsed;
    public uint biClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Notifyicondataw
{
    public uint cbSize;
    public nint hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public nint hIcon;
    public fixed char szTip[128];
    public uint dwState;
    public uint dwStateMask;
    public fixed char szInfo[256];
    public uint uVersion;
    public fixed char szInfoTitle[64];
    public uint dwInfoFlags;
    public Guid guidItem;
    public nint hBalloonIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ProcessPowerThrottlingState
{
    public uint Version;
    public uint ControlMask;
    public uint StateMask;
}

#endregion

internal static unsafe partial class Win32
{
    #region Constants

    public const uint WmDestroy = 0x0002;
    public const uint WmClose = 0x0010;
    public const uint WmEndsession = 0x0016;
    public const uint WmTimer = 0x0113;
    public const uint WmHotkey = 0x0312;
    public const uint WmApp = 0x8000;

    public const uint WsPopup = 0x80000000;
    public const uint WsExTopmost = 0x00000008;
    public const uint WsExTransparent = 0x00000020;
    public const uint WsExToolwindow = 0x00000080;
    public const uint WsExLayered = 0x00080000;
    public const uint WsExNoactivate = 0x08000000;

    public const int SwHide = 0;
    public const int SwShownoactivate = 4;

    public const uint SwpNosize = 0x0001;
    public const uint SwpNomove = 0x0002;
    public const uint SwpNoactivate = 0x0010;
    public static readonly nint HwndTopmost = -1;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModNorepeat = 0x4000;

    public const uint MonitorDefaulttonearest = 2;
    public const uint MonitorDefaulttoprimary = 1;

    public const uint UlwAlpha = 0x00000002;
    public const byte AcSrcOver = 0;
    public const byte AcSrcAlpha = 1;

    public const uint NimAdd = 0, NimModify = 1, NimDelete = 2;
    public const uint NifIcon = 0x02, NifTip = 0x04, NifGuid = 0x20, NifShowtip = 0x80;

    public const uint WmPowerbroadcast = 0x0218;
    public const uint PbtApmsuspend = 0x0004;
    public const uint PbtApmresumesuspend = 0x0007;
    public const uint PbtApmresumeautomatic = 0x0012;
    
    #endregion

    #region user32
    
    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindow(string className, string? windowName);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    public static partial ushort RegisterClassEx(in Wndclassex wc);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowEx(uint exStyle, string className, string? windowName, uint style,
        int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial nint DefWindowProc(nint hwnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    private static partial int GetMessage(out Msg msg, nint hwnd, uint min, uint max);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static partial nint DispatchMessage(in Msg msg);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessage(nint hwnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hwnd, int id);

    [LibraryImport("user32.dll")]
    public static partial nuint SetTimer(nint hwnd, nuint id, uint elapseMs, nint proc);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool KillTimer(nint hwnd, nuint id);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hwnd, int cmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateLayeredWindow(nint hwnd, nint hdcDst, in Point pptDst, in Size psize,
        nint hdcSrc, in Point pptSrc, uint crKey, in Blendfunction pblend, uint flags);

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitor, ref Monitorinfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint hIcon);

    #endregion

    #region shell32 / shcore / gdi32 / kernel32

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_NotifyIcon(uint message, ref Notifyicondataw data);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll", EntryPoint = "DeleteDC")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDc(nint hdc);

    [LibraryImport("gdi32.dll", EntryPoint = "CreateDIBSection")]
    public static partial nint CreateDibSection(nint hdc, in Bitmapinfoheader bmi, uint usage, out nint bits,
        nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint obj);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW")]
    public static partial nint GetModuleHandle(nint name);

    [LibraryImport("kernel32.dll")]
    public static partial ushort GetUserDefaultUILanguage();

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessInformation(nint process, int infoClass,
        in ProcessPowerThrottlingState info, uint size);

    #endregion

    /// <summary>
    ///     EcoQoS ("effeciency mode"): running overlay process on Effective cores (if applicable)
    /// </summary>
    public static void EnableEcoQos()
    {
        var state = new ProcessPowerThrottlingState { Version = 1, ControlMask = 1, StateMask = 1 };
        _ = SetProcessInformation(GetCurrentProcess(), 4, in state, (uint)sizeof(ProcessPowerThrottlingState));
    }

    /// <summary>
    ///     UI-message loop. Lock in GetMessage before WM_QUIT
    /// </summary>
    public static int RunMessageLoop()
    {
        Msg msg;
        while (GetMessage(out msg, 0, 0, 0) > 0)
            DispatchMessage(in msg);

        return (int)msg.wParam;
    }
}