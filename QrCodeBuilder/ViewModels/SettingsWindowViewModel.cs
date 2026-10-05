using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using QrCodeBuilder.Localization;
using QrCodeBuilder.Persistence;

namespace QrCodeBuilder.ViewModels;

/// <summary>Auswahleintrag des Sprachumschalters — Flagge plus Eigenbezeichnung.</summary>
public sealed record CultureOption(string Iso, string Display, string Flag)
{
    public override string ToString() => $"{Flag} {Display}";
}

public sealed partial class SettingsWindowViewModel : ViewModelBase
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private CultureOption _selectedCulture;

    public SettingsWindowViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        Cultures = LocalizationService.SupportedCultures
            .Select(c => new CultureOption(c.Iso, c.Display, c.Flag))
            .ToList();

        _selectedCulture = Cultures.FirstOrDefault(c => c.Iso == LocalizationService.Instance.CurrentIso)
                           ?? Cultures[0];
    }

    public IReadOnlyList<CultureOption> Cultures { get; }

    partial void OnSelectedCultureChanged(CultureOption value)
    {
        // Wirkt sofort in allen Fenstern — kein Neustart-Hinweis.
        LocalizationService.Instance.SetCulture(value.Iso);
        _settingsService.Save(_settingsService.Load() with { UiCulture = value.Iso });

        Log.Info("UI-Sprache gewechselt auf {Iso}.", value.Iso);
    }
}
