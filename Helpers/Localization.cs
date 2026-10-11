using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.CompilerServices;
using Saku_Overclock.Overlay.Strings;

namespace Saku_Overclock.Overlay.Helpers;

public abstract class Localization
{
    private static readonly bool IsEnglish =
        !CultureInfo.InstalledUICulture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

    private static readonly Localization Active = IsEnglish
        ? new EnUsLocalization()
        : new RuRuLocalization();

    private protected abstract FrozenDictionary<string, string> Table { get; }

    /// <summary>
    ///     Return localized string by key
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Resolve(string key)
    {
        return Active.Table.GetValueOrDefault(key, key);
    }
}

public static class LocalizationExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetLocalized(this string key)
        => Localization.Resolve(key);
}