using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Helpers;

namespace Saku_Overclock.Overlay.Ui;

internal sealed unsafe class ToastWindow(ILogger logger) : NativeWindow(logger)
{
    private const string ClassName = "SakuOverlay.Toast";
    private const nuint TimerId = 1;

    private const int FrameMs = 15;
    private const int FadeInMs = 220;
    private const int HoldMs = 2200;
    private const int FadeOutMs = 260;

    // Size in dp (if 100% scale equals to pixels)
    private const float WidthDp = 360, HeightDp = 76, RadiusDp = 16;
    private const float PadDp = 14, IconDp = 44, GapDp = 12;
    private const float BottomMarginDp = 32, SlideDp = 18;

    private enum Phase { Hidden, In, Hold, Out }

    private Phase _phase;
    private long _phaseStart;

    // Ready toast bitmap
    private nint _hdcMem, _hBitmap, _hOldBitmap;
    private int _w, _h;

    // Final position (upper left) and text placement
    private int _x, _y, _slide;

    public void Show(string title, string glyph, string description)
    {
        Gdip.EnsureStarted();

        var monitor = Win32.MonitorFromWindow(Win32.GetForegroundWindow(), Win32.MonitorDefaulttoprimary);
        var scale = Win32.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96f : 1f;

        Render(title, glyph, description, scale);
        Place(monitor, scale);

        var wasVisible = _phase != Phase.Hidden;

        if (Handle == 0)
        {
            Create(ClassName,
                Win32.WsExLayered | Win32.WsExTopmost | Win32.WsExToolwindow |
                Win32.WsExNoactivate | Win32.WsExTransparent,
                Win32.WsPopup);
        }

        if (wasVisible)
        {
            Apply(255, 0);
            Enter(Phase.Hold, HoldMs);
            return;
        }

        Apply(0, _slide);
        Win32.ShowWindow(Handle, Win32.SwShownoactivate);
        Win32.SetWindowPos(Handle, Win32.HwndTopmost, 0, 0, 0, 0,
            Win32.SwpNomove | Win32.SwpNosize | Win32.SwpNoactivate);
        Enter(Phase.In, FrameMs);
    }

    public void Hide()
    {
        if (Handle != 0)
        {
            Win32.KillTimer(Handle, TimerId);
            Win32.ShowWindow(Handle, Win32.SwHide);
        }

        Dispose();
    }

    public override void Dispose()
    {
        FreeBitmap();
        _phase = Phase.Hidden;
        base.Dispose();
    }

    protected override bool OnMessage(uint msg, nuint wParam, nint lParam, out nint result)
    {
        result = 0;

        if (msg == Win32.WmTimer && wParam == TimerId)
        {
            Tick();
            return true;
        }

        return false;
    }

    #region Animation

    private void Enter(Phase phase, int intervalMs)
    {
        _phase = phase;
        _phaseStart = Environment.TickCount64;

        _ = Win32.SetTimer(Handle, TimerId, (uint)intervalMs, 0);
    }

    private void Tick()
    {
        var elapsed = Environment.TickCount64 - _phaseStart;

        switch (_phase)
        {
            case Phase.In:
            {
                var p = Math.Min(1f, elapsed / (float)FadeInMs);
                var e = 1f - (1f - p) * (1f - p) * (1f - p); // easeOutCubic

                Apply((byte)(255f * e), (int)MathF.Round(_slide * (1f - e)));
                if (p >= 1f) Enter(Phase.Hold, HoldMs);
                break;
            }

            case Phase.Hold:
                Enter(Phase.Out, FrameMs);
                break;

            case Phase.Out:
            {
                var p = Math.Min(1f, elapsed / (float)FadeOutMs);
                var e = p * p; // easeInQuad

                Apply((byte)(255f * (1f - e)), (int)MathF.Round(_slide * 0.5f * e));
                if (p >= 1f) Hide(); 
                break;
            }
        }
    }

    /// <summary>
    ///     Displays rendered bitmap with specified transparency and Y-offset, without redrawing
    /// </summary>
    private void Apply(byte alpha, int offsetY)
    {
        var dst = new Point { X = _x, Y = _y + offsetY };
        var size = new Size { CX = _w, CY = _h };
        var src = new Point();
        var blend = new Blendfunction
        {
            BlendOp = Win32.AcSrcOver,
            SourceConstantAlpha = alpha,
            AlphaFormat = Win32.AcSrcAlpha 
        };

        Win32.UpdateLayeredWindow(Handle, 0, in dst, in size, _hdcMem, in src, 0, in blend, Win32.UlwAlpha);
    }

