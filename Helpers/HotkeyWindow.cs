using System.Runtime.InteropServices;

namespace Saku_Overclock.Overlay.Helpers;

public class HotkeyWindow : IDisposable
{
    private const string ClassName = "SakuOverlayHotkeySink";

    public event Action<int>? HotkeyPressed;

    public nint Handle { get; }

    public HotkeyWindow()
    {
        var hInstance = Win32.GetModuleHandle(0);

        WndProcDelegate wndProc = WndProc;
        var wndProcPtr = Marshal.GetFunctionPointerForDelegate(wndProc);
        GC.KeepAlive(wndProc);

        var wc = new Wndclassex
        {
            cbSize = (uint)Marshal.SizeOf<Wndclassex>(),
            lpfnWndProc = wndProcPtr,
            hInstance = hInstance,
            lpszClassName = Marshal.StringToHGlobalUni(ClassName)
        };

        var atom = Win32.RegisterClassEx(in wc);
        if (atom == 0)
            throw new InvalidOperationException(
                $"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");

        Handle = Win32.CreateWindowEx(
            0, ClassName, null, 0, 0, 0, 0, 0, -3, 0, hInstance, 0);

        if (Handle == 0)
            throw new InvalidOperationException(
                $"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
    }

    private nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (msg == Win32.WmHotkey)
        {
            HotkeyPressed?.Invoke((int)wParam);
            return 0;
        }
        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (Handle != 0)
            Win32.DestroyWindow(Handle);
    }

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nuint wParam, nint lParam);
}