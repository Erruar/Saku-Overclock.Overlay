using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Services.Ipc;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Overlay.Services;

public class RtssSettingsService(IpcConnectionService ipc, ICpuGateService cpuService, ILogger<RtssSettingsService> logger)
    : SimpleIpcSettingsBase<RtssSettings>(ipc, "RtssSettings", IpcJsonContext.Default.RtssSettings, new())
        , IRtssSettingsService
{
    public List<RtssElementsClass> RtssElements
    {
        get => Get(s => s.RtssElements.ToList());
        set => Set(cache => cache.RtssElements = value);
    }

    public bool IsAdvancedCodeEditorEnabled
    {
        get => Get(s => s.IsAdvancedCodeEditorEnabled);
        set => Set(cache => cache.IsAdvancedCodeEditorEnabled = value);
    }

    public string AdvancedCodeEditor
    {
        get => Get(s => s.AdvancedCodeEditor);
        set => Set(cache => cache.AdvancedCodeEditor = value);
    }

    public void LoadSettings() => _ = LoadSettingsAsync();
    
    public void SaveSettings() => Save();
}