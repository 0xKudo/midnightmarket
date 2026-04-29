#!/usr/bin/env python3
"""
bake_country_id.py
Rasterizes countries.json (equirectangular) to:
  - CountryID.png    4096x2048, R=idx%256, G=idx//256 (nearest-neighbor, no AA)
  - BorderMap.png    4096x2048 grayscale, white pixels at country edges
  - iso_to_index.json  {iso: index} lookup for GlobeRenderer

Usage:
  python tools/bake_country_id.py \
      --countries ArmsFair/Assets/StreamingAssets/GeoData/countries.json \
      --out-textures ArmsFair/Assets/Textures \
      --out-index ArmsFair/Assets/StreamingAssets/GeoData/iso_to_index.json
"""
import argparse, json, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

try:
    from shapely.geometry import shape
except ImportError:
    print("ERROR: pip install shapely", file=sys.stderr); sys.exit(1)

W, H = 4096, 2048

def lng_lat_to_px(lng, lat):
    x = (lng + 180.0) / 360.0 * W
    y = (90.0 - lat) / 180.0 * H
    return x, y

def ring_to_pixels(ring_coords):
    return [lng_lat_to_px(c[0], c[1]) for c in ring_coords]

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--countries", required=True)
    p.add_argument("--out-textures", required=True)
    p.add_argument("--out-index", required=True)
    args = p.parse_args()

    countries = json.loads(Path(args.countries).read_text(encoding="utf-8"))
    out_tex = Path(args.out_textures)
    out_tex.mkdir(parents=True, exist_ok=True)

    iso_to_index = {}
    id_img = Image.new("RGB", (W, H), (0, 0, 0))
    id_draw = ImageDraw.Draw(id_img)

    for idx, country in enumerate(countries):
        iso = country["iso"]
        iso_to_index[iso] = idx
        # idx=0 would be (0,0,0) — same as ocean black, so offset by 1
        enc = idx + 1
        r = enc % 256
        g = enc // 256
        color = (r, g, 0)

        geom = country["geometry"]
        gtype = geom["type"]
        coords = geom["coordinates"]

        if gtype == "Polygon":
            rings = [coords[0]]
        elif gtype == "MultiPolygon":
            rings = [poly[0] for poly in coords]
        else:
            continue

        for ring in rings:
            pts = ring_to_pixels(ring)
            if len(pts) >= 3:
                id_draw.polygon(pts, fill=color)

        if (idx + 1) % 50 == 0 or (idx + 1) == len(countries):
            print(f"  Rasterized {idx+1}/{len(countries)}...")

    print("Generating border map...")
    border_img = Image.new("L", (W, H), 0)
    border_pixels = border_img.load()
    id_pixels = id_img.load()
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            center = id_pixels[x, y]
            is_border = any(
                id_pixels[x + dx, y + dy] != center
                for dx in (-1, 0, 1) for dy in (-1, 0, 1)
                if (dx, dy) != (0, 0)
            )
            if is_border:
                border_pixels[x, y] = 255

    border_img = border_img.filter(ImageFilter.GaussianBlur(radius=1.5))

    id_path = out_tex / "CountryID.png"
    border_path = out_tex / "BorderMap.png"
    id_img.save(id_path)
    border_img.save(border_path)
    Path(args.out_index).write_text(
        json.dumps(iso_to_index, separators=(",", ":")), encoding="utf-8")

    print(f"Wrote {id_path} ({id_path.stat().st_size // 1024} KB)")
    print(f"Wrote {border_path} ({border_path.stat().st_size // 1024} KB)")
    print(f"Wrote {args.out_index}")
    print(f"Done. {len(countries)} countries indexed.")

if __name__ == "__main__":
    main()
