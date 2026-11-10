#!/usr/bin/env python3
# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy==2.5.4", "pillow==13.0.0"]
# ///
"""Build the NCode Identity logo kit: the {id} mark as SVG masters, and every PNG and ICO export.

    uv run brand/build.py        uv installs the two dependencies above for the run
    python brand/build.py        with numpy and Pillow already installed

Rewrites every generated file under brand/. brand/README.md says which file is for what.

The mark is the letters i and d between the braces of the NCode Group's mark, {N}. It is drawn on a 64-unit
grid from straight lines and circular arcs, so the SVG paths and the PNG pixels come from the same geometry.
Sizes from 16 to 48 pixels have their own pixel-fitted geometry (FITTED), because a scaled-down master puts
its strokes between pixels. The text of the lockups and the cards is read as outlines from outlines.json,
which brand/outlines.py writes.
"""
from __future__ import annotations

import argparse
import base64
import io
import json
import math
import re
import struct
import sys
import urllib.parse
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
from PIL import Image

NAME = "NCode Identity"
PREFIX = "ncode-identity"   # every file's name starts with it

# ---- Palette: the NCode Group's, one hue (225 degrees) at three lightnesses ----------------------
BLUE = "#0033CC"   # NCode Blue, hsl(225 100% 40%): the mark on light backgrounds, and the tile
LIGHT = "#809FFF"  # NCode Blue Light, hsl(225 100% 75%): the mark on dark backgrounds
NAVY = "#0A1F5C"   # NCode Navy, hsl(225 80% 20%): the dark tile and the dark cards
WHITE = "#FFFFFF"
BLACK = "#000000"

INKS = {"blue": BLUE, "light": LIGHT, "white": WHITE, "black": BLACK}                  # transparent
TILES = {"blue": (BLUE, WHITE), "white": (WHITE, BLUE), "dark": (NAVY, LIGHT)}         # fill, mark
CARDS = {"light": (WHITE, BLUE), "dark": (NAVY, LIGHT), "blue": (BLUE, WHITE)}         # fill, lockup

GRID = 64.0            # the master's canvas
RADIUS = 14.0 / 64.0   # corner radius of a rounded tile, as a fraction of its width
TIGHT = 1.0            # mark across 7/8 of the canvas: icons shown small (favicon, NuGet, badges)
REGULAR = 0.875        # app tiles and avatars shown in a rounded square
SAFE = 0.8125          # inside the center circle of 80% diameter: circular crops, maskable icons

# The largest file each platform takes, in bytes.
NUGET_LIMIT = GITHUB_LIMIT = 1024 * 1024   # 1 MB
GITLAB_LIMIT = 200 * 1024                  # 200 KiB
MYAPPS_LIMIT = 100 * 1000                  # 100 KB


# ---- Geometry: closed contours of lines, circular arcs and Bezier curves ------------------------
def _num(v: float, digits: int) -> str:
    s = f"{v:.{digits}f}".rstrip("0").rstrip(".")
    return "0" if s in ("", "-0") else s


def _on_arc(center, r, angle):
    t = math.radians(angle)
    return (center[0] + r * math.cos(t), center[1] + r * math.sin(t))


@dataclass
class Contour:
    """One closed outline. Angles are in degrees with y pointing down, so a growing angle turns clockwise.

    Outlines run clockwise on screen, so that shapes which overlap add up to their union under the
    non-zero fill rule, in an SVG path and in the rasterizer alike.
    """

    start: tuple[float, float]
    segments: list = field(default_factory=list)

    def line(self, x, y):
        last = self.end_of(self.segments[-1]) if self.segments else self.start
        if math.dist(last, (x, y)) > 1e-9:               # a hook without a straight end adds no line
            self.segments.append(("L", (x, y)))
        return self

    def arc(self, cx, cy, r, a0, a1):
        self.segments.append(("A", (cx, cy), r, a0, a1))
        return self

    def cubic(self, p1, p2, p3):
        self.segments.append(("C", p1, p2, p3))
        return self

    def end_of(self, segment):
        if segment[0] == "A":
            return _on_arc(segment[1], segment[2], segment[4])
        return segment[-1]

    def reversed(self) -> Contour:
        points = [self.start] + [self.end_of(s) for s in self.segments]
        out = Contour(points[-1])
        for i in range(len(self.segments) - 1, -1, -1):
            s = self.segments[i]
            if s[0] == "L":
                out.line(*points[i])
            elif s[0] == "A":
                out.arc(*s[1], s[2], s[4], s[3])
            else:
                out.cubic(s[2], s[1], points[i])
        return out


