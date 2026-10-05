using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace QrCodeBuilder.Services;

/// <summary>
/// Der Update-Kanal ist ein Ordner im Firmennetz statt GitHub Releases — die Ausnahme,
/// die der Kroste-Standard für dienstliche Werkzeuge vorsieht: kein Proxy, kein
/// Internetzugang nötig, Ausrollen ist ein Kopiervorgang. Vertrauensanker ist die
/// NTFS-Berechtigung auf dem Ordner.
///
/// Im Ordner liegen je Version die Pakete und eine Notes-Datei:
/// <c>QrCodeBuilder-1.2.0-win-x64.zip</c>, <c>QrCodeBuilder-1.2.0-x86_64.AppImage</c>,
/// <c>QrCodeBuilder-1.2.0-linux-x64.tar.gz</c>, <c>QrCodeBuilder-1.2.0-notes.md</c>.
/// Die Version steht im Dateinamen — sie aus dem Paket zu lesen hieße, es bei jedem
/// Start herunterzuladen.
/// </summary>
public static partial class UpdateChannel
{
    public const string AppName = "QrCodeBuilder";

    /// <summary>Ist der Wert ein Ordner (UNC, absoluter Unix-Pfad, Laufwerksbuchstabe)?</summary>
    public static bool LooksLikeFolder(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        var s = channel.Trim();

        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // "/" deckt den absoluten Unix-Pfad UND "//server/share" ab. Ohne diesen Fall
        // gälte "/srv/rollout" als Adresse — real in DTM erst im Linux-CI aufgefallen.
        return s.StartsWith('\\') || s.StartsWith('/') || (s.Length > 2 && s[1] == ':');
    }

    /// <summary>Paketart für die laufende Plattform und Form (AppImage oder entpacktes tar.gz).</summary>
    public static string PackageSuffix(bool isWindows, bool runsAsAppImage)
    {
        var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";

        if (isWindows)
        {
            return $"-win-{arch}.zip";
        }

        // Nach der laufenden Form wählen: läuft die App aus dem tar.gz und es käme das
        // AppImage, bräche der Austausch mit „kein laufendes AppImage" ab.
        return runsAsAppImage ? "-x86_64.AppImage" : $"-linux-{arch}.tar.gz";
    }

    /// <summary>
    /// Sucht das Paket mit der HÖCHSTEN Version — bewusst nicht das jüngste nach
    /// Zeitstempel: ein zurückkopiertes altes Paket wäre sonst ein „Update" nach unten.
    /// </summary>
    public static (Version Version, string Path)? FindNewestPackage(string folder, string suffix)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        (Version Version, string Path)? best = null;

        foreach (var file in Directory.EnumerateFiles(folder, $"{AppName}-*{suffix}"))
        {
            var version = ParseVersion(Path.GetFileName(file), suffix);

            if (version is not null && (best is null || version > best.Value.Version))
            {
                best = (version, file);
            }
        }

        return best;
    }

    /// <summary>Version aus <c>QrCodeBuilder-1.2.0{suffix}</c>, sonst null.</summary>
    public static Version? ParseVersion(string fileName, string suffix)
    {
        var prefix = AppName + "-";

        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var core = fileName[prefix.Length..^suffix.Length];

        return VersionPattern().IsMatch(core) && Version.TryParse(core, out var version) ? version : null;
    }

    /// <summary>
    /// Sammelt die Versionshinweise aller Versionen nach der installierten bis
    /// einschließlich der neuen, neueste zuerst — wer zwei Versionen überspringt,
    /// soll auch beide Änderungen sehen. Null, wenn keine Notes-Datei dabei ist.
    /// </summary>
    public static string? CollectReleaseNotes(string folder, Version installed, Version latest)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var notes = Directory.EnumerateFiles(folder, $"{AppName}-*-notes.md")
            .Select(file => (Version: ParseVersion(Path.GetFileName(file), "-notes.md"), File: file))
            .Where(n => n.Version is not null && n.Version > installed && n.Version <= latest)
            .OrderByDescending(n => n.Version)
            .ToList();

        if (notes.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder();

        foreach (var (_, file) in notes)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }

            text.Append(File.ReadAllText(file).Trim()).Append('\n');
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>Normalisiert MinVer-Versionen (<c>1.4.0+abc</c>, <c>1.4.1-alpha.0.3</c>) für den Vergleich.</summary>
    public static Version ParseInstalledVersion(string? informationalVersion)
    {
        var core = (informationalVersion ?? string.Empty).Split('+')[0].Split('-')[0].Trim();

        return Version.TryParse(core, out var version) ? version : new Version(0, 0, 0);
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex VersionPattern();
}