    #endregion

    #region Layout

    private void Place(nint monitor, float scale)
    {
        var info = new Monitorinfo { cbSize = (uint)sizeof(Monitorinfo) };
        Win32.GetMonitorInfo(monitor, ref info);

        // Centered on monitor, higher than taskbar
        _x = info.rcWork.Left + (info.rcWork.Right - info.rcWork.Left - _w) / 2;
        _y = info.rcWork.Bottom - _h - (int)MathF.Round(BottomMarginDp * scale);
        _slide = (int)MathF.Round(SlideDp * scale);
    }

    #endregion

    #region Rendering

    private void Render(string title, string glyph, string description, float scale)
    {
        FreeBitmap();

        _w = (int)MathF.Round(WidthDp * scale);
        _h = (int)MathF.Round(HeightDp * scale);

        var bmi = new Bitmapinfoheader
        {
            biSize = (uint)sizeof(Bitmapinfoheader),
            biWidth = _w,
            biHeight = -_h, // top-down, otherwise, bitmap will end up upside down
            biPlanes = 1,
            biBitCount = 32
        };

        var hdcScreen = Win32.GetDC(0);
        _hBitmap = Win32.CreateDibSection(hdcScreen, in bmi, 0, out var bits, 0, 0);
        _hdcMem = Win32.CreateCompatibleDC(hdcScreen);
        Win32.ReleaseDC(0, hdcScreen);

        if (_hBitmap == 0 || _hdcMem == 0 || bits == 0)
        {
            FreeBitmap();
            throw new InvalidOperationException("Failed to create toast bitmap");
        }

        _hOldBitmap = Win32.SelectObject(_hdcMem, _hBitmap);

        // GDI+ draws into DIB memory (premultiplied ARGB), which UpdateLayeredWindow reading
        Gdip.GdipCreateBitmapFromScan0(_w, _h, _w * 4, Gdip.PixelFormat32BppPargb, bits, out var image);
        Gdip.GdipGetImageGraphicsContext(image, out var g);
        Gdip.GdipSetSmoothingMode(g, Gdip.SmoothingModeAntiAlias);
        Gdip.GdipSetTextRenderingHint(g, Gdip.TextRenderingHintAntiAlias);
        Gdip.GdipGraphicsClear(g, 0);

        var light = SystemTheme.IsLightApps();
        var accent = SystemTheme.AccentArgb();
        var bgColor = light ? Gdip.Argb(0xF2, 0xF9, 0xF9, 0xF9) : Gdip.Argb(0xF2, 0x2B, 0x2B, 0x2B);
        var borderColor = light ? Gdip.Argb(0x30, 0x00, 0x00, 0x00) : Gdip.Argb(0x40, 0xFF, 0xFF, 0xFF);
        var titleColor = light ? Gdip.Argb(0xFF, 0x1A, 0x1A, 0x1A) : Gdip.Argb(0xFF, 0xFF, 0xFF, 0xFF);
        var descColor = light ? Gdip.Argb(0xFF, 0x5C, 0x5C, 0x5C) : Gdip.Argb(0xFF, 0xC5, 0xC5, 0xC5);

        // Background: round rect with thin border
        Gdip.GdipCreatePath(0, out var path);
        Gdip.AddRoundRect(path, 0.5f, 0.5f, _w - 1f, _h - 1f, RadiusDp * scale);
        Gdip.GdipCreateSolidFill(bgColor, out var bgBrush);
        Gdip.GdipFillPath(g, bgBrush, path);
        Gdip.GdipCreatePen1(borderColor, 1f, Gdip.UnitPixel, out var pen);
        Gdip.GdipDrawPath(g, pen, path);

        var pad = PadDp * scale;
        var textLeft = pad;

        if (glyph.Length > 0)
        {
            DrawIcon(g, glyph, accent, pad, scale);
            textLeft = pad + IconDp * scale + GapDp * scale;
        }

        DrawTexts(g, FirstLine(title), FirstLine(description), titleColor, descColor, textLeft, pad, scale);

        Gdip.GdipDeletePen(pen);
        Gdip.GdipDeleteBrush(bgBrush);
        Gdip.GdipDeletePath(path);
        Gdip.GdipDeleteGraphics(g);
        Gdip.GdipDisposeImage(image); // Before FreeBitmap image points to DIB memory
    }