@dataclass
class Shape:
    contours: list = field(default_factory=list)

    def __add__(self, other: Shape) -> Shape:
        return Shape(self.contours + other.contours)

    def placed(self, scale=1.0, dx=0.0, dy=0.0) -> Shape:
        """Scaled about the origin, then shifted. The scale is uniform, so arcs stay circular."""
        def p(q):
            return (q[0] * scale + dx, q[1] * scale + dy)

        out = Shape()
        for c in self.contours:
            n = Contour(p(c.start))
            for s in c.segments:
                if s[0] == "L":
                    n.line(*p(s[1]))
                elif s[0] == "A":
                    n.arc(*p(s[1]), s[2] * scale, s[3], s[4])
                else:
                    n.cubic(p(s[1]), p(s[2]), p(s[3]))
            out.contours.append(n)
        return out

    def mirrored(self, axis_x: float) -> Shape:
        """Flipped left to right about x = axis_x, keeping each contour's winding direction."""
        def p(q):
            return (2 * axis_x - q[0], q[1])

        out = Shape()
        for c in self.contours:
            n = Contour(p(c.start))
            for s in c.segments:
                if s[0] == "L":
                    n.line(*p(s[1]))
                elif s[0] == "A":
                    n.arc(*p(s[1]), s[2], 180 - s[3], 180 - s[4])
                else:
                    n.cubic(p(s[1]), p(s[2]), p(s[3]))
            out.contours.append(n.reversed())
        return out

    def path(self, digits: int = 3) -> str:
        """SVG path data."""
        d = []
        for c in self.contours:
            cur = c.start
            d.append(f"M{_num(cur[0], digits)} {_num(cur[1], digits)}")
            for s in c.segments:
                if s[0] == "L":
                    x, y = s[1]
                    if abs(y - cur[1]) < 1e-9:
                        d.append(f"H{_num(x, digits)}")
                    elif abs(x - cur[0]) < 1e-9:
                        d.append(f"V{_num(y, digits)}")
                    else:
                        d.append(f"L{_num(x, digits)} {_num(y, digits)}")
                    cur = (x, y)
                elif s[0] == "A":
                    _, center, r, a0, a1 = s
                    cur = _on_arc(center, r, a1)
                    rr = _num(r, digits)
                    d.append(f"A{rr} {rr} 0 {int(abs(a1 - a0) > 180)} {int(a1 > a0)} "
                             f"{_num(cur[0], digits)} {_num(cur[1], digits)}")
                else:
                    d.append("C" + " ".join(f"{_num(q[0], digits)} {_num(q[1], digits)}" for q in s[1:]))
                    cur = s[3]
            d.append("Z")
        return "".join(d)

    def polygons(self, tolerance: float = 0.01) -> list[list[tuple[float, float]]]:
        """Each contour as a polygon that stays within the tolerance of its curves."""
        out = []
        for c in self.contours:
            pts = [c.start]
            for s in c.segments:
                if s[0] == "L":
                    pts.append(s[1])
                elif s[0] == "A":
                    _, center, r, a0, a1 = s
                    step = 2 * math.degrees(math.acos(max(-1.0, 1 - tolerance / r))) if r > tolerance else 360
                    n = max(1, math.ceil(abs(a1 - a0) / step))
                    pts.extend(_on_arc(center, r, a0 + (a1 - a0) * i / n) for i in range(1, n + 1))
                else:
                    p0, (p1, p2, p3) = pts[-1], s[1:]
                    length = math.dist(p0, p1) + math.dist(p1, p2) + math.dist(p2, p3)
                    n = max(4, math.ceil(math.sqrt(length / tolerance)))
                    for i in range(1, n + 1):
                        t = i / n
                        u = 1 - t
                        pts.append(tuple(u ** 3 * p0[k] + 3 * u * u * t * p1[k] + 3 * u * t * t * p2[k] + t ** 3 * p3[k]
                                         for k in (0, 1)))
            out.append(pts)
        return out


def parse_path(d: str) -> Shape:
    """The subset of SVG path data that Shape.path writes without arcs: M, L, H, V, C and Z."""
    shape, contour, cur = Shape(), None, (0.0, 0.0)
    for cmd, args in re.findall(r"([MLHVCZ])([^MLHVCZ]*)", d):
        v = [float(t) for t in re.findall(r"-?\d*\.?\d+", args)]
        if cmd == "M":
            cur = (v[0], v[1])
            contour = Contour(cur)
            shape.contours.append(contour)
        elif cmd == "L":
            cur = (v[0], v[1])
            contour.line(*cur)
        elif cmd == "H":
            cur = (v[0], cur[1])
            contour.line(*cur)
        elif cmd == "V":
            cur = (cur[0], v[0])
            contour.line(*cur)
        elif cmd == "C":
            contour.cubic((v[0], v[1]), (v[2], v[3]), (v[4], v[5]))
            cur = (v[4], v[5])
    return shape


def rectangle(x, y, w, h) -> Shape:
    return Shape([Contour((x, y)).line(x + w, y).line(x + w, y + h).line(x, y + h)])


def rounded_rectangle(x, y, w, h, r) -> Shape:
    c = Contour((x + r, y))
    c.line(x + w - r, y).arc(x + w - r, y + r, r, 270, 360)
    c.line(x + w, y + h - r).arc(x + w - r, y + h - r, r, 0, 90)
    c.line(x + r, y + h).arc(x + r, y + h - r, r, 90, 180)
    c.line(x, y + r).arc(x + r, y + r, r, 180, 270)
    return Shape([c])


