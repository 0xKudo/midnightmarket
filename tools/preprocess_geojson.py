#!/usr/bin/env python3
"""
preprocess_geojson.py
Reduces Natural Earth 1:50m GeoJSON (~25 MB) to two compact files for Unity:
  - countries.json   — simplified polygons, one entry per country
  - adjacency.json   — border graph (which countries share a border)

Usage:
  python tools/preprocess_geojson.py \
      --input  ne_50m_admin_0_countries.geojson \
      --outdir ArmsFair.Unity/Assets/StreamingAssets/GeoData

Download source data:
  https://www.naturalearthdata.com/downloads/50m-cultural-vectors/
  File: ne_50m_admin_0_countries.zip
"""

import argparse
import json
import sys
from pathlib import Path

try:
    from shapely.geometry import shape, mapping
    from shapely.ops import unary_union
except ImportError:
    print("ERROR: shapely not installed. Run: pip install shapely", file=sys.stderr)
    sys.exit(1)


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Preprocess Natural Earth GeoJSON for Unity")
    p.add_argument("--input",  required=True, help="Path to ne_50m_admin_0_countries.geojson")
    p.add_argument("--outdir", required=True, help="Output directory for countries.json and adjacency.json")
    p.add_argument("--simplify", type=float, default=0.1,
                   help="Shapely simplify tolerance in degrees (default: 0.1)")
    p.add_argument("--buffer", type=float, default=0.05,
                   help="Buffer in degrees for near-border detection (default: 0.05)")
    return p.parse_args()


def load_geojson(path: str) -> dict:
    p = Path(path)
    if not p.exists():
        print(f"ERROR: input file not found: {path}", file=sys.stderr)
        sys.exit(1)
    print(f"Loading {p} ({p.stat().st_size // 1024} KB)...")
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def extract_iso(props: dict) -> str:
    return (props.get("ADM0_A3") or props.get("ISO_A3") or props.get("iso_a3")
            or props.get("ISO3166-1-Alpha-3") or "UNK")


def build_countries(features: list, simplify_tol: float) -> tuple[list, dict]:
    """
    Returns (countries_list, shapes_dict).
    countries_list — simplified geometries ready for Unity.
    shapes_dict    — buffered shapes for adjacency calculation.
    """
    countries = []
    shapes    = {}

    total = len(features)
    for i, feature in enumerate(features, 1):
        props = feature["properties"]
        iso   = extract_iso(props)
        name  = props.get("NAME") or props.get("name") or iso

        try:
            geom       = shape(feature["geometry"])
            simplified = geom.simplify(simplify_tol, preserve_topology=True)
        except Exception as e:
            print(f"  WARN: skipping {iso} ({name}) — geometry error: {e}")
            continue

        centroid = simplified.centroid
        countries.append({
            "iso":      iso,
            "name":     name,
            "centroid": [round(centroid.x, 4), round(centroid.y, 4)],
            "geometry": mapping(simplified),
        })
        shapes[iso] = geom  # un-buffered for now; buffer applied in adjacency step

        if i % 50 == 0 or i == total:
            print(f"  Simplified {i}/{total} countries...")

    return countries, shapes


def build_adjacency(shapes: dict, buffer_deg: float) -> dict:
    """
    Builds border graph using buffered intersection test.
    Two countries are adjacent if their buffered geometries intersect.
    """
    print(f"Building adjacency graph ({len(shapes)} countries, buffer={buffer_deg}°)...")
    buffered = {iso: geom.buffer(buffer_deg) for iso, geom in shapes.items()}

    adjacency: dict[str, list[str]] = {}
    isos = list(buffered.keys())
    total = len(isos)

    for i, iso_a in enumerate(isos, 1):
        geom_a = buffered[iso_a]
        neighbours = [
            iso_b for iso_b in isos
            if iso_a != iso_b and geom_a.intersects(buffered[iso_b])
        ]
        adjacency[iso_a] = neighbours

        if i % 50 == 0 or i == total:
            print(f"  Adjacency {i}/{total}...")

    return adjacency


def write_json(data: object, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, separators=(",", ":"), ensure_ascii=False)
    print(f"Wrote {path} ({path.stat().st_size // 1024} KB)")


def main() -> None:
    args    = parse_args()
    outdir  = Path(args.outdir)
    data    = load_geojson(args.input)
    features = data.get("features", [])

    if not features:
        print("ERROR: no features found in GeoJSON", file=sys.stderr)
        sys.exit(1)

    print(f"Found {len(features)} features")

    countries, shapes = build_countries(features, args.simplify)
    adjacency         = build_adjacency(shapes, args.buffer)

    write_json(countries, outdir / "countries.json")
    write_json(adjacency, outdir / "adjacency.json")

    border_counts = [len(v) for v in adjacency.values()]
    print(
        f"\nDone. {len(countries)} countries, "
        f"avg {sum(border_counts)/max(len(border_counts),1):.1f} neighbours each."
    )


if __name__ == "__main__":
    main()
