using System.Text.Json;
using Saku_Overclock.Overlay.Services.Ipc;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Ipc;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Overlay.Services;

public partial class CoreGatewayService : IDisposable
{
    private readonly IpcConnectionService _ipc;
    private HardwareInfoSnapshot? _hwSnapshot;

    public CoreGatewayService(IpcConnectionService ipc)
    {
        _ipc = ipc;
        _ipc.OnEvent += OnIpcEvent;
    }

    public async Task WarmupAsync()
    {
        var json = await _ipc.SendCommandAsync("Get_HardwareInfo");
        _hwSnapshot = string.IsNullOrEmpty(json)
            ? new HardwareInfoSnapshot()
            : JsonSerializer.Deserialize(json, IpcJsonContext.Default.HardwareInfoSnapshot) ?? new HardwareInfoSnapshot();
    }

    private void OnIpcEvent(string name, string payload)
    {
        switch (name)
        {
            case "Apply_PresetApplied": OnPresetApplied(payload); break;
            case "Apply_PresetChanged": OnPresetChanged(payload); break;
        }
    }

    public void Dispose() => _ipc.OnEvent -= OnIpcEvent;
}

public partial class CoreGatewayService : ICpuGateService
{
    public bool IsAvailable => _hwSnapshot?.IsAvailable ?? false;
    public uint PhysicalCores => _hwSnapshot?.PhysicalCores ?? 0;
    public uint[] CoreDisableMap => _hwSnapshot?.CoreDisableMap ?? [];
    public uint Cores => _hwSnapshot?.Cores ?? 0;
    public string CpuName => _hwSnapshot?.CpuName ?? string.Empty;
    public bool Smt => _hwSnapshot?.Smt ?? false;
    public CommonMotherBoardInfo MotherBoardInfo => _hwSnapshot?.MotherBoardInfo ?? new CommonMotherBoardInfo();
    public bool Avx512AvailableByCodename => _hwSnapshot?.Avx512AvailableByCodename ?? false;
    public string CpuCodeName => _hwSnapshot?.CpuCodeName ?? string.Empty;
    public string SmuVersion => _hwSnapshot?.SmuVersion ?? string.Empty;
    public uint PowerTableVersion => _hwSnapshot?.PowerTableVersion ?? 0;
    public uint PowerTableSize => _hwSnapshot?.PowerTableSize ?? 0;

    public CodenameGeneration GetCodenameGeneration() => _hwSnapshot?.CodenameGeneration ?? default;
    public MemoryConfig GetMemoryConfig() => _hwSnapshot?.MemoryConfig ?? new MemoryConfig();

    public async Task<string> GenerateDebugReportAsync() =>
        await _ipc.SendCommandAsync("Generate_Debug_Report");
}

public partial class CoreGatewayService : IApplyerGateService
{

    public async Task ApplyPreset(Preset preset, bool saveInfo = false)
    {
        var req = new ApplyPresetRequest { Preset = preset, SaveInfo = saveInfo };
        var json = JsonSerializer.Serialize(req, IpcJsonContext.Default.ApplyPresetRequest);
        await _ipc.SendCommandAsync("Apply_Preset", json);
    }
    
    public async Task<PresetId?> SwitchNextPreset()
    {
        var json = await _ipc.SendCommandAsync("Apply_SwitchNextPreset");
        return JsonSerializer.Deserialize(json, IpcJsonContext.Default.PresetId);
    }
    
    public async Task<string?> GetAppliedPreset()
    {
        var json = await _ipc.SendCommandAsync("Apply_GetSelectedPresetName");
        return JsonSerializer.Deserialize(json, IpcJsonContext.Default.String);
    }

    public event Action<Preset>? PresetChanged;
    public event Action<List<ApplyResult>>? OnSettingsApplied;
    public event Action<string>? OnStringSettingsApplied;

    private void OnPresetApplied(string payload)
    {
        var results = JsonSerializer.Deserialize(payload, IpcJsonContext.Default.ListApplyResult);
        if (results != null)
        {
            OnSettingsApplied?.Invoke(results);
            OnStringSettingsApplied?.Invoke(string.Empty);
        }
    }
    
    private void OnPresetChanged(string payload)
    {
        var results = JsonSerializer.Deserialize(payload, IpcJsonContext.Default.Preset);
        if (results != null)
        {
            PresetChanged?.Invoke(results);
        }
    }
}