using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace RealmForge.Converters;

/// <summary>
/// Converts an avares:// URI string to an Avalonia Bitmap for use in Image.Source bindings.
/// Results are cached by URI to avoid re-loading the same asset on every binding pass.
/// </summary>
public class AvaresBitmapConverter : IValueConverter
{
    public static readonly AvaresBitmapConverter Instance = new();

    private static readonly Dictionary<string, Bitmap?> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string uri || string.IsNullOrEmpty(uri))
            return null;

        if (!Cache.TryGetValue(uri, out var bitmap))
        {
            try
            {
                var stream = AssetLoader.Open(new Uri(uri));
                Cache[uri] = bitmap = new Bitmap(stream);
            }
            catch
            {
                Cache[uri] = null;
            }
        }

        return bitmap;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
