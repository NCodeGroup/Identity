#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# dependencies = ["fonttools==4.68.0", "uharfbuzz==0.57.0"]
# ///
"""Write brand/outlines.json: the logo kit's lines of text as outlines, so that no file of the kit needs a font.

    uv run brand/outlines.py                        uv installs the two dependencies above for the run
    uv run brand/outlines.py --archive Inter-4.1.zip    with the release already downloaded

The lines are set in Inter Display (https://github.com/rsms/inter), which is under the SIL Open Font License
1.1; its copyright notice and license are in THIRD-PARTY-NOTICES.md at the repository root. The script
downloads the release pinned below, verifies its SHA-256 and the SHA-256 of each font file it reads, shapes
each line with HarfBuzz, so the font's kerning applies, and writes the glyphs' outlines as SVG path data.

A line's outlines have capitals 32 units tall, as tall as the d of the mark, the first glyph's origin at
x = 0 and the baseline at y = 0, with y pointing down. The output is deterministic: a change to LINES or to
the pinned release changes the committed file, and brand/build.py, run next, draws the kit from it.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import sys
import urllib.request
import zipfile
from pathlib import Path

import uharfbuzz as hb
from fontTools.pens.basePen import BasePen
from fontTools.ttLib import TTFont

# The release, its archive and the two font files read from it.
RELEASE = "https://github.com/rsms/inter/releases/download/v4.1/Inter-4.1.zip"
RELEASE_SHA256 = "9883fdd4a49d4fb66bd8177ba6625ef9a64aa45899767dde3d36aa425756b11e"
FONTS = {
    "Inter Display Medium": ("extras/otf/InterDisplay-Medium.otf",
                             "9a17712bbd6da13b7ce9beb056d19fe15648799a11d5c35f8086a1270a0d13b2"),
    "Inter Display Regular": ("extras/otf/InterDisplay-Regular.otf",
                              "35c1f7437dd6d63f4e60a49144df4b4e4edc190e13ffc1e54b86083747fe3acf"),
}

# The lines: the wordmark, and the descriptor under it on a link preview.
LINES = {
    "wordmark": ("NCode Identity", "Inter Display Medium"),
    "descriptor": ("OpenID Connect and OAuth 2.x for .NET", "Inter Display Regular"),
}

CAP_HEIGHT = 32   # the height of a capital in the outlines' units


def verified(data: bytes, expected: str, what: str) -> bytes:
    actual = hashlib.sha256(data).hexdigest()
    if actual != expected:
        raise SystemExit(f"{what} has SHA-256 {actual}, expected {expected}.")
    return data


def number(value: float) -> str:
    text = f"{value:.2f}".rstrip("0").rstrip(".")
    return "0" if text in ("", "-0") else text


class PathPen(BasePen):
    """Collects a glyph's outline as SVG path data, scaled, shifted, and turned to y pointing down."""

    def __init__(self, glyphs, scale: float, dx: float, dy: float):
        super().__init__(glyphs)
        self.scale, self.dx, self.dy = scale, dx, dy
        self.data: list[str] = []
        self.at = ("", "")

    def point(self, p) -> tuple[str, str]:
        return number(p[0] * self.scale + self.dx), number(self.dy - p[1] * self.scale)

    def _moveTo(self, p):
        self.at = self.point(p)
        self.data.append(f"M{self.at[0]} {self.at[1]}")

    def _lineTo(self, p):
        x, y = self.point(p)
        if y == self.at[1]:
            self.data.append(f"H{x}")
        elif x == self.at[0]:
            self.data.append(f"V{y}")
        else:
            self.data.append(f"L{x} {y}")
        self.at = (x, y)

    def _curveToOne(self, p1, p2, p3):
        points = [self.point(p) for p in (p1, p2, p3)]
        self.data.append("C" + " ".join(f"{x} {y}" for x, y in points))
        self.at = points[2]

    def _closePath(self):
        self.data.append("Z")


def outline(font: bytes, text: str) -> dict:
    """The text shaped with the font's default features: its path, and its advance width."""
    table = TTFont(io.BytesIO(font))
    glyphs, order = table.getGlyphSet(), table.getGlyphOrder()
    scale = CAP_HEIGHT / table["OS/2"].sCapHeight
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(hb.Font(hb.Face(font)), buffer)
    data, pen_x = [], 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        pen = PathPen(glyphs, scale, (pen_x + position.x_offset) * scale, position.y_offset * scale)
        glyphs[order[info.codepoint]].draw(pen)
        data.extend(pen.data)
        pen_x += position.x_advance
    return {"advance": round(pen_x * scale, 2), "path": "".join(data)}


def main() -> int:
    here = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description="Write the logo kit's lines of text as outlines.")
    parser.add_argument("--archive", type=Path, help="the release's zip file, when it is already downloaded")
    parser.add_argument("--output", type=Path, default=here / "outlines.json",
                        help="the file to write (default: outlines.json beside this script)")
    args = parser.parse_args()

    if args.archive:
        archive = args.archive.read_bytes()
    else:
        print(f"Downloading {RELEASE}")
        with urllib.request.urlopen(RELEASE) as response:
            archive = response.read()
    release = zipfile.ZipFile(io.BytesIO(verified(archive, RELEASE_SHA256, "The release archive")))
    fonts = {name: verified(release.read(member), sha256, member) for name, (member, sha256) in FONTS.items()}

    lines = {}
    for name, (text, font) in LINES.items():
        lines[name] = {"text": text, "font": font, **outline(fonts[font], text)}
        print(f"{name:<12}{text!r}, {font}: advance {lines[name]['advance']}, {len(lines[name]['path'])} characters")
    document = {
        "source": "Inter 4.1, https://github.com/rsms/inter, SIL Open Font License 1.1: THIRD-PARTY-NOTICES.md",
        "capHeight": CAP_HEIGHT,
        "lines": lines,
    }
    args.output.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Wrote {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
