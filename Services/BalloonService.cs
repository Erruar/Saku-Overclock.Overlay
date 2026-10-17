using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Helpers;

namespace Saku_Overclock.Overlay.Services;

internal sealed unsafe partial class BalloonService(IUiThread ui, ILogger<BalloonService> logger) : IBalloonService
{
    private const uint IconId = 0x5341;
    private const uint CallbackMsg = Win32.WmApp + 2; // WM_APP+1 used by UiThread.WmRun

    private const uint NimSetVersion = 4;
    private const uint NifMessage = 0x01, NifInfo = 0x10;
    private const uint NifWarning = 0x02;
    private const uint NotifyIconVersion4 = 4;
    private const uint NinSelect = 0x400, NinKeySelect = 0x401, NinBalloonUserClick = 0x405;
    private const nint IdiWarning = 32515;
    private const uint ImageIcon = 1, LrLoadFromFile = 0x10;
    private const int SmCxSmIcon = 49, SmCySmIcon = 50;
    private const string IconRelativePath = "Assets\\WindowIcon.ico";

    private static readonly uint WmTaskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");

    private Action? _onClick;
    private string _title = "", _text = "";
    private bool _added;
    private bool _subscribed;
    private nint _appIcon;       // App logo, 0 = file not found
    private bool _useStockIcon;

    public void Show(string title, string text, Action onClick)
    {
        _title = title;
        _text = text;
        _onClick = onClick;

        if (_appIcon == 0) _appIcon = LoadAppIcon();

        if (!_subscribed)
        {
            ui.MessageReceived += OnMessage;
            _subscribed = true;
        }

        if (_added)
        {
            var data = Build(true);
            _added = Win32.Shell_NotifyIcon(Win32.NimModify, ref data);
        }

        if (!_added) Add(true);
    }

    public void Dismiss()
    {
        if (_added)
        {
            var data = Build(false);
            Win32.Shell_NotifyIcon(Win32.NimDelete, ref data);
            _added = false;
        }

        _onClick = null;

        if (_appIcon != 0)
        {
            Win32.DestroyIcon(_appIcon);
            _appIcon = 0;
        }

        _useStockIcon = false;

        if (!_subscribed) return;
        ui.MessageReceived -= OnMessage;
        _subscribed = false;
    }

    private void Add(bool balloon)
    {
        var data = Build(balloon);
        if (!Win32.Shell_NotifyIcon(Win32.NimAdd, ref data))
        {
            if (_appIcon != 0 || _useStockIcon)
            {
                logger.LogWarning("Shell_NotifyIcon(NIM_ADD) failed for the notification");
                return;
            }

            _useStockIcon = true;
            data = Build(balloon);
            if (!Win32.Shell_NotifyIcon(Win32.NimAdd, ref data))
            {
                logger.LogWarning("Shell_NotifyIcon(NIM_ADD) failed for the notification");
                return;
            }
        }

        _added = true;
        data.uVersion = NotifyIconVersion4;
        Win32.Shell_NotifyIcon(NimSetVersion, ref data);
    }

    private void OnMessage(uint msg, nuint wParam, nint lParam)
    {
        if (msg == CallbackMsg)
        {
            var evt = (uint)((ulong)lParam & 0xFFFF);
            if (evt is not (NinSelect or NinKeySelect or NinBalloonUserClick)) return;

            var callback = _onClick;
            Dismiss();
            try
            {
                callback?.Invoke();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification click handler failed");
            }
        }
        else if (msg == WmTaskbarCreated && _onClick != null)
        {
            _added = false;
            Add(false);
        }
    }

    private Notifyicondataw Build(bool balloon)
    {
        var data = new Notifyicondataw
        {
            cbSize = (uint)sizeof(Notifyicondataw),
            hWnd = ui.Handle,
            uID = IconId,
            uFlags = Win32.NifTip | Win32.NifShowtip | NifMessage | (balloon ? NifInfo : 0),
            uCallbackMessage = CallbackMsg,
            dwInfoFlags = NifWarning
        };

        if (_appIcon != 0)
        {
            data.uFlags |= Win32.NifIcon;
            data.hIcon = _appIcon;
        }
        else if (_useStockIcon)
        {
            data.uFlags |= Win32.NifIcon;
            data.hIcon = LoadIcon(0, IdiWarning);
        }

        Copy(_title, data.szTip, 128);
        Copy(_title, data.szInfoTitle, 64);
        Copy(_text, data.szInfo, 256);
        return data;
    }

    private nint LoadAppIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, IconRelativePath);
            if (!File.Exists(path)) return 0;

            var icon = LoadImage(0, path, ImageIcon, GetSystemMetrics(SmCxSmIcon), GetSystemMetrics(SmCySmIcon),
                LrLoadFromFile);
            if (icon == 0) logger.LogWarning("Could not load notification icon from {Path}", path);
            return icon;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load notification icon");
            return 0;
        }
    }

    private static void Copy(string s, char* dst, int capacity)
    {
        var n = Math.Min(s.Length, capacity - 1);
        s.AsSpan(0, n).CopyTo(new Span<char>(dst, n));
        dst[n] = '\0';
    }

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadImage(nint hInstance, string name, uint type, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")]
    private static partial nint LoadIcon(nint hInstance, nint iconName);
}