using System.Xml.Linq;
using FluentAssertions;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Tests;

public class QrRendererTests
{
    private static readonly QrMatrix Matrix = QrMatrix.Encode("https://example.com", QrErrorCorrection.M);

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(7, false)]
    [InlineData(40, false)]
    public void Pngkantenlaenge_ist_Module_mal_Modulgroesse(int moduleSize, bool quietZone)
    {
        var style = new QrStyle { ModuleSize = moduleSize, QuietZone = quietZone };
        var expected = (Matrix.Size + (quietZone ? 8 : 0)) * moduleSize;

        var png = PngReader.Read(QrRenderer.RenderPng(Matrix, style));

        png.Width.Should().Be(expected);
        png.Height.Should().Be(expected);
        QrRenderer.PixelSize(Matrix, style).Should().Be(expected);
    }

    [Fact]
    public void Jedes_Pixel_entspricht_seinem_Modul()
    {
        var style = new QrStyle { ModuleSize = 3, QuietZone = true };
        var png = PngReader.Read(QrRenderer.RenderPng(Matrix, style));

        for (var y = 0; y < png.Height; y++)
        {
            for (var x = 0; x < png.Width; x++)
            {
                var row = (y / 3) - QrMatrix.QuietZoneModules;
                var column = (x / 3) - QrMatrix.QuietZoneModules;
                var inside = row >= 0 && row < Matrix.Size && column >= 0 && column < Matrix.Size;
                var expected = inside && Matrix[row, column] ? 1 : 0;

                if (png.IndexAt(x, y) != expected)
                {
                    Assert.Fail($"Pixel ({x},{y}) ist {png.IndexAt(x, y)}, erwartet {expected}.");
                }
            }
        }
    }

    [Fact]
    public void Palette_traegt_die_gewaehlten_Farben()
    {
        var style = new QrStyle { Foreground = new QrColor(0x12, 0x3E, 0x6B), Background = new QrColor(0xF0, 0xF1, 0xF2) };

        var png = PngReader.Read(QrRenderer.RenderPng(Matrix, style));

        png.Palette.Should().Equal(0xF0, 0xF1, 0xF2, 0x12, 0x3E, 0x6B);
        png.Transparency.Should().BeNull("deckende Farben brauchen keinen tRNS-Block");
    }

    [Fact]
    public void Transparenter_Hintergrund_landet_im_tRNS_Block()
    {
        var style = new QrStyle { Background = new QrColor(255, 255, 255, 0) };

        var png = PngReader.Read(QrRenderer.RenderPng(Matrix, style));

        png.Transparency.Should().Equal(0, 255);
        png.Chunks.Should().ContainInOrder("IHDR", "PLTE", "tRNS", "IDAT", "IEND");
    }

    [Fact]
    public void Svg_ist_wohlgeformt_und_hat_die_richtige_Groesse()
    {
        var style = new QrStyle { ModuleSize = 5 };
        var total = Matrix.Size + 8;

        var svg = XDocument.Parse(QrRenderer.RenderSvg(Matrix, style));
        XNamespace ns = "http://www.w3.org/2000/svg";

        svg.Root!.Name.Should().Be(ns + "svg");
        svg.Root.Attribute("width")!.Value.Should().Be((total * 5).ToString(System.Globalization.CultureInfo.InvariantCulture));
        svg.Root.Attribute("viewBox")!.Value.Should().Be($"0 0 {total} {total}");
        svg.Root.Element(ns + "rect")!.Attribute("fill")!.Value.Should().Be("#FFFFFF");
        svg.Root.Element(ns + "path")!.Attribute("fill")!.Value.Should().Be("#000000");
    }

    [Fact]
    public void Svg_Pfad_deckt_genau_die_dunklen_Module_ab()
    {
        var svg = XDocument.Parse(QrRenderer.RenderSvg(Matrix, new QrStyle { QuietZone = false }));
        XNamespace ns = "http://www.w3.org/2000/svg";
        var d = svg.Root!.Element(ns + "path")!.Attribute("d")!.Value;

        // Jeder Lauf ist "M{x} {y}h{n}v1h-{n}z" — die Summe der n ist die Zahl dunkler Module.
        var runs = System.Text.RegularExpressions.Regex.Matches(d, @"M(\d+) (\d+)h(\d+)v1h-\3z");
        var dark = 0;
        for (var r = 0; r < Matrix.Size; r++)
        {
            for (var c = 0; c < Matrix.Size; c++)
            {
                dark += Matrix[r, c] ? 1 : 0;
            }
        }

        runs.Sum(m => int.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(dark);
        string.Concat(runs.Select(m => m.Value)).Should().Be(d, "der Pfad besteht nur aus solchen Läufen");
    }

    [Fact]
    public void Svg_ohne_Hintergrund_und_mit_halbtransparentem_Vordergrund()
    {
        var style = new QrStyle
        {
            Foreground = new QrColor(0, 0, 0, 128),
            Background = new QrColor(255, 255, 255, 0),
        };

        var svg = QrRenderer.RenderSvg(Matrix, style);

        svg.Should().NotContain("<rect");
        svg.Should().Contain("fill-opacity=\"0.502\"");
    }
}
