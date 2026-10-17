namespace Saku_Overclock.Overlay.Contracts;

public interface IBalloonService
{
    /// <summary>
    ///     Show notification. Icon left in tray waiting for user action
    /// </summary>
    void Show(string title, string text, Action onClick);
 
    /// <summary>
    ///     Remove notification and icon
    /// </summary>
    void Dismiss();
}