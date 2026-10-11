using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Saku_Overclock.Overlay.Helpers;

internal abstract unsafe class NativeWindow(ILogger logger) : IDisposable
{
    private static readonly Dictionary<nint, NativeWindow> Map = [];
    private static readonly HashSet<string> RegisteredClasses = [];

    private readonly ILogger _logger = logger;

    public nint Handle { get; private set; }

    protected void Create(string className, uint exStyle, uint style)
    {
        var hInstance = Win32.GetModuleHandle(0);

        if (RegisteredClasses.Add(className))
        {
            fixed (char* name = className)
            {
                var wc = new Wndclassex
                {
                    cbSize = (uint)sizeof(Wndclassex),
                    lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nuint, nint, nint>)&WndProcThunk,
                    hInstance = hInstance,
                    lpszClassName = (nint)name
                };
                Win32.RegisterClassEx(in wc);
            }
        }

        Handle = Win32.CreateWindowEx(exStyle, className, null, style, 0, 0, 0, 0, 0, 0, hInstance, 0);
        if (Handle == 0) throw new InvalidOperationException("CreateWindowEx failed: " + className);
        Map[Handle] = this;
    }

    protected abstract bool OnMessage(uint msg, nuint wParam, nint lParam, out nint result);

    [UnmanagedCallersOnly]
    private static nint WndProcThunk(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (Map.TryGetValue(hwnd, out var window))
        {
            try
            {
                if (window.OnMessage(msg, wParam, lParam, out var result))
                    return result;
            }
            catch (Exception ex)
            {
                window._logger.LogError(ex, "Unhandled exception in WndProc (msg=0x{Msg:X})", msg);
            }
        }

        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public virtual void Dispose()
    {
        if (Handle == 0) return;
        var h = Handle;
        Handle = 0;
        Win32.DestroyWindow(h);
        Map.Remove(h);
    }
}