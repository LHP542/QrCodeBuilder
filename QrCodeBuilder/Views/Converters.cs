using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Views;

/// <summary>PNG-Bytes aus dem ViewModel → Bitmap für das Image-Control.</summary>
public sealed class PngToBitmapConverter : IValueConverter
{
    public static PngToBitmapConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } png)
        {
            return null;
        }

        using var stream = new MemoryStream(png);
        return new Bitmap(stream);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Hex-Text → Farbfläche neben dem Eingabefeld. Ungültige Eingaben zeigen nichts.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public static HexToBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        QrColor.TryParse(value as string, out var color)
            ? new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B))
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
