using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Helpers;

namespace Saku_Overclock.Overlay.Services;

public partial class KeyboardHotkeysService(ILogger<KeyboardHotkeysService> logger)
    : IKeyboardHotkeysService
{
    private bool _enabled;

    private const int HkCustom = 1;
    private const int HkPremade = 2;
    private const int HkRtss = 3;
    
    /// <summary>
    ///     Virtual keys Win32 (subset).
    /// </summary>
    internal enum VirtualKey : uint
    {
        W = 0x57,
        P = 0x50,
        R = 0x52
    }

    private HotkeyWindow? _sink;

    public event Action? NextPresetPressed;
    public event Action? ToggleRtssPressed;

    public void Enable()
    {
        if (_enabled) return;

        _sink = new HotkeyWindow();
        _sink.HotkeyPressed += OnHotkeyPressed;

        // Ctrl+Alt+W / Ctrl+Alt+P / Ctrl+Alt+R
        const uint mods = Win32.ModControl | Win32.ModAlt | Win32.ModNorepeat;

        var ok = true;
        ok &= Win32.RegisterHotKey(_sink.Handle, HkCustom,  mods, (uint)VirtualKey.W);
        ok &= Win32.RegisterHotKey(_sink.Handle, HkPremade, mods, (uint)VirtualKey.P);
        ok &= Win32.RegisterHotKey(_sink.Handle, HkRtss,    mods, (uint)VirtualKey.R);

        if (!ok)
        {
            logger.LogWarning(
                "One of hotkeys already \"in use\" (Win32 error {err})",
                System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }

        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled || _sink is null) return;

        Win32.UnregisterHotKey(_sink.Handle, HkCustom);
        Win32.UnregisterHotKey(_sink.Handle, HkPremade);
        Win32.UnregisterHotKey(_sink.Handle, HkRtss);

        _sink.HotkeyPressed -= OnHotkeyPressed;
        _sink.Dispose();
        _sink = null;

        _enabled = false;
    }

    private void OnHotkeyPressed(int id)
    {
        try
        {
            switch (id)
            {
                case HkPremade:
                case HkCustom:
                    NextPresetPressed?.Invoke();
                    break;
                case HkRtss:
                    ToggleRtssPressed?.Invoke();
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hotkey handler failed");
        }
    }

    public void Dispose()
    {
        Disable();
        GC.SuppressFinalize(this);
    }
}