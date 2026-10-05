using FluentAssertions;
using NLog;
using NLog.Layouts;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Tests;

public class MaskingTests
{
    [Theory]
    [InlineData("api_key=abc123", "api_key=***")]
    [InlineData("Token: sehr-geheim", "Token=***")]
    [InlineData("Passwort=hunter2", "Passwort=***")]
    [InlineData("Server=db1;Password=geheim;Database=x", "Server=db1;Password=***;Database=x")]
    public void Geheimnisse_werden_maskiert(string eingabe, string erwartet)
    {
        MaskingLayoutRenderer.Mask(eingabe).Should().Be(erwartet);
    }

    [Fact]
    public void Harmloser_Text_bleibt_unveraendert()
    {
        const string text = "Export geschrieben: qr-example.com.png in 12 ms";

        MaskingLayoutRenderer.Mask(text).Should().Be(text);
    }

    [Fact]
    public void Layout_rendert_die_vollstaendige_Nachricht()
    {
        // Regressionstest: ist der masked-Renderer nicht registriert, liefert NLog
        // für diese Layout-Zeile nur noch "}" statt der Nachricht.
        var layout = Layout.FromString("${masked:inner=${message}}");
        var ereignis = LogEventInfo.Create(LogLevel.Info, "test", "Bild kopiert: 290 × 290 px");

        layout.Render(ereignis).Should().Be("Bild kopiert: 290 × 290 px");
    }

    [Fact]
    public void Layout_maskiert_ein_Geheimnis_in_der_Nachricht()
    {
        var layout = Layout.FromString("${masked:inner=${message}}");
        var ereignis = LogEventInfo.Create(LogLevel.Info, "test", "Verbindung mit password=Fake-Geheimnis-42 aufgebaut");

        var text = layout.Render(ereignis);

        text.Should().NotContain("Fake-Geheimnis-42").And.Contain("Verbindung mit");
    }
}
