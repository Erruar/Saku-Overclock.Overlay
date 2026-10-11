using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Helpers;
using Saku_Overclock.Overlay.Services.Ipc;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Contracts;

namespace Saku_Overclock.Overlay.Services;

public sealed class AppActivationWorker(
    IUiThread ui,
    IpcConnectionService ipc,
    IAppSettingsService settings,
    INotifyIconsService iconsSettings,
    IRtssSettingsService rtssSettings,
    ITrayMonService trayMon,
    IRtssService rtss,
    IApplyerGateService applyer,
    CoreGatewayService gateService,
    IBackgroundDataReceiver sensors,
    IKeyboardHotkeysService hotkeys,
    IPresetToastService toast,
    IHostApplicationLifetime lifetime,
    ILogger<AppActivationWorker> logger)
    : IHostedService
{
    private bool _ready;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 1. Efficiency mode: running overlay process on E-cores and not boosting CPU
        Win32.EnableEcoQos();

        // 2. Any exception -> Event Viewer
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogError(e.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})",
                e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        // 3. Sensor updaters
        sensors.Start();
        sensors.Updated += trayMon.UpdateNotifyIcons;
        sensors.Updated += rtss.UpdateRtss;

        // 4. Hotkeys
        hotkeys.NextPresetPressed += () => _ = SwitchNextPresetAsync();
        hotkeys.ToggleRtssPressed += ToggleRtss;

        // 5. Settings changed
        // TODO: add settings changed IPC messages
        /*settings.Changed += () => ui.Post(ApplyFeatures);
        niIcons.Changed += () => ui.Post(niIcons.Rebuild);*/

        // 6. Lifecycle and IPC
        ipc.Connected += () => _ = LoadAsync();
        ipc.Disconnected += () => ui.Post(DisableAll);
        ipc.OnEvent += OnIpcEvent;
        ui.SessionEnding += lifetime.StopApplication;

        // 7. IPC connection
        ipc.EnsureConnection();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // 1. Stop IPC
        ipc.Dispose();

        // 2. Disable all features
        await ui.InvokeAsync(DisableAll, cancellationToken);

        // 3. GDI+ close main window
        Gdip.Shutdown();
    }
    
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private async Task LoadAsync()
    {
        if (!await _loadGate.WaitAsync(0)) return;

        try
        {
            await settings.LoadSettingsAsync();
            iconsSettings.LoadSettings();
            rtssSettings.LoadSettings();
            await gateService.WarmupAsync(); // Cores count for Rtss

            ui.Post(() =>
            {
                _ready = true;
                ApplyFeatures();
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load settings from service");
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private void ApplyFeatures()
    {
        if (!_ready) return;

        if (settings.HotkeysEnabled) hotkeys.Enable(); else hotkeys.Disable();
        if (settings.NiIconsEnabled) trayMon.Enable(); else trayMon.Disable();
        if (settings.RtssMetricsEnabled) rtss.Enable(); else rtss.Disable();

        if (settings.NiIconsEnabled || settings.RtssMetricsEnabled) sensors.Start(); else sensors.Stop();
    }

    private void DisableAll()
    {
        if (!_ready) return;
        
        _ready = false;
        sensors.Stop();
        hotkeys.Disable();
        trayMon.Disable();
        rtss.Disable();
        toast.Hide();
    }

    private async Task SwitchNextPresetAsync()
    {
        try
        {
            var preset = await applyer.SwitchNextPreset();
            if (preset is { } p)
                ui.Post(() => toast.Show(p.PresetName, p.PresetIcon, p.PresetDesc));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to switch preset by hotkey");
        }
    }

    private void ToggleRtss()
    {
        settings.RtssMetricsEnabled = !settings.RtssMetricsEnabled;
        ApplyFeatures();

        toast.Show("RTSS " + (settings.RtssMetricsEnabled ? "OverlayEnabled".GetLocalized() : "OverlayDisabled".GetLocalized()), "");
    }

    private void OnIpcEvent(string name, string payload)
    {
        if (name == "Overlay_Exit") lifetime.StopApplication();
    }
}