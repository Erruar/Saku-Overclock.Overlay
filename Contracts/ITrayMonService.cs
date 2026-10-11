using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Overlay.Contracts;

public interface ITrayMonService
{
    /// <summary>
    ///     Enable updating tray icons
    /// </summary>
    void Enable();

    /// <summary>
    ///     Remove all tray icons
    /// </summary>
    void Disable();

    /// <summary>
    ///     Rebuild icons
    /// </summary>
    void Rebuild();

    /// <summary>
    ///     Update text on icon
    /// </summary>
    void UpdateNotifyIcons(SensorsInformation sensorsInformation);
}