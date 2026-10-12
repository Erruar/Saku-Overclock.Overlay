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
        }.ToFrozenDictionary(StringComparer.Ordinal);
}