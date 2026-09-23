using System.Runtime.InteropServices;

namespace Saku_Overclock.Overlay;

internal static partial class NativeMethods
{
    private const string User32 = "user32.dll";
    private const string Shell32 = "shell32.dll";
    private const string Kernel32 = "kernel32.dll";

    [LibraryImport(User32, SetLastError = true)]
    internal static partial ushort RegisterClassExW(in Wndclassexw lpwcx);

    [LibraryImport(User32, EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [LibraryImport(User32)]
    internal static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport(User32)]
    internal static partial void PostQuitMessage(int nExitCode);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint hWnd);

    [LibraryImport(User32, SetLastError = true)]
    internal static partial int GetMessageW(out Msg lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateMessage(in Msg lpMsg);

    [LibraryImport(User32)]
    internal static partial nint DispatchMessageW(in Msg lpMsg);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport(Shell32, EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool Shell_NotifyIconW(uint dwMessage, ref Notifyicondataw lpData);

    [LibraryImport(Kernel32)]
    internal static partial nint GetModuleHandleW(nint lpModuleName);
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Wndclassexw
{
    public uint cbSize;
    public uint style;
    public delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint> lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public nint hInstance;
    public nint hIcon;
    public nint hCursor;
    public nint hbrBackground;
    public char* lpszMenuName;
    public char* lpszClassName;
    public nint hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Point { public int X, Y; }

[StructLayout(LayoutKind.Sequential)]
internal struct Msg
{
    public nint hwnd;
    public uint message;
    public nint wParam;
    public nint lParam;
    public uint time;
    public Point pt;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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
    public uint uVersionOrTimeout;
    public fixed char szInfoTitle[64];
    public uint dwInfoFlags;
    public Guid guidItem;
    public nint hBalloonIcon;
}