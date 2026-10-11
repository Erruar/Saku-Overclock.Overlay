namespace Saku_Overclock.Overlay.Contracts;

public interface IKeyboardHotkeysService : IDisposable
{
    /// <summary>
    ///     Enable hotkeys service
    /// </summary>
    void Enable();

    /// <summary>
    ///     Disable hotkeys service
    /// </summary>
    void Disable();

    /// <summary>
    ///     Preset changed event
    /// </summary>
    event Action NextPresetPressed;

    /// <summary>
    ///     Rtss overlay changed event
    /// </summary>
    event Action ToggleRtssPressed;
}