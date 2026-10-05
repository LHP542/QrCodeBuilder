using System.Diagnostics;
using NLog;

namespace QrCodeBuilder.Services;

/// <summary>
/// Prüft unter Linux, ob überhaupt ein Tray-Host (StatusNotifier-Watcher) lauscht.
///
/// WARUM: <c>new TrayIcon(...)</c> wirft NICHT, wenn niemand das Icon anzeigen kann
/// (GNOME ohne Erweiterung, manche Tiling-WMs). Ohne diese Probe versteckt
/// Minimieren das Fenster per Hide() unwiederbringlich. Im Zweifel false —
/// lieber ein Fenster zu viel in der Taskleiste als eines, das niemand zurückholt.
/// </summary>
public static class StatusNotifierProbe
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly string[] WatcherNames =
    [
        "org.kde.StatusNotifierWatcher",
        "org.freedesktop.StatusNotifierWatcher",
    ];

    public static bool IsTrayAvailable()
    {
        // Windows und macOS haben immer einen Infobereich.
        if (!OperatingSystem.IsLinux())
        {
            return true;
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
        {
            Log.Info("Kein DBus-Session-Bus — Tray deaktiviert.");
            return false;
        }

        foreach (var name in WatcherNames)
        {
            if (HasOwner(name))
            {
                Log.Info("Tray-Host gefunden: {Name}", name);
                return true;
            }
        }

        Log.Info("Kein StatusNotifier-Watcher auf dem Session-Bus — Tray deaktiviert.");
        return false;
    }

    /// <summary>
    /// Bewusst Process.Start auf gdbus: gefragt ist der Session-Bus DIESES Prozesses.
    /// Fehlt gdbus, gilt der Tray als nicht verfügbar.
    /// </summary>
    private static bool HasOwner(string busName)
    {
        try
        {
            var psi = new ProcessStartInfo("gdbus")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var arg in new[]
                     {
                         "call", "--session",
                         "--dest", "org.freedesktop.DBus",
                         "--object-path", "/org/freedesktop/DBus",
                         "--method", "org.freedesktop.DBus.NameHasOwner",
                         busName,
                     })
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);

            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(2000))
            {
                process.Kill();
                return false;
            }

            return process.ExitCode == 0 && output.Contains("true", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "gdbus-Abfrage für {Name} fehlgeschlagen.", busName);
            return false;
        }
    }
}
