using QrCodeBuilder.Services;

namespace QrCodeBuilder.Persistence;

/// <summary>
/// Gespeicherte Einstellungen: die zuletzt benutzte Darstellung, damit der nächste
/// Code genauso aussieht wie der letzte. Inhalte (Texte, WLAN-Passwörter) werden
/// bewusst NICHT gespeichert.
/// </summary>
public sealed record AppSettings
{
    /// <summary>ISO-Code der UI-Sprache. Nullable für Rückwärtskompatibilität.</summary>
    public string? UiCulture { get; init; }

    public QrErrorCorrection ErrorCorrection { get; init; } = QrErrorCorrection.M;

    public int ModuleSize { get; init; } = 10;

    public string Foreground { get; init; } = QrColor.Black.ToHex();

    public string Background { get; init; } = QrColor.White.ToHex();

    public bool QuietZone { get; init; } = true;

    /// <summary>Netzwerkordner mit den Update-Paketen und Versionshinweisen.</summary>
    public string? UpdateChannel { get; init; } = DefaultUpdateChannel;

    public const string DefaultUpdateChannel = @"\\samba01\542$\5424_IT-Basis-Dienste\QrCodeBuilder";

    public static AppSettings Default { get; } = new();
}
