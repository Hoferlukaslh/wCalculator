using System;
using Avalonia.Markup.Xaml;

namespace CalcApp.Localization;

/// <summary>XAML markup extension returning a localized string: <c>{loc:T Key=History}</c>.</summary>
public sealed class TExtension : MarkupExtension
{
    public TExtension()
    {
    }

    public TExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Instance.Get(Key);
}
