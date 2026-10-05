using FluentAssertions;
using QrCodeBuilder.Persistence;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Tests;

public sealed class UpdateChannelTests : IDisposable
{
    /// <summary>Paketendung, die der Checker auf der Plattform des Testlaufs sucht.</summary>
    private static readonly string Suffix = UpdateChannel.PackageSuffix(OperatingSystem.IsWindows(), runsAsAppImage: false);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "QrCodeBuilder-Kanal-" + Guid.NewGuid().ToString("N"));

    public UpdateChannelTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [InlineData(@"\\samba01\542$\5424_IT-Basis-Dienste\QrCodeBuilder", true)]
    [InlineData(@"C:\Rollout", true)]
    [InlineData("/srv/rollout", true)]
    [InlineData("//server/share", true)]
    [InlineData("https://github.com/LHP542/QrCodeBuilder", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Ordner_wird_an_der_Schreibweise_erkannt(string? kanal, bool erwartet)
    {
        UpdateChannel.LooksLikeFolder(kanal).Should().Be(erwartet);
    }

    [Fact]
    public void Das_Temp_Verzeichnis_gilt_auf_jeder_Plattform_als_Ordner()
    {
        // Real in DTM: "/tmp/…" galt als Adresse, der Checker lief still gegen GitHub.
        UpdateChannel.LooksLikeFolder(Path.GetTempPath()).Should().BeTrue();
    }

    [Fact]
    public void Standardordner_ist_der_Samba_Share()
    {
        AppSettings.Default.UpdateChannel.Should().Be(@"\\samba01\542$\5424_IT-Basis-Dienste\QrCodeBuilder");
    }

    [Theory]
    [InlineData("QrCodeBuilder-1.2.3-win-x64.zip", "-win-x64.zip", "1.2.3")]
    [InlineData("QrCodeBuilder-0.10.0-notes.md", "-notes.md", "0.10.0")]
    [InlineData("QrCodeBuilder-1.2-win-x64.zip", "-win-x64.zip", null)]
    [InlineData("Anderes-1.2.3-win-x64.zip", "-win-x64.zip", null)]
    [InlineData("QrCodeBuilder-1.2.3-beta-win-x64.zip", "-win-x64.zip", null)]
    public void Version_steht_im_Dateinamen(string datei, string suffix, string? erwartet)
    {
        UpdateChannel.ParseVersion(datei, suffix)?.ToString(3).Should().Be(erwartet);
        if (erwartet is null)
        {
            UpdateChannel.ParseVersion(datei, suffix).Should().BeNull();
        }
    }

    [Fact]
    public void Hoechste_Version_gewinnt_nicht_die_juengste_Datei()
    {
        var neu = Package("1.10.0");
        var alt = Package("1.9.0");

        // Das ältere Paket wurde zuletzt zurückkopiert — nach Zeitstempel wäre es die Wahl.
        File.SetLastWriteTimeUtc(neu, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(alt, DateTime.UtcNow);

        UpdateChannel.FindNewestPackage(_folder, Suffix)!.Value.Path.Should().Be(neu);
    }

    [Fact]
    public void Versionshinweise_aller_uebersprungenen_Versionen_neueste_zuerst()
    {
        Notes("0.1.0", "## 0.1.0\n- schon installiert");
        Notes("0.2.0", "## 0.2.0\n- zweite");
        Notes("0.3.0", "## 0.3.0\n- dritte");
        Notes("0.4.0", "## 0.4.0\n- liegt noch nicht als Paket");

        var text = UpdateChannel.CollectReleaseNotes(_folder, new Version(0, 1, 0), new Version(0, 3, 0));

        text.Should().Be("## 0.3.0\n- dritte\n\n## 0.2.0\n- zweite");
    }

    [Fact]
    public void Ohne_Notes_Datei_gibt_es_keine_Versionshinweise()
    {
        UpdateChannel.CollectReleaseNotes(_folder, new Version(0, 1, 0), new Version(0, 2, 0)).Should().BeNull();
    }

    [Fact]
    public void Neuere_Version_im_Ordner_wird_mit_Paket_und_Notes_gemeldet()
    {
        Package("0.1.0");
        var paket = Package("0.2.0");
        Notes("0.2.0", "## 0.2.0\n- neu");

        var result = UpdateService.Check(_folder, new Version(0, 1, 1));

        result.UpdateAvailable.Should().BeTrue();
        result.CanInstall.Should().BeTrue();
        result.LatestVersion.Should().Be("0.2.0");
        result.PackagePath.Should().Be(paket);
        result.ReleaseNotes.Should().Be("## 0.2.0\n- neu");
    }

    [Fact]
    public void Gleiche_Version_ist_kein_Update()
    {
        Package("0.2.0");

        var result = UpdateService.Check(_folder, new Version(0, 2, 0));

        result.UpdateAvailable.Should().BeFalse();
        result.PackagePath.Should().BeNull();
        result.Problem.Should().Be(UpdateProblem.None);
    }

    [Fact]
    public void Leerer_oder_fehlender_Ordner_heisst_nicht_erreichbar()
    {
        UpdateService.Check(_folder, new Version(0, 1, 0)).Problem.Should().Be(UpdateProblem.Unreachable);
        UpdateService.Check(Path.Combine(_folder, "gibt-es-nicht"), new Version(0, 1, 0)).Problem.Should().Be(UpdateProblem.Unreachable);
    }

    [Fact]
    public async Task Ohne_Ordner_wird_gar_nicht_erst_gesucht()
    {
        var result = await new UpdateService(() => "  ").CheckForUpdateAsync(force: true);

        result.Problem.Should().Be(UpdateProblem.NoChannel);
        result.UpdateAvailable.Should().BeFalse();
    }

    private string Package(string version)
    {
        var path = Path.Combine(_folder, $"QrCodeBuilder-{version}{Suffix}");
        File.WriteAllText(path, "Paket");
        return path;
    }

    private void Notes(string version, string text) =>
        File.WriteAllText(Path.Combine(_folder, $"QrCodeBuilder-{version}-notes.md"), text);
}
