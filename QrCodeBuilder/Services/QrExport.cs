using System.Text;

namespace QrCodeBuilder.Services;

public enum QrExportFormat
{
    Png,
    Svg,
}

/// <summary>Ein vom Nutzer gewähltes Ziel: beschreibbarer Stream plus Anzeigename für die Statuszeile.</summary>
public sealed record SaveTarget(Stream Stream, string DisplayName) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}

/// <summary>Speichern-Dialog. Liefert null, wenn der Nutzer abbricht.</summary>
public interface ISaveFileDialog
{
    Task<SaveTarget?> PickAsync(string suggestedFileName, QrExportFormat format);
}

/// <summary>Legt ein PNG als Bild in die Zwischenablage.</summary>
public interface IImageClipboard
{
    Task CopyPngAsync(byte[] png);
}

public static class QrExport
{
    /// <summary>Höchstlänge des aus dem Inhalt abgeleiteten Namensteils.</summary>
    private const int MaxNameLength = 40;

    public static string Extension(QrExportFormat format) => format == QrExportFormat.Svg ? "svg" : "png";

    public static async Task WriteAsync(Stream target, QrExportFormat format, QrMatrix matrix, QrStyle style)
    {
        ArgumentNullException.ThrowIfNull(target);

        var bytes = format == QrExportFormat.Svg
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(QrRenderer.RenderSvg(matrix, style))
            : QrRenderer.RenderPng(matrix, style);

        await target.WriteAsync(bytes).ConfigureAwait(false);
        await target.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Dateinamensvorschlag aus dem Inhalt: <c>qr-example.com-shop.png</c>.
    /// Schema und Sonderzeichen fallen weg; bleibt nichts übrig, heißt die Datei <c>qr-code</c>.
    /// </summary>
    public static string SuggestFileName(string? content, QrExportFormat format)
    {
        var text = content ?? string.Empty;

        var schema = text.IndexOf("://", StringComparison.Ordinal);
        if (schema >= 0)
        {
            text = text[(schema + 3)..];
        }
        else if (text.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase))
        {
            // Nur die SSID — das Passwort gehört nie in einen Dateinamen.
            var ssid = text.IndexOf("S:", StringComparison.Ordinal);
            text = ssid >= 0 ? "wlan-" + text[(ssid + 2)..].Split(';')[0] : "wlan";
        }

        var name = new StringBuilder();
        var lastWasDash = false;

        foreach (var c in text)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '_')
            {
                name.Append(char.ToLowerInvariant(c));
                lastWasDash = false;
            }
            else if (!lastWasDash && name.Length > 0)
            {
                name.Append('-');
                lastWasDash = true;
            }

            if (name.Length >= MaxNameLength)
            {
                break;
            }
        }

        var core = name.ToString().Trim('-', '.');

        return core.Length == 0
            ? $"qr-code.{Extension(format)}"
            : $"qr-{core}.{Extension(format)}";
    }
}
