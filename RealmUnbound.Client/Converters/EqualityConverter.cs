using System.Globalization;
using Avalonia.Data.Converters;

namespace RealmUnbound.Client.Converters;

/// <summary>Returns <see langword="true"/> when the bound value's string representation equals the converter parameter.</summary>
public sealed class EqualityConverter : IValueConverter
{
    /// <summary>Gets the shared singleton instance.</summary>
    public static readonly EqualityConverter Instance = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
