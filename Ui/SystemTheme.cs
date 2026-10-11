using Microsoft.Win32;

namespace Saku_Overclock.Overlay.Ui;

internal static class SystemTheme
{
    public static bool IsLightApps()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Get system accent color, default blue
    /// </summary>
    public static uint AccentArgb()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("ColorizationColor") is int color) return 0xFF000000u | (uint)color;
        }
        catch
        {
            // registry unavailable, use blue
        }

        return 0xFF0078D4;
    }

    /// <summary>
    ///     Determine text color depends on background contrast
    /// </summary>
    public static uint ContrastOn(uint argb)
    {
        var r = (argb >> 16) & 0xFF;
        var g = (argb >> 8) & 0xFF;
        var b = argb & 0xFF;
        var brightness = 0.299 * r + 0.587 * g + 0.114 * b;

        return brightness < 128 ? 0xFFFFFFFF : 0xFF000000;
    }
}