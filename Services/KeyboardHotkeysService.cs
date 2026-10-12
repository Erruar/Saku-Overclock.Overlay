using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Helpers;

namespace Saku_Overclock.Overlay.Services;

public sealed class KeyboardHotkeysService(IUiThread ui, ILogger<KeyboardHotkeysService> logger)
    : IKeyboardHotkeysService
{
    private bool _enabled;

    private const int HkCustom = 1;
    private const int HkPremade = 2;
    private const int HkRtss = 3;

    private enum VirtualKey : uint { W = 0x57, P = 0x50, R = 0x52 }

    public event Action? NextPresetPressed;
    public event Action? ToggleRtssPressed;

    public void Enable()
    {
        if (_enabled) return;

        const uint mods = Win32.ModControl | Win32.ModAlt | Win32.ModNorepeat;

        Register(HkCustom,  mods, VirtualKey.W);
        Register(HkPremade, mods, VirtualKey.P);
        Register(HkRtss,    mods, VirtualKey.R);

        ui.MessageReceived += OnMessage;
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled) return;

        ui.MessageReceived -= OnMessage;
        Win32.UnregisterHotKey(ui.Handle, HkCustom);
        Win32.UnregisterHotKey(ui.Handle, HkPremade);
        Win32.UnregisterHotKey(ui.Handle, HkRtss);

        _enabled = false;
    }

    private void Register(int id, uint mods, VirtualKey key)
    {
        if (!Win32.RegisterHotKey(ui.Handle, id, mods, (uint)key))
            logger.LogWarning("Hotkey Ctrl+Alt+{Key} is already in use (Win32 error {Err})",
                key, System.Runtime.InteropServices.Marshal.GetLastPInvokeError());
    }

    private void OnMessage(uint msg, nuint wParam, nint lParam)
    {
        if (msg != Win32.WmHotkey) return;

        try
        {
            switch ((int)wParam)
            {
                case HkPremade:
                case HkCustom: NextPresetPressed?.Invoke(); break;
                case HkRtss:   ToggleRtssPressed?.Invoke(); break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hotkey handler failed");
        }
    }

    public void Dispose() { Disable(); GC.SuppressFinalize(this); }
}