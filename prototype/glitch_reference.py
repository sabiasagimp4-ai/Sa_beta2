"""Numpy preview reference for the Sa_Glitch YMM4 effect (ymm/../GlitchChroma.hlsl).

This mirrors the shader's math (band hash, chromatic aberration, HSV
saturation/hue, scanlines, vignette) closely enough to preview what the
effect looks like, using nearest-neighbour sampling instead of the GPU's
bilinear D2DSampleInputAtPosition. It is NOT a pixel-exact oracle the way
Sa_aohue's check_native.py is for the AE plugin -- treat it as an
approximation for generating preview stills, not a golden reference.

Usage:
    python glitch_reference.py --selfcheck
    python glitch_reference.py input.png output_dir [--preset NAME ...]
"""
from __future__ import annotations

import argparse
import sys
from dataclasses import dataclass, replace

import numpy as np


@dataclass(frozen=True)
class Params:
    glitch_amount: float = 0.0          # 0..1
    glitch_band_size: float = 12.0      # px >= 1
    glitch_max_shift: float = 60.0      # px
    glitch_seed: float = 0.0
    chromatic_aberration: float = 0.0   # px
    scanline_amount: float = 0.0        # 0..1
    scanline_spacing: float = 3.0       # px >= 1
    vignette_amount: float = 0.0        # 0..1
    saturation: float = 1.0             # 0..3
    hue_rotate: float = 0.0             # degrees
    brightness: float = 1.0             # 0..2


PRESETS: dict[str, Params] = {
    "default": Params(glitch_amount=0.25, chromatic_aberration=6, scanline_amount=0.15, vignette_amount=0.20),
    "subtle_vhs": Params(glitch_amount=0.10, glitch_band_size=24, chromatic_aberration=4,
                          scanline_amount=0.25, scanline_spacing=2, vignette_amount=0.30, saturation=1.1),
    "datamosh": Params(glitch_amount=0.85, glitch_band_size=6, glitch_max_shift=140,
                        chromatic_aberration=14, scanline_amount=0.0, vignette_amount=0.10),
    "retro_crt": Params(glitch_amount=0.05, glitch_band_size=40, chromatic_aberration=3,
                         scanline_amount=0.45, scanline_spacing=3, vignette_amount=0.45, brightness=1.05),
    "psychedelic": Params(glitch_amount=0.35, glitch_band_size=18, chromatic_aberration=10,
                           saturation=1.8, hue_rotate=95, scanline_amount=0.1, vignette_amount=0.2),
    "chaos": Params(glitch_amount=1.0, glitch_band_size=5, glitch_max_shift=200, chromatic_aberration=40,
                     scanline_amount=0.5, scanline_spacing=2, vignette_amount=0.5, saturation=2.2,
                     hue_rotate=45, brightness=1.1),
}


def hash11(x: np.ndarray) -> np.ndarray:
    s = np.sin(x) * 43758.5453123
    return s - np.floor(s)


def band_shift(height: int, p: Params) -> np.ndarray:
    band_size = max(1.0, p.glitch_band_size)
    y = np.arange(height, dtype=np.float64)
    band = np.floor(y / band_size)
    active = hash11(band * 0.6180339887 + p.glitch_seed * 13.37) < (0.15 + 0.55 * p.glitch_amount)
    direction = hash11(band * 1.3702 + p.glitch_seed * 4.7 + 91.0) * 2.0 - 1.0
    return active.astype(np.float64) * direction * p.glitch_max_shift * p.glitch_amount  # (H,)


def gather_rows(image: np.ndarray, col_index: np.ndarray) -> np.ndarray:
    """image: (H,W,3), col_index: (H,W) nearest-neighbour column per pixel."""
    height, width, _ = image.shape
    idx = np.clip(np.round(col_index), 0, width - 1).astype(np.int64)
    return np.take_along_axis(image, idx[:, :, None], axis=1)


def rgb_to_hsv(rgb: np.ndarray) -> np.ndarray:
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    maxc = np.max(rgb, axis=-1)
    minc = np.min(rgb, axis=-1)
    v = maxc
    d = maxc - minc
    s = np.where(maxc > 0, d / np.where(maxc > 0, maxc, 1), 0.0)
    d_safe = np.where(d > 0, d, 1.0)
    rc = (maxc - r) / d_safe
    gc = (maxc - g) / d_safe
    bc = (maxc - b) / d_safe
    h = np.select(
        [maxc == minc, maxc == r, maxc == g],
        [np.zeros_like(maxc), (bc - gc), 2.0 + (rc - bc)],
        default=4.0 + (gc - rc),
    )
    h = (h / 6.0) % 1.0
    return np.stack([h, s, v], axis=-1)


