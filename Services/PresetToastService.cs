using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Ui;

namespace Saku_Overclock.Overlay.Services;

public sealed class PresetToastService(ILogger<PresetToastService> logger) : IPresetToastService
{
    private ToastWindow? _window;

    public void Show(string title, string iconGlyph, string? description = null)
    {
        try
        {
            _window ??= new ToastWindow(logger);
            _window.Show(title, iconGlyph, description ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show preset toast");
            _window?.Hide();
        }
    }

    public void Hide() => _window?.Hide();
}