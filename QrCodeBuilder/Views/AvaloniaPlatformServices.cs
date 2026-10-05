using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using NLog;
using QrCodeBuilder.Localization;
using QrCodeBuilder.Services;

namespace QrCodeBuilder.Views;

/// <summary>
/// Speichern-Dialog über den StorageProvider. Geschrieben wird über den Stream der
/// gewählten Datei statt über einen Pfad — so klappt es auch dort, wo es keinen echten
/// Pfad gibt (Flatpak-Portal unter Linux).
/// </summary>
public sealed class StorageSaveFileDialog(Func<TopLevel?> topLevel) : ISaveFileDialog
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private IStorageFolder? _lastFolder;

    public async Task<SaveTarget?> PickAsync(string suggestedFileName, QrExportFormat format)
    {
        var storage = topLevel()?.StorageProvider;

        if (storage is null || !storage.CanSave)
        {
            Log.Warn("Kein Speichern-Dialog verfügbar.");
            return null;
        }

        var extension = QrExport.Extension(format);
        var fileType = format == QrExportFormat.Svg
            ? new FilePickerFileType(L.T("FileType_Svg")) { Patterns = ["*.svg"], MimeTypes = ["image/svg+xml"] }
            : new FilePickerFileType(L.T("FileType_Png")) { Patterns = ["*.png"], MimeTypes = ["image/png"] };

        var startFolder = _lastFolder
                          ?? await storage.TryGetWellKnownFolderAsync(WellKnownFolder.Pictures);

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L.T(format == QrExportFormat.Svg ? "Export_SaveSvg" : "Export_SavePng"),
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension,
            FileTypeChoices = [fileType],
            ShowOverwritePrompt = true,
            SuggestedStartLocation = startFolder,
        });

        if (file is null)
        {
            return null;
        }

        // Nächster Export startet im selben Ordner — die Sitzung lang, nicht dauerhaft.
        _lastFolder = await file.GetParentAsync() ?? _lastFolder;

        var stream = await file.OpenWriteAsync();

        // OpenWriteAsync kürzt eine bestehende Datei nicht auf allen Plattformen —
        // ohne das bliebe beim Überschreiben einer größeren Datei Müll am Ende stehen.
        if (stream.CanSeek)
        {
            stream.SetLength(0);
        }

        return new SaveTarget(stream, file.Path.IsFile ? file.Path.LocalPath : file.Name);
    }
}

/// <summary>Bild in die Zwischenablage — für Einfügen in Word, Outlook, Teams und Co.</summary>
public sealed class AvaloniaImageClipboard(Func<TopLevel?> topLevel) : IImageClipboard
{
    public async Task CopyPngAsync(byte[] png)
    {
        var clipboard = topLevel()?.Clipboard
                        ?? throw new InvalidOperationException("Keine Zwischenablage verfügbar.");

        using var stream = new MemoryStream(png);
        using var bitmap = new Bitmap(stream);

        await clipboard.SetBitmapAsync(bitmap);

        // Ohne Flush ist das Bild unter Windows weg, sobald die App beendet wird.
        await clipboard.FlushAsync();
    }
}
