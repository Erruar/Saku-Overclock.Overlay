using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Helpers;

namespace Saku_Overclock.Overlay.Services;

public sealed class UiThreadWorker(
    IUiThread ui,
    IHostApplicationLifetime lifetime,
    ILogger<UiThreadWorker> logger)
    : IHostedService
{
    private Thread? _thread;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _thread = new Thread(() => Run(ready)) { Name = "Overlay.UI", IsBackground = true };
        _thread.Start();

        return ready.Task;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        ui.Post(() => Win32.PostQuitMessage(0));
        _thread?.Join(TimeSpan.FromSeconds(5));

        return Task.CompletedTask;
    }

    private void Run(TaskCompletionSource ready)
    {
        try
        {
            ui.Initialize();
            ready.SetResult();

            Win32.RunMessageLoop();
        }
        catch (Exception ex)
        {
            ready.TrySetException(ex);
            logger.LogCritical(ex, "UI thread crashed");
            lifetime.StopApplication();
        }
        finally
        {
            ui.Cleanup(); 
        }
    }
}