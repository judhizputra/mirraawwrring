using System.Windows.Markup;

namespace Mirraawwrring.App.Resources;

/// <summary>
/// Markup extension untuk teks UI: <c>Text="{loc:Str Main_Title}"</c>.
/// Namespace XAML: <c>xmlns:loc="clr-namespace:Mirraawwrring.App.Resources"</c>.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class StrExtension : MarkupExtension
{
    public StrExtension() { }

    public StrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
}
