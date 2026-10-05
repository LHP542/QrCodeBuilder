using FluentAssertions;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Tests;

public class ReleaseNotesFormatterTests
{
    [Fact]
    public void Markdown_Zeichen_verschwinden_aus_der_Anzeige()
    {
        const string notes = "## 0.2.0 — 2026-10-05\r\n\r\n- Updates aus dem Ordner\r\n  `\\\\samba01\\x` statt Internet.\r\n- Zweiter Punkt\r\n";

        var lines = ReleaseNotesFormatter.Parse(notes);

        lines.Should().Equal(
            new NotesLine("0.2.0 — 2026-10-05", true),
            new NotesLine(string.Empty, false),
            new NotesLine("• Updates aus dem Ordner", false),
            new NotesLine(@"   \\samba01\x statt Internet.", false),
            new NotesLine("• Zweiter Punkt", false));
    }

    [Fact]
    public void Mehrfache_und_randstaendige_Leerzeilen_fallen_weg()
    {
        var lines = ReleaseNotesFormatter.Parse("\n\n## A\n\n\n\n- x\n\n\n");

        lines.Select(l => l.Text).Should().Equal("A", string.Empty, "• x");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Leere_Notes_ergeben_keine_Zeilen(string? notes)
    {
        ReleaseNotesFormatter.Parse(notes).Should().BeEmpty();
    }
}