# ---- Rasterizer: exact area coverage, by signed-area accumulation ---------------------------------
def _clip(points, w, h):
    """Sutherland-Hodgman clip of a polygon to the canvas."""
    def cut(pts, inside, cross):
        out, prev = [], pts[-1] if pts else None
        for cur in pts:
            if inside(cur):
                if not inside(prev):
                    out.append(cross(prev, cur))
                out.append(cur)
            elif inside(prev):
                out.append(cross(prev, cur))
            prev = cur
        return out

    def at_x(x):
        return lambda a, b: (x, a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0]))

    def at_y(y):
        return lambda a, b: (a[0] + (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]), y)

    points = cut(points, lambda q: q[0] >= 0, at_x(0.0))
    points = cut(points, lambda q: q[0] <= w, at_x(float(w)))
    points = cut(points, lambda q: q[1] >= 0, at_y(0.0))
    return cut(points, lambda q: q[1] <= h, at_y(float(h)))


def coverage(shape: Shape, w: int, h: int) -> np.ndarray:
    """The fraction of each pixel the shape covers, 0 to 1 (non-zero winding).

    Every edge adds its signed area to the pixels it crosses and its full height to the pixel after
    them; a running sum along each row then gives the coverage. An edge on a pixel boundary therefore
    gives exactly 0 or 1, which is what keeps the pixel-fitted sizes sharp.
    """
    acc = np.zeros((h, w + 2))
    for polygon in shape.polygons():
        polygon = _clip(polygon, w, h)
        for i, (x0, y0) in enumerate(polygon):
            x1, y1 = polygon[(i + 1) % len(polygon)]
            if y0 == y1:
                continue
            sign = 1.0
            if y0 > y1:
                x0, y0, x1, y1, sign = x1, y1, x0, y0, -1.0
            slope = (x1 - x0) / (y1 - y0)
            x = x0
            for y in range(max(0, math.floor(y0)), min(h, math.ceil(y1))):
                dy = min(y + 1, y1) - max(y, y0)
                if dy <= 0:
                    continue
                x_next = x + slope * dy
                d = dy * sign
                xa, xb = (x, x_next) if x < x_next else (x_next, x)
                ia, ib = math.floor(xa), math.ceil(xb)
                row = acc[y]
                if ib <= ia + 1:                         # the edge stays inside one pixel column
                    mid = 0.5 * (x + x_next) - ia
                    row[ia] += d * (1 - mid)
                    row[ia + 1] += d * mid
                else:                                    # it crosses several: a trapezoid per column
                    s = 1.0 / (xb - xa)
                    fa = xa - ia
                    first = 0.5 * s * (1 - fa) ** 2
                    fb = xb - ib + 1.0
                    last = 0.5 * s * fb * fb
                    row[ia] += d * first
                    if ib == ia + 2:
                        row[ia + 1] += d * (1.0 - first - last)
                    else:
                        second = s * (1.5 - fa)
                        row[ia + 1] += d * (second - first)
                        row[ia + 2:ib - 1] += d * s
                        row[ib - 1] += d * (1.0 - second - (ib - ia - 3) * s - last)
                    row[ib] += d * last
                x = x_next
    return np.clip(np.abs(np.cumsum(acc, axis=1))[:, :w], 0.0, 1.0)


def rgb(color: str) -> np.ndarray:
    return np.array([int(color[i:i + 2], 16) for i in (1, 3, 5)], dtype=np.float64)


class Canvas:
    """An RGBA image in floats. Shapes are composited source-over in sRGB, as browsers composite SVG."""

    def __init__(self, w: int, h: int, background: str | None = None):
        self.w, self.h = w, h
        self.color = np.zeros((h, w, 3))
        self.alpha = np.zeros((h, w))
        if background:
            self.color[:] = rgb(background)
            self.alpha[:] = 1.0

    def fill(self, shape: Shape, color: str) -> Canvas:
        cover = coverage(shape, self.w, self.h)
        under = self.alpha * (1 - cover)
        alpha = cover + under
        mixed = rgb(color) * cover[..., None] + self.color * under[..., None]
        self.color = np.divide(mixed, alpha[..., None], out=np.zeros_like(mixed), where=alpha[..., None] > 0)
        self.alpha = alpha
        return self

    def image(self) -> Image.Image:
        alpha = np.rint(self.alpha * 255).astype(np.uint8)
        color = np.rint(self.color).clip(0, 255).astype(np.uint8)
        color[alpha == 0] = 0
        return Image.fromarray(np.dstack([color, alpha]), "RGBA")


# ---- The mark ------------------------------------------------------------------------------------
@dataclass(frozen=True)
class Spec:
    """One drawing of the mark, in the units of a square canvas `size` wide."""

    size: float
    stroke: float                    # the braces' stroke
    brace: tuple[float, float]       # x of the left brace's beak tip and of its hook ends
    beak_radius: float               # center-line radius of the two arcs into the beak
    hook_radius: float               # center-line radius of the two hooks
    brace_y: tuple[float, float]     # top and bottom of the braces
    stem: float                      # the stems of the i and the d
    bowl: float                      # the stroke of the d's bowl
    i_x: float                       # left of the i
    d_x: tuple[float, float]         # left and right of the d
    letter_y: tuple[float, float, float]   # top of the d, top of the i and of the bowl, and the baseline
    dot: tuple[float, float]         # top of the i's dot, and its diameter
    taper: float = 0.0               # how much thinner than the stroke the beak's tip is
    beak_y: float | None = None      # the beak's center, when it isn't the middle of the brace
    square_dot: bool = False         # the dot as a square of whole pixels
    stepped: int = 0                 # the bowl as bars of whole pixels, its two corners cut by this many


