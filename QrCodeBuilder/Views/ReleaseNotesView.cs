using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Views;

/// <summary>
/// Zeigt die Versionshinweise im Update-Dialog und im Über-Fenster:
/// Überschriften fett, Listenpunkte als „•" — ohne die rohen Markdown-Zeichen.
/// </summary>
internal static class ReleaseNotesView
{
    public static void Show(SelectableTextBlock target, string? markdown)
    {
        target.Inlines ??= [];
        target.Inlines.Clear();

        var first = true;

        foreach (var line in ReleaseNotesFormatter.Parse(markdown))
        {
            if (!first)
            {
                target.Inlines.Add(new LineBreak());
            }

            first = false;

            target.Inlines.Add(line.IsHeading
                ? new Run(line.Text) { FontWeight = FontWeight.SemiBold }
                : new Run(line.Text));
        }
    }
}
