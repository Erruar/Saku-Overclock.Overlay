using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Saku_Overclock.Overlay.Helpers;

public static partial class RtssHandler
{
    private const string DllName = "SakuRTSSCLI.dll";
    private static bool _isRtssInitialized;

    #region DLL Voids

    public static void ChangeOsdText(string text)
    {
        if (!_isRtssInitialized)
        {
            DisplayText(text);
            _isRtssInitialized = true;
        }
        else
        {
            UpdateOsd(text.Replace("<Br>", "\n"));
        }
    }

    public static unsafe void ChangeOsdTextSpan(ReadOnlySpan<char> text)
    {
        // UTF-8 max length equals 3 bytes per character and null-terminator
        var maxByteCount = text.Length * 3 + 1;
        var byteBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount); 

        try
        {
            Encoding.UTF8.TryGetBytes(text, byteBuffer, out int bytesWritten);
            byteBuffer[bytesWritten] = 0; // null-terminator

            fixed (byte* bytePtr = byteBuffer)
            {
                if (!_isRtssInitialized)
                {
                    displayTextSpan(bytePtr, bytesWritten);
                    _isRtssInitialized = true;
                }
                else
                {
                    UpdateOSDSpan(bytePtr, bytesWritten);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(byteBuffer);
        }
    }

    public static void ResetOsdText()
    {
        if (_isRtssInitialized)
        {
            _ = ReleaseOSD();
            _isRtssInitialized = false;
        }
    }

    #endregion

    #region DLL Imports

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial void DisplayText(string text);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int Refresh();

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial uint EmbedGraph(uint dwOffset, float* lpBuffer, uint dwBufferPos, uint dwBufferSize,
        int dwWidth, int dwHeight, int dwMargin, float fltMin, float fltMax, uint dwFlags);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial uint GetClientsNum();

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial uint GetSharedMemoryVersion();

    [LibraryImport(DllName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial void UpdateOsd(string lpText);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int ReleaseOSD();

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial void UpdateOSDSpan(byte* lpText, int length);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial void displayTextSpan(byte* text, int length);

    #endregion
}