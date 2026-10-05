using Avalonia.Controls.ApplicationLifetimes;
#if DEBUG
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NLog;
using QrCodeBuilder.Localization;
using QrCodeBuilder.Persistence;
using QrCodeBuilder.Services;
using QrCodeBuilder.ViewModels;
#endif

namespace QrCodeBuilder.Views;

/// <summary>
/// Werkzeugmodus (nur Debug): baut die Fenster mit Demodaten, rendert sie im eigenen
/// Prozess per RenderTargetBitmap in PNGs und beendet sich. Aufruf:
///
/// <code>dotnet run --project QrCodeBuilder -- --screenshots &lt;ordner&gt;</code>
///
/// Warum: UI-Fernsteuerung von außen (SetForegroundWindow, PrintWindow) ist nach
/// Kroste-Standard tabu — Verhaltens-AV hält das für einen RAT. Der Weg führt über
/// den eigenen Prozess. Die Bilder danach ANSEHEN, nicht nur die Dateigröße prüfen.
/// </summary>
internal static class ScreenshotMode
{
    public const string Switch = "--screenshots";

#if DEBUG
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static bool IsRequested(string[] args) =>
        args.Any(a => string.Equals(a, Switch, StringComparison.OrdinalIgnoreCase));

    public static void Run(IClassicDesktopStyleApplicationLifetime desktop, string[] args)
    {
        // Sonst beendet sich die App nach dem ersten geschlossenen Fenster — wortlos.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var index = Array.FindIndex(args, a => string.Equals(a, Switch, StringComparison.OrdinalIgnoreCase));
        var target = index >= 0 && index + 1 < args.Length ? args[index + 1] : "screenshots";

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RenderAllAsync(Path.GetFullPath(target));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Werkzeugmodus fehlgeschlagen.");
                Console.Error.WriteLine(ex);
            }
            finally
            {
                desktop.Shutdown();
            }
        });
    }

    private static async Task RenderAllAsync(string directory)
    {
        Directory.CreateDirectory(directory);

        // Eigenes Einstellungsverzeichnis: der Werkzeugmodus darf die echten Einstellungen
        // nicht verändern und keine echten Werte ins Bild holen.
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "QrCodeBuilder-screenshots");

        if (Directory.Exists(settingsDirectory))
        {
            Directory.Delete(settingsDirectory, recursive: true);
        }

        var settings = new SettingsService(settingsDirectory);

        foreach (var iso in new[] { "en", "de" })
        {
            LocalizationService.Instance.SetCulture(iso);

            var link = NewViewModel(settings);
            link.Text = "https://github.com/LHP542/QrCodeBuilder";
            await CaptureAsync(new MainWindow { DataContext = link }, directory, $"main-text-{iso}.png");

            var wifi = NewViewModel(settings);
            wifi.SelectedContentKind = wifi.ContentKinds.First(k => k.Value == QrContentKind.Wifi);
            wifi.WifiSsid = "Gäste-WLAN";
            wifi.WifiPassword = "Demo-Kennwort;123";
            wifi.ForegroundHex = "#123E6B";
            await CaptureAsync(new MainWindow { DataContext = wifi }, directory, $"main-wifi-{iso}.png");

            var warning = NewViewModel(settings);
            warning.Text = "Kontrast zu gering";
            warning.ForegroundHex = "#BBBBBB";
            await CaptureAsync(new MainWindow { DataContext = warning }, directory, $"main-warning-{iso}.png");

            await CaptureAsync(new SettingsWindow(new SettingsWindowViewModel(settings)), directory, $"settings-{iso}.png");

            var updates = new UpdateService(() => null);
            await CaptureAsync(new AboutWindow(updates), directory, $"about-{iso}.png");

            await CaptureAsync(new UpdatePromptWindow(updates, DemoUpdate), directory, $"update-{iso}.png");
        }
    }

    /// <summary>Erfundenes Update für das Bild des Update-Dialogs — es wird nichts installiert.</summary>
    private static readonly UpdateCheckResult DemoUpdate = new(
        UpdateAvailable: true,
        LatestVersion: "0.3.0",
        PackagePath: "demo.zip",
        ReleaseNotes: """
            ## 0.3.0 — Beispiel

            - Neu: Kontakt (vCard) als Inhaltstyp
            - Behoben: Dateiname bei sehr langen Links

            ## 0.2.0 — Beispiel

            - Updates und Versionshinweise kommen aus dem Netzwerkordner
            """);

    /// <summary>
    /// Jedes Bild bekommt frische Einstellungen — sonst übernimmt das nächste Bild die
    /// gespeicherte Darstellung des vorigen (die App merkt sich Farben bewusst).
    /// </summary>
    private static MainWindowViewModel NewViewModel(SettingsService settings)
    {
        settings.Save(AppSettings.Default);
        return new MainWindowViewModel(settings, new NoDialog(), new NoClipboard());
    }

    private static async Task CaptureAsync(Window window, string directory, string fileName)
    {
        var opened = new TaskCompletionSource();
        window.Opened += (_, _) => opened.TrySetResult();
        window.Show();
        await opened.Task;

        // Einen Layout-Durchlauf abwarten, damit Bindings und Bild stehen.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);

        using (var bitmap = new RenderTargetBitmap(size, new Vector(96, 96)))
        {
            bitmap.Render(window);

            var path = Path.Combine(directory, fileName);
            bitmap.Save(path, new PngBitmapEncoderOptions());
            Console.WriteLine($"geschrieben: {path} ({size.Width}x{size.Height})");
            Log.Info("Screenshot geschrieben: {Path}", path);
        }

        window.Close();
    }

    private sealed class NoDialog : ISaveFileDialog
    {
        public Task<SaveTarget?> PickAsync(string suggestedFileName, QrExportFormat format) =>
            Task.FromResult<SaveTarget?>(null);
    }

    private sealed class NoClipboard : IImageClipboard
    {
        public Task CopyPngAsync(byte[] png) => Task.CompletedTask;
    }
#else
    public static bool IsRequested(string[] args) => false;

    public static void Run(IClassicDesktopStyleApplicationLifetime desktop, string[] args)
    {
    }
#endif
}
