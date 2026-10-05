namespace QrCodeBuilder.Services;

/// <summary>Eine Anzeigezeile der Versionshinweise.</summary>
public sealed record NotesLine(string Text, bool IsHeading);

/// <summary>
/// Übersetzt das kleine Markdown-Subset aus CHANGELOG.md in Anzeigezeilen:
/// <c>## Überschrift</c>, <c>- Punkt</c> mit eingerückten Folgezeilen, `Code`.
/// Mehr kann die Datei nicht, mehr braucht sie nicht.
/// </summary>
public static class ReleaseNotesFormatter
{
    public static IReadOnlyList<NotesLine> Parse(string? markdown)
    {
        var lines = new List<NotesLine>();

        if (string.IsNullOrWhiteSpace(markdown))
        {
            return lines;
        }

        foreach (var raw in markdown.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd().Replace("`", string.Empty, StringComparison.Ordinal);
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith('#'))
            {
                lines.Add(new NotesLine(trimmed.TrimStart('#').Trim(), IsHeading: true));
            }
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                lines.Add(new NotesLine("• " + trimmed[2..], IsHeading: false));
            }
            else if (line.StartsWith("  ", StringComparison.Ordinal) && trimmed.Length > 0)
            {
                // Fortsetzung eines Listenpunkts — unter dem Text einrücken, nicht unter dem Punkt.
                lines.Add(new NotesLine("   " + trimmed, IsHeading: false));
            }
            else
            {
                lines.Add(new NotesLine(trimmed, IsHeading: false));
            }
        }

        // Keine Leerzeilen am Anfang/Ende und nie zwei hintereinander.
        var compact = new List<NotesLine>();

        foreach (var line in lines)
        {
            var empty = line.Text.Length == 0;

            if (empty && (compact.Count == 0 || compact[^1].Text.Length == 0))
            {
                continue;
            }

            compact.Add(line);
        }

        while (compact.Count > 0 && compact[^1].Text.Length == 0)
        {
            compact.RemoveAt(compact.Count - 1);
        }

        return compact;
    }
}
