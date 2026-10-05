using System.Globalization;

namespace QrCodeBuilder.Services;

/// <summary>
/// Farbe eines QR-Codes, unabhängig von Avalonia, damit Rendering und Export
/// ohne UI-Plattform testbar bleiben. Die Hex-Schreibweise folgt der
/// Avalonia-Konvention: <c>#RRGGBB</c> oder <c>#AARRGGBB</c> (Alpha vorn).
/// </summary>
public readonly record struct QrColor(byte R, byte G, byte B, byte A = 255)
{
    public static QrColor Black { get; } = new(0, 0, 0);

    public static QrColor White { get; } = new(255, 255, 255);

    public bool IsOpaque => A == 255;

    public bool IsTransparent => A == 0;

    /// <summary>Akzeptiert <c>#RGB</c>, <c>#RRGGBB</c> und <c>#AARRGGBB</c>, das <c>#</c> ist optional.</summary>
    public static bool TryParse(string? text, out QrColor color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hex = text.Trim().TrimStart('#');

        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length is not (6 or 8)
            || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (hex.Length == 6)
        {
            value |= 0xFF000000;
        }

        color = new QrColor(
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value,
            (byte)(value >> 24));

        return true;
    }

    public string ToHex() => IsOpaque
        ? $"#{R:X2}{G:X2}{B:X2}"
        : $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    /// <summary>Nur die Farbanteile, für SVG (Alpha geht dort über fill-opacity).</summary>
    public string ToRgbHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Relative Leuchtdichte nach WCAG 2.x (0 = schwarz, 1 = weiß).</summary>
    public double RelativeLuminance
    {
        get
        {
            static double Channel(byte value)
            {
                var c = value / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Channel(R)) + (0.7152 * Channel(G)) + (0.0722 * Channel(B));
        }
    }

    /// <summary>Kontrastverhältnis nach WCAG, 1 bis 21.</summary>
    public static double Contrast(QrColor first, QrColor second)
    {
        var a = first.RelativeLuminance;
        var b = second.RelativeLuminance;

        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public override string ToString() => ToHex();
}
