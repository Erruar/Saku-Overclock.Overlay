using System.Runtime.InteropServices;

namespace Saku_Overclock.Overlay.Helpers;

[StructLayout(LayoutKind.Sequential)]
internal struct RectF(float x, float y, float w, float h)
{
    public float X = x, Y = y, W = w, H = h;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GdiplusStartupInput
{
    public uint GdiplusVersion;
    public nint DebugEventCallback;
    public int SuppressBackgroundThread;
    public int SuppressExternalCodecs;
}

internal static partial class Gdip
{
    private const string Dll = "gdiplus.dll";

    public const int PixelFormat32BppArgb = 0x0026200A;
    public const int PixelFormat32BppPargb = 0x000E200B;
    public const int SmoothingModeAntiAlias = 4;
    public const int TextRenderingHintAntiAlias = 4;
    public const int UnitPixel = 2;
    public const int FontStyleRegular = 0, FontStyleBold = 1;

    // StringFormat
    public const int FlagNoWrap = 0x1000, FlagNoClip = 0x4000, FlagLineLimit = 0x2000;
    public const int AlignNear = 0, AlignCenter = 1;
    public const int TrimEllipsisCharacter = 3;

    private static nuint _token;
    private static bool _started;

    public static void EnsureStarted()
    {
        if (_started) return;
        var input = new GdiplusStartupInput { GdiplusVersion = 1 };
        if (GdiplusStartup(out _token, in input, 0) == 0) _started = true;
    }

    public static void Shutdown()
    {
        if (!_started) return;
        GdiplusShutdown(_token);
        _started = false;
    }

    public static uint Argb(byte a, byte r, byte g, byte b) =>
        ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

    /// <summary>
    ///    Create round rec
    /// </summary>
    public static void AddRoundRect(nint path, float x, float y, float w, float h, float r)
    {
        var d = r * 2;
        GdipAddPathArc(path, x, y, d, d, 180, 90);
        GdipAddPathArc(path, x + w - d, y, d, d, 270, 90);
        GdipAddPathArc(path, x + w - d, y + h - d, d, d, 0, 90);
        GdipAddPathArc(path, x, y + h - d, d, d, 90, 90);
        GdipClosePathFigure(path);
    }

    [LibraryImport(Dll)] private static partial int GdiplusStartup(out nuint token, in GdiplusStartupInput input, nint output);
    [LibraryImport(Dll)] private static partial void GdiplusShutdown(nuint token);

    // Bitmap / Graphics
    [LibraryImport(Dll)] public static partial int GdipCreateBitmapFromScan0(int width, int height, int stride, int format, nint scan0, out nint bitmap);
    [LibraryImport(Dll)] public static partial int GdipGetImageGraphicsContext(nint image, out nint graphics);
    [LibraryImport(Dll)] public static partial int GdipDisposeImage(nint image);
    [LibraryImport(Dll)] public static partial int GdipDeleteGraphics(nint graphics);
    [LibraryImport(Dll, EntryPoint = "GdipCreateHICONFromBitmap")] public static partial int GdipCreateHiconFromBitmap(nint bitmap, out nint hicon);
    [LibraryImport(Dll)] public static partial int GdipSetSmoothingMode(nint graphics, int mode);
    [LibraryImport(Dll)] public static partial int GdipSetTextRenderingHint(nint graphics, int hint);
    [LibraryImport(Dll)] public static partial int GdipGraphicsClear(nint graphics, uint argb);

    // Brushes / paths
    [LibraryImport(Dll)] public static partial int GdipCreateSolidFill(uint argb, out nint brush);
    [LibraryImport(Dll)] public static partial int GdipCreateLineBrushFromRect(in RectF rect, uint argb1, uint argb2, int mode, int wrap, out nint brush);
    [LibraryImport(Dll)] public static partial int GdipDeleteBrush(nint brush);
    [LibraryImport(Dll)] public static partial int GdipCreatePen1(uint argb, float width, int unit, out nint pen);
    [LibraryImport(Dll)] public static partial int GdipDeletePen(nint pen);
    [LibraryImport(Dll)] public static partial int GdipCreatePath(int fillMode, out nint path);
    [LibraryImport(Dll)] public static partial int GdipDeletePath(nint path);
    [LibraryImport(Dll)] public static partial int GdipAddPathArc(nint path, float x, float y, float w, float h, float start, float sweep);
    [LibraryImport(Dll)] public static partial int GdipClosePathFigure(nint path);
    [LibraryImport(Dll)] public static partial int GdipAddPathLine(nint path, float x1, float y1, float x2, float y2);
    [LibraryImport(Dll)] public static partial int GdipFillPath(nint graphics, nint brush, nint path);
    [LibraryImport(Dll)] public static partial int GdipDrawPath(nint graphics, nint pen, nint path);
    [LibraryImport(Dll)] public static partial int GdipFillEllipse(nint graphics, nint brush, float x, float y, float w, float h);
    [LibraryImport(Dll)] public static partial int GdipFillRectangle(nint graphics, nint brush, float x, float y, float w, float h);

    // Text
    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GdipCreateFontFamilyFromName(string name, nint collection, out nint family);
    [LibraryImport(Dll)] public static partial int GdipDeleteFontFamily(nint family);
    [LibraryImport(Dll)] public static partial int GdipCreateFont(nint family, float emSize, int style, int unit, out nint font);
    [LibraryImport(Dll)] public static partial int GdipDeleteFont(nint font);
    [LibraryImport(Dll)] public static partial int GdipCreateStringFormat(int attributes, ushort language, out nint format);
    [LibraryImport(Dll)] public static partial int GdipDeleteStringFormat(nint format);
    [LibraryImport(Dll)] public static partial int GdipSetStringFormatAlign(nint format, int align);
    [LibraryImport(Dll)] public static partial int GdipSetStringFormatLineAlign(nint format, int align);
    [LibraryImport(Dll)] public static partial int GdipSetStringFormatTrimming(nint format, int trimming);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GdipDrawString(nint graphics, string text, int length, nint font, in RectF layout, nint format, nint brush);
}