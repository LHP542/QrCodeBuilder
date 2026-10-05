using FluentAssertions;
using QrCodeBuilder.Persistence;
using QrCodeBuilder.Services;
using QrCodeBuilder.ViewModels;

namespace QrCodeBuilder.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "QrCodeBuilder-Tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeDialog _dialog = new();
    private readonly FakeClipboard _clipboard = new();

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SettingsService Settings => new(_directory);

    private MainWindowViewModel NewViewModel() => new(Settings, _dialog, _clipboard);

    [Fact]
    public void Ohne_Inhalt_gibt_es_keinen_Code_und_keinen_Export()
    {
        var vm = NewViewModel();

        vm.HasCode.Should().BeFalse();
        vm.PreviewPng.Should().BeNull();
        vm.SavePngCommand.CanExecute(null).Should().BeFalse();
        vm.SaveSvgCommand.CanExecute(null).Should().BeFalse();
        vm.CopyImageCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Text_erzeugt_sofort_Vorschau_und_schaltet_die_Exportknoepfe_frei()
    {
        var vm = NewViewModel();
        var freigaben = 0;
        vm.SavePngCommand.CanExecuteChanged += (_, _) => freigaben++;

        vm.Text = "https://example.com";

        vm.HasCode.Should().BeTrue();
        vm.PreviewPng.Should().NotBeNull();
        vm.SavePngCommand.CanExecute(null).Should().BeTrue();
        freigaben.Should().BeGreaterThan(0, "ohne CanExecuteChanged bliebe der Knopf grau");
    }

    [Fact]
    public async Task Png_Export_schreibt_in_das_gewaehlte_Ziel_mit_der_eingestellten_Groesse()
    {
        var vm = NewViewModel();
        vm.Text = "https://example.com";
        vm.ModuleSize = 6;

        await vm.SavePngCommand.ExecuteAsync(null);

        _dialog.LastSuggestion.Should().Be("qr-example.com.png");
        var png = PngReader.Read(_dialog.Written!);
        png.Width.Should().Be((QrMatrix.Encode("https://example.com", QrErrorCorrection.M).Size + 8) * 6);
    }

    [Fact]
    public async Task Svg_Export_schreibt_SVG()
    {
        var vm = NewViewModel();
        vm.Text = "Hallo";

        await vm.SaveSvgCommand.ExecuteAsync(null);

        System.Text.Encoding.UTF8.GetString(_dialog.Written!).Should().Contain("<svg");
    }

    [Fact]
    public async Task Abbruch_im_Dialog_schreibt_nichts_und_wirft_nicht()
    {
        var vm = NewViewModel();
        vm.Text = "Hallo";
        _dialog.Cancel = true;
        var status = vm.StatusText;

        await vm.SavePngCommand.ExecuteAsync(null);

        _dialog.Written.Should().BeNull();
        vm.StatusText.Should().Be(status);
    }

    [Fact]
    public async Task Kopieren_legt_das_PNG_in_voller_Groesse_ab()
    {
        var vm = NewViewModel();
        vm.Text = "Hallo";
        vm.ModuleSize = 2;

        await vm.CopyImageCommand.ExecuteAsync(null);

        PngReader.Read(_clipboard.Last!).Width.Should().Be((21 + 8) * 2);
    }

    [Fact]
    public void Wlan_Modus_baut_das_WIFI_Schema()
    {
        var vm = NewViewModel();
        vm.SelectedContentKind = vm.ContentKinds.Single(k => k.Value == QrContentKind.Wifi);

        vm.HasCode.Should().BeFalse("ohne SSID gibt es nichts zu kodieren");

        vm.WifiSsid = "Gast";
        vm.WifiPassword = "geheim";

        vm.IsWifiMode.Should().BeTrue();
        vm.BuildPayload().Should().Be("WIFI:T:WPA;S:Gast;P:geheim;;");
        vm.HasCode.Should().BeTrue();
    }

    [Fact]
    public void Offenes_Wlan_sperrt_das_Passwortfeld()
    {
        var vm = NewViewModel();

        vm.SelectedWifiSecurity = vm.WifiSecurities.Single(s => s.Value == WifiSecurity.None);

        vm.IsWifiPasswordEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("#FFFFFF", "#000000")]   // invertiert
    [InlineData("#BBBBBB", "#FFFFFF")]   // zu wenig Kontrast
    [InlineData("nix", "#FFFFFF")]       // ungültige Farbe
    public void Problematische_Farben_erzeugen_einen_Hinweis(string vorne, string hinten)
    {
        var vm = NewViewModel();
        vm.Text = "Hallo";

        vm.ForegroundHex = vorne;
        vm.BackgroundHex = hinten;

        vm.HasWarning.Should().BeTrue();
        vm.HasCode.Should().BeTrue("ein Hinweis blockiert den Export nicht");
    }

    [Fact]
    public void Transparenter_Hintergrund_erzeugt_keine_Kontrastwarnung()
    {
        var vm = NewViewModel();
        vm.Text = "Hallo";

        vm.BackgroundHex = "#00FFFFFF";

        vm.HasWarning.Should().BeFalse();
    }

    [Fact]
    public void Zu_langer_Inhalt_wird_gemeldet_statt_zu_werfen()
    {
        var vm = NewViewModel();
        vm.SelectedErrorCorrection = vm.ErrorCorrections.Single(e => e.Value == QrErrorCorrection.H);

        vm.Text = new string('x', 5000);

        vm.HasCode.Should().BeFalse();
        vm.HasWarning.Should().BeTrue();
        vm.SavePngCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Darstellung_wird_gespeichert_und_beim_naechsten_Start_uebernommen()
    {
        var vm = NewViewModel();
        vm.ModuleSize = 17;
        vm.ForegroundHex = "#123E6B";
        vm.QuietZone = false;
        vm.SelectedErrorCorrection = vm.ErrorCorrections.Single(e => e.Value == QrErrorCorrection.Q);
        vm.Text = "Inhalte werden nie gespeichert";

        var neu = NewViewModel();

        neu.ModuleSize.Should().Be(17);
        neu.ForegroundHex.Should().Be("#123E6B");
        neu.QuietZone.Should().BeFalse();
        neu.SelectedErrorCorrection.Value.Should().Be(QrErrorCorrection.Q);
        neu.Text.Should().BeEmpty();
        File.ReadAllText(Path.Combine(_directory, "settings.json")).Should().NotContain("Inhalte");
    }

    private sealed class FakeDialog : ISaveFileDialog
    {
        private MemoryStream? _stream;

        public bool Cancel { get; set; }

        public string? LastSuggestion { get; private set; }

        /// <summary>MemoryStream.ToArray funktioniert auch nach Dispose — das ViewModel schließt den Stream.</summary>
        public byte[]? Written => _stream?.ToArray();

        public Task<SaveTarget?> PickAsync(string suggestedFileName, QrExportFormat format)
        {
            LastSuggestion = suggestedFileName;

            if (Cancel)
            {
                return Task.FromResult<SaveTarget?>(null);
            }

            _stream = new MemoryStream();
            return Task.FromResult<SaveTarget?>(new SaveTarget(_stream, suggestedFileName));
        }
    }

    private sealed class FakeClipboard : IImageClipboard
    {
        public byte[]? Last { get; private set; }

        public Task CopyPngAsync(byte[] png)
        {
            Last = png;
            return Task.CompletedTask;
        }
    }
}
