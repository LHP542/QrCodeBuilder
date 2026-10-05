using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using QrCodeBuilder.Localization;
using QrCodeBuilder.Persistence;
using QrCodeBuilder.Services;
using QrCodeBuilder.ViewModels;
using QrCodeBuilder.Views;

namespace QrCodeBuilder;

public partial class App : Application
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    // GC-Referenz: ohne Feld sammelt der GC den TrayController ein und das Icon verschwindet.
    private TrayController? _tray;

    private ServiceProvider? _services;
    private MainWindow? _mainWindow;

    /// <summary>Vom Program.Main übergebener Single-Instance-Guard (null im Werkzeugmodus).</summary>
    public static SingleInstanceGuard? PendingGuard { get; set; }

    /// <summary>Container für Fenster, die erst auf Klick entstehen (Einstellungen, Über).</summary>
    public static IServiceProvider? Services { get; private set; }

    public static bool IsToolMode(string[] args) => ScreenshotMode.IsRequested(args);

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        GlobalExceptionHandler.Install();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Werkzeugmodus: Fenster mit Demodaten rendern und beenden, ohne Hauptfenster.
            if (ScreenshotMode.IsRequested(desktop.Args ?? []))
            {
                ScreenshotMode.Run(desktop, desktop.Args ?? []);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            _services = BuildServices(() => _mainWindow);
            Services = _services;

            // Sprache setzen, BEVOR das erste Fenster gebaut wird — sonst flackert es.
            var settings = _services.GetRequiredService<SettingsService>().Load();

            if (!string.IsNullOrWhiteSpace(settings.UiCulture))
            {
                LocalizationService.Instance.SetCulture(settings.UiCulture);
            }

            _mainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
            };

            desktop.MainWindow = _mainWindow;

            _tray = new TrayController(this, _mainWindow);
            _tray.Install();

            if (PendingGuard is { } guard)
            {
                // Feuert im ThreadPool — Fensterzugriffe gehören auf den UI-Thread.
                guard.ActivationRequested += () => Dispatcher.UIThread.Post(() => _tray?.Restore());
            }

            _mainWindow.Opened += OnMainWindowOpened;

            desktop.Exit += (_, _) =>
            {
                Log.Info("Desktop-Lifetime beendet.");
                _services?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Update-Check beim Start — im Hintergrund, damit ein hängender Proxy nie das
    /// Hauptfenster aufhält, und nur mit Zustimmung des Nutzers vor der Installation.
    /// </summary>
    private async void OnMainWindowOpened(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        // Einmal pro Start reicht — sonst poppt der Dialog nach jedem Wiederherstellen erneut auf.
        window.Opened -= OnMainWindowOpened;

        var updateService = _services?.GetService<UpdateService>();

        if (updateService is null)
        {
            return;
        }

        try
        {
            var result = await updateService.CheckForUpdateAsync().ConfigureAwait(true);

            // Ohne Paket für diese Plattform gäbe es nichts zu installieren.
            if (!result.CanInstall)
            {
                return;
            }

            Log.Info("Update {Version} verfügbar — Nutzer wird gefragt.", result.LatestVersion);

            var prompt = new UpdatePromptWindow(updateService, result);
            await prompt.ShowDialog(window);
        }
        catch (Exception ex)
        {
            // Ein fehlgeschlagener Check darf die App nie stören.
            Log.Warn(ex, "Update-Check beim Start fehlgeschlagen.");
        }
    }

    private static ServiceProvider BuildServices(Func<TopLevel?> mainWindow)
    {
        var services = new ServiceCollection();

        services.AddSingleton(_ => new SettingsService(SettingsService.DefaultDirectory));
        // Der Ordner wird bei jeder Prüfung frisch gelesen — eine Änderung in den
        // Einstellungen wirkt damit ohne Neustart.
        services.AddSingleton(sp => new UpdateService(() => sp.GetRequiredService<SettingsService>().Load().UpdateChannel));
        services.AddSingleton<ISaveFileDialog>(_ => new StorageSaveFileDialog(mainWindow));
        services.AddSingleton<IImageClipboard>(_ => new AvaloniaImageClipboard(mainWindow));

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
