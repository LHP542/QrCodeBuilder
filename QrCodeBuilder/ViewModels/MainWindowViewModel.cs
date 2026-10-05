using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using QrCodeBuilder.Localization;
using QrCodeBuilder.Persistence;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.ViewModels;

public enum QrContentKind
{
    Text,
    Wifi,
}

/// <summary>
/// Hauptfenster: Eingabe links, Vorschau und Export rechts. Jede Änderung erzeugt den
/// Code sofort neu — es gibt keinen „Erzeugen"-Knopf, den man vergessen könnte.
/// Das ViewModel kennt kein Avalonia: die Vorschau geht als PNG-Bytes an die View.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>Unterhalb dieses Kontrasts (WCAG) lesen viele Handykameras nicht mehr sicher.</summary>
    public const double MinimumContrast = 3.0;

    /// <summary>Die Vorschau wird mindestens so groß gerendert und dann verkleinert angezeigt.</summary>
    private const int PreviewTargetPixels = 480;

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>Eigenschaften, deren Änderung den Code neu erzeugt.</summary>
    private static readonly HashSet<string> CodeInputs =
    [
        nameof(SelectedContentKind), nameof(Text), nameof(WifiSsid), nameof(WifiPassword),
        nameof(SelectedWifiSecurity), nameof(WifiHidden), nameof(SelectedErrorCorrection),
        nameof(ModuleSize), nameof(ForegroundHex), nameof(BackgroundHex), nameof(QuietZone),
    ];

    /// <summary>Eigenschaften, die als Darstellung gespeichert werden (Inhalte nie).</summary>
    private static readonly HashSet<string> StyleInputs =
    [
        nameof(SelectedErrorCorrection), nameof(ModuleSize), nameof(ForegroundHex),
        nameof(BackgroundHex), nameof(QuietZone),
    ];

    private readonly SettingsService _settingsService;
    private readonly ISaveFileDialog _saveDialog;
    private readonly IImageClipboard _clipboard;
    private readonly bool _initialized;

    private QrMatrix? _matrix;
    private QrStyle _style = new();

    [ObservableProperty]
    private Choice<QrContentKind> _selectedContentKind;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _wifiSsid = string.Empty;

    [ObservableProperty]
    private string _wifiPassword = string.Empty;

    [ObservableProperty]
    private Choice<WifiSecurity> _selectedWifiSecurity;

    [ObservableProperty]
    private bool _wifiHidden;

    [ObservableProperty]
    private Choice<QrErrorCorrection> _selectedErrorCorrection;

    [ObservableProperty]
    private int _moduleSize;

    [ObservableProperty]
    private string _foregroundHex;

    [ObservableProperty]
    private string _backgroundHex;

    [ObservableProperty]
    private bool _quietZone;

    [ObservableProperty]
    private byte[]? _previewPng;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SavePngCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSvgCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyImageCommand))]
    private bool _hasCode;

    [ObservableProperty]
    private string _infoText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    private string _warningText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public MainWindowViewModel(SettingsService settingsService, ISaveFileDialog saveDialog, IImageClipboard clipboard)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _saveDialog = saveDialog ?? throw new ArgumentNullException(nameof(saveDialog));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

        var settings = _settingsService.Load();

        _selectedContentKind = ContentKinds[0];
        _selectedWifiSecurity = WifiSecurities[0];
        _selectedErrorCorrection = ErrorCorrections.FirstOrDefault(c => c.Value == settings.ErrorCorrection)
                                   ?? ErrorCorrections[1];
        _moduleSize = Math.Clamp(settings.ModuleSize, QrStyle.MinModuleSize, QrStyle.MaxModuleSize);
        _foregroundHex = settings.Foreground;
        _backgroundHex = settings.Background;
        _quietZone = settings.QuietZone;

        _statusText = L.T("Status_Ready");
        _initialized = true;

        Regenerate();
    }

    public IReadOnlyList<Choice<QrContentKind>> ContentKinds { get; } =
    [
        new(QrContentKind.Text, "Kind_Text"),
        new(QrContentKind.Wifi, "Kind_Wifi"),
    ];

    public IReadOnlyList<Choice<WifiSecurity>> WifiSecurities { get; } =
    [
        new(WifiSecurity.Wpa, "Wifi_Security_Wpa"),
        new(WifiSecurity.Wep, "Wifi_Security_Wep"),
        new(WifiSecurity.None, "Wifi_Security_None"),
    ];

    public IReadOnlyList<Choice<QrErrorCorrection>> ErrorCorrections { get; } =
    [
        new(QrErrorCorrection.L, "Ecc_L"),
        new(QrErrorCorrection.M, "Ecc_M"),
        new(QrErrorCorrection.Q, "Ecc_Q"),
        new(QrErrorCorrection.H, "Ecc_H"),
    ];

    public bool IsTextMode => SelectedContentKind.Value == QrContentKind.Text;

    public bool IsWifiMode => SelectedContentKind.Value == QrContentKind.Wifi;

    public bool IsWifiPasswordEnabled => SelectedWifiSecurity.Value != WifiSecurity.None;

    public bool HasWarning => WarningText.Length > 0;

    /// <summary>Kantenlänge der Exportdatei — für die Anzeige neben dem Regler.</summary>
    public string ExportSizeText => _matrix is null
        ? L.F("Style_ModuleSize_Value", ModuleSize)
        : L.F("Style_ModuleSize_ValueWithPixels", ModuleSize, QrRenderer.PixelSize(_matrix, _style));

    /// <summary>Der Text, der im Code landet. Leer, wenn noch nichts eingegeben ist.</summary>
    public string BuildPayload()
    {
        if (IsWifiMode)
        {
            return string.IsNullOrWhiteSpace(WifiSsid)
                ? string.Empty
                : QrPayload.Wifi(WifiSsid.Trim(), WifiPassword, SelectedWifiSecurity.Value, WifiHidden);
        }

        return Text;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (!_initialized || e.PropertyName is null)
        {
            return;
        }

        if (e.PropertyName == nameof(SelectedContentKind))
        {
            OnPropertyChanged(nameof(IsTextMode));
            OnPropertyChanged(nameof(IsWifiMode));
        }

        if (e.PropertyName == nameof(SelectedWifiSecurity))
        {
            OnPropertyChanged(nameof(IsWifiPasswordEnabled));
        }

        if (CodeInputs.Contains(e.PropertyName))
        {
            Regenerate();
        }

        if (StyleInputs.Contains(e.PropertyName))
        {
            PersistStyle();
        }
    }

    /// <summary>Erzeugt Matrix und Vorschau neu. Fehler landen in <see cref="WarningText"/>, nie als Ausnahme.</summary>
    private void Regenerate()
    {
        var warnings = new List<string>();
        _style = BuildStyle(warnings);

        var payload = BuildPayload();

        if (payload.Length == 0)
        {
            _matrix = null;
            PreviewPng = null;
            HasCode = false;
            InfoText = IsWifiMode ? L.T("Preview_EmptyWifi") : L.T("Preview_Empty");
        }
        else
        {
            try
            {
                _matrix = QrMatrix.Encode(payload, SelectedErrorCorrection.Value);
                PreviewPng = QrRenderer.RenderPng(_matrix, PreviewStyle(_matrix));
                HasCode = true;
                InfoText = L.F("Preview_Info", _matrix.Version, _matrix.Size, payload.Length);
            }
            catch (QrContentTooLongException ex)
            {
                // Kein Fehler im Programm, sondern zu viel Text — nur Debug, kein Stacktrace-Spam.
                Log.Debug(ex, "Inhalt zu lang für Fehlerkorrektur {Ecc} ({Length} Zeichen).",
                    SelectedErrorCorrection.Value, payload.Length);

                _matrix = null;
                PreviewPng = null;
                HasCode = false;
                InfoText = string.Empty;
                warnings.Insert(0, L.T("Warning_TooLong"));
            }
        }

        WarningText = string.Join(Environment.NewLine, warnings);
        OnPropertyChanged(nameof(ExportSizeText));
    }

    private QrStyle BuildStyle(List<string> warnings)
    {
        var foreground = ParseColor(ForegroundHex, QrColor.Black, "Style_Foreground", warnings);
        var background = ParseColor(BackgroundHex, QrColor.White, "Style_Background", warnings);

        // Ein transparenter Hintergrund wird auf dem Untergrund des Nutzers gelesen —
        // den kennen wir nicht, also gibt es dafür keine Kontrastwarnung.
        if (!background.IsTransparent)
        {
            if (foreground.RelativeLuminance > background.RelativeLuminance)
            {
                warnings.Add(L.T("Warning_Inverted"));
            }
            else if (QrColor.Contrast(foreground, background) < MinimumContrast)
            {
                warnings.Add(L.T("Warning_LowContrast"));
            }
        }

        return new QrStyle
        {
            ModuleSize = ModuleSize,
            Foreground = foreground,
            Background = background,
            QuietZone = QuietZone,
        };
    }

    private static QrColor ParseColor(string hex, QrColor fallback, string labelKey, List<string> warnings)
    {
        if (QrColor.TryParse(hex, out var color))
        {
            return color;
        }

        warnings.Add(L.F("Warning_InvalidColor", L.T(labelKey)));
        return fallback;
    }

    private QrStyle PreviewStyle(QrMatrix matrix)
    {
        var modules = QrRenderer.TotalModules(matrix, _style);
        var moduleSize = Math.Clamp(
            (PreviewTargetPixels + modules - 1) / modules,
            QrStyle.MinModuleSize,
            QrStyle.MaxModuleSize);

        return _style with { ModuleSize = moduleSize };
    }

    private void PersistStyle()
    {
        var current = _settingsService.Load();

        _settingsService.Save(current with
        {
            ErrorCorrection = SelectedErrorCorrection.Value,
            ModuleSize = ModuleSize,
            Foreground = ForegroundHex,
            Background = BackgroundHex,
            QuietZone = QuietZone,
        });
    }

    [RelayCommand(CanExecute = nameof(HasCode))]
    private Task SavePngAsync() => ExportAsync(QrExportFormat.Png);

    [RelayCommand(CanExecute = nameof(HasCode))]
    private Task SaveSvgAsync() => ExportAsync(QrExportFormat.Svg);

    [RelayCommand(CanExecute = nameof(HasCode))]
    private async Task CopyImageAsync()
    {
        if (_matrix is null)
        {
            return;
        }

        Log.Info("Nutzeraktion: Bild kopieren ({Pixels} px).", QrRenderer.PixelSize(_matrix, _style));

        try
        {
            await _clipboard.CopyPngAsync(QrRenderer.RenderPng(_matrix, _style));
            StatusText = L.F("Status_Copied", QrRenderer.PixelSize(_matrix, _style));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Kopieren in die Zwischenablage fehlgeschlagen.");
            StatusText = L.T("Status_CopyFailed");
        }
    }

    private async Task ExportAsync(QrExportFormat format)
    {
        if (_matrix is null)
        {
            return;
        }

        var matrix = _matrix;
        var style = _style;
        var suggested = QrExport.SuggestFileName(BuildPayload(), format);

        Log.Info("Nutzeraktion: Export als {Format}, Vorschlag {Name}.", format, suggested);

        try
        {
            var target = await _saveDialog.PickAsync(suggested, format);

            if (target is null)
            {
                Log.Info("Export abgebrochen.");
                return;
            }

            await using (target)
            {
                var started = System.Diagnostics.Stopwatch.StartNew();
                await QrExport.WriteAsync(target.Stream, format, matrix, style);

                Log.Info("Export geschrieben: {Name} in {Ms} ms.", target.DisplayName, started.ElapsedMilliseconds);
                StatusText = L.F("Status_Saved", target.DisplayName, QrRenderer.PixelSize(matrix, style));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Export als {Format} fehlgeschlagen.", format);
            StatusText = L.T("Status_SaveFailed");
        }
    }
}
