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
            ["Settings_ni_Values_STAPM"] = "Лимит STAPM",
            ["Settings_ni_Values_Fast"] = "Реальная мощн.",
            ["Settings_ni_Values_Slow"] = "Средняя мощн.",
            ["Settings_ni_Values_VRMEDC"] = "Макс. ток VRM",
            ["Settings_ni_Values_CPUTEMP"] = "Температура ЦП",
            ["Settings_ni_Values_CPUUsage"] = "Использование ЦП",
            ["Settings_ni_Values_AVGCPUCLK"] = "Частота ЦП",
            ["Settings_ni_Values_AVGCPUVOLT"] = "Напряжение ЦП",
            ["Settings_ni_Values_GFXCLK"] = "Частота iGPU",
            ["Settings_ni_Values_GFXTEMP"] = "Температура iGPU",
            ["Settings_ni_Values_GFXVOLT"] = "Напряжение iGPU",
            ["Settings_ni_Values_DgpuFreq"] = "Частота dGPU",
            ["Settings_ni_Values_DgpuTemp"] = "Температура dGPU",
            ["Settings_ni_Values_RamUsage"] = "Использование ОЗУ",
            ["Settings_ni_Values_CurrentValue"] = "\nТекущее: ",
            ["Settings_ni_Values_MinValue"] = "Минимум: ",
            ["Settings_ni_Values_MaxValue"] = "\nМаксимум: "
            
        }.ToFrozenDictionary(StringComparer.Ordinal);
}