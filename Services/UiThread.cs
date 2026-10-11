using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Helpers;

namespace Saku_Overclock.Overlay.Services;

internal sealed class UiThread(ILogger<UiThread> logger) : NativeWindow(logger), IUiThread
{
    private const string ClassName = "SakuOverlay.Host";
    private const uint WmRun = Win32.WmApp + 1;

    private readonly ILogger _log = logger;
    private readonly ConcurrentQueue<Action> _queue = new();

    public event UiMessageHandler? MessageReceived;
    public event Action? SessionEnding;

    public void Initialize() => Create(ClassName, Win32.WsExToolwindow, Win32.WsPopup);

    public void Cleanup() => Dispose();

    public void Post(Action action)
    {
        _queue.Enqueue(action);

        var hwnd = Handle;
        if (hwnd != 0) Win32.PostMessage(hwnd, WmRun, 0, 0);
    }

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        if (Handle == 0) return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Post(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task.WaitAsync(cancellationToken);
    }

    protected override bool OnMessage(uint msg, nuint wParam, nint lParam, out nint result)
    {
        result = 0;

        switch (msg)
        {
            case WmRun:
                Drain();
                return true;

            case Win32.WmEndsession when wParam != 0:
                SessionEnding?.Invoke();
                return true;
        }

        MessageReceived?.Invoke(msg, wParam, lParam);
        return false;
    }

    private void Drain()
    {
        while (_queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled exception in UI thread action");
            }
        }
    }
}