# The master. The braces are the NCode Group mark's: a stroke of 4, 48 high. The letters have the same
# stroke: the d 16 wide and 32 high, its bowl a ring 16 across, and the i as high as the bowl under a dot
# of 5. 4 units clear left and right, 8 above and below.
MASTER = Spec(size=64, stroke=4, brace=(4, 16), beak_radius=6, hook_radius=5, brace_y=(8, 56), taper=2,
              stem=4, bowl=4, i_x=20, d_x=(28, 44), letter_y=(16, 32, 48), dot=(21.5, 5))

# The same drawing with every stem on whole pixels, for the sizes where half a pixel shows.
FITTED = {
    # 16: one-pixel strokes, the beak half a pixel above the middle, on a row, and the bowl three bars around one hole.
    16: Spec(size=16, stroke=1, brace=(1, 4), beak_radius=1.5, hook_radius=1, brace_y=(2, 14), beak_y=7.5,
             stem=1, bowl=1, i_x=5, d_x=(7, 11), letter_y=(4, 8, 12), dot=(6, 1), square_dot=True, stepped=1),
    # 20: two-pixel stems, with a one-pixel bowl, which keeps its hole two pixels wide.
    20: Spec(size=20, stroke=2, brace=(1, 5), beak_radius=2, hook_radius=2, brace_y=(2, 18),
             stem=2, bowl=1, i_x=6, d_x=(9, 14), letter_y=(5, 9, 15), dot=(6, 2), square_dot=True, stepped=1),
    24: Spec(size=24, stroke=2, brace=(1, 5), beak_radius=2, hook_radius=2, brace_y=(3, 21),
             stem=2, bowl=2, i_x=7, d_x=(11, 17), letter_y=(6, 12, 18), dot=(8, 2), square_dot=True, stepped=1),
    # 32: the dot a pixel lower than the master's, so that every letter's edge is on an even pixel and the
    # drawing stays sharp at half its size, where a browser shows favicon.svg on a 100% display.
    32: Spec(size=32, stroke=2, brace=(2, 8), beak_radius=3, hook_radius=2.5, brace_y=(4, 28),
             stem=2, bowl=2, i_x=10, d_x=(14, 22), letter_y=(8, 16, 24), dot=(12, 2), square_dot=True),
    40: Spec(size=40, stroke=3, brace=(2, 9), beak_radius=3.5, hook_radius=3.5, brace_y=(5, 35), taper=1,
             stem=3, bowl=3, i_x=12, d_x=(18, 28), letter_y=(10, 20, 30), dot=(13, 4)),
    48: Spec(size=48, stroke=3, brace=(3, 12), beak_radius=4.5, hook_radius=3.5, brace_y=(6, 42), taper=1,
             stem=3, bowl=3, i_x=15, d_x=(21, 33), letter_y=(12, 24, 36), dot=(16, 4)),
}


def left_brace(s: Spec) -> Shape:
    """The outline of a constant-width stroke: hook, stem, beak, stem, hook.

    The hooks and the arcs into the beak are quarter circles. The two arcs into the beak cross on the
    inner side, which leaves the notch a curly brace has there; `taper` moves their centers together,
    so the beak narrows to a tip `stroke - taper` high.
    """
    half = s.stroke / 2
    top, bottom = s.brace_y
    tip_x, hook_x = s.brace
    stem_x = tip_x + s.beak_radius                     # the stem's center line
    beak_y = (top + bottom) / 2 if s.beak_y is None else s.beak_y
    reach = s.beak_radius - s.taper / 2                # from an arc's center to the beak's center line
    hook_top = (stem_x + s.hook_radius, top + half + s.hook_radius)
    hook_bottom = (stem_x + s.hook_radius, bottom - half - s.hook_radius)
    beak_upper = (tip_x, beak_y - reach)
    beak_lower = (tip_x, beak_y + reach)
    hook_out, hook_in = s.hook_radius + half, s.hook_radius - half
    beak_out, beak_in = s.beak_radius + half, s.beak_radius - half
    notch = math.degrees(math.asin(reach / beak_out))  # where the outer beak arcs cross, seen from a center
    tip_half = (s.stroke - s.taper) / 2

    c = Contour((hook_x, top))
    c.line(hook_top[0], top).arc(*hook_top, hook_out, 270, 180)             # over the top hook
    c.line(stem_x - half, beak_upper[1]).arc(*beak_upper, beak_in, 0, 90)   # down the stem, into the beak
    c.line(tip_x, beak_y + tip_half).arc(*beak_lower, beak_in, 270, 360)    # the tip, and out of the beak
    c.line(stem_x - half, hook_bottom[1]).arc(*hook_bottom, hook_out, 180, 90)
    c.line(hook_x, bottom).line(hook_x, bottom - s.stroke)                  # the bottom hook's end
    c.line(hook_bottom[0], bottom - s.stroke).arc(*hook_bottom, hook_in, 90, 180)
    c.line(stem_x + half, beak_lower[1]).arc(*beak_lower, beak_out, 0, -notch)   # up into the notch
    c.arc(*beak_upper, beak_out, notch, 0)                                       # and out of it
    c.line(stem_x + half, hook_top[1]).arc(*hook_top, hook_in, 180, 270)
    c.line(hook_x, top + s.stroke)
    return Shape([c.reversed()])                       # clockwise, as every outline here is


