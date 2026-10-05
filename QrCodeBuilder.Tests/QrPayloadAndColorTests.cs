using FluentAssertions;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Tests;

public class QrPayloadTests
{
    [Fact]
    public void Wpa_Netz_mit_Passwort()
    {
        QrPayload.Wifi("Gäste", "geheim", WifiSecurity.Wpa, hidden: false)
            .Should().Be("WIFI:T:WPA;S:Gäste;P:geheim;;");
    }

    [Fact]
    public void Offenes_Netz_laesst_das_Passwort_weg()
    {
        QrPayload.Wifi("Cafe", "wird-ignoriert", WifiSecurity.None, hidden: false)
            .Should().Be("WIFI:T:nopass;S:Cafe;;");
    }

    [Fact]
    public void Verstecktes_Netz_wird_markiert()
    {
        QrPayload.Wifi("Labor", "x", WifiSecurity.Wep, hidden: true)
            .Should().Be("WIFI:T:WEP;S:Labor;P:x;H:true;;");
    }

    [Theory]
    [InlineData(@"a;b", @"a\;b")]
    [InlineData(@"a,b", @"a\,b")]
    [InlineData(@"a:b", @"a\:b")]
    [InlineData(@"a\b", @"a\\b")]
    [InlineData("a\"b", "a\\\"b")]
    [InlineData("normal", "normal")]
    public void Sonderzeichen_werden_entwertet(string roh, string erwartet)
    {
        QrPayload.Escape(roh).Should().Be(erwartet);
    }

    [Fact]
    public void Semikolon_im_Passwort_bricht_das_Feld_nicht_ab()
    {
        QrPayload.Wifi("Netz", "pass;word", WifiSecurity.Wpa, hidden: false)
            .Should().Contain(@"P:pass\;word;");
    }
}

public class QrColorTests
{
    [Theory]
    [InlineData("#000000", 0, 0, 0, 255)]
    [InlineData("123E6B", 0x12, 0x3E, 0x6B, 255)]
    [InlineData("#fff", 255, 255, 255, 255)]
    [InlineData("#80FF0000", 255, 0, 0, 0x80)]
    [InlineData("  #00ffffff ", 255, 255, 255, 0)]
    public void Gueltige_Schreibweisen_werden_gelesen(string text, int r, int g, int b, int a)
    {
        QrColor.TryParse(text, out var color).Should().BeTrue();

        color.Should().Be(new QrColor((byte)r, (byte)g, (byte)b, (byte)a));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("rot")]
    public void Ungueltige_Eingaben_werden_abgelehnt(string? text)
    {
        QrColor.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public void Hex_Schreibweise_ist_umkehrbar()
    {
        new QrColor(0x12, 0x3E, 0x6B).ToHex().Should().Be("#123E6B");
        new QrColor(0x12, 0x3E, 0x6B, 0x40).ToHex().Should().Be("#40123E6B");
    }

    [Fact]
    public void Schwarz_auf_Weiss_hat_den_maximalen_Kontrast()
    {
        QrColor.Contrast(QrColor.Black, QrColor.White).Should().BeApproximately(21.0, 0.01);
        QrColor.Contrast(QrColor.White, QrColor.White).Should().BeApproximately(1.0, 0.01);
    }
}
