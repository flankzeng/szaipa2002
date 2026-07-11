#!/usr/bin/env python3
"""Build lossless web-font subsets for the modern public site.

The core subset contains every character currently present in public Razor,
CSS, and JavaScript sources. Characters outside that set keep using the
original font through a non-overlapping unicode-range fallback.
"""

from __future__ import annotations

import argparse
import re
import subprocess
from pathlib import Path

from fontTools.ttLib import TTFont


TEXT_SUFFIXES = {".cshtml", ".css", ".js"}
ALWAYS_INCLUDED = set(chr(codepoint) for codepoint in range(0x20, 0x7F))
ALWAYS_INCLUDED.update("，。！？；：、“”‘’（）《》〈〉【】—…·　")


def collect_source_characters(source_root: Path) -> set[str]:
    characters = set(ALWAYS_INCLUDED)
    for path in source_root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        if any(part in {"node_modules", "bin", "obj"} for part in path.parts):
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        characters.update(re.findall(r"[\u3000-\u303F\u3400-\u9FFF\uFF00-\uFFEF]", text))
    return characters


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--font", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    font = TTFont(args.font)
    available = set(font.getBestCmap())
    core = {ord(character) for character in collect_source_characters(args.source_root)} & available
    args.output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            "pyftsubset",
            str(args.font),
            f"--output-file={args.output}",
            f"--unicodes={','.join(f'U+{codepoint:04X}' for codepoint in sorted(core))}",
            "--flavor=woff2",
            "--layout-features=*",
            "--no-hinting",
        ],
        check=True,
    )

    print(f"core_glyphs={len(core)}")
    print(f"fallback_glyphs={len(available - core)}")
    print(f"output_bytes={args.output.stat().st_size}")


if __name__ == "__main__":
    main()
