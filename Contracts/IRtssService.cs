using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Overlay.Contracts;

public interface IRtssService
{
    /// <summary>
    ///     Enable updating rtss overlay
    /// </summary>
    void Enable();

    /// <summary>
    ///     Remove rtss overlay text
    /// </summary>
    void Disable();

    /// <summary>
    ///     Fast refresh settings if changed
    /// </summary>
    void RefreshSettings();

    /// <summary>
    ///     Update text on icon
    /// </summary>
    void UpdateRtss(SensorsInformation sensorsInformation);
}