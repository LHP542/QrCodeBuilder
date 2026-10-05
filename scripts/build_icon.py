#!/usr/bin/env python3
"""QR Code Builder App-Icon-Generator (Pillow).

Gleiches Design wie scripts/build_icon.ps1 — Änderungen IMMER in beiden
Fassungen nachziehen, sonst driften PNG und ICO je nach Rechner auseinander.

Design: stilisierter QR-Code auf einem 7x7-Raster — drei Finder-Muster in den
Ecken und ein X aus Datenmodulen unten rechts, in Kroste-Gold auf dunklem,
abgerundetem Grund.

Erzeugt:
  QrCodeBuilder/Assets/qrcodebuilder.png  (256x256, master)
  QrCodeBuilder/Assets/qrcodebuilder.ico  (Windows-Multi-Res)

Aufruf: python3 scripts/build_icon.py   (braucht: pip install pillow)
"""

from pathlib import Path

from PIL import Image, ImageDraw

GOLD = (224, 177, 76, 255)      # #E0B14C
SURFACE = (26, 29, 33, 255)     # #1A1D21
BORDER = (60, 68, 78, 255)      # #3C444E

CORNER = 48
RAND = 34
ZELLEN = 7
FINDER = [(0, 0), (4, 0), (0, 4)]
DATEN = [(4, 4), (6, 4), (5, 5), (4, 6), (6, 6)]
GROESSEN = [16, 24, 32, 48, 64, 128, 256]

ASSETS = Path(__file__).resolve().parent.parent / "QrCodeBuilder" / "Assets"


def icon(size: int) -> Image.Image:
    skala = size / 256.0
    bild = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    g = ImageDraw.Draw(bild)

    randbreite = max(1, int(2 * skala))
    g.rounded_rectangle((0, 0, size - 1, size - 1), radius=CORNER * skala,
                        fill=SURFACE, outline=BORDER, width=randbreite)

    zelle = (256 - 2 * RAND) / ZELLEN * skala
    start = RAND * skala
    radius = max(0.0, 0.18 * zelle)

    def feld(x, y, w, h, r, farbe):
        g.rounded_rectangle((x, y, x + w, y + h), radius=r, fill=farbe)

    for spalte, zeile in FINDER:
        x = start + spalte * zelle
        y = start + zeile * zelle
        # Außenquadrat, ausgestanzter Ring, Kern — Verhältnis wie beim echten Finder.
        feld(x, y, 3 * zelle, 3 * zelle, 2 * radius, GOLD)
        feld(x + 0.5 * zelle, y + 0.5 * zelle, 2 * zelle, 2 * zelle, radius, SURFACE)
        feld(x + zelle, y + zelle, zelle, zelle, radius, GOLD)

    for spalte, zeile in DATEN:
        feld(start + spalte * zelle, start + zeile * zelle, zelle, zelle, radius, GOLD)

    return bild


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)

    icon(256).save(ASSETS / "qrcodebuilder.png")

    # Jede Größe einzeln rendern statt herunterzuskalieren — sonst verschwimmen
    # die Finder-Ringe bei 16x16.
    frames = [icon(s) for s in GROESSEN]
    frames[-1].save(ASSETS / "qrcodebuilder.ico", sizes=[(s, s) for s in GROESSEN],
                    append_images=frames[:-1])

    print(f"geschrieben: {ASSETS / 'qrcodebuilder.png'}")
    print(f"geschrieben: {ASSETS / 'qrcodebuilder.ico'}")


if __name__ == "__main__":
    main()