def disc(cx, cy, r) -> Shape:
    return Shape([Contour((cx + r, cy)).arc(cx, cy, r, 0, 180).arc(cx, cy, r, 180, 360)])


def ring(cx, cy, outer, inner) -> Shape:
    """A disc, and a circle wound the other way, which cuts its hole."""
    return disc(cx, cy, outer) + Shape([disc(cx, cy, inner).contours[0].reversed()])


def letters(s: Spec) -> Shape:
    """The i and the d: a stem under a dot, and a bowl that a taller stem closes on the right."""
    top, middle, bottom = s.letter_y
    left, right = s.d_x
    dot_top, dot = s.dot
    dot_x = s.i_x + (s.stem - dot) / 2                 # the dot is centered on its stem
    shape = rectangle(s.i_x, middle, s.stem, bottom - middle)
    shape += rectangle(dot_x, dot_top, dot, dot) if s.square_dot else disc(dot_x + dot / 2, dot_top + dot / 2, dot / 2)
    shape += rectangle(right - s.stem, top, s.stem, bottom - top)
    if s.stepped:                                      # a top bar, a left bar and a bottom bar
        cut, width = s.stepped, right - s.stem - left
        shape += rectangle(left + cut, middle, width - cut, s.bowl)
        shape += rectangle(left, middle + cut, s.bowl, bottom - middle - 2 * cut)
        shape += rectangle(left + cut, bottom - s.bowl, width - cut, s.bowl)
    else:                                              # a ring that stands on the baseline, behind the stem
        radius = (right - left) / 2
        shape += ring(right - radius, bottom - radius, radius, radius - s.bowl)
    return shape


def mark(s: Spec = MASTER) -> Shape:
    brace = left_brace(s)
    return brace + letters(s) + brace.mirrored(s.size / 2)


def mark_for(size: int, fit: float = TIGHT) -> Shape:
    """The mark in the pixels of a size-by-size image: fitted where there is a fitted drawing."""
    if fit == TIGHT and size in FITTED:
        return mark(FITTED[size])
    return mark().placed(size / GRID * fit, size * (1 - fit) / 2, size * (1 - fit) / 2)


# ---- The text --------------------------------------------------------------------------------------
# outlines.json holds each line as outlines, set in Inter Display by brand/outlines.py: capitals 32 units
# tall, as tall as the mark's d, the first glyph's origin at x = 0 and the baseline at y = 0.
def extent(shape: Shape) -> tuple[float, float, float, float]:
    """The left, top, right and bottom of the shape's ink."""
    xs, ys = zip(*(p for polygon in shape.polygons() for p in polygon))
    return min(xs), min(ys), max(xs), max(ys)


def read_lines() -> dict[str, Shape]:
    source = Path(__file__).resolve().parent / "outlines.json"
    if not source.is_file():
        raise SystemExit(f"{source} is missing: brand/outlines.py writes it.")
    lines = json.loads(source.read_text(encoding="utf-8"))["lines"]
    return {name: parse_path(line["path"]) for name, line in lines.items()}


LINES = read_lines()
WORDMARK = LINES["wordmark"]        # "NCode Identity", Inter Display Medium
DESCRIPTOR = LINES["descriptor"]    # the product in one line, Inter Display Regular
WORD_LEFT, _, WORD_RIGHT, WORD_BOTTOM = extent(WORDMARK)


@dataclass(frozen=True)
class Lockup:
    """The mark and the wordmark in one drawing."""

    width: float
    height: float
    symbol: tuple[float, float, float]   # the mark's scale, and where its canvas's corner is
    word: tuple[float, float]            # the wordmark's origin: where its baseline starts

    def shape(self, scale=1.0, dx=0.0, dy=0.0) -> Shape:
        return (mark().placed(*self.symbol) + WORDMARK.placed(1, *self.word)).placed(scale, dx, dy)


# Side by side: the mark's 64, 8 clear, the wordmark on the letters' baseline, and 4 clear as on the left.
HORIZONTAL = Lockup(width=float(round(72 + WORD_RIGHT + 4)), height=GRID, symbol=(1, 0, 0), word=(72, 48))

# Stacked: the mark at twice its size over the wordmark, both centered, for a space nearer a square.
STACKED_WIDTH = 4.0 * math.ceil((WORD_RIGHT - WORD_LEFT + 24) / 4)   # at least 12 clear at each side
STACKED = Lockup(width=STACKED_WIDTH, height=192.0, symbol=(2, STACKED_WIDTH / 2 - GRID, 0),
                 word=((STACKED_WIDTH - WORD_LEFT - WORD_RIGHT) / 2, 168))

# The descriptor under the side-by-side lockup, on a link preview: capitals 14 units tall, centered.
DESCRIPTOR_SCALE = 14 / 32
DESCRIPTOR_BASELINE = 86.0
DESCRIPTOR_LINE = DESCRIPTOR.placed(
    DESCRIPTOR_SCALE, (HORIZONTAL.width - sum(extent(DESCRIPTOR)[0::2]) * DESCRIPTOR_SCALE) / 2, DESCRIPTOR_BASELINE)


