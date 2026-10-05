using FluentAssertions;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Tests;

public class QrExportTests
{
    [Theory]
    [InlineData("https://example.com/shop", QrExportFormat.Png, "qr-example.com-shop.png")]
    [InlineData("https://example.com/", QrExportFormat.Svg, "qr-example.com.svg")]
    [InlineData("Hallo Welt!", QrExportFormat.Png, "qr-hallo-welt.png")]
    [InlineData("", QrExportFormat.Png, "qr-code.png")]
    [InlineData("äöü", QrExportFormat.Svg, "qr-code.svg")]
    [InlineData(null, QrExportFormat.Png, "qr-code.png")]
    public void Dateinamensvorschlag(string? inhalt, QrExportFormat format, string erwartet)
    {
        QrExport.SuggestFileName(inhalt, format).Should().Be(erwartet);
    }

    [Fact]
    public void Wlan_Vorschlag_nennt_die_SSID_aber_nie_das_Passwort()
    {
        var payload = QrPayload.Wifi("Gast", "SehrGeheim", WifiSecurity.Wpa, hidden: false);

        var name = QrExport.SuggestFileName(payload, QrExportFormat.Png);

        name.Should().Be("qr-wlan-gast.png");
        name.Should().NotContainEquivalentOf("geheim");
    }

    [Fact]
    public void Sehr_langer_Inhalt_ergibt_einen_kurzen_Namen()
    {
        var name = QrExport.SuggestFileName(new string('a', 500), QrExportFormat.Png);

        name.Length.Should().BeLessThan(60);
    }

    [Fact]
    public async Task Png_Export_schreibt_ein_gueltiges_PNG()
    {
        var matrix = QrMatrix.Encode("Export", QrErrorCorrection.M);
        using var stream = new MemoryStream();

        await QrExport.WriteAsync(stream, QrExportFormat.Png, matrix, new QrStyle { ModuleSize = 4 });

        PngReader.Read(stream.ToArray()).Width.Should().Be((21 + 8) * 4);
    }

    [Fact]
    public async Task Svg_Export_ist_UTF8_ohne_BOM()
    {
        var matrix = QrMatrix.Encode("Export", QrErrorCorrection.M);
        using var stream = new MemoryStream();

        await QrExport.WriteAsync(stream, QrExportFormat.Svg, matrix, new QrStyle());

        var bytes = stream.ToArray();
        bytes.Take(5).Should().Equal("<?xml"u8.ToArray(), "ein BOM vor der XML-Deklaration stört manche Grafikprogramme");
    }
}
