using System.Globalization;
using System.Resources;

namespace Mirraawwrring.App.Resources;

/// <summary>
/// Akses ke teks UI di <c>Strings.resx</c>. Semua teks bahasa Indonesia dikumpulkan di sana
/// supaya kelak bisa diterjemahkan (tambahkan <c>Strings.en.resx</c> dsb.).
/// Di XAML pakai <c>{loc:Str Kunci}</c>, di C# pakai <c>Strings.Get("Kunci")</c>.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new("Mirraawwrring.App.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string key)
    {
        return Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";
    }

    public static string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }
}