# ---- Drawings ------------------------------------------------------------------------------------
def draw_mark(size: int, ink: str, fit: float = TIGHT) -> Image.Image:
    return Canvas(size, size).fill(mark_for(size, fit), ink).image()


def draw_tile(size: int, tile: str, corners: str, fit: float) -> Image.Image:
    """corners: 'rounded' leaves the corners transparent; 'square' fills the whole image."""
    fill, ink = TILES[tile]
    if corners == "square":
        canvas = Canvas(size, size, fill)
    else:
        canvas = Canvas(size, size).fill(rounded_rectangle(0, 0, size, size, RADIUS * size), fill)
    return canvas.fill(mark_for(size, fit), ink).image()


def draw_lockup(lockup: Lockup, height: int, ink: str) -> Image.Image:
    scale = height / lockup.height
    return Canvas(round(lockup.width * scale), height).fill(lockup.shape(scale), ink).image()


def draw_card(w: int, h: int, card: str, widest: float = 0.5, descriptor: bool = False) -> Image.Image:
    """The side-by-side lockup centered on a solid card, at most `widest` of the card wide, in steps
    that keep its strokes on whole pixels. With the descriptor, the two lines are centered together."""
    fill, ink = CARDS[card]
    scale = math.floor(min(widest * w / HORIZONTAL.width, 0.4 * h / GRID) * 4) / 4
    middle = (MASTER.brace_y[0] + DESCRIPTOR_BASELINE) / 2 if descriptor else GRID / 2
    dx, dy = round((w - HORIZONTAL.width * scale) / 2), round(h / 2 - middle * scale)
    shape = HORIZONTAL.shape(scale, dx, dy)
    if descriptor:
        shape += DESCRIPTOR_LINE.placed(scale, dx, dy)
    return Canvas(w, h, fill).fill(shape, ink).image()


# ---- Writers -------------------------------------------------------------------------------------
def svg(w: float, h: float, body: str, title: str = NAME) -> str:
    size = f'width="{_num(w, 3)}" height="{_num(h, 3)}" viewBox="0 0 {_num(w, 3)} {_num(h, 3)}"'
    return (f'<svg xmlns="http://www.w3.org/2000/svg" {size} role="img" aria-label="{title}">'
            f"<title>{title}</title>{body}</svg>\n")


def auto_style(selector: str = "path") -> str:
    """Blue, or light blue when the viewer's system or browser asks for a dark scheme."""
    return (f"<style>{selector}{{fill:{BLUE}}}@media (prefers-color-scheme:dark)"
            f"{{{selector}{{fill:{LIGHT}}}}}</style>")


def png_bytes(image: Image.Image) -> bytes:
    """The smallest PNG Pillow writes without changing a pixel: a palette when 256 colors are enough."""
    pixels = np.asarray(image.convert("RGBA"))
    colors, index = np.unique(pixels.reshape(-1, 4), axis=0, return_inverse=True)
    out = io.BytesIO()
    if len(colors) <= 256:
        order = np.lexsort((colors[:, 2], colors[:, 1], colors[:, 0], colors[:, 3]))   # least opaque first
        rank = np.empty(len(order), dtype=np.int64)
        rank[order] = np.arange(len(order))
        colors = colors[order]
        paletted = Image.fromarray(rank[index.reshape(-1)].reshape(pixels.shape[:2]).astype(np.uint8), "P")
        paletted.putpalette(colors[:, :3].tobytes())
        translucent = int((colors[:, 3] < 255).sum())
        extra = {"transparency": colors[:translucent, 3].tobytes()} if translucent else {}
        paletted.save(out, "PNG", optimize=True, **extra)
    elif (pixels[..., 3] == 255).all():
        image.convert("RGB").save(out, "PNG", optimize=True)
    else:
        image.save(out, "PNG", optimize=True)
    data = out.getvalue()
    if not np.array_equal(np.asarray(Image.open(io.BytesIO(data)).convert("RGBA")), pixels):
        raise RuntimeError("the PNG that was written doesn't decode to the image that was drawn")
    return data


def ico_bytes(images: list[Image.Image], bitmaps_below: int = 0) -> bytes:
    """An ICO holding the images. Entries narrower than `bitmaps_below` are stored as 32-bit bitmaps,
    which every Windows API reads; the others as PNG, which browsers and Windows Vista and later read."""
    entries = []
    for image in images:
        w, h = image.size
        if w < bitmaps_below:
            pixels = np.asarray(image)
            rows = pixels[::-1]                                             # bottom row first
            row_bytes = (w + 31) // 32 * 4                                  # the 1-bit mask, padded to 32 bits
            mask = b"".join(np.packbits(r[:, 3] == 0).tobytes().ljust(row_bytes, b"\0") for r in rows)
            color = rows[:, :, [2, 1, 0, 3]].tobytes()                     # BGRA
            data = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(color) + len(mask), 0, 0, 0, 0)
            data += color + mask
        else:
            out = io.BytesIO()
            image.save(out, "PNG", optimize=True)                           # RGBA: Windows wants 32 bits here
            data = out.getvalue()
        entries.append((w, h, data))
    header = struct.pack("<HHH", 0, 1, len(entries))
    offset = len(header) + 16 * len(entries)
    directory = b""
    for w, h, data in entries:
        directory += struct.pack("<BBBBHHII", w % 256, h % 256, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    icon = header + directory + b"".join(data for _, _, data in entries)
    for image in images:                                                    # read each entry back, as png_bytes does
        entry = Image.open(io.BytesIO(icon))
        entry.size = image.size
        if not np.array_equal(np.asarray(entry.convert("RGBA")), np.asarray(image)):
            raise RuntimeError(f"the icon's {image.width}-pixel entry doesn't decode to the image that was drawn")
    return icon


class Kit:
    def __init__(self, root: Path):
        self.root = root
        self.files: dict[str, bytes] = {}

    def text(self, name: str, content: str):
        self.files[name] = content.encode("utf-8")

    def png(self, name: str, image: Image.Image, limit: int | None = None, opaque: bool = False):
        data = png_bytes(image)
        if limit is not None and len(data) > limit:
            raise RuntimeError(f"{name} is {len(data)} bytes, over the {limit} its platform allows")
        if opaque and np.asarray(image)[..., 3].min() < 255:
            raise RuntimeError(f"{name} must have no transparency")
        self.files[name] = data

    def write(self):
        for name, data in self.files.items():
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)


