using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace QrCodeBuilder.Services;

/// <summary>Darstellungsoptionen — alles außer dem Inhalt selbst.</summary>
public sealed record QrStyle
{
    public const int MinModuleSize = 1;
    public const int MaxModuleSize = 40;

    /// <summary>Kantenlänge eines Moduls in Pixeln (PNG) bzw. Einheiten (SVG).</summary>
    public int ModuleSize { get; init; } = 10;

    public QrColor Foreground { get; init; } = QrColor.Black;

    public QrColor Background { get; init; } = QrColor.White;

    /// <summary>Vier Module Rand. Ohne ihn lesen viele Scanner den Code schlecht.</summary>
    public bool QuietZone { get; init; } = true;
}

/// <summary>
/// Zeichnet eine <see cref="QrMatrix"/> als PNG oder SVG.
///
/// Bewusst selbst geschrieben statt über die Renderer von QRCoder: deren
/// Farbüberladungen hängen an System.Drawing, das unter Linux nicht läuft.
/// Für zwei Farben genügt ein Paletten-PNG mit 1 Bit pro Pixel — klein, verlustfrei
/// und mit Transparenz über den tRNS-Block.
/// </summary>
public static class QrRenderer
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Kantenlänge in Modulen inklusive Ruhezone.</summary>
    public static int TotalModules(QrMatrix matrix, QrStyle style) =>
        matrix.Size + (style.QuietZone ? 2 * QrMatrix.QuietZoneModules : 0);

    /// <summary>Kantenlänge des fertigen Bildes in Pixeln.</summary>
    public static int PixelSize(QrMatrix matrix, QrStyle style) =>
        TotalModules(matrix, style) * ClampModuleSize(style.ModuleSize);

    public static byte[] RenderPng(QrMatrix matrix, QrStyle style)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(style);

        var moduleSize = ClampModuleSize(style.ModuleSize);
        var offset = style.QuietZone ? QrMatrix.QuietZoneModules : 0;
        var pixels = PixelSize(matrix, style);
        var rowBytes = (pixels + 7) / 8;

        using var png = new MemoryStream();
        png.Write(PngSignature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), pixels);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), pixels);
        header[8] = 1;  // Bit pro Pixel
        header[9] = 3;  // Farbtyp: Palette
        WriteChunk(png, "IHDR", header);

        // Index 0 = Hintergrund, Index 1 = Vordergrund.
        WriteChunk(png, "PLTE",
        [
            style.Background.R, style.Background.G, style.Background.B,
            style.Foreground.R, style.Foreground.G, style.Foreground.B,
        ]);

        if (!style.Background.IsOpaque || !style.Foreground.IsOpaque)
        {
            WriteChunk(png, "tRNS", [style.Background.A, style.Foreground.A]);
        }

        WriteChunk(png, "IDAT", CompressScanlines(matrix, moduleSize, offset, pixels, rowBytes));
        WriteChunk(png, "IEND", []);

        return png.ToArray();
    }

    public static string RenderSvg(QrMatrix matrix, QrStyle style)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(style);

        var total = TotalModules(matrix, style);
        var pixels = PixelSize(matrix, style);
        var offset = style.QuietZone ? QrMatrix.QuietZoneModules : 0;
        var inv = CultureInfo.InvariantCulture;

        // Hart '\n' statt AppendLine: SVG soll auf jedem System dieselben Bytes ergeben.
        var svg = new StringBuilder();
        svg.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>").Append('\n');
        svg.Append(inv, $"<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"{pixels}\" height=\"{pixels}\" viewBox=\"0 0 {total} {total}\" shape-rendering=\"crispEdges\">").Append('\n');

        if (!style.Background.IsTransparent)
        {
            svg.Append(inv, $"<rect width=\"{total}\" height=\"{total}\" fill=\"{style.Background.ToRgbHex()}\"{Opacity(style.Background)}/>").Append('\n');
        }

        svg.Append(inv, $"<path fill=\"{style.Foreground.ToRgbHex()}\"{Opacity(style.Foreground)} d=\"");

        // Waagerechte Läufe dunkler Module zu je einem Rechteck zusammenfassen —
        // das macht die Datei um ein Vielfaches kleiner als ein Rechteck pro Modul.
        for (var row = 0; row < matrix.Size; row++)
        {
            var column = 0;

            while (column < matrix.Size)
            {
                if (!matrix[row, column])
                {
                    column++;
                    continue;
                }

                var start = column;

                while (column < matrix.Size && matrix[row, column])
                {
                    column++;
                }

                svg.Append(inv, $"M{start + offset} {row + offset}h{column - start}v1h-{column - start}z");
            }
        }

        svg.Append("\"/>").Append('\n');
        svg.Append("</svg>").Append('\n');

        return svg.ToString();
    }

    private static string Opacity(QrColor color) => color.IsOpaque
        ? string.Empty
        : string.Create(CultureInfo.InvariantCulture, $" fill-opacity=\"{color.A / 255.0:0.###}\"");

    private static int ClampModuleSize(int value) =>
        Math.Clamp(value, QrStyle.MinModuleSize, QrStyle.MaxModuleSize);

    private static byte[] CompressScanlines(QrMatrix matrix, int moduleSize, int offset, int pixels, int rowBytes)
    {
        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            // Eine Zeile pro Modulreihe bauen und moduleSize-mal schreiben — die Zeilen
            // innerhalb eines Moduls sind identisch.
            var line = new byte[rowBytes + 1];

            for (var moduleRow = 0; moduleRow < pixels / moduleSize; moduleRow++)
            {
                Array.Clear(line);

                var row = moduleRow - offset;

                if (row >= 0 && row < matrix.Size)
                {
                    for (var x = 0; x < pixels; x++)
                    {
                        var column = (x / moduleSize) - offset;

                        if (column >= 0 && column < matrix.Size && matrix[row, column])
                        {
                            // Filterbyte 0 an Position 0, danach MSB zuerst.
                            line[1 + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                        }
                    }
                }

                for (var repeat = 0; repeat < moduleSize; repeat++)
                {
                    zlib.Write(line);
                }
            }
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream target, string type, byte[] data)
    {
        Span<byte> buffer = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        target.Write(buffer);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        target.Write(typeBytes);
        target.Write(data);

        var crc = Crc32.Update(Crc32.Update(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
        target.Write(buffer);
    }

    /// <summary>CRC-32 nach PNG-Spezifikation (Polynom 0xEDB88320).</summary>
    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Update(uint crc, ReadOnlySpan<byte> data)
        {
            foreach (var b in data)
            {
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }

            return crc;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];

            for (uint n = 0; n < 256; n++)
            {
                var c = n;

                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }

                table[n] = c;
            }

            return table;
        }
    }
}
