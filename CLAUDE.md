# QrCodeBuilder

## Grundlagen

- **Was:** Desktop-App, die QR-Codes (Text/Link, WLAN) erzeugt und als PNG, SVG oder per Zwischenablage exportiert.
- **Repo:** `github.com/LHP542/QrCodeBuilder` (öffentlich — nötig, weil der Update-Check anonym gegen die GitHub-API läuft; privat gäbe es 404).
- **Stack:** C# / .NET 10 / Avalonia 12.1.1, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, NLog (mit Secret-Masking), QRCoder 1.8.0 (nur Kodierung), xunit.v3 + FluentAssertions 7.x
- **Struktur:** Flach (kein `src/`), `.slnx`, Central Package Management, `Directory.Build.props` mit `EnforceCodeStyleInBuild`, MinVer (Tags `v*`)
- **Konventionen:** Kroste-Standard (Skill `kroste-avalonia`): ChromeWindow/TitleBar, Card-Look, Tray, Single-Instance, Self-Update, EN+DE.
- **Org-Abweichung (LHP542, Vorbild LDAPeek):** kein Buy-me-a-coffee, keine `FUNDING.yml`, README auf Deutsch aus Nutzersicht mit Bildern in `docs/`.
- **Kommunikation:** Deutsch, "du". Lars entwirft, Claude implementiert.

## Aktueller Stand

- v0.1.0: Text/Link und WLAN (WPA/WEP/offen, versteckt), Fehlerkorrektur L/M/Q/H, Modulgröße 1–40 px, Vorder-/Hintergrundfarbe als Hex (inkl. Alpha → transparent), Ruhezone an/aus.
- Export: PNG speichern, SVG speichern, PNG in die Zwischenablage. Dateinamensvorschlag aus dem Inhalt (bei WLAN nur SSID, nie das Passwort).
- Hinweise (keine Sperren): zu geringer Kontrast (< 3:1 WCAG), invertierte Farben, ungültige Farbe, Inhalt zu lang.
- Darstellung wird gespeichert (`settings.json`), Inhalte nie.
- 92 Tests grün, Gegenprobe mit absichtlich kaputtem Resource-Key und verschobenem Pixelversatz: beide rot.

## Roadmap

- Kandidaten (nicht beauftragt): weitere Inhaltstypen (Kontakt/vCard, E-Mail, Telefon), Logo in der Mitte (braucht Fehlerkorrektur H), Farbwähler statt Hex-Feld, Ziehen der Vorschau per Drag & Drop in andere Programme, Stapel-Export aus CSV.
- `scripts/build_icon.py` ist auf dem Arbeitslaptop nicht ausgeführt worden (pip-Downloads 403); bei Gelegenheit auf Bazzite gegen `build_icon.ps1` abgleichen.

## Referenz

- **Kodierung vs. Darstellung:** `QrMatrix.Encode` nutzt nur `QRCodeGenerator` und schneidet die 4-Modul-Ruhezone von QRCoder ab; die Ruhezone setzt erst `QrRenderer`. Test `Ruhezone_ist_abgeschnitten_und_Finder_sitzen_in_den_Ecken` sichert den Versatz.
- **Eigener Renderer statt QRCoder-Renderer:** deren Farbüberladungen hängen an System.Drawing (unter Linux nicht lauffähig). `QrRenderer.RenderPng` schreibt ein Paletten-PNG mit 1 Bit/Pixel (+ `tRNS` für Transparenz), zeilenweise in einen `ZLibStream`. `RenderSvg` fasst waagerechte Läufe zu Pfadsegmenten zusammen. Die Tests lesen das PNG mit `PngReader` zurück (inkl. CRC) und vergleichen jedes Pixel mit der Matrix.
- **ViewModel ohne Avalonia:** `MainWindowViewModel` liefert `PreviewPng` als Bytes; `PngToBitmapConverter` macht daraus die Bitmap. Dialog und Zwischenablage hinter `ISaveFileDialog`/`IImageClipboard` (Implementierung `Views/AvaloniaPlatformServices.cs`), damit der Export testbar ist.
- **Speichern über Stream** (`IStorageFile.OpenWriteAsync`, danach `SetLength(0)`), nicht über Pfad — funktioniert auch mit Flatpak-Portalen.
- **Zwischenablage:** `ClipboardExtensions.SetBitmapAsync` + `FlushAsync` (sonst ist das Bild unter Windows nach dem Beenden weg).
- **ComboBox-Beschriftungen:** `Choice<T>` mit `LocalizedString`-Label + DataTemplate auf `LocalizedChoice` → folgen dem Sprachwechsel live.
- **SystemAccentColor** in `App.axaml` auf Kroste-Blau gelegt, sonst sind Regler/Checkbox Windows-Systemblau.
- **Logs** unter `LocalApplicationData/QrCodeBuilder/logs` statt `logs/` neben der Exe: das BaseDirectory ist beim AppImage read-only.
- **Werkzeugmodus** `--screenshots <ordner>` (nur Debug, `Views/ScreenshotMode.cs`): rendert Haupt-, WLAN-, Hinweis-, Einstellungs- und Über-Fenster in EN und DE; jedes Bild mit frischen Einstellungen in einem Temp-Ordner. Quelle der README-Bilder.
- **Arbeitslaptop/NuGet:** Versionen in `Directory.Packages.props` folgen `C:\NuGet-Local` (Proxy blockt `.nupkg`). **QRCoder 1.8.0 lag dort nicht**: beim Aufsetzen wurde es aus dem Tag `v1.8.0` (github.com/codebude/QRCoder) für net10.0 selbst gepackt und liegt so im lokalen NuGet-Cache. Das offizielle Paket sollte über den Citrix-Browser nach `C:\NuGet-Local` geholt und der Cache-Ordner `%USERPROFILE%\.nuget\packages\qrcoder\1.8.0` danach gelöscht werden. Die CI zieht ohnehin das offizielle Paket von nuget.org.
- **Windows-Flaggen:** Segoe UI Emoji hat keine Flaggen-Glyphen, im Sprachumschalter steht unter Windows „DE"/„GB" statt der Flagge. Kein Fehler der App.
