using System.Collections.Frozen;
using Saku_Overclock.Overlay.Helpers;

namespace Saku_Overclock.Overlay.Strings;

internal sealed class EnUsLocalization : Localization
{
    private protected override FrozenDictionary<string, string> Table { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OverlayDisabled"] = "Overlay disabled",
            ["OverlayEnabled"] = "Overlay enabled",
            ["Preset_SmartPreset"] = "Smart mode",
            ["Preset_PowerfulPreset"] = "Performance",
            ["Preset_SmartPresetDesc"] = "Optimization for current workloads",
            ["Preset_PowerfulPresetDesc"] = "Maximum processor power",
            ["Settings_ni_Values_STAPM"] = "Stapm value",
            ["Settings_ni_Values_Fast"] = "Fast value",
            ["Settings_ni_Values_Slow"] = "Slow value",
            ["Settings_ni_Values_VRMEDC"] = "VRM EDC current",
            ["Settings_ni_Values_CPUTEMP"] = "CPU Temperature",
            ["Settings_ni_Values_CPUUsage"] = "CPU Usage",
            ["Settings_ni_Values_AVGCPUCLK"] = "AVG CPU Freq",
            ["Settings_ni_Values_AVGCPUVOLT"] = "AVG CPU Voltage",
            ["Settings_ni_Values_GFXCLK"] = "iGPU Frequency",
            ["Settings_ni_Values_GFXTEMP"] = "iGPU Temperature",
            ["Settings_ni_Values_GFXVOLT"] = "iGPU Voltage",
            ["Settings_ni_Values_DgpuFreq"] = "dGPU Frequency",
            ["Settings_ni_Values_DgpuTemp"] = "dGPU Temperature",
            ["Settings_ni_Values_RamUsage"] = "RAM Usage",
            ["Settings_ni_Values_CurrentValue"] = "\nCurrent: ",
            ["Settings_ni_Values_MinValue"] = "Minimum: ",
            ["Settings_ni_Values_MaxValue"] = "\nMaximum: "
        }.ToFrozenDictionary(StringComparer.Ordinal);
}