def hsv_to_rgb(hsv: np.ndarray) -> np.ndarray:
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    i = np.floor(h * 6.0)
    f = h * 6.0 - i
    p = v * (1.0 - s)
    q = v * (1.0 - s * f)
    t = v * (1.0 - s * (1.0 - f))
    i = i.astype(np.int64) % 6
    conditions = [i == k for k in range(6)]
    r = np.select(conditions, [v, q, p, p, t, v])
    g = np.select(conditions, [t, v, v, q, p, p])
    b = np.select(conditions, [p, p, t, v, v, q])
    return np.stack([r, g, b], axis=-1)


def render(image: np.ndarray, p: Params) -> np.ndarray:
    """image: float64 (H,W,3) in [0,1] (straight, unpremultiplied RGB)."""
    height, width, _ = image.shape
    if (p.glitch_amount <= 0 and p.chromatic_aberration <= 0 and p.scanline_amount <= 0
            and p.vignette_amount <= 0 and p.saturation == 1 and p.hue_rotate == 0 and p.brightness == 1):
        return image.copy()

    shift = band_shift(height, p)  # (H,)
    xs = np.arange(width, dtype=np.float64)[None, :] + shift[:, None]  # (H,W)

    if p.chromatic_aberration > 0:
        r = gather_rows(image[..., 0:1], xs + p.chromatic_aberration)[..., 0]
        g = gather_rows(image[..., 1:2], xs)[..., 0]
        b = gather_rows(image[..., 2:3], xs - p.chromatic_aberration)[..., 0]
        rgb = np.stack([r, g, b], axis=-1)
    else:
        rgb = gather_rows(image, xs)

    if p.saturation != 1 or p.hue_rotate != 0:
        hsv = rgb_to_hsv(np.clip(rgb, 0, 1))
        hsv[..., 1] = np.clip(hsv[..., 1] * p.saturation, 0, 1)
        hsv[..., 0] = (hsv[..., 0] + p.hue_rotate / 360.0) % 1.0
        rgb = hsv_to_rgb(hsv)

    rgb = rgb * p.brightness

    if p.scanline_amount > 0:
        y = np.arange(height, dtype=np.float64)
        wave = 0.5 + 0.5 * np.cos(y * (2 * np.pi / max(1.0, p.scanline_spacing)))
        rgb = rgb * (1 + (wave[:, None, None] - 1) * p.scanline_amount)

    if p.vignette_amount > 0:
        cy, cx = height / 2.0, width / 2.0
        ey, ex = max(height / 2.0, 1.0), max(width / 2.0, 1.0)
        yy, xx = np.mgrid[0:height, 0:width]
        dist = np.sqrt(((xx - cx) / ex) ** 2 + ((yy - cy) / ey) ** 2)
        falloff = np.clip(1 - dist ** 2, 0, 1)
        rgb = rgb * (1 + (falloff[:, :, None] - 1) * p.vignette_amount)

    return np.clip(rgb, 0, 1)


def selfcheck() -> None:
    rng = np.random.default_rng(0)
    image = rng.random((17, 23, 3))

    identity = render(image, Params())
    assert np.array_equal(identity, image), "neutral params must be a byte-for-byte passthrough"

    values = np.linspace(-500, 500, 4000)
    h = hash11(values)
    assert np.all(h >= 0) and np.all(h < 1), "hash11 must stay in [0,1)"

    out = render(image, PRESETS["chaos"])
    assert out.shape == image.shape
    assert np.all(out >= 0) and np.all(out <= 1), "output must stay in [0,1]"

    out_seed_a = render(image, replace(PRESETS["datamosh"], glitch_seed=1))
    out_seed_b = render(image, replace(PRESETS["datamosh"], glitch_seed=2))
    assert not np.array_equal(out_seed_a, out_seed_b), "different seeds should usually change the glitch pattern"

    print("PASS: glitch_reference selfcheck")


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", nargs="?", help="input image path")
    parser.add_argument("output_dir", nargs="?", help="directory for rendered PNGs")
    parser.add_argument("--preset", action="append", help="preset name(s); default: all presets")
    parser.add_argument("--selfcheck", action="store_true", help="run internal consistency checks and exit")
    args = parser.parse_args(argv)

    if args.selfcheck:
        selfcheck()
        return 0

    if not args.input or not args.output_dir:
        parser.error("input and output_dir are required unless --selfcheck is given")

    from pathlib import Path
    from PIL import Image

    src = Image.open(args.input).convert("RGB")
    array = np.asarray(src, dtype=np.float64) / 255.0

    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    names = args.preset or list(PRESETS.keys())
    for name in names:
        params = PRESETS[name]
        result = render(array, params)
        out = Image.fromarray((result * 255.0 + 0.5).astype(np.uint8), mode="RGB")
        out_path = out_dir / f"sa_glitch_{name}.png"
        out.save(out_path)
        print(f"wrote {out_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
