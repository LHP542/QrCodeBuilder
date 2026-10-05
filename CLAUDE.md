# QrCodeBuilder

## Grundlagen

- **Was:** Desktop-App, die QR-Codes (Text/Link, WLAN) erzeugt und als PNG, SVG oder per Zwischenablage exportiert.
- **Repo:** `github.com/LHP542/QrCodeBuilder` (öffentlich). GitHub Releases sind nur die **Bauquelle** — die App holt Updates seit 0.2.0 aus dem Share.
- **Dienstliches Werkzeug, Update-Kanal = Netzwerkordner** `\\samba01\542$\5424_IT-Basis-Dienste\QrCodeBuilder` (Ausnahme aus `references/autoupdate.md`, wie LDAPeek). Versionen 0.1.0/0.1.1 prüften noch gegen GitHub; sie finden 0.2.0 dort und wechseln damit auf den Share.
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
- v0.2.0: Update-Kanal Netzwerkordner (Standard in `AppSettings.DefaultUpdateChannel`, änderbar unter Einstellungen → Updates, leer = aus). Versionshinweise aller übersprungenen Versionen im Update-Dialog und im Über-Fenster.
- 118 Tests grün. Gegenprobe (v0.1.0) mit absichtlich kaputtem Resource-Key und verschobenem Pixelversatz: beide rot.

## Roadmap

- Kandidaten (nicht beauftragt): weitere Inhaltstypen (Kontakt/vCard, E-Mail, Telefon), Logo in der Mitte (braucht Fehlerkorrektur H), Farbwähler statt Hex-Feld, Ziehen der Vorschau per Drag & Drop in andere Programme, Stapel-Export aus CSV.
- `scripts/build_icon.py` ist auf dem Arbeitslaptop nicht ausgeführt worden (pip-Downloads 403); bei Gelegenheit auf Bazzite gegen `build_icon.ps1` abgleichen.

## Referenz

- **Release-Ablauf:** (1) Abschnitt `## X.Y.Z — Datum` in `CHANGELOG.md` (nutzerseitig formuliert), (2) Tag `vX.Y.Z` → `release.yml` schneidet ihn per `scripts/extract-notes.sh` als `QrCodeBuilder-X.Y.Z-notes.md` heraus (fehlt er, bricht das Release ab), nutzt ihn als Release-Text und hängt ihn an, (3) `scripts\publish-share.ps1 -Version X.Y.Z -Source <Ordner>` prüft jede Datei gegen den SHA-256-`digest` des GitHub-Assets (Metadaten über `gh api` kommen durch den Proxy) und kopiert Notes ZUERST, dann Pakete über `.tmp`-Namen auf den Share. **Der FortiProxy blockt `.zip`/`.tar.gz` von `release-assets.githubusercontent.com` (403), `.md` und `.AppImage` gehen durch** — auf dem Arbeitslaptop lädt Lars die Pakete daher über den Citrix-Browser, `-Source` zeigt auf diesen Ordner. Den Filter nicht umgehen (z. B. Pakete umbenennen): das ist eine Sicherheitsvorgabe. Das Kopieren auf den Share ist das eigentliche Ausrollen an die Kollegen — vorher mit Lars abstimmen.
- **Ordner-Kanal (`Services/UpdateChannel.cs`):** Version steht im Dateinamen; höchste Version gewinnt, nicht der jüngste Zeitstempel; Paket nach Plattform/Laufform (`-win-x64.zip`, `-x86_64.AppImage` bei `$APPIMAGE`, sonst `-linux-x64.tar.gz`). `CollectReleaseNotes` sammelt alle `*-notes.md` mit installiert < v ≤ neu, neueste zuerst. Unerreichbarer Ordner ist `Debug`, kein Fehler (Notebook unterwegs). `UpdateService.DownloadAndApplyAsync` kopiert erst lokal, entpackt dann (Windows) und startet das Austausch-Skript; Aufrufer MÜSSEN danach `TerminateForUpdate()` rufen (About-Dialog und `UpdatePromptWindow`).
- **Notes-Anzeige:** `ReleaseNotesFormatter` (testbar, Services) macht aus dem Markdown-Subset Zeilen; `Views/ReleaseNotesView` setzt sie als Inlines (Überschriften fett, „•").

- **Kodierung vs. Darstellung:** `QrMatrix.Encode` nutzt nur `QRCodeGenerator` und schneidet die 4-Modul-Ruhezone von QRCoder ab; die Ruhezone setzt erst `QrRenderer`. Test `Ruhezone_ist_abgeschnitten_und_Finder_sitzen_in_den_Ecken` sichert den Versatz.
- **Eigener Renderer statt QRCoder-Renderer:** deren Farbüberladungen hängen an System.Drawing (unter Linux nicht lauffähig). `QrRenderer.RenderPng` schreibt ein Paletten-PNG mit 1 Bit/Pixel (+ `tRNS` für Transparenz), zeilenweise in einen `ZLibStream`. `RenderSvg` fasst waagerechte Läufe zu Pfadsegmenten zusammen. Die Tests lesen das PNG mit `PngReader` zurück (inkl. CRC) und vergleichen jedes Pixel mit der Matrix.
- **ViewModel ohne Avalonia:** `MainWindowViewModel` liefert `PreviewPng` als Bytes; `PngToBitmapConverter` macht daraus die Bitmap. Dialog und Zwischenablage hinter `ISaveFileDialog`/`IImageClipboard` (Implementierung `Views/AvaloniaPlatformServices.cs`), damit der Export testbar ist.
- **Speichern über Stream** (`IStorageFile.OpenWriteAsync`, danach `SetLength(0)`), nicht über Pfad — funktioniert auch mit Flatpak-Portalen.
- **Zwischenablage:** `ClipboardExtensions.SetBitmapAsync` + `FlushAsync` (sonst ist das Bild unter Windows nach dem Beenden weg).
- **ComboBox-Beschriftungen:** `Choice<T>` mit `LocalizedString`-Label + DataTemplate auf `LocalizedChoice` → folgen dem Sprachwechsel live.
- **SystemAccentColor** in `App.axaml` auf Kroste-Blau gelegt, sonst sind Regler/Checkbox Windows-Systemblau.
- **Logs** unter `LocalApplicationData/QrCodeBuilder/logs` statt `logs/` neben der Exe: das BaseDirectory ist beim AppImage read-only.
- **Werkzeugmodus** `--screenshots <ordner>` (nur Debug, `Views/ScreenshotMode.cs`): rendert Haupt-, WLAN-, Hinweis-, Einstellungs-, Über- und Update-Fenster (mit erfundenen Notes) in EN und DE; jedes Bild mit frischen Einstellungen in einem Temp-Ordner. Quelle der README-Bilder.
- **Arbeitslaptop/NuGet:** Versionen in `Directory.Packages.props` folgen `C:\NuGet-Local` (Proxy blockt `.nupkg`). **QRCoder 1.8.0 lag dort nicht**: beim Aufsetzen wurde es aus dem Tag `v1.8.0` (github.com/codebude/QRCoder) für net10.0 selbst gepackt und liegt so im lokalen NuGet-Cache. Das offizielle Paket sollte über den Citrix-Browser nach `C:\NuGet-Local` geholt und der Cache-Ordner `%USERPROFILE%\.nuget\packages\qrcoder\1.8.0` danach gelöscht werden. Die CI zieht ohnehin das offizielle Paket von nuget.org.
- **Windows-Flaggen:** Segoe UI Emoji hat keine Flaggen-Glyphen, im Sprachumschalter steht unter Windows „DE"/„GB" statt der Flagge. Kein Fehler der App.
