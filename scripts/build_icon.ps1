# QR Code Builder App-Icon-Generator (PowerShell-Port von build_icon.py).
#
# Warum zwei Fassungen: der Arbeitslaptop hat nicht überall ein echtes Python.
# Beide Fassungen müssen dasselbe Icon erzeugen — Änderungen am Design also
# IMMER in beiden nachziehen, sonst driften PNG und ICO je nach Rechner auseinander.
#
# Design: stilisierter QR-Code auf einem 7x7-Raster — drei Finder-Muster in den
# Ecken und ein X aus Datenmodulen unten rechts, in Kroste-Gold auf dunklem,
# abgerundetem Grund. Grob genug, dass es als 16x16-Favicon noch nach QR aussieht.
#
# Erzeugt:
#   QrCodeBuilder/Assets/qrcodebuilder.png  (256x256, master)
#   QrCodeBuilder/Assets/qrcodebuilder.ico  (Windows-Multi-Res)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

# Kroste-Palette (aus App.axaml)
$gold    = [System.Drawing.Color]::FromArgb(255, 224, 177, 76)   # #E0B14C
$surface = [System.Drawing.Color]::FromArgb(255, 26, 29, 33)     # #1A1D21
$border  = [System.Drawing.Color]::FromArgb(255, 60, 68, 78)     # #3C444E

$corner = 48
$appName = 'qrcodebuilder'
$outDir = Join-Path $PSScriptRoot '..' | Join-Path -ChildPath 'QrCodeBuilder' |
    Join-Path -ChildPath 'Assets'

# Raster: 7x7 Zellen innerhalb eines Rands von 34 px (bei 256 px).
$rand = 34
$zellen = 7

# Finder-Muster: linke obere Zelle (Spalte, Zeile), jeweils 3x3 Zellen groß.
$finder = @(@(0, 0), @(4, 0), @(0, 4))

# Datenmodule unten rechts als X — eine Zelle je Eintrag.
$daten = @(@(4, 4), @(6, 4), @(5, 5), @(4, 6), @(6, 6))

function New-RoundedPath {
    param([float]$X, [float]$Y, [float]$W, [float]$H, [float]$R)

    $pfad = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $R * 2

    if ($d -le 0) {
        $pfad.AddRectangle((New-Object System.Drawing.RectangleF($X, $Y, $W, $H)))
        return $pfad
    }

    $pfad.AddArc($X, $Y, $d, $d, 180, 90)
    $pfad.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $pfad.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $pfad.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $pfad.CloseFigure()

    return $pfad
}

function Fill-Rounded {
    param($G, $Pinsel, [float]$X, [float]$Y, [float]$W, [float]$H, [float]$R)

    $pfad = New-RoundedPath $X $Y $W $H $R
    $G.FillPath($Pinsel, $pfad)
    $pfad.Dispose()
}

function New-Icon {
    param([int]$Size)

    $skala = $Size / 256.0
    $bild = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bild)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $randbreite = [Math]::Max(1, [int](2 * $skala))
    $grundPfad = New-RoundedPath 0 0 ($Size - 1) ($Size - 1) ($corner * $skala)

    $grund = New-Object System.Drawing.SolidBrush($surface)
    $g.FillPath($grund, $grundPfad)

    $stift = New-Object System.Drawing.Pen($border, $randbreite)
    $g.DrawPath($stift, $grundPfad)
    $stift.Dispose()
    $grundPfad.Dispose()

    $zelle = (256 - (2 * $rand)) / $zellen * $skala
    $start = $rand * $skala
    $motiv = New-Object System.Drawing.SolidBrush($gold)
    $radius = [Math]::Max(0, 0.18 * $zelle)

    foreach ($f in $finder) {
        $x = $start + ($f[0] * $zelle)
        $y = $start + ($f[1] * $zelle)

        # Außenquadrat, ausgestanzter Ring, Kern — Verhältnis wie beim echten Finder.
        Fill-Rounded $g $motiv $x $y (3 * $zelle) (3 * $zelle) (2 * $radius)
        Fill-Rounded $g $grund ($x + 0.5 * $zelle) ($y + 0.5 * $zelle) (2 * $zelle) (2 * $zelle) $radius
        Fill-Rounded $g $motiv ($x + $zelle) ($y + $zelle) $zelle $zelle $radius
    }

    foreach ($d in $daten) {
        Fill-Rounded $g $motiv ($start + ($d[0] * $zelle)) ($start + ($d[1] * $zelle)) $zelle $zelle $radius
    }

    $motiv.Dispose()
    $grund.Dispose()
    $g.Dispose()
    return $bild
}

