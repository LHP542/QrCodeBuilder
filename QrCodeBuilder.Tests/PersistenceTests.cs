using FluentAssertions;
using QrCodeBuilder.Persistence;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "QrCodeBuilder-Tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Ohne_Datei_gelten_die_Standardwerte()
    {
        new SettingsService(_directory).Load().Should().Be(AppSettings.Default);
    }

    [Fact]
    public void Einstellungen_ueberstehen_Speichern_und_Laden()
    {
        var service = new SettingsService(_directory);
        var settings = AppSettings.Default with { UiCulture = "de", ErrorCorrection = QrErrorCorrection.H, ModuleSize = 12 };

        service.Save(settings).Should().BeTrue();

        new SettingsService(_directory).Load().Should().Be(settings);
    }

    [Fact]
    public void Enums_stehen_als_Namen_in_der_Datei()
    {
        new SettingsService(_directory).Save(AppSettings.Default with { ErrorCorrection = QrErrorCorrection.H });

        File.ReadAllText(Path.Combine(_directory, "settings.json")).Should().Contain("\"H\"");
    }

    [Fact]
    public void Kaputte_Datei_wird_gesichert_statt_ueberschrieben()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{ das ist kein JSON");

        var loaded = new SettingsService(_directory).Load();

        loaded.Should().Be(AppSettings.Default);
        File.Exists(path + ".broken").Should().BeTrue();
        File.ReadAllText(path + ".broken").Should().Be("{ das ist kein JSON");
    }

    [Fact]
    public void Speichern_hinterlaesst_keine_tmp_Datei()
    {
        new SettingsService(_directory).Save(AppSettings.Default);

        Directory.GetFiles(_directory).Should().ContainSingle().Which.Should().EndWith("settings.json");
    }
}