    private void DrawIcon(nint g, string glyph, uint accent, float left, float scale)
    {
        var d = IconDp * scale;
        var top = (_h - d) / 2f;

        Gdip.GdipCreateSolidFill(accent, out var accentBrush);
        Gdip.GdipFillEllipse(g, accentBrush, left, top, d, d);

        // Segoe Fluent Icons already in Windows 11, on Windows 10 fallback Segoe MDL2 Assets
        var family = CreateFamily("Segoe Fluent Icons", "Segoe MDL2 Assets");
        Gdip.GdipCreateFont(family, 22f * scale, Gdip.FontStyleRegular, Gdip.UnitPixel, out var font);
        Gdip.GdipCreateStringFormat(Gdip.FlagNoWrap | Gdip.FlagNoClip, 0, out var format);
        Gdip.GdipSetStringFormatAlign(format, Gdip.AlignCenter);
        Gdip.GdipSetStringFormatLineAlign(format, Gdip.AlignCenter);
        Gdip.GdipCreateSolidFill(SystemTheme.ContrastOn(accent), out var glyphBrush);

        var rect = new RectF(left, top, d, d);
        Gdip.GdipDrawString(g, glyph, -1, font, in rect, format, glyphBrush);

        Gdip.GdipDeleteBrush(glyphBrush);
        Gdip.GdipDeleteStringFormat(format);
        Gdip.GdipDeleteFont(font);
        Gdip.GdipDeleteFontFamily(family);
        Gdip.GdipDeleteBrush(accentBrush);
    }

    private void DrawTexts(nint g, string title, string description, uint titleColor, uint descColor,
        float left, float rightPad, float scale)
    {
        var width = _w - left - rightPad;

        var titleFamily = CreateFamily("Segoe UI Semibold", "Segoe UI");
        var bodyFamily = CreateFamily("Segoe UI", "Segoe UI");
        Gdip.GdipCreateFont(titleFamily, 16f * scale, Gdip.FontStyleRegular, Gdip.UnitPixel, out var titleFont);
        Gdip.GdipCreateFont(bodyFamily, 12.5f * scale, Gdip.FontStyleRegular, Gdip.UnitPixel, out var descFont);

        // Single line, truncated with ellipsis, vertically centered within its rectangle
        Gdip.GdipCreateStringFormat(Gdip.FlagNoWrap, 0, out var format);
        Gdip.GdipSetStringFormatLineAlign(format, Gdip.AlignCenter);
        Gdip.GdipSetStringFormatTrimming(format, Gdip.TrimEllipsisCharacter);

        Gdip.GdipCreateSolidFill(titleColor, out var titleBrush);
        Gdip.GdipCreateSolidFill(descColor, out var descBrush);

        if (description.Length == 0)
        {
            var rect = new RectF(left, 0, width, _h);
            Gdip.GdipDrawString(g, title, -1, titleFont, in rect, format, titleBrush);
        }
        else
        {
            var titleRect = new RectF(left, _h / 2f - 25f * scale, width, 24f * scale);
            var descRect = new RectF(left, _h / 2f, width, 22f * scale);
            Gdip.GdipDrawString(g, title, -1, titleFont, in titleRect, format, titleBrush);
            Gdip.GdipDrawString(g, description, -1, descFont, in descRect, format, descBrush);
        }

        Gdip.GdipDeleteBrush(descBrush);
        Gdip.GdipDeleteBrush(titleBrush);
        Gdip.GdipDeleteStringFormat(format);
        Gdip.GdipDeleteFont(descFont);
        Gdip.GdipDeleteFont(titleFont);
        Gdip.GdipDeleteFontFamily(bodyFamily);
        Gdip.GdipDeleteFontFamily(titleFamily);
    }

    private static nint CreateFamily(string preferred, string fallback)
    {
        if (Gdip.GdipCreateFontFamilyFromName(preferred, 0, out var family) == 0 && family != 0) return family;

        Gdip.GdipCreateFontFamilyFromName(fallback, 0, out family);
        return family;
    }

    /// <summary>
    ///     Only the first line appears in the toast, PresetDesc can be multi-line
    /// </summary>
    private static string FirstLine(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        return end < 0 ? text : text[..end];
    }

    private void FreeBitmap()
    {
        if (_hdcMem != 0)
        {
            if (_hOldBitmap != 0) Win32.SelectObject(_hdcMem, _hOldBitmap);
            Win32.DeleteDc(_hdcMem);
        }

        if (_hBitmap != 0) Win32.DeleteObject(_hBitmap);

        _hdcMem = 0;
        _hBitmap = 0;
        _hOldBitmap = 0;
    }

    #endregion
}