def build(kit: Kit):
    master = mark().path()
    regular = mark().placed(REGULAR, GRID * (1 - REGULAR) / 2, GRID * (1 - REGULAR) / 2).path()

    # mark/: the mark alone on a transparent background.
    for name, ink in INKS.items():
        kit.text(f"mark/{PREFIX}-mark-{name}.svg", svg(64, 64, f'<path fill="{ink}" d="{master}"/>'))
        for size in (256, 1024):
            kit.png(f"mark/{PREFIX}-mark-{name}-{size}.png", draw_mark(size, ink))
    kit.text(f"mark/{PREFIX}-mark-auto.svg", svg(64, 64, f'{auto_style()}<path d="{master}"/>'))

    # tile/: the mark on a solid tile, the app-icon form. "square" has no rounded corners, for a
    # platform that cuts its own.
    for name, (fill, ink) in TILES.items():
        glyph = f'<path fill="{ink}" d="{regular}"/>'
        rounded = f'<rect width="64" height="64" rx="14" fill="{fill}"/>'
        square = f'<path fill="{fill}" d="M0 0H64V64H0Z"/>'
        kit.text(f"tile/{PREFIX}-tile-{name}.svg", svg(64, 64, rounded + glyph))
        kit.text(f"tile/{PREFIX}-tile-{name}-square.svg", svg(64, 64, square + glyph))
        for size in (256, 1024):
            kit.png(f"tile/{PREFIX}-tile-{name}-{size}.png", draw_tile(size, name, "rounded", REGULAR))
        kit.png(f"tile/{PREFIX}-tile-{name}-square-1024.png", draw_tile(1024, name, "square", REGULAR), opaque=True)

    # lockup/: the mark and the wordmark, side by side and stacked.
    for lockup, stem, heights in ((HORIZONTAL, f"lockup/{PREFIX}-lockup", (128, 512)),
                                  (STACKED, f"lockup/{PREFIX}-lockup-stacked", (384, 1536))):
        drawing = lockup.shape().path(2)
        for name, ink in INKS.items():
            kit.text(f"{stem}-{name}.svg", svg(lockup.width, lockup.height, f'<path fill="{ink}" d="{drawing}"/>'))
            for height in heights:
                kit.png(f"{stem}-{name}-h{height}.png", draw_lockup(lockup, height, ink))
        kit.text(f"{stem}-auto.svg", svg(lockup.width, lockup.height, f'{auto_style()}<path d="{drawing}"/>'))

    # favicon/: the blue tile. The ICO holds a pixel-fitted drawing per size; the SVG is the 32-pixel
    # drawing, which is sharp at 32 and at its multiples.
    small = mark(FITTED[32]).path()
    icon_sizes = (16, 20, 24, 32, 40, 48, 64)
    kit.files["favicon/favicon.ico"] = ico_bytes([draw_tile(s, "blue", "rounded", TIGHT) for s in icon_sizes])
    kit.text("favicon/favicon.svg", svg(32, 32, f'<rect width="32" height="32" rx="7" fill="{BLUE}"/>'
                                                 f'<path fill="{WHITE}" d="{small}"/>'))
    kit.png("favicon/apple-touch-icon.png", draw_tile(180, "blue", "square", REGULAR), opaque=True)
    kit.png("favicon/icon-192.png", draw_tile(192, "blue", "rounded", REGULAR))
    kit.png("favicon/icon-512.png", draw_tile(512, "blue", "rounded", REGULAR))
    kit.png("favicon/icon-maskable-512.png", draw_tile(512, "blue", "square", SAFE), opaque=True)
    kit.text("favicon/site.webmanifest", json.dumps({
        "name": NAME, "short_name": "Identity",
        "icons": [{"src": "icon-192.png", "sizes": "192x192", "type": "image/png"},
                  {"src": "icon-512.png", "sizes": "512x512", "type": "image/png"},
                  {"src": "icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable"}],
        "theme_color": BLUE, "background_color": WHITE, "display": "standalone"}, indent=2) + "\n")
    # The same without a tile: blue for a light tab strip, light blue for a dark one.
    kit.files["favicon/transparent/favicon.ico"] = ico_bytes([draw_mark(s, BLUE) for s in icon_sizes])
    kit.files["favicon/transparent/favicon-dark.ico"] = ico_bytes([draw_mark(s, LIGHT) for s in icon_sizes])
    kit.text("favicon/transparent/favicon.svg", svg(32, 32, f'{auto_style()}<path d="{small}"/>'))

    # windows/: an application icon. Bitmaps up to 64 pixels, so System.Drawing and the resource
    # compiler read it, and a PNG at 256.
    kit.files[f"windows/{PREFIX}.ico"] = ico_bytes(
        [draw_tile(s, "blue", "rounded", TIGHT) for s in icon_sizes + (256,)], bitmaps_below=256)

    # nuget/: 128 by 128, as NuGet recommends: twice the master, so every stem is on whole pixels, and
    # still is when a client shows the icon at half or a quarter of that.
    for name in TILES:
        kit.png(f"nuget/{PREFIX}-nuget-tile-{name}.png", draw_tile(128, name, "rounded", TIGHT), limit=NUGET_LIMIT)
    for name in ("blue", "light"):
        kit.png(f"nuget/{PREFIX}-nuget-mark-{name}.png", draw_mark(128, INKS[name]), limit=NUGET_LIMIT)

    # profile/: a square that a circle can be cut from (GitHub and GitLab users, Gravatar, X, LinkedIn).
    for name in TILES:
        kit.png(f"profile/{PREFIX}-profile-tile-{name}.png", draw_tile(1024, name, "square", SAFE), opaque=True)
    for name in ("blue", "light"):
        kit.png(f"profile/{PREFIX}-profile-mark-{name}.png", draw_mark(1024, INKS[name], SAFE))

    # github/: an organization's or an app's picture (about 500 pixels, under 1 MB) and a repository's
    # social preview (1280 by 640, under 1 MB), which carries the descriptor.
    for name in TILES:
        kit.png(f"github/{PREFIX}-github-avatar-tile-{name}.png", draw_tile(500, name, "square", REGULAR),
                limit=GITHUB_LIMIT, opaque=True)
    for name in ("blue", "light"):
        kit.png(f"github/{PREFIX}-github-avatar-mark-{name}.png", draw_mark(500, INKS[name], REGULAR),
                limit=GITHUB_LIMIT)
    for name in CARDS:
        kit.png(f"github/{PREFIX}-github-social-{name}.png", draw_card(1280, 640, name, 0.625, descriptor=True),
                limit=GITHUB_LIMIT, opaque=True)

    # gitlab/: a group's or project's avatar (192 pixels, 200 KiB at most). A logo.png at a
    # repository's root is the project's avatar until one is uploaded.
    for name in TILES:
        kit.png(f"gitlab/{PREFIX}-gitlab-avatar-tile-{name}.png", draw_tile(192, name, "square", REGULAR),
                limit=GITLAB_LIMIT, opaque=True)
    for name in ("blue", "light"):
        kit.png(f"gitlab/{PREFIX}-gitlab-avatar-mark-{name}.png", draw_mark(192, INKS[name], REGULAR),
                limit=GITLAB_LIMIT)
    kit.png("gitlab/logo.png", draw_tile(192, "blue", "square", REGULAR), limit=GITLAB_LIMIT, opaque=True)

    # myapps/: Microsoft Entra's My Apps takes exactly 215 by 215 pixels, PNG, 100 KB at most, and
    # asks for a solid background.
    for name in TILES:
        kit.png(f"myapps/{PREFIX}-myapps-tile-{name}.png", draw_tile(215, name, "square", REGULAR),
                limit=MYAPPS_LIMIT, opaque=True)

    # badge/: the mark cropped to its edges, as small a file as it gets, for a badge's logo. The .txt
    # files hold it as the value of Shields.io's logo parameter.
    cropped = mark().placed(1, -4, -8).path(2)
    for name in ("white", "blue", "light"):
        badge = ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 56 48">'
                 f'<path fill="{INKS[name]}" d="{cropped}"/></svg>')
        kit.text(f"badge/{PREFIX}-badge-{name}.svg", badge + "\n")
        uri = "data:image/svg+xml;base64," + base64.b64encode(badge.encode()).decode()
        kit.text(f"badge/shields-logo-{name}.txt", urllib.parse.quote(uri, safe="") + "\n")

    # social/: link previews (1200 by 630), which carry the descriptor, and profile banners (1500 by 500).
    for name in CARDS:
        kit.png(f"social/{PREFIX}-og-{name}.png", draw_card(1200, 630, name, 0.625, descriptor=True), opaque=True)
        kit.png(f"social/{PREFIX}-banner-{name}.png", draw_card(1500, 500, name), opaque=True)


def main() -> int:
    here = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=f"Build the {NAME} logo kit.")
    parser.add_argument("--output", type=Path, default=here,
                        help="the folder to write the kit into (default: this one)")
    args = parser.parse_args()
    kit = Kit(args.output)
    build(kit)
    kit.write()
    for name, data in kit.files.items():
        print(f"{name:<64}{len(data):>8}")
    print(f"{len(kit.files)} files, {sum(len(d) for d in kit.files.values())} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
