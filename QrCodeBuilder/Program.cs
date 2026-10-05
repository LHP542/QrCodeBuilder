using Avalonia;
using Avalonia.Media;
using NLog;
using QrCodeBuilder.Services;

namespace QrCodeBuilder;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Masking MUSS vor dem ersten Logger-Aufruf registriert sein, sonst
        // schluckt ${masked:…} das Ende jeder Nachricht.
        MaskingLayoutRenderer.Register();

        var log = LogManager.GetCurrentClassLogger();
        log.Info("QrCodeBuilder startet (Version {Version}).", UpdateService.AppVersion);

        // Werkzeugmodi brauchen keinen Guard: sie rendern und beenden sich wieder.
        var guard = App.IsToolMode(args) ? null : new SingleInstanceGuard();

        if (guard is not null && !guard.TryClaim())
        {
            // Zweitstart: die laufende Instanz in den Vordergrund holen und gehen.
            guard.NotifyPrimary();
            guard.Dispose();
            log.Info("Bereits gestartet — laufende Instanz aktiviert, Zweitstart beendet sich.");
            LogManager.Shutdown();
            return 0;
        }

        App.PendingGuard = guard;

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            log.Fatal(ex, "Unbehandelter Fehler beim Start.");
            throw;
        }
        finally
        {
            guard?.Dispose();
            log.Info("QrCodeBuilder beendet.");
            LogManager.Shutdown();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(EmojiFontOptions())
            .LogToTrace();

    /// <summary>
    /// Inter bringt keine Emoji-Glyphen mit. Ohne diesen Fallback rendern die
    /// Länderflaggen im Sprachumschalter als Ersatzkästchen.
    ///
    /// FALLE: <c>WithInterFont()</c> setzt die Standardfamilie über dieselben
    /// Options. Wer sie ersetzt, muss <see cref="FontManagerOptions.DefaultFamilyName"/>
    /// erneut angeben — sonst fällt die ganze App auf die System-Schrift zurück.
    /// </summary>
    private static FontManagerOptions EmojiFontOptions()
    {
        var emojiFamily = OperatingSystem.IsWindows() ? "Segoe UI Emoji" : "Noto Color Emoji";

        return new FontManagerOptions
        {
            DefaultFamilyName = "fonts:Inter#Inter",
            FontFallbacks =
            [
                new FontFallback { FontFamily = new FontFamily(emojiFamily) },
            ],
        };
    }
}
