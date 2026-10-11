namespace Saku_Overclock.Overlay.Contracts;

public interface IPresetToastService
{
    /// <summary>
    ///     Show toast window
    /// </summary>
    /// <param name="title">Preset name or message title</param>
    /// <param name="iconGlyph">Segoe Fluent Icon</param>
    /// <param name="description">Description, can be null</param>
    void Show(string title, string iconGlyph, string? description = null);

    /// <summary>
    ///     Hide and dispose resources
    /// </summary>
    void Hide();
}