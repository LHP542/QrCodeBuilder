using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NLog;
using QrCodeBuilder.Localization;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Views;

/// <summary>
/// System-Tray-Integration nach Kroste-Standard.
/// - <b>Minimieren</b> → Fenster verschwindet in den Tray (<see cref="Window.Hide"/>).
/// - <b>Schließen</b> → App beendet regulär (kein <c>ShutdownMode</c>-Umbau).
/// - Klick aufs Tray-Icon oder Menü „Anzeigen" → Fenster kommt zurück.
/// - Menü „Beenden" → sauberer Desktop-Shutdown.
///
/// Fünf Pflicht-Absicherungen (Skill: references/design.md → System-Tray):
/// - GC-Referenz: die App muss die Instanz in einem Feld halten.
/// - Restore-Guard: _restoreInProgress-Flag + Dispatcher.UIThread.Post.
/// - try/catch mit Fallback: headless-Server / kaputtes DBus → Standard-Minimieren.
/// - Linux hängt an Tmds.DBus.Protocol (transitive via Avalonia, kein neues Paket).
/// - Linux: vorher per <see cref="StatusNotifierProbe"/> prüfen, ob ein Tray-Host lauscht.
/// </summary>
public sealed class TrayController
{
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    private readonly Application _app;
    private readonly Window _window;
    // Direkt erzeugt statt erst in BuildMenu: so sind sie nie null und die
    // Texte lassen sich ohne Null-Prüfung nachziehen.
    private readonly NativeMenuItem _showItem = new();
    private readonly NativeMenuItem _quitItem = new();

    private TrayIcon? _tray;
    private bool _restoreInProgress;

    public TrayController(Application app, Window window)
    {
        _app = app;
        _window = window;
    }

    public void Install()
    {
        if (!StatusNotifierProbe.IsTrayAvailable())
        {
            // Ohne Tray-Host bliebe ein per Hide() verstecktes Fenster unerreichbar.
            _logger.Info("Kein Tray verfügbar — Minimieren bleibt Standard-Minimieren.");
            return;
        }

        try
        {
            var iconUri = new Uri("avares://QrCodeBuilder/Assets/qrcodebuilder.png");
            var icon = AssetLoader.Exists(iconUri)
                ? new WindowIcon(new Bitmap(AssetLoader.Open(iconUri)))
                : null;

            _tray = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "QR Code Builder",
                IsVisible = true,
                Menu = BuildMenu(),
            };
            _tray.Clicked += (_, _) => Restore();

            TrayIcon.SetIcons(_app, new TrayIcons { _tray });
            _window.PropertyChanged += OnWindowPropertyChanged;

            // Ein NativeMenuItem-Header ist ein fertiger String und folgt dem
            // Sprachwechsel nicht von selbst — anders als {loc:Tr} im XAML.
            LocalizationService.Instance.PropertyChanged += (_, _) => ApplyMenuTexts();

            _logger.Info("System-Tray installiert (Minimize → Tray).");
        }
        catch (Exception ex)
        {
            _tray = null;
            _logger.Warn(ex, "System-Tray nicht verfügbar — Fallback: Standard-Minimieren.");
        }
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        _showItem.Click += (_, _) => Restore();
        menu.Add(_showItem);

        menu.Add(new NativeMenuItemSeparator());

        _quitItem.Click += (_, _) => Quit();
        menu.Add(_quitItem);

        ApplyMenuTexts();

        return menu;
    }

    private void ApplyMenuTexts()
    {
        _showItem.Header = L.T("Tray_Show");
        _quitItem.Header = L.T("Tray_Quit");
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty) return;
        if (_restoreInProgress) return;
        if (_window.WindowState != WindowState.Minimized) return;

        // Hide() schließt nicht — Prozess bleibt am Leben.
        _window.Hide();
    }

    /// <summary>Holt das Fenster zurück — auch für den Single-Instance-Guard (Zweitstart).</summary>
    public void Restore()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _restoreInProgress = true;
            try
            {
                _window.Show();
                _window.WindowState = WindowState.Normal;
                _window.Activate();
            }
            finally
            {
                _restoreInProgress = false;
            }
        });
    }

    private void Quit()
    {
        if (_app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
