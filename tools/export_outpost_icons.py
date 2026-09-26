"""Rasterize the approved SVG originals for RimWorld (requires resvg-py)."""

from pathlib import Path
import struct
import xml.etree.ElementTree as ET

import resvg_py


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Art" / "OutpostIcons"
OUTPUT = ROOT / "Textures" / "DreamsOutposts" / "World"


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    count = 0
    for file in sorted((ROOT / "Defs").rglob("*.xml")):
        for node in ET.parse(file).getroot().findall("DreamsOutposts.OutpostTypeDef"):
            name = node.findtext("defName")
            if not name:
                continue
            data = resvg_py.svg_to_bytes(
                svg_path=str(SOURCE / (name + ".svg")),
                width=128, height=128, skip_system_fonts=True,
            )
            assert data[:8] == b"\x89PNG\r\n\x1a\n"
            assert struct.unpack(">II", data[16:24]) == (128, 128)
            assert data[25] == 6, "Expected RGBA PNG"
            (OUTPUT / (name + ".png")).write_bytes(data)
            count += 1
    print(f"Exported {count} SVG originals to 128x128 RGBA PNG textures.")


if __name__ == "__main__":
    main()
