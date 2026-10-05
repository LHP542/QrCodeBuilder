using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using NLog;

namespace QrCodeBuilder.Services;

/// <summary>Ergebnis einer Update-Prüfung.</summary>
/// <param name="LatestVersion">Neueste Version im Ordner, null wenn keine gefunden.</param>
/// <param name="PackagePath">Paket für diese Plattform, null wenn keins passt.</param>
/// <param name="ReleaseNotes">Versionshinweise aller übersprungenen Versionen, neueste zuerst.</param>
/// <param name="Problem">Warum nicht geprüft werden konnte — für die Anzeige im Über-Fenster.</param>
public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string? LatestVersion,
    string? PackagePath,
    string? ReleaseNotes,
    UpdateProblem Problem = UpdateProblem.None)
{
    public bool CanInstall => UpdateAvailable && PackagePath is not null;
}

public enum UpdateProblem
{
    None,
    NoChannel,
    Unreachable,
}

/// <summary>
/// Update-Prüfung und echtes Self-Update gegen den Ordner-Kanal im Firmennetz
/// (siehe <see cref="UpdateChannel"/>). Ablauf nach Kroste-Standard: prüfen →
/// Zustimmung → Paket ERST kopieren, DANN entpacken → Austausch-Skript starten →
/// App beenden (<see cref="TerminateForUpdate"/>).
/// </summary>
public sealed class UpdateService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly Func<string?> _channel;
    private UpdateCheckResult? _cached;

    /// <param name="channel">Liefert den aktuell eingestellten Update-Ordner (Einstellungen).</param>
    public UpdateService(Func<string?> channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <summary>Instanz-Zugriff auf <see cref="AppVersion"/> — das AboutWindow bindet daran.</summary>
    public string CurrentVersion => AppVersion;

    /// <summary>Version aus der Assembly (MinVer schreibt sie als InformationalVersion), ohne +sha.</summary>
    public static string AppVersion
    {
        get
        {
            var raw = Assembly.GetExecutingAssembly()
                          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                      ?? "0.0.0";

            var plus = raw.IndexOf('+');
            return plus > 0 ? raw[..plus] : raw;
        }
    }

    /// <summary>Sieht im Ordner nach. Ergebnis wird zwischengespeichert; <paramref name="force"/> prüft neu.</summary>
    public Task<UpdateCheckResult> CheckForUpdateAsync(bool force = false)
    {
        if (!force && _cached is not null)
        {
            return Task.FromResult(_cached);
        }

        var channel = _channel()?.Trim();

        if (!UpdateChannel.LooksLikeFolder(channel))
        {
            Log.Info("Kein gültiger Update-Ordner eingestellt ({Channel}).", channel ?? "leer");
            return Task.FromResult(_cached = new UpdateCheckResult(false, null, null, null, UpdateProblem.NoChannel));
        }

        // Ein nicht erreichbares Netzlaufwerk kann Sekunden hängen — nie auf dem UI-Thread.
        return Task.Run(() => _cached = Check(channel!, UpdateChannel.ParseInstalledVersion(AppVersion)));
    }

    /// <summary>Kern der Prüfung, ohne Cache und Assembly-Version — damit testbar.</summary>
    public static UpdateCheckResult Check(string folder, Version installed)
    {
        var stopwatch = Stopwatch.StartNew();
        var suffix = UpdateChannel.PackageSuffix(
            OperatingSystem.IsWindows(),
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE")));

        try
        {
            var newest = UpdateChannel.FindNewestPackage(folder, suffix);

            if (newest is null)
            {
                // Ein Notebook ohne Netzlaufwerk ist der Normalfall, nicht die Störung —
                // deshalb Debug statt Warn/Error.
                Log.Debug("Im Update-Ordner {Folder} liegt kein Paket *{Suffix} (oder er ist nicht erreichbar).", folder, suffix);
                return new UpdateCheckResult(false, null, null, null, UpdateProblem.Unreachable);
            }

            var (latest, path) = newest.Value;
            var isNewer = latest > installed;
            var notes = isNewer ? UpdateChannel.CollectReleaseNotes(folder, installed, latest) : null;

            Log.Info(
                "Update-Prüfung nach {Ms} ms: installiert {Installed}, im Ordner {Latest}, neuer: {IsNewer}, Notes: {HasNotes}",
                stopwatch.ElapsedMilliseconds, installed, latest, isNewer, notes is not null);

            return new UpdateCheckResult(isNewer, latest.ToString(3), isNewer ? path : null, notes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug(ex, "Update-Ordner {Folder} nicht lesbar.", folder);
            return new UpdateCheckResult(false, null, null, null, UpdateProblem.Unreachable);
        }
    }

    /// <summary>
    /// Kopiert das Paket lokal, bereitet es vor und startet das Austausch-Skript.
    /// Bei <c>true</c> MUSS der Aufrufer <see cref="TerminateForUpdate"/> rufen —
    /// das Skript wartet auf das Prozessende, sonst hängt das Update.
    /// </summary>
    public static async Task<bool> DownloadAndApplyAsync(string packagePath, IProgress<double>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        try
        {
            var workDir = Path.Combine(Path.GetTempPath(), $"{UpdateChannel.AppName}-update-{Environment.ProcessId}");

            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }

            Directory.CreateDirectory(workDir);

            // Erst kopieren, dann entpacken: das Paket könnte zwischen Prüfung und Entpacken
            // ausgetauscht werden, und ein Netzlaufwerk, das mitten im Entpacken wegbricht,
            // hinterließe einen halb ersetzten Programmordner.
            var localPackage = Path.Combine(workDir, Path.GetFileName(packagePath));
            await CopyWithProgressAsync(packagePath, localPackage, progress).ConfigureAwait(false);
            Log.Info("Update-Paket lokal kopiert: {Path}", localPackage);

            string script;

            if (OperatingSystem.IsWindows())
            {
                var payload = Path.Combine(workDir, "payload");
                await Task.Run(() => ZipFile.ExtractToDirectory(localPackage, payload, overwriteFiles: true)).ConfigureAwait(false);
                script = WriteWindowsInstaller(workDir, payload);
            }
            else
            {
                script = WriteLinuxInstaller(workDir, localPackage);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                Arguments = OperatingSystem.IsWindows() ? $"/c \"{script}\"" : $"\"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workDir,
            });

            Log.Info("Installer gestartet: {Script}", script);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Self-Update fehlgeschlagen.");
            return false;
        }
    }

    /// <summary>
    /// Beendet die App, damit der Installer weiterlaufen kann. Von JEDEM Aufrufer
    /// von <see cref="DownloadAndApplyAsync"/> bei Erfolg zu rufen.
    /// </summary>
    public static void TerminateForUpdate()
    {
        Log.Info("App beendet sich für den Update-Austausch.");
        LogManager.Flush();

        // Fail-Safe: falls Exit an einem Finalizer hängen bleibt, hart nachlegen.
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500).ConfigureAwait(false);
            Process.GetCurrentProcess().Kill();
        });

        Environment.Exit(0);
    }

    /// <summary>Semantischer Vergleich — Stringvergleich stuft 1.10.0 fälschlich unter 1.9.0 ein.</summary>
    public static bool IsNewer(string candidate, string current) =>
        UpdateChannel.ParseInstalledVersion(candidate) > UpdateChannel.ParseInstalledVersion(current);

    private static async Task CopyWithProgressAsync(string source, string target, IProgress<double>? progress)
    {
        var buffer = new byte[1 << 20];
        var copied = 0L;

        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, useAsync: true);
        await using var output = File.Create(target);

        var total = input.Length;
        int count;

        while ((count = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
            copied += count;

            if (total > 0)
            {
                progress?.Report((double)copied / total);
            }
        }
    }

    /// <summary>
    /// Batch-Zeilen OHNE führende Einrückung — ein eingerücktes :label ist für cmd kein
    /// gültiges Sprungziel, das goto scheitert still und die ALTE Version startet neu.
    /// Gewartet wird per Wait-Process statt tasklist-Schleife.
    /// </summary>
    private static string WriteWindowsInstaller(string workDir, string payloadDir)
    {
        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var exePath = Path.Combine(appDir, $"{UpdateChannel.AppName}.exe");
        var logFile = Path.Combine(workDir, "update.log");
        var pid = Environment.ProcessId;

        string[] lines =
        [
            "@echo off",
            $"echo [%date% %time%] Warte auf Ende von PID {pid} >> \"{logFile}\"",
            $"powershell -NoProfile -Command \"Wait-Process -Id {pid} -ErrorAction SilentlyContinue\"",
            "rem Kurzer Nachlauf, damit Windows die Dateihandles freigibt.",
            "ping -n 3 127.0.0.1 > nul",
            $"echo [%date% %time%] Kopiere nach \"{appDir}\" >> \"{logFile}\"",
            $"xcopy \"{payloadDir}\\*\" \"{appDir}\\\" /E /Y /I >> \"{logFile}\" 2>&1",
            "if errorlevel 1 goto failed",
            $"echo [%date% %time%] Starte neu >> \"{logFile}\"",
            $"start \"\" \"{exePath}\"",
            "goto ende",
            ":failed",
            $"echo [%date% %time%] FEHLER beim Kopieren >> \"{logFile}\"",
            $"start \"\" \"{exePath}\"",
            ":ende",
        ];

        var scriptPath = Path.Combine(workDir, "install.bat");
        File.WriteAllLines(scriptPath, lines);
        return scriptPath;
    }

    /// <summary>
    /// Log NICHT nach BaseDirectory/logs — beim laufenden AppImage ist das ein read-only
    /// Squashfs-Mount, bash bricht sofort ab und die App wird nie ersetzt.
    /// </summary>
    private static string WriteLinuxInstaller(string workDir, string assetPath)
    {
        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        var scriptPath = Path.Combine(workDir, "install.sh");

        var stateDir = Environment.GetEnvironmentVariable("XDG_STATE_HOME")
                       ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        var logPath = Path.Combine(stateDir, UpdateChannel.AppName, "update.log");

        var body = new List<string>
        {
            "#!/bin/bash",
            $"mkdir -p \"$(dirname '{logPath}')\" 2>/dev/null || true",
            $"exec >>'{logPath}' 2>&1 || exec >>/tmp/{UpdateChannel.AppName}-update.log 2>&1",
            "set -x",
            $"while kill -0 {Environment.ProcessId} 2>/dev/null; do sleep 0.5; done",
            "sleep 1",
        };

        if (!string.IsNullOrWhiteSpace(appImage))
        {
            // "Text file busy": das laufende AppImage ist als Loop-Device gemountet —
            // cp -f behält den Inode, mv/rm scheitern.
            body.Add($"cp -f '{assetPath}' '{appImage}'");
            body.Add($"chmod +x '{appImage}'");
            body.Add($"setsid '{appImage}' >/dev/null 2>&1 &");
        }
        else
        {
            body.Add($"tar -xzf '{assetPath}' -C '{appDir}'");
            body.Add($"chmod +x '{appDir}/{UpdateChannel.AppName}'");
            body.Add($"setsid '{appDir}/{UpdateChannel.AppName}' >/dev/null 2>&1 &");
        }

        body.Add("exit 0");

        // Hart '\n' statt AppendLine: unter Windows erzeugte CRLF-Zeilen brechen bash.
        File.WriteAllText(scriptPath, string.Join('\n', body) + "\n");

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return scriptPath;
    }
}
