using QRCoder;
using QRCoder.Exceptions;

namespace QrCodeBuilder.Services;

/// <summary>Fehlerkorrekturstufe: wie viel Prozent des Codes beschädigt sein dürfen.</summary>
public enum QrErrorCorrection
{
    /// <summary>ca. 7 %</summary>
    L,

    /// <summary>ca. 15 %</summary>
    M,

    /// <summary>ca. 25 %</summary>
    Q,

    /// <summary>ca. 30 %</summary>
    H,
}

/// <summary>Inhalt passt nicht in die größte QR-Version bei der gewählten Fehlerkorrektur.</summary>
public sealed class QrContentTooLongException(string message, Exception inner) : Exception(message, inner);

/// <summary>
/// Kodierte Modulmatrix OHNE Ruhezone. Die Ruhezone ist eine Frage der Darstellung,
/// nicht der Kodierung — deshalb setzt sie erst der <see cref="QrRenderer"/>.
/// </summary>
public sealed class QrMatrix
{
    /// <summary>Breite der Ruhezone in Modulen laut ISO/IEC 18004.</summary>
    public const int QuietZoneModules = 4;

    private readonly bool[,] _modules;

    private QrMatrix(bool[,] modules)
    {
        _modules = modules;
    }

    /// <summary>Kantenlänge in Modulen (21 bei Version 1, 177 bei Version 40).</summary>
    public int Size => _modules.GetLength(0);

    /// <summary>QR-Version 1 bis 40, ergibt sich aus der Kantenlänge.</summary>
    public int Version => (Size - 17) / 4;

    public bool this[int row, int column] => _modules[row, column];

    /// <summary>
    /// Kodiert den Text. QRCoder wählt die kleinste passende Version und Kodierung
    /// (ISO-8859-1, sonst UTF-8) selbst — Umlaute funktionieren damit ohne Zutun.
    /// </summary>
    /// <exception cref="QrContentTooLongException">Text passt in keine QR-Version.</exception>
    public static QrMatrix Encode(string content, QrErrorCorrection errorCorrection)
    {
        ArgumentException.ThrowIfNullOrEmpty(content);

        QRCodeData data;

        try
        {
            data = QRCodeGenerator.GenerateQrCode(content, ToEccLevel(errorCorrection));
        }
        catch (DataTooLongException ex)
        {
            throw new QrContentTooLongException(ex.Message, ex);
        }

        using (data)
        {
            // QRCoder liefert die Matrix MIT 4 Modulen Ruhezone auf jeder Seite.
            var raw = data.ModuleMatrix;
            var size = raw.Count - (2 * QuietZoneModules);
            var modules = new bool[size, size];

            for (var row = 0; row < size; row++)
            {
                var bits = raw[row + QuietZoneModules];

                for (var column = 0; column < size; column++)
                {
                    modules[row, column] = bits[column + QuietZoneModules];
                }
            }

            return new QrMatrix(modules);
        }
    }

    private static QRCodeGenerator.ECCLevel ToEccLevel(QrErrorCorrection value) => value switch
    {
        QrErrorCorrection.L => QRCodeGenerator.ECCLevel.L,
        QrErrorCorrection.Q => QRCodeGenerator.ECCLevel.Q,
        QrErrorCorrection.H => QRCodeGenerator.ECCLevel.H,
        _ => QRCodeGenerator.ECCLevel.M,
    };
}
