using System.Collections.Frozen;
using Saku_Overclock.Overlay.Helpers;

namespace Saku_Overclock.Overlay.Strings;

internal sealed class RuRuLocalization : Localization
{ 
    private protected override FrozenDictionary<string, string> Table { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OverlayDisabled"]   = "Оверлей отключён",
            ["OverlayEnabled"]    = "Оверлей включён"
        }.ToFrozenDictionary(StringComparer.Ordinal);
}