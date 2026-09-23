"""Measure visible reward bounds for gallery layout, leaving source textures unchanged.

Run with Python 3 and Pillow after changing the reward catalog or reward artwork.
The resulting metadata is used at runtime without readable textures or pixel scans.
"""

from pathlib import Path
import re

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Fortune/Art"
CATALOG = ROOT / "Assets/Fortune/Content/RewardCatalog.asset"
OUTPUT = ROOT / "Assets/Fortune/Presentation/RewardArtBounds.cs"
ALPHA_THRESHOLD = 12


def main():
    names = list(dict.fromkeys([
        *re.findall(r"^\s+AssetName: (.+)$", CATALOG.read_text(), re.MULTILINE),
        "ui_card_icon_death",
    ]))
    entries = []
    for name in names:
        source = next(path for path in ART.glob(name + ".*") if path.suffix.lower() in (".png", ".tga"))
        with Image.open(source) as image:
            alpha = image.convert("RGBA").getchannel("A")
            bounds = alpha.point(lambda value: 255 if value > ALPHA_THRESHOLD else 0).getbbox()
            if bounds is None:
                raise ValueError("Reward artwork is fully transparent: " + name)
            left, top, right, bottom = bounds
            values = (left / image.width, top / image.height,
                      (right - left) / image.width, (bottom - top) / image.height)
            rect = ", ".join(f"{value:.8f}f" for value in values)
            entries.append(f'            {{ "{name}", new Rect({rect}) }},')
    OUTPUT.write_text("""using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vertigo.Fortune.Presentation
{
    // Measured from original source artwork with alpha > 12. Coordinates are normalized, top-left based.
    // Regenerate with Tools/generate_reward_art_bounds.py after replacing reward artwork.
    internal static class RewardArtBounds
    {
        internal static readonly IReadOnlyDictionary<string, Rect> All = new Dictionary<string, Rect>(StringComparer.Ordinal)
        {
""" + "\n".join(entries) + "\n        };\n    }\n}\n")
    print(f"Measured {len(entries)} reward textures. Wrote {OUTPUT.relative_to(ROOT)}.")


if __name__ == "__main__":
    main()
