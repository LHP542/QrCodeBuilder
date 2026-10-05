using NLog;

namespace QrCodeBuilder.Persistence;

/// <summary>Lädt und speichert <see cref="AppSettings"/> atomar über den <see cref="JsonStore"/>.</summary>
public sealed class SettingsService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly JsonStore _store;

    public SettingsService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _store = new JsonStore(Path.Combine(dataDirectory, "settings.json"));
    }

    /// <summary>Standard-Datenverzeichnis: %AppData%\QrCodeBuilder bzw. ~/.config/QrCodeBuilder.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QrCodeBuilder");

    public AppSettings Load()
    {
        var stored = _store.Load<AppSettings>();

        if (stored is null)
        {
            Log.Info("Keine Einstellungen gefunden — Standardwerte werden verwendet.");
            return AppSettings.Default;
        }

        return stored;
    }

    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return _store.Save(settings);
    }
}
