// WICHTIG (Avalonia 12): KEIN manuelles InitializeComponent() definieren —
// der NameGenerator emittiert es zusammen mit den x:Name-Feldern selbst.
using Avalonia.Interactivity;
using Avalonia.Threading;
using QrCodeBuilder.Localization;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Views;

/// <summary>
/// Zustimmung zum Self-Update beim Start (Kroste-Standard: Update-Check ist nicht
/// blockierend, die Installation passiert nie ungefragt). Zeigt den Fortschritt und
/// beendet die App, sobald der Installer läuft.
/// </summary>
public partial class UpdatePromptWindow : ChromeWindow
{
    private readonly string? _packagePath;

    // Parameterloser Ctor für den XAML-Designer.
    public UpdatePromptWindow()
    {
        InitializeComponent();
    }

    public UpdatePromptWindow(UpdateService updateService, UpdateCheckResult result) : this()
    {
        _packagePath = result.PackagePath;

        Headline.Text = L.F("Update_Headline", result.LatestVersion);
        Body.Text = L.F("Update_Body", updateService.CurrentVersion);

        // Versionshinweise aus dem Update-Ordner — fehlen sie, bleibt die Karte weg.
        ReleaseNotesView.Show(Notes, result.ReleaseNotes);
        NotesCard.IsVisible = !string.IsNullOrWhiteSpace(result.ReleaseNotes);

        LaterButton.Click += (_, _) => Close();
        InstallButton.Click += OnInstall;
    }

    private async void OnInstall(object? sender, RoutedEventArgs e)
    {
        if (_packagePath is null) return;

        InstallButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        Progress.IsVisible = true;
        Status.IsVisible = true;

        var progress = new Progress<double>(value => Dispatcher.UIThread.Post(() =>
        {
            Progress.Value = value;
            Status.Text = L.F("Update_Downloading", (int)(value * 100));
        }));

        var started = await UpdateService.DownloadAndApplyAsync(_packagePath, progress);

        if (!started)
        {
            Status.Text = L.T("Update_Failed");
            Progress.IsVisible = false;
            InstallButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            return;
        }

        // PFLICHT: Das Installer-Skript wartet auf das Prozessende — ohne dieses
        // Beenden hängt es und die Anzeige bleibt bei 100 % stehen.
        Status.Text = L.T("Update_Restarting");
        UpdateService.TerminateForUpdate();
    }
}
