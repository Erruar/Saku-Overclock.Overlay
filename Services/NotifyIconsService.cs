using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Services.Ipc;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Overlay.Services;

public class NotifyIconsService(IpcConnectionService ipc)
    : SimpleIpcSettingsBase<List<NiIconsElements>>(ipc, "NotifyIcons", IpcJsonContext.Default.ListNiIconsElements, [])
        , INotifyIconsService
{
    public event Action? Changed;
    
    public List<NiIconsElements> Elements
    {
        get => Get(s => s);
        set => Set(cache => { cache.Clear(); cache.AddRange(value); });
    }
    public void LoadSettings() => _ = LoadSettingsAsync();

    public void SaveSettings() => Save();

    protected override void OnExternalUpdate() => Changed?.Invoke();
}