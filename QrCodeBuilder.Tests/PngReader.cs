using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace QrCodeBuilder.Tests;

/// <summary>
/// Minimaler Leser für genau das PNG, das QrRenderer schreibt (Palette, 1 Bit,
/// Filter 0). Prüft dabei Signatur und CRC jedes Blocks — ein falscher CRC würde in
/// Bildbetrachtern als „beschädigte Datei" auffallen, aber in keinem anderen Test.
/// </summary>
internal sealed class PngReader
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly byte[] _pixels;

    private PngReader(int width, int height, byte[] palette, byte[]? transparency, byte[] pixels, List<string> chunks)
    {
        Width = width;
        Height = height;
        Palette = palette;
        Transparency = transparency;
        _pixels = pixels;
        Chunks = chunks;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Palette { get; }

    public byte[]? Transparency { get; }

    public IReadOnlyList<string> Chunks { get; }

    /// <summary>Palettenindex an (x, y): 0 = Hintergrund, 1 = Vordergrund.</summary>
    public int IndexAt(int x, int y)
    {
        var rowBytes = (Width + 7) / 8;
        var b = _pixels[(y * (rowBytes + 1)) + 1 + (x >> 3)];
        return (b >> (7 - (x & 7))) & 1;
    }

    public static PngReader Read(byte[] png)
    {
        if (!png.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new InvalidDataException("Keine PNG-Signatur.");
        }

        var position = 8;
        var chunks = new List<string>();
        var idat = new MemoryStream();
        int width = 0, height = 0;
        byte[] palette = [];
        byte[]? transparency = null;

        while (position < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
            var type = Encoding.ASCII.GetString(png, position + 4, 4);
            var data = png.AsSpan(position + 8, length).ToArray();
            var crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position + 8 + length));

            if (crc != Crc(png.AsSpan(position + 4, 4 + length)))
            {
                throw new InvalidDataException($"CRC von {type} stimmt nicht.");
            }

            chunks.Add(type);

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));
                    if (data[8] != 1 || data[9] != 3)
                    {
                        throw new InvalidDataException("Erwartet: Palette mit 1 Bit.");
                    }

                    break;
                case "PLTE":
                    palette = data;
                    break;
                case "tRNS":
                    transparency = data;
                    break;
                case "IDAT":
                    idat.Write(data);
                    break;
            }

            position += 12 + length;
        }

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);

        return new PngReader(width, height, palette, transparency, raw.ToArray(), chunks);
    }

    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in data)
        {
            crc ^= b;

            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
