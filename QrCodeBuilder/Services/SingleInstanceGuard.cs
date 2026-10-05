using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace QrCodeBuilder.Services;

/// <summary>
/// Verhindert Mehrfach-Instanzen: die erste Instance öffnet einen Named-Pipe-Server,
/// spätere Starts finden den Server, senden „ACTIVATE" und beenden sich sofort. Der
/// Server-Prozess feuert <see cref="ActivationRequested"/> im UI-Thread, damit das
/// Fenster aus dem Tray geholt und aktiviert werden kann.
///
/// <para>Named Pipes sind unter .NET cross-platform: Windows nutzt echte NPCs,
/// Linux/macOS ein Unix-Domain-Socket unter <c>/tmp/CoreFxPipe_&lt;name&gt;</c>. Wenn
/// eine vorherige Instance gecrasht ist, kann dort ein verwaister Socket liegen —
/// wir erkennen das (Verbindungsversuch schlägt fehl) und räumen auf, bevor wir uns
/// als neuer Server registrieren.</para>
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    // Nutzerspezifischer Pipe-Name — sonst blockieren mehrere User auf einem Host einander.
    private const string BasePipeName = "QrCodeBuilder.SingleInstance";
    private const byte ActivateCommand = (byte)'A';
    private const int ConnectTimeoutMs = 500;

    private readonly string _pipeName;
    private NamedPipeServerStream? _server;
    private CancellationTokenSource? _cts;

    public bool IsPrimary { get; private set; }

    /// <summary>Wird gefeuert, wenn eine zweite Instance den Fokus einfordert (im ThreadPool).</summary>
    public event Action? ActivationRequested;

    public SingleInstanceGuard()
        : this($"{BasePipeName}.{Environment.UserName}") { }

    /// <summary>Testbarer Ctor mit explizitem Pipe-Namen.</summary>
    public SingleInstanceGuard(string pipeName)
    {
        _pipeName = pipeName;
    }

    /// <summary>
    /// Registriert diese Instance als primär (öffnet den Pipe-Server).
    /// Gibt <c>false</c> zurück, wenn bereits eine primäre Instance läuft — der Aufrufer
    /// sollte dann <see cref="NotifyPrimary"/> aufrufen und sich beenden.
    /// </summary>
    public bool TryClaim()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                _server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                _cts = new CancellationTokenSource();
                _ = ListenLoopAsync(_cts.Token);
                IsPrimary = true;
                Log.Info("Single-Instance-Guard: primäre Instance (Pipe {Pipe})", _pipeName);
                return true;
            }
            catch (IOException) when (attempt == 0 && !PrimaryIsAlive())
            {
                // Verwaister Socket (Linux/macOS nach Crash) — einmalig aufräumen und retry.
                Log.Warn("Verwaister Single-Instance-Socket erkannt — räume auf und versuche erneut");
                TryCleanupStaleSocket();
            }
            catch (IOException)
            {
                IsPrimary = false;
                return false;
            }
        }

        IsPrimary = false;
        return false;
    }

    /// <summary>
    /// Aus der Zweitinstanz aufgerufen: schickt einen Aktivierungsbefehl an die
    /// primäre Instance. Gibt <c>true</c> zurück, wenn die Nachricht angekommen ist.
    /// </summary>
    public bool NotifyPrimary()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(ConnectTimeoutMs);
            client.WriteByte(ActivateCommand);
            client.Flush();
            Log.Info("Zweitinstanz: Aktivierungssignal an primäre Instance gesendet");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Zweitinstanz konnte die primäre Instance nicht benachrichtigen");
            return false;
        }
    }

    private bool PrimaryIsAlive()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(ConnectTimeoutMs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryCleanupStaleSocket()
    {
        if (OperatingSystem.IsWindows())
            return; // Windows räumt Named Pipes vom OS auf
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + _pipeName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Verwaister Socket konnte nicht entfernt werden");
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _server is not null)
        {
            try
            {
                await _server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                var buffer = new byte[1];
                var read = await _server.ReadAsync(buffer.AsMemory(0, 1), ct).ConfigureAwait(false);
                _server.Disconnect();

                if (read == 1 && buffer[0] == ActivateCommand)
                    ActivationRequested?.Invoke();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Single-Instance-Listener: Verbindung fehlgeschlagen");
            }
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* best effort */ }
        try { _server?.Dispose(); } catch { /* best effort */ }
        _cts?.Dispose();
        _server = null;
        _cts = null;
    }
}
