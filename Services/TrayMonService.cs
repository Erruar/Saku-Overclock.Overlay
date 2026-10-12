using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Helpers;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Overlay.Services;

public sealed unsafe class TrayMonService(
    IUiThread ui,
    INotifyIconsService settings,
    ILogger<TrayMonService> logger) : ITrayMonService
{
    private sealed class MinMax
    {
        public double Min;
        public double Max;
    }

    private sealed class TrayIcon(string name, Guid guid)
    {
        public string Name { get; } = name;
        public Guid Guid { get; set; } = guid;
        public nint HIcon;
        public string Tooltip = string.Empty;
        public bool Added;
        public int AddFailures;
        public string? LastText; 
        public string? LastTooltip;
    }

    private const nuint RecreateTimerId = 0x5452; 
    private const int RecreateDelayMs = 2000; 
    private const int MaxRecreateAttempts = 6;
    private const int MaxAddFailuresBeforeNewGuid = 3;

    private static readonly uint WmTaskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");

    private readonly List<MinMax> _iconsMinMaxValues =
    [
        new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new()
    ]; // Saving min-max TrayMon icons values

    private readonly Dictionary<string, TrayIcon> _trayIcons = []; // Enabled TrayMon icons

    private bool _enabled;
    private bool _iconsCreated;
    private bool _suspended; 
    private bool _recreatePending;
    private int _recreateAttempts;

    private readonly string _stapmText = "Settings_ni_Values_STAPM".GetLocalized();
    private readonly string _fastText = "Settings_ni_Values_Fast".GetLocalized();
    private readonly string _slowText = "Settings_ni_Values_Slow".GetLocalized();
    private readonly string _vrmEdcText = "Settings_ni_Values_VRMEDC".GetLocalized();
    private readonly string _cpuTempText = "Settings_ni_Values_CPUTEMP".GetLocalized();
    private readonly string _cpuUsageText = "Settings_ni_Values_CPUUsage".GetLocalized();
    private readonly string _cpuFreqText = "Settings_ni_Values_AVGCPUCLK".GetLocalized();
    private readonly string _cpuVoltText = "Settings_ni_Values_AVGCPUVOLT".GetLocalized();
    private readonly string _gfxFreqText = "Settings_ni_Values_GFXCLK".GetLocalized();
    private readonly string _gfxTempText = "Settings_ni_Values_GFXTEMP".GetLocalized();
    private readonly string _gfxVoltText = "Settings_ni_Values_GFXVOLT".GetLocalized();
    private readonly string _dGpuFreqText = "Settings_ni_Values_DgpuFreq".GetLocalized();
    private readonly string _dGpuTempText = "Settings_ni_Values_DgpuTemp".GetLocalized();
    private readonly string _ramUsageText = "Settings_ni_Values_RamUsage".GetLocalized();

    private readonly string _niCurrentValueText = "Settings_ni_Values_CurrentValue".GetLocalized();
    private readonly string _niMinvalueText = "Settings_ni_Values_MinValue".GetLocalized();
    private readonly string _niMaxvalueText = "Settings_ni_Values_MaxValue".GetLocalized();

    #region Lifecycle

    public void Enable()
    {
        if (_enabled) return;

        _enabled = true;
        _suspended = false;
        ui.MessageReceived += OnWindowMessage;
    }

    public void Disable()
    {
        _enabled = false;
        _suspended = false;
        _recreatePending = false;
        _recreateAttempts = 0;

        ui.MessageReceived -= OnWindowMessage;
        Win32.KillTimer(ui.Handle, RecreateTimerId);

        RemoveAllIcons();
        _iconsCreated = false;
    }

    /// <summary> Update icons from UI </summary>
    public void Rebuild()
    {
        if (!_enabled) return;

        RemoveAllIcons();
        _iconsCreated = false;

        if (!_suspended) CreateNotifyIcons();
    }

    #endregion

    #region Power / shell messages

    private void OnWindowMessage(uint msg, nuint wParam, nint lParam)
    {
        if (msg == Win32.WmPowerbroadcast)
        {
            switch ((uint)wParam)
            {
                case Win32.PbtApmsuspend:
                    // Sleep or hibernation
                    _suspended = true;
                    _recreatePending = false;
                    Win32.KillTimer(ui.Handle, RecreateTimerId);
                    break;

                case Win32.PbtApmresumeautomatic:
                case Win32.PbtApmresumesuspend:
                    // Right after sleep or hibernation, with timer
                    _suspended = true;
                    _recreatePending = false;
                    _recreateAttempts = 0;
                    ScheduleRecreate();
                    break;
            }
        }
        else if (msg == WmTaskbarCreated)
        {
            // Explorer restarted, update all icons
            foreach (var icon in _trayIcons.Values) icon.Added = false;

            _recreatePending = false;
            _recreateAttempts = 0;
            ScheduleRecreate();
        }
        else if (msg == Win32.WmTimer && wParam == RecreateTimerId)
        {
            Win32.KillTimer(ui.Handle, RecreateTimerId);
            _recreatePending = false;
            RecreateIcons();
        }
    }

    private void ScheduleRecreate()
    {
        if (!_enabled || _recreatePending) return;

        if (_recreateAttempts >= MaxRecreateAttempts)
        {
            logger.LogWarning("TrayMon: cant restore icons after {Attempts} tries", _recreateAttempts);
            _suspended = false;
            return;
        }

        _recreateAttempts++;
        _recreatePending = true;
        _ = Win32.SetTimer(ui.Handle, RecreateTimerId, (uint)(RecreateDelayMs * _recreateAttempts), 0);
    }

    private void RecreateIcons()
    {
        _suspended = false;
        if (!_enabled) return;

        var allAdded = true;

        foreach (var icon in _trayIcons.Values.ToArray())
        {
            var element = settings.Elements.FirstOrDefault(e => e.Name == icon.Name && e.IsEnabled);
            if (element == null)
            {
                Release(icon);
                _trayIcons.Remove(icon.Name);
                continue;
            }

            var fresh = CreateIconFast(element);
            if (fresh != 0)
            {
                if (icon.HIcon != 0) Win32.DestroyIcon(icon.HIcon);
                icon.HIcon = fresh;
            }

            icon.LastText = null;
            icon.LastTooltip = null;

            if (!TryAdd(icon)) allAdded = false;
        }

        if (allAdded) _recreateAttempts = 0;
        else ScheduleRecreate();
    }

    #endregion

    #region Update

    public void UpdateNotifyIcons(SensorsInformation sensorsInformation)
    {
        if (!_enabled || _suspended) return;

        try
        {
            if (!_iconsCreated) CreateNotifyIcons();

            var sensorValues = new[]
            {
                sensorsInformation.CpuStapmValue,
                sensorsInformation.CpuFastValue,
                sensorsInformation.CpuSlowValue,
                sensorsInformation.VrmEdcValue,
                sensorsInformation.CpuTempValue,
                sensorsInformation.CpuUsage,
                sensorsInformation.CpuFrequency,
                sensorsInformation.CpuVoltage,
                sensorsInformation.ApuFrequency,
                sensorsInformation.ApuTempValue,
                sensorsInformation.ApuVoltage,
                sensorsInformation.NvidiaGpuFrequency,
                sensorsInformation.NvidiaGpuTemperature,
                sensorsInformation.RamUsagePercent
            };

            for (var i = 0; i < sensorValues.Length && i < _iconsMinMaxValues.Count; i++)
                UpdateMinMaxValues(_iconsMinMaxValues, i,
                    sensorValues[i]); // Changing min-max

            UpdateAllIconTexts(sensorsInformation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while updating TrayMon icons");
        }
    }

    private void UpdateAllIconTexts(SensorsInformation sensorsInformation)
    {
        try
        {
            // Grouping UI work in one method
            var iconUpdates = new[]
            {
                ("Settings_ni_Values_STAPM", sensorsInformation.CpuStapmValue, "W", _iconsMinMaxValues[0],
                    _stapmText),
                ("Settings_ni_Values_Fast", sensorsInformation.CpuFastValue, "W", _iconsMinMaxValues[1], _fastText),
                ("Settings_ni_Values_Slow", sensorsInformation.CpuSlowValue, "W", _iconsMinMaxValues[2], _slowText),
                ("Settings_ni_Values_VRMEDC", sensorsInformation.VrmEdcValue, "A", _iconsMinMaxValues[3],
                    _vrmEdcText),
                ("Settings_ni_Values_CPUTEMP", sensorsInformation.CpuTempValue, "C", _iconsMinMaxValues[4],
                    _cpuTempText),
                ("Settings_ni_Values_CPUUsage", sensorsInformation.CpuUsage, "%", _iconsMinMaxValues[5],
                    _cpuUsageText),
                ("Settings_ni_Values_AVGCPUCLK", sensorsInformation.CpuFrequency, "GHz", _iconsMinMaxValues[6],
                    _cpuFreqText),
                ("Settings_ni_Values_AVGCPUVOLT", sensorsInformation.CpuVoltage, "V", _iconsMinMaxValues[7],
                    _cpuVoltText),
                ("Settings_ni_Values_GFXCLK", sensorsInformation.ApuFrequency, "MHz", _iconsMinMaxValues[8],
                    _gfxFreqText),
                ("Settings_ni_Values_GFXTEMP", sensorsInformation.ApuTempValue, "C", _iconsMinMaxValues[9],
                    _gfxTempText),
                ("Settings_ni_Values_GFXVOLT", sensorsInformation.ApuVoltage, "V", _iconsMinMaxValues[10],
                    _gfxVoltText),
                ("Settings_ni_Values_DgpuFreq", sensorsInformation.NvidiaGpuFrequency, "GHz", _iconsMinMaxValues[11],
                    _dGpuFreqText),
                ("Settings_ni_Values_DgpuTemp", sensorsInformation.NvidiaGpuTemperature, "C", _iconsMinMaxValues[12],
                    _dGpuTempText),
                ("Settings_ni_Values_RamUsage", sensorsInformation.RamUsagePercent, "%", _iconsMinMaxValues[13],
                    _ramUsageText)
            };

            foreach (var (key, value, unit, minMax, textControl) in iconUpdates)
                UpdateNiIconText(key, value, unit, minMax, textControl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Icons text update failed");
        }
    }

    private void UpdateMinMaxValues(List<MinMax> minMaxValues, int index, double currentValue)
    {
        if (index >= 0 && index < minMaxValues.Count)
        {
            if (minMaxValues[index].Min == 0.0d) minMaxValues[index].Min = currentValue;
            if (index == 4 && currentValue > 150) currentValue = 150; // Fix impossible temp (found on Ryzen 5 6600H)

            minMaxValues[index].Max = Math.Max(minMaxValues[index].Max, currentValue);
            minMaxValues[index].Min = Math.Min(minMaxValues[index].Min, currentValue);
        }
        else
        {
            logger.LogWarning(
                "UpdateMinMaxValues: Incorrect index {Index}. List size: {Count}",
                index, minMaxValues.Count);
        }
    }

    private void UpdateNiIconText(string key, double currentValue, string unit, MinMax minMaxValue,
        string description) // Updating current TrayMon icon text
    {
        // Rounding values
        var currentValueText = $"{currentValue:0.#}";
        var minValueText = $"{minMaxValue.Min:0.#}";
        var maxValueText = $"{minMaxValue.Max:0.#}";


        var tooltip = $"{description}" +
                      _niCurrentValueText + currentValueText + unit; // Tooltip


        var extendedTooltip = _niMinvalueText + minValueText + unit +
                              _niMaxvalueText + maxValueText +
                              unit; // Advanced tooltip (min-max values)

        Change_Ni_Icons_Text(key, currentValueText, tooltip, extendedTooltip);
    }

    private void Change_Ni_Icons_Text(string iconName, string? newText, string? tooltipText = null,
        string? advancedTooltip = null)
    {
        if (string.IsNullOrEmpty(iconName)) return;

        if (!_trayIcons.TryGetValue(iconName, out var icon) || !icon.Added) return;

        var element = settings.Elements.FirstOrDefault(e => e.Name == iconName);
        if (element == null) return;

        // Update tooltip
        var tooltip = tooltipText == null
            ? icon.Tooltip
            : element.ContextMenuType == 2
                ? $"{tooltipText}\n{advancedTooltip}"
                : tooltipText;

        var textChanged = newText != icon.LastText;
        if (!textChanged && tooltip == icon.LastTooltip) return;

        // Save pointer to old icon
        var oldIcon = icon.HIcon;

        if (textChanged)
        {
            // Create new icon
            var newIcon = UpdateIconText(newText, element.Color,
                element.IsGradient ? element.SecondColor : string.Empty,
                element.FontSize, element.IconShape, element.BgOpacity,
                element.FontWeight == 1);

            if (newIcon == 0) return; 

            icon.HIcon = newIcon;
            icon.LastText = newText;
        }

        icon.Tooltip = tooltip;
        icon.LastTooltip = tooltip;

        if (!Notify(Win32.NimModify, icon))
        {
            icon.Added = false;
            ScheduleRecreate();
        }

        if (oldIcon != icon.HIcon) Win32.DestroyIcon(oldIcon);
    }

    #endregion

    #region Icons lifecycle

    private void CreateNotifyIcons()
    {
        Gdip.EnsureStarted();

        _recreatePending = false;
        _recreateAttempts = 0;

        var guidsChanged = false;
        var anyNotAdded = false;

        foreach (var element in settings.Elements.Where(element => element.IsEnabled))
        {
            if (!Guid.TryParse(element.Guid, out var parsedGuid) || parsedGuid == Guid.Empty)
            {
                parsedGuid = Guid.NewGuid();
                element.Guid = parsedGuid.ToString();
                guidsChanged = true;
            }

            // Check for Icon with same ID: if exist - remove
            if (_trayIcons.Remove(element.Name, out var existing)) Release(existing);

            var hIcon = CreateIconFast(element);
            if (hIcon == 0)
            {
                logger.LogError("Icon {Name} create failed", element.Name);
                continue;
            }

            // Crating TrayMon icons
            var icon = new TrayIcon(element.Name, parsedGuid)
            {
                HIcon = hIcon,
                Tooltip = element.ContextMenuType != 0 ? element.Name : ""
            };

            _trayIcons[element.Name] = icon;
            if (!TryAdd(icon)) anyNotAdded = true;
        }

        if (guidsChanged) settings.SaveSettings();
        if (anyNotAdded) ScheduleRecreate();

        _iconsCreated = true;
    }

    /// <summary>
    ///     Adding icon (or update after sleep), retry by timer if failed
    /// </summary>
    private bool TryAdd(TrayIcon icon)
    {
        if (icon.Added && Notify(Win32.NimModify, icon))
        {
            icon.AddFailures = 0;
            return true;
        }

        Notify(Win32.NimDelete, icon);

        icon.Added = Notify(Win32.NimAdd, icon);
        if (icon.Added)
        {
            icon.AddFailures = 0;
            return true;
        }

        // GUID registration is tied to executable path. Shell might reject a GUID created by a
        // different executable (such as client application). We issue a new GUID only upon a
        // persistent failure and only when the taskbar is ready because otherwise,
        // a temporary glitch (such as Explorer not yet having loaded after login)
        // would cause the icon's pinned position to be reset
        if (++icon.AddFailures >= MaxAddFailuresBeforeNewGuid && Win32.FindWindow("Shell_TrayWnd", null) != 0)
        {
            icon.AddFailures = 0;
            icon.Guid = Guid.NewGuid();

            var element = settings.Elements.FirstOrDefault(e => e.Name == icon.Name);
            if (element != null)
            {
                element.Guid = icon.Guid.ToString();
                settings.SaveSettings();
            }

            logger.LogWarning("TrayMon: icon GUID {Name} rejected by shell, new created", icon.Name);
        }

        return false;
    }

    private void RemoveAllIcons()
    {
        foreach (var icon in _trayIcons.Values) Release(icon);
        _trayIcons.Clear();
    }

    /// <summary>
    ///     Remove TrayMon icon
    /// </summary>
    private void Release(TrayIcon icon)
    {
        if (icon.Added) Notify(Win32.NimDelete, icon);

        icon.Added = false;

        if (icon.HIcon != 0)
        {
            Win32.DestroyIcon(icon.HIcon);
            icon.HIcon = 0;
        }
    }

    /// <summary>
    ///     Shell_NotifyIcon for icon
    /// </summary>
    private bool Notify(uint message, TrayIcon icon)
    {
        var data = new Notifyicondataw()
        {
            cbSize = (uint)sizeof(Notifyicondataw),
            hWnd = ui.Handle,
            uFlags = Win32.NifGuid | Win32.NifIcon | Win32.NifTip,
            hIcon = icon.HIcon,
            guidItem = icon.Guid
        };

        var tip = icon.Tooltip.AsSpan();
        if (tip.Length > 127) tip = tip[..127];
        tip.CopyTo(new Span<char>(data.szTip, 128));

        return Win32.Shell_NotifyIcon(message, ref data);
    }

    #endregion

    #region Rendering (GDI+)

    /// <summary>
    ///     Placeholder icon with no value yet. Returns icon handle or 0
    /// </summary>
    private nint CreateIconFast(NiIconsElements element)
    {
        nint bitmap = 0, g = 0, brush = 0, textBrush = 0, family = 0, font = 0;

        try
        {
            if (Gdip.GdipCreateBitmapFromScan0(32, 32, 0, Gdip.PixelFormat32BppArgb, 0, out bitmap) != 0) return 0;
            Gdip.GdipGetImageGraphicsContext(bitmap, out g);

            Gdip.GdipCreateSolidFill(ToArgb(element.Color, element.BgOpacity), out brush);
            Gdip.GdipFillRectangle(g, brush, 0, 0, 32, 32);

            Gdip.GdipCreateSolidFill(
                GetContrastColor(element.Color, element.IsGradient ? element.SecondColor : null), out textBrush);
            Gdip.GdipCreateFontFamilyFromName("Arial", 0, out family);
            Gdip.GdipCreateFont(family, 22f, Gdip.FontStyleRegular, Gdip.UnitPixel, out font);

            var position = new RectF(-13.4f, 2.3f, 0, 0);
            Gdip.GdipDrawString(g, "Saku", -1, font, in position, 0, textBrush);

            Gdip.GdipCreateHiconFromBitmap(bitmap, out var hIcon);
            return hIcon;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CreateIconFast failed for color={Color} opacity={Opacity}",
                element.Color, element.BgOpacity);
            return 0;
        }
        finally
        {
            if (font != 0) Gdip.GdipDeleteFont(font);
            if (family != 0) Gdip.GdipDeleteFontFamily(family);
            if (textBrush != 0) Gdip.GdipDeleteBrush(textBrush);
            if (brush != 0) Gdip.GdipDeleteBrush(brush);
            if (g != 0) Gdip.GdipDeleteGraphics(g);
            if (bitmap != 0) Gdip.GdipDisposeImage(bitmap);
        }
    }

    /// <summary>
    ///     Draw icon with text
    /// </summary>
    private nint UpdateIconText(string? newText, string newColor, string secondColor, int fontSize,
        int iconShape, double opacity, bool useBold)
    {
        nint bitmap = 0, g = 0, path = 0, bgBrush = 0, textBrush = 0, family = 0, font = 0;

        try
        {
            // Create new icon with existing text
            if (Gdip.GdipCreateBitmapFromScan0(32, 32, 0, Gdip.PixelFormat32BppArgb, 0, out bitmap) != 0) return 0;
            Gdip.GdipGetImageGraphicsContext(bitmap, out g);
            Gdip.GdipSetSmoothingMode(g, Gdip.SmoothingModeAntiAlias);

            // Background
            var bgColor = ToArgb(newColor, opacity);
            if (secondColor != string.Empty)
            {
                var rect = new RectF(0, 0, 32, 32);
                Gdip.GdipCreateLineBrushFromRect(in rect, bgColor, ToArgb(secondColor, opacity),
                    0, // LinearGradientModeHorizontal
                    0, // WrapModeTile
                    out bgBrush);
            }
            else
            {
                Gdip.GdipCreateSolidFill(bgColor, out bgBrush);
            }

            // Drawing shape
            switch (iconShape)
            {
                case 1: // Round cube
                    path = CreateRoundedRectanglePath(7);
                    Gdip.GdipFillPath(g, bgBrush, path);
                    break;
                case 2: // Circle
                    Gdip.GdipFillEllipse(g, bgBrush, 0, 0, 32, 32);
                    break;
                default: // 0 - Cube
                    Gdip.GdipFillRectangle(g, bgBrush, 0, 0, 32, 32);
                    break;
            }

            // Update text position
            Gdip.GdipCreateSolidFill(
                GetContrastColor(newColor, secondColor != string.Empty ? secondColor : null), out textBrush);
            var textPosition = GetTextPosition(newText, fontSize, out var fontSizeT, out var newTextT);
            Gdip.GdipCreateFontFamilyFromName("Segoe UI", 0, out family);
            Gdip.GdipCreateFont(family, fontSizeT * 2f, useBold ? Gdip.FontStyleBold : Gdip.FontStyleRegular,
                Gdip.UnitPixel, out font);

            // Draw text
            Gdip.GdipDrawString(g, newTextT ?? string.Empty, -1, font, in textPosition, 0, textBrush);

            // Create icon from Bitmap
            Gdip.GdipCreateHiconFromBitmap(bitmap, out var hIcon);
            return hIcon;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create icon");
            return 0;
        }
        finally
        {
            // Cleanup Resources
            if (font != 0) Gdip.GdipDeleteFont(font);
            if (family != 0) Gdip.GdipDeleteFontFamily(family);
            if (textBrush != 0) Gdip.GdipDeleteBrush(textBrush);
            if (bgBrush != 0) Gdip.GdipDeleteBrush(bgBrush);
            if (path != 0) Gdip.GdipDeletePath(path);
            if (g != 0) Gdip.GdipDeleteGraphics(g);
            if (bitmap != 0) Gdip.GdipDisposeImage(bitmap);
        }
    }

    /// <summary>
    ///     Creating round cube
    /// </summary>
    private static nint CreateRoundedRectanglePath(int radius)
    {
        const float left = 0, top = 0, right = 32, bottom = 32; // Rectangle(0, 0, 32, 32)

        Gdip.GdipCreatePath(0, out var path);
        var diameter = radius * 2;
        const float factor = 0.99f; // Fix pixel eating in GDI+

        // Top left corner
        Gdip.GdipAddPathArc(path, left, top, diameter, diameter, 180, 90);

        // Top line
        Gdip.GdipAddPathLine(path, left + radius, top, right - radius - factor, top);

        // Top right corner
        Gdip.GdipAddPathArc(path, right - diameter - factor, top, diameter, diameter, 270, 90);

        // Right line
        Gdip.GdipAddPathLine(path, right, top + radius, right, bottom - radius - factor);

        // Bottom right corner
        Gdip.GdipAddPathArc(path, right - diameter - factor, bottom - diameter - factor, diameter, diameter, 0, 90);

        // Bottom line
        Gdip.GdipAddPathLine(path, right - radius - factor, bottom, left + radius, bottom);

        // Bottom left corner
        Gdip.GdipAddPathArc(path, left, bottom - diameter - factor, diameter, diameter, 90, 90);

        // Bottom line
        Gdip.GdipAddPathLine(path, left, bottom - radius - factor, left, top + radius);

        Gdip.GdipClosePathFigure(path);
        return path;
    }

    /// <summary>
    ///     Get text position. Return null-sized layout-round (x, y, 0, 0):
    /// </summary>
    private static RectF GetTextPosition(string? newText, float fontSize, out float newFontSize,
        out string? newFixedText)
    {
        var yPosition =
            -1.475f * fontSize +
            16.2f; // Premade "compiled" function based on a dataset collected across all possible font sizes
        newFixedText = newText;
        var xPos = 20f;
        if (!newText!.Contains('.')) newText += ".0";

        if (!string.IsNullOrEmpty(newText) && newText.Contains('.'))
        {
            var parts = newText.Split('.');
            var wholePartLength = parts[0].Length;
            switch
                (wholePartLength) // TrayMon© - developed by Erruar, so you don't need to figure out how it works. All values were compiled into functions using NumPy
            {
                case 1:
                    var offset1 = (int)fontSize switch
                    {
                        14 => 3.3f,
                        13 => -5f,
                        12 => -1f,
                        11 => 2f,
                        _ => 0f
                    };
                    xPos = -0.0715488215f * fontSize * fontSize * fontSize
                        + 2.83311688f * fontSize * fontSize
                        - 35.2581049f * fontSize + 135.071284f
                                                 + offset1;
                    newFixedText = fontSize > 13 ? parts[0] : newText;
                    break;
                case 2:
                    var offset2 = (int)fontSize == 10 ? 2.17329f : (int)fontSize == 9 ? -2.17329f : 0f;
                    xPos = 0.0614478114f * fontSize * fontSize * fontSize
                           - 2.48160173f * fontSize * fontSize
                           + 31.8379028f * fontSize - 132.756133f
                           + offset2;
                    newFixedText = fontSize > 9 ? parts[0] : newText;
                    break;
                case 3:
                    fontSize = fontSize > 12 ? 12 : fontSize;
                    xPos = 0.33333333f * fontSize * fontSize * fontSize
                        - 10.07142857f * fontSize * fontSize
                        + 98.5952381f * fontSize - 316.8f;
                    yPosition = -1.475f * fontSize + 16.2f;
                    break;
                case > 3:
                    fontSize = fontSize > 12 ? 12 : fontSize - 2;
                    xPos = 0.00378787879f * fontSize * fontSize * fontSize
                        - 0.00487012987f * fontSize * fontSize
                        - 2.32251082f * fontSize + 14.982684f;
                    yPosition = -1.475f * fontSize + 16.2f;
                    break;
                default:
                    xPos = 0f;
                    break;
            }
        }

        newFontSize = fontSize;
        return new RectF(xPos, yPosition, 0, 0);
    }

    private static (byte R, byte G, byte B) ParseRgb(string color)
    {
        var value = color.TrimStart('#');
        return (Convert.ToByte(value[..2], 16),
            Convert.ToByte(value.Substring(2, 2), 16),
            Convert.ToByte(value.Substring(4, 2), 16));
    }

    private static uint ToArgb(string color, double opacity)
    {
        var (r, g, b) = ParseRgb(color);
        return Gdip.Argb((byte)Math.Clamp((int)(opacity * 255), 0, 255), r, g, b);
    }

    /// <summary>
    ///     Get color brightness
    /// </summary>
    private static double GetBrightness(string color)
    {
        var (r, g, b) = ParseRgb(color);
        return 0.299 * r + 0.587 * g + 0.114 * b;
    }

    /// <summary>Get contrast color for background (ARGB)</summary>
    private static uint GetContrastColor(string color1, string? color2 = null)
    {
        var brightness1 = GetBrightness(color1);

        double? brightness2 = null;
        if (!string.IsNullOrEmpty(color2)) brightness2 = GetBrightness(color2);

        // Get average color brightness
        var averageBrightness = brightness2 == null
            ? brightness1
            : (brightness1 + brightness2.Value) / 2;

        // Restoring the text color based on average brightness
        return averageBrightness < 128 ? 0xFFFFFFFF : 0xFF000000;
    }

    #endregion
}