using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Overlay.Contracts;

public interface INotifyIconsService
{
    /// <summary>
    ///     NotifyIcons elements
    /// </summary>
    List<NiIconsElements> Elements { get; set; }

    /// <summary>
    ///     Settings changed in client app
    /// </summary>
    event Action? Changed;

    /// <summary>
    ///     Load NotifyIcons elements
    /// </summary>
    void LoadSettings();

    /// <summary>
    ///     Save NotifyIcons elements
    /// </summary>
    void SaveSettings();
}