using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Helpers;
using Saku_Overclock.Overlay.Services;
using Saku_Overclock.Overlay.Services.Ipc;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Contracts;

namespace Saku_Overclock.Overlay;

public static class App
{
    public static async Task Main()
    {
        if (!SingleInstance.TryAcquire()) return;

        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddEventLog();

        // IPC
        builder.Services.AddSingleton<IpcConnectionService>();
        builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
        builder.Services.AddSingleton<INotifyIconsService, NotifyIconsService>();
        builder.Services.AddSingleton<IRtssSettingsService, RtssSettingsService>();
        builder.Services.AddSingleton<IRtssService, RtssService>();
        builder.Services.AddSingleton<CoreGatewayService>();
        builder.Services.AddSingleton<ICpuGateService>(sp => sp.GetRequiredService<CoreGatewayService>());
        builder.Services.AddSingleton<IApplyerGateService>(sp => sp.GetRequiredService<CoreGatewayService>());

        // Core Services
        builder.Services.AddSingleton<IUiThread, UiThread>();
        builder.Services.AddSingleton<IBackgroundDataReceiver, BackgroundDataReceiver>();
        builder.Services.AddSingleton<IKeyboardHotkeysService, KeyboardHotkeysService>();
        builder.Services.AddSingleton<IPresetToastService, PresetToastService>();
        builder.Services.AddSingleton<ITrayMonService, TrayMonService>();
        builder.Services.AddSingleton<IBalloonService, BalloonService>();

        // UI-thread
        builder.Services.AddHostedService<UiThreadWorker>();

        // Activation
        builder.Services.AddHostedService<AppActivationWorker>();

        await builder.Build().RunAsync();
    }
}