# ICO von Hand schreiben: System.Drawing kann kein Multi-Res-ICO speichern.
function Save-Ico {
    param([System.Drawing.Bitmap[]]$Frames, [string]$Path)

    $pngs = foreach ($frame in $Frames) {
        $puffer = New-Object System.IO.MemoryStream
        $frame.Save($puffer, [System.Drawing.Imaging.ImageFormat]::Png)
        , $puffer.ToArray()
    }

    $datei = [System.IO.File]::Create($Path)
    $schreiber = New-Object System.IO.BinaryWriter($datei)

    $schreiber.Write([uint16]0)                  # reserviert
    $schreiber.Write([uint16]1)                  # Typ 1 = Icon
    $schreiber.Write([uint16]$Frames.Count)

    # Verzeichnis: 6 Byte Kopf + 16 Byte je Eintrag, danach die PNG-Daten.
    $offset = 6 + (16 * $Frames.Count)

    for ($i = 0; $i -lt $Frames.Count; $i++) {
        $kante = $Frames[$i].Width
        # 256 wird im ICO-Format als 0 kodiert.
        $schreiber.Write([byte]$(if ($kante -ge 256) { 0 } else { $kante }))
        $schreiber.Write([byte]$(if ($kante -ge 256) { 0 } else { $kante }))
        $schreiber.Write([byte]0)                # Farbpalette
        $schreiber.Write([byte]0)                # reserviert
        $schreiber.Write([uint16]1)              # Farbebenen
        $schreiber.Write([uint16]32)             # Bit pro Pixel
        $schreiber.Write([uint32]$pngs[$i].Length)
        $schreiber.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }

    foreach ($png in $pngs) { $schreiber.Write($png) }

    $schreiber.Dispose()
    $datei.Dispose()
}

$zielOrdner = [System.IO.Path]::GetFullPath($outDir)
$null = New-Item -ItemType Directory -Path $zielOrdner -Force

$master = New-Icon -Size 256
$pngPfad = Join-Path $zielOrdner "$appName.png"
$master.Save($pngPfad, [System.Drawing.Imaging.ImageFormat]::Png)

# Jede Größe einzeln rendern statt herunterzuskalieren, sonst verschwimmen die
# Finder-Ringe bei 16x16.
$groessen = @(16, 24, 32, 48, 64, 128, 256)
$frames = foreach ($g in $groessen) { New-Icon -Size $g }

$icoPfad = Join-Path $zielOrdner "$appName.ico"
Save-Ico -Frames $frames -Path $icoPfad

# Kontrollbild: alle Größen nebeneinander auf hellem UND dunklem Grund — die
# Master-PNG sagt nichts darüber, wie die kleinen Varianten im ICO aussehen.
$breite = ($groessen | Measure-Object -Sum).Sum + (10 * ($groessen.Count + 1))
$hoehe = 2 * (256 + 20)
$kontrolle = New-Object System.Drawing.Bitmap([int]$breite, [int]$hoehe)
$kg = [System.Drawing.Graphics]::FromImage($kontrolle)
$kg.FillRectangle([System.Drawing.Brushes]::White, 0, 0, $breite, 276)
$kg.FillRectangle([System.Drawing.Brushes]::Black, 0, 276, $breite, 276)
$x = 10
for ($i = 0; $i -lt $groessen.Count; $i++) {
    $kg.DrawImageUnscaled($frames[$i], $x, 10)
    $kg.DrawImageUnscaled($frames[$i], $x, 286)
    $x += $groessen[$i] + 10
}
$kg.Dispose()
$kontrollPfad = Join-Path ([System.IO.Path]::GetTempPath()) "$appName-icon-check.png"
$kontrolle.Save($kontrollPfad, [System.Drawing.Imaging.ImageFormat]::Png)
$kontrolle.Dispose()

foreach ($frame in $frames) { $frame.Dispose() }
$master.Dispose()

"geschrieben: $pngPfad"
"geschrieben: $icoPfad"
"Kontrollbild: $kontrollPfad"
