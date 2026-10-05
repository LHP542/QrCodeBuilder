using FluentAssertions;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Tests;

public class QrMatrixTests
{
    [Fact]
    public void Kurzer_Text_ergibt_Version_1_mit_21_Modulen()
    {
        var matrix = QrMatrix.Encode("Hallo", QrErrorCorrection.M);

        matrix.Size.Should().Be(21);
        matrix.Version.Should().Be(1);
    }

    [Fact]
    public void Ruhezone_ist_abgeschnitten_und_Finder_sitzen_in_den_Ecken()
    {
        // Wäre der Versatz beim Ausschneiden der Ruhezone falsch, lägen die Finder
        // verschoben — der Code sähe richtig aus und wäre trotzdem unlesbar.
        var matrix = QrMatrix.Encode("https://github.com/LHP542/QrCodeBuilder", QrErrorCorrection.M);
        var n = matrix.Size;

        AssertFinder(matrix, 0, 0);
        AssertFinder(matrix, 0, n - 7);
        AssertFinder(matrix, n - 7, 0);
    }

    [Fact]
    public void Timing_Muster_wechselt_zwischen_den_Findern_ab()
    {
        var matrix = QrMatrix.Encode("Timing", QrErrorCorrection.L);

        for (var i = 8; i < matrix.Size - 8; i++)
        {
            matrix[6, i].Should().Be(i % 2 == 0, $"Zeile 6, Spalte {i}");
            matrix[i, 6].Should().Be(i % 2 == 0, $"Spalte 6, Zeile {i}");
        }
    }

    [Fact]
    public void Hoehere_Fehlerkorrektur_braucht_bei_gleichem_Inhalt_mindestens_gleich_viele_Module()
    {
        const string text = "https://example.com/ein/etwas/laengerer/pfad?mit=parametern";

        var low = QrMatrix.Encode(text, QrErrorCorrection.L);
        var high = QrMatrix.Encode(text, QrErrorCorrection.H);

        high.Size.Should().BeGreaterThan(low.Size);
    }

    [Fact]
    public void Umlaute_und_Emoji_lassen_sich_kodieren()
    {
        var act = () => QrMatrix.Encode("Grüße aus Potsdam 🌳", QrErrorCorrection.Q);

        act.Should().NotThrow();
    }

    [Fact]
    public void Zu_langer_Inhalt_meldet_eine_eigene_Ausnahme()
    {
        var text = new string('x', 5000);

        var act = () => QrMatrix.Encode(text, QrErrorCorrection.H);

        act.Should().Throw<QrContentTooLongException>();
    }

    private static void AssertFinder(QrMatrix matrix, int top, int left)
    {
        for (var r = 0; r < 7; r++)
        {
            for (var c = 0; c < 7; c++)
            {
                var ring = r is 0 or 6 || c is 0 or 6;
                var core = r is >= 2 and <= 4 && c is >= 2 and <= 4;

                matrix[top + r, left + c].Should().Be(ring || core, $"Finder bei ({top},{left}), Zelle ({r},{c})");
            }
        }
    }
}
