using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Saku_Overclock.Overlay;

internal static unsafe class App
{
    private const uint WmDestroy = 0x0002;
    private const uint WmPowerbroadcast = 0x0218;
    private const uint WmHotkey = 0x0312;
    private const uint WmApp = 0x8000;
    private const uint WmTraycallback = WmApp + 1;

    private const int PbtApmsuspend = 0x0004;
    private const int PbtApmresumesuspend = 0x0007;
    // deliberately NOT handling PBT_APMRESUMEAUTOMATIC -- it fires before the
    // session is fully restored, re-adding icon there is a race with Explorer.

    private const uint NimAdd = 0x0;
    private const uint NimDelete = 0x2;
    private const uint NifMessage = 0x1;
    private const uint NifTip = 0x4;
    private const uint NifGuid = 0x20;

    private static readonly Guid TrayIconGuid = new("6D0F0A1E-6D6C-4B7B-9D2E-1B0B0B5E9A11");

    private static nint _hwnd;
    private static bool _trayIconPresent;

    private static int Main(string[] args)
    {
        // TODO: args[0] is expected to be IPC endpoint name service
        // passed in via CreateProcessAsUser's command line, wire up the
        // pipe/contract client here once shared contracts project is referenced.

        RegisterAndCreateWindow();
        RegisterHotkeys();
        AddTrayIcon();

        int result;
        while ((result = NativeMethods.GetMessageW(out var msg, 0, 0, 0)) != 0)
        {
            if (result == -1) break; // GetMessage failed 
            NativeMethods.TranslateMessage(in msg);
            NativeMethods.DispatchMessageW(in msg);
        }

        return 0;
    }

    private static void RegisterAndCreateWindow()
    {
        const string className = "SakuOverclockOverlayWnd";

        fixed (char* classNamePtr = className)
        {
            var wc = new Wndclassexw
            {
                cbSize = (uint)sizeof(Wndclassexw),
                lpfnWndProc = &WndProc,
                hInstance = NativeMethods.GetModuleHandleW(0),
                lpszClassName = classNamePtr
            };
            NativeMethods.RegisterClassExW(in wc);
        }

        const uint wsExLayered = 0x00080000;
        const uint wsExToolwindow = 0x00000080; // no taskbar entry
        const uint wsExTopmost = 0x00000008;
        const uint wsExNoactivate = 0x08000000; // never steals focus from game/app under it
        const uint wsPopup = 0x80000000;

        _hwnd = NativeMethods.CreateWindowExW(
            wsExLayered | wsExToolwindow | wsExTopmost | wsExNoactivate,
            className, "Saku Overclock Overlay", wsPopup,
            0, 0, 420, 120,
            0, 0, NativeMethods.GetModuleHandleW(0), 0);

        // deliberately not calling ShowWindow here stays hidden until
        // a preset switch (WM_HOTKEY below) triggers fade-in.
    }

    private static void RegisterHotkeys()
    {
        // TODO: read real combo(s) from persisted preset-switch config
        // via IPC/shared config instead of this placeholder (Ctrl+Alt+F1),
        // and register one id per configured preset.
        const uint modControl = 0x0002, modAlt = 0x0001;
        const uint vkF1 = 0x70;
        NativeMethods.RegisterHotKey(_hwnd, 1, modControl | modAlt, vkF1);
    }

    private static void AddTrayIcon()
    {
        var nid = new Notifyicondataw
        {
            cbSize = (uint)Marshal.SizeOf<Notifyicondataw>(),
            hWnd = _hwnd,
            uFlags = NifMessage | NifTip | NifGuid,
            uCallbackMessage = WmTraycallback,
            guidItem = TrayIconGuid
            // TODO: NIF_ICON + hIcon once TrayMon sensor-readout icon
            // rendering is ported over from client
        };
        SetTip(ref nid, "Saku Overclock");
        _trayIconPresent = NativeMethods.Shell_NotifyIconW(NimAdd, ref nid);
    }

    private static void RemoveTrayIcon()
    {
        if (!_trayIconPresent) return;
        var nid = new Notifyicondataw
        {
            cbSize = (uint)Marshal.SizeOf<Notifyicondataw>(),
            hWnd = _hwnd,
            uFlags = NifGuid,
            guidItem = TrayIconGuid
        };
        NativeMethods.Shell_NotifyIconW(NimDelete, ref nid);
        _trayIconPresent = false;
    }

    private static void SetTip(ref Notifyicondataw nid, string tip)
    {
        fixed (char* dst = nid.szTip)
        {
            var span = new Span<char>(dst, 128);
            var len = Math.Min(tip.Length, 127);
            tip.AsSpan(0, len).CopyTo(span);
            span[len] = '\0';
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WmPowerbroadcast:
                switch ((int)wParam)
                {
                    case PbtApmsuspend:
                        // remove icon before sleep 
                        RemoveTrayIcon();
                        break;
                    case PbtApmresumesuspend:
                        AddTrayIcon();
                        // TODO: if a D3D11/DirectComposition device is alive at this point, drop reference 
                        // it's invalid post-resume (DXGI_ERROR_DEVICE_REMOVED/RESET)
                        // Recreating it lazily on next popup show is enough,
                        // no need to eagerly reinitialize it here
                        break;
                }
                return 1; 

            case WmTraycallback:
                // TODO: low word of lParam carries mouse event
                // (WM_LBUTTONUP / WM_RBUTTONUP etc.) wire up a context menu
                break;

            case WmHotkey:
                // TODO: (int)wParam is hotkey id registered above.
                // Send "switch preset" to service w\IPC,
                // then fade-in & opacity animation on reply
                break;

            case WmDestroy:
                RemoveTrayIcon();
                NativeMethods.PostQuitMessage(0);
                return 0;
        }

        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }
}