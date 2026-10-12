using System.Collections.Frozen;
using Saku_Overclock.Overlay.Helpers;

namespace Saku_Overclock.Overlay.Strings;

internal sealed class RuRuLocalization : Localization
{ 
    private protected override FrozenDictionary<string, string> Table { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OverlayDisabled"] = "Оверлей отключён",
            ["OverlayEnabled"] = "Оверлей включён",
            ["Preset_SmartPreset"] = "Умный режим",
            ["Preset_PowerfulPreset"] = "Производительный",
            ["Preset_SmartPresetDesc"] = "Оптимизация под текущую нагрузку",
            ["Preset_PowerfulPresetDesc"] = "Максимальная мощность процессора",
            
        }.ToFrozenDictionary(StringComparer.Ordinal);
}