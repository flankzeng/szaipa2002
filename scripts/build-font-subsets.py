#!/usr/bin/env python3
"""Build local web-font subsets without changing the repository's old glyphs.

Single-font mode keeps the original Phase 1 workflow. Batch Noto mode builds:

* one core per used weight from modern source text plus the read-only database
  codepoint snapshot;
* small GB2312 buffer shards for future common Chinese, loaded only when a new
  character is not already in the core;
* the generated @font-face section in font-subsets.css.

The database snapshot contains Unicode codepoints only. No database value or
credential is used by this script.
"""

from __future__ import annotations

import argparse
import re
import shutil
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path

from fontTools.pens.recordingPen import RecordingPen
from fontTools.ttLib import TTFont


TEXT_SUFFIXES = {".cshtml", ".css", ".js"}
ALWAYS_INCLUDED = set(chr(codepoint) for codepoint in range(0x20, 0x7F))
ALWAYS_INCLUDED.update("，。！？；：、“”‘’（）《》〈〉【】—…·　")
GB_SHARD_SIZE = 512
GENERATED_BEGIN = "/* BEGIN GENERATED NOTO SUBSETS -- scripts/build-font-subsets.py */"
GENERATED_END = "/* END GENERATED NOTO SUBSETS */"
REQUIRED_TOOLS = ("hb-subset", "woff2_decompress", "woff2_compress")


@dataclass(frozen=True)
class FaceRole:
    core_family: str
    buffer_family: str
    weight: int


@dataclass(frozen=True)
class FontSpec:
    source_name: str
    output_stem: str
    roles: tuple[FaceRole, ...]


PUBLIC_SANS = ("Szaipa Noto Sans SC", "Szaipa Noto Sans SC GB")
PUBLIC_SERIF = ("Szaipa Noto Serif SC", "Szaipa Noto Serif SC GB")
STAFF_SERIF = ("Szaipa Noto Serif SC Staff", "Szaipa Noto Serif SC Staff GB")

# Public families retain the exact weight set that the removed loli.net request
# exposed to the browser. Staff has separate families because its old Google
# request included Serif 600, while the public request did not. This prevents a
# newly declared 600 face from changing public 600 text from the old 700 match.
# Black/900 is omitted because no formal modern selector requests it.
SITE_NOTO_FONTS = (
    FontSpec("NotoSansSC-Thin.woff2", "NotoSansSC-Thin", (FaceRole(*PUBLIC_SANS, 100),)),
    FontSpec(
        "NotoSansSC-Light.woff2",
        "NotoSansSC-Light",
        (FaceRole(*PUBLIC_SANS, 300),),
    ),
    FontSpec(
        "NotoSansSC-Regular.woff2",
        "NotoSansSC-Regular",
        (FaceRole(*PUBLIC_SANS, 400),),
    ),
    FontSpec(
        "NotoSansSC-Medium.woff2",
        "NotoSansSC-Medium",
        (FaceRole(*PUBLIC_SANS, 500),),
    ),
    FontSpec(
        "NotoSansSC-Bold.woff2",
        "NotoSansSC-Bold",
        (FaceRole(*PUBLIC_SANS, 700),),
    ),
    FontSpec("NotoSerifSC-ExtraLight.woff2", "NotoSerifSC-ExtraLight", (FaceRole(*PUBLIC_SERIF, 100),)),
    FontSpec("NotoSerifSC-Light.woff2", "NotoSerifSC-Light", (FaceRole(*PUBLIC_SERIF, 300),)),
    FontSpec(
        "NotoSerifSC-Regular.woff2",
        "NotoSerifSC-Regular",
        (FaceRole(*PUBLIC_SERIF, 400), FaceRole(*STAFF_SERIF, 400)),
    ),
    FontSpec("NotoSerifSC-Medium.woff2", "NotoSerifSC-Medium", (FaceRole(*PUBLIC_SERIF, 500),)),
    FontSpec("NotoSerifSC-SemiBold.woff2", "NotoSerifSC-SemiBold", (FaceRole(*STAFF_SERIF, 600),)),
    FontSpec(
        "NotoSerifSC-Bold.woff2",
        "NotoSerifSC-Bold",
        (FaceRole(*PUBLIC_SERIF, 700), FaceRole(*STAFF_SERIF, 700)),
    ),
)


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


def read_codepoint_set(path: Path) -> set[int]:
    codepoints: set[int] = set()
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.split("#", 1)[0]
        for token in line.split():
            match = re.fullmatch(r"U\+([0-9A-Fa-f]{4,6})(?:-U\+([0-9A-Fa-f]{4,6}))?", token)
            if match is None:
                raise ValueError(f"Invalid codepoint token in {path}: {token}")
            start = int(match.group(1), 16)
            end = int(match.group(2), 16) if match.group(2) else start
            if end < start:
                raise ValueError(f"Invalid descending range in {path}: {token}")
            codepoints.update(range(start, end + 1))
    return codepoints


def collect_gb2312_codepoints() -> set[int]:
    codepoints: set[int] = set()
    for lead in range(0xA1, 0xF8):
        for trail in range(0xA1, 0xFF):
            try:
                value = bytes((lead, trail)).decode("gb2312")
            except UnicodeDecodeError:
                continue
            codepoints.update(ord(character) for character in value)
    return codepoints


def is_cjk(codepoint: int) -> bool:
    return (
        0x3400 <= codepoint <= 0x4DBF
        or 0x4E00 <= codepoint <= 0x9FFF
        or 0xF900 <= codepoint <= 0xFAFF
    )


def decompress_font(font_path: Path, temporary_directory: Path) -> Path:
    copied_source = temporary_directory / font_path.name
    shutil.copyfile(font_path, copied_source)
    subprocess.run(
        ["woff2_decompress", str(copied_source)],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )
    decompressed = copied_source.with_suffix(".ttf")
    if not decompressed.is_file():
        raise RuntimeError(f"woff2_decompress did not create {decompressed}")
    return decompressed


def require_font_tools() -> None:
    missing = [tool for tool in REQUIRED_TOOLS if shutil.which(tool) is None]
    if missing:
        raise RuntimeError(
            "Missing required font tools: " + ", ".join(missing)
            + ". Install HarfBuzz and Google's woff2 utilities before regenerating subsets."
        )


def glyph_drawing(font: TTFont, glyph_name: str) -> list[tuple]:
    pen = RecordingPen()
    font.getGlyphSet()[glyph_name].draw(pen)
    return pen.value


def validate_same_outlines(source: TTFont, subset: TTFont, codepoints: set[int], output_path: Path) -> None:
    source_cmap = source.getBestCmap()
    subset_cmap = subset.getBestCmap()
    for codepoint in codepoints:
        source_name = source_cmap[codepoint]
        subset_name = subset_cmap[codepoint]
        if glyph_drawing(source, source_name) != glyph_drawing(subset, subset_name):
            raise RuntimeError(f"Outline changed in {output_path} for U+{codepoint:04X}")
        if source["hmtx"].metrics[source_name] != subset["hmtx"].metrics[subset_name]:
            raise RuntimeError(f"Metrics changed in {output_path} for U+{codepoint:04X}")

    def name_records(font: TTFont) -> set[tuple[int, int, int, int, bytes]]:
        return {
            (record.nameID, record.platformID, record.platEncID, record.langID, record.string)
            for record in font["name"].names
        }

    if name_records(source) != name_records(subset):
        raise RuntimeError(f"Name/license metadata changed in {output_path}")


def build_subset(
    decompressed_font_path: Path,
    source_font: TTFont,
    output_path: Path,
    codepoints: set[int],
) -> int:
    if not codepoints:
        raise ValueError(f"Refusing to create an empty subset: {output_path}")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="szaipa-subset-") as subset_tmp:
        subset_ttf = Path(subset_tmp) / "subset.ttf"
        subprocess.run(
            [
                "hb-subset",
                str(decompressed_font_path),
                f"--output-file={subset_ttf}",
                f"--unicodes={','.join(f'{codepoint:X}' for codepoint in sorted(codepoints))}",
                "--layout-features=*",
                "--no-hinting",
                "--name-IDs=*",
                "--name-languages=*",
                "--name-legacy",
            ],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
        )
        subprocess.run(
            ["woff2_compress", str(subset_ttf)],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
        )
        compressed = subset_ttf.with_suffix(".woff2")
        if not compressed.is_file():
            raise RuntimeError(f"woff2_compress did not create {compressed}")
        shutil.move(compressed, output_path)

    subset = TTFont(output_path)
    subset_codepoints = set(subset.getBestCmap())
    missing = codepoints - subset_codepoints
    if missing:
        raise RuntimeError(f"Subset validation failed for {output_path}: {len(missing)} codepoints missing")
    if subset["OS/2"].usWeightClass != source_font["OS/2"].usWeightClass:
        raise RuntimeError(f"Weight metadata changed in {output_path}")
    validate_same_outlines(source_font, subset, codepoints, output_path)
    return output_path.stat().st_size


def split_shards(codepoints: set[int]) -> list[set[int]]:
    ordered = sorted(codepoints)
    return [set(ordered[index:index + GB_SHARD_SIZE]) for index in range(0, len(ordered), GB_SHARD_SIZE)]


def compact_ranges(codepoints: set[int]) -> str:
    ordered = sorted(codepoints)
    ranges: list[tuple[int, int]] = []
    start = previous = ordered[0]
    for codepoint in ordered[1:]:
        if codepoint == previous + 1:
            previous = codepoint
            continue
        ranges.append((start, previous))
        start = previous = codepoint
    ranges.append((start, previous))
    return ",".join(
        f"U+{start:04X}" if start == end else f"U+{start:04X}-{end:04X}"
        for start, end in ranges
    )


def font_face(family: str, file_name: str, weight: int, unicode_range: set[int] | None = None) -> str:
    lines = [
        "@font-face {",
        f'    font-family: "{family}";',
        f'    src: url("../fonts/{file_name}") format("woff2");',
        "    font-style: normal;",
        f"    font-weight: {weight};",
        "    font-display: swap;",
    ]
    if unicode_range is not None:
        lines.append(f"    unicode-range: {compact_ranges(unicode_range)};")
    lines.append("}")
    return "\n".join(lines)


def update_generated_css(path: Path, blocks: list[str]) -> None:
    text = path.read_text(encoding="utf-8")
    generated = GENERATED_BEGIN + "\n" + "\n\n".join(blocks) + "\n" + GENERATED_END
    if GENERATED_BEGIN in text and GENERATED_END in text:
        before, remainder = text.split(GENERATED_BEGIN, 1)
        _, after = remainder.split(GENERATED_END, 1)
        text = before + generated + after
    else:
        text = generated + "\n\n" + text
    path.write_text(text, encoding="utf-8")


def build_noto_batch(
    font_dir: Path,
    output_dir: Path,
    css_output: Path,
    source_codepoints: set[int],
    database_codepoints: set[int],
) -> None:
    core_requested = source_codepoints | database_codepoints
    buffer_requested = collect_gb2312_codepoints() - core_requested
    buffer_shards = split_shards(buffer_requested)
    css_blocks = [
        "/*\n"
        " * Local Noto cores use the repository's original outlines. Current source and\n"
        " * read-only database characters are in core; GB2312-only future characters\n"
        " * fall through to small same-outline buffer shards.\n"
        " */"
    ]
    total_bytes = 0

    with tempfile.TemporaryDirectory(prefix="szaipa-noto-sources-") as source_tmp:
        temporary_directory = Path(source_tmp)
        for spec in SITE_NOTO_FONTS:
            source = font_dir / spec.source_name
            decompressed = decompress_font(source, temporary_directory)
            font = TTFont(decompressed)
            available = set(font.getBestCmap())
            missing_database_cjk = {cp for cp in database_codepoints if is_cjk(cp)} - available
            if missing_database_cjk:
                formatted = ",".join(f"U+{cp:04X}" for cp in sorted(missing_database_cjk))
                raise RuntimeError(f"{source.name} cannot cover database CJK codepoints: {formatted}")

            core = core_requested & available
            core_name = f"{spec.output_stem}.core.woff2"
            core_bytes = build_subset(decompressed, font, output_dir / core_name, core)
            total_bytes += core_bytes
            for old_shard in output_dir.glob(f"{spec.output_stem}.gb*.woff2"):
                old_shard.unlink()

            supported_database = database_codepoints & available
            subset = TTFont(output_dir / core_name)
            missing_supported_database = supported_database - set(subset.getBestCmap())
            if missing_supported_database:
                raise RuntimeError(f"{core_name} misses {len(missing_supported_database)} supported database codepoints")

            for role in spec.roles:
                css_blocks.append(font_face(role.core_family, core_name, role.weight))

            shard_count = 0
            shard_bytes = 0
            for index, requested_shard in enumerate(buffer_shards):
                shard = requested_shard & available
                if not shard:
                    continue
                shard_name = f"{spec.output_stem}.gb{index:02d}.woff2"
                size = build_subset(decompressed, font, output_dir / shard_name, shard)
                shard_count += 1
                shard_bytes += size
                total_bytes += size
                for role in spec.roles:
                    css_blocks.append(font_face(role.buffer_family, shard_name, role.weight, shard))

            print(
                f"font={source.name};core_codepoints={len(core)};core_bytes={core_bytes};"
                f"db_supported={len(supported_database)};db_outside_source={len(database_codepoints - available)};"
                f"gb_shards={shard_count};gb_bytes={shard_bytes}",
                flush=True,
            )

    update_generated_css(css_output, css_blocks)
    print(
        f"database_codepoints={len(database_codepoints)};source_codepoints={len(source_codepoints)};"
        f"gb2312_codepoints={len(collect_gb2312_codepoints())};total_bytes={total_bytes};css={css_output}"
    )


def main() -> None:
    require_font_tools()
    parser = argparse.ArgumentParser()
    source_group = parser.add_mutually_exclusive_group(required=True)
    source_group.add_argument("--font", type=Path)
    source_group.add_argument(
        "--font-dir",
        type=Path,
        help="Build all Noto faces used by the modern public and Staff pages.",
    )
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--codepoints-file", type=Path)
    parser.add_argument("--css-output", type=Path)
    args = parser.parse_args()

    source_codepoints = {ord(character) for character in collect_source_characters(args.source_root)}

    if args.font is not None:
        if args.output is None or args.output_dir is not None:
            parser.error("--font requires --output and cannot be combined with --output-dir")
        if not args.font.is_file():
            raise FileNotFoundError(f"Font source does not exist: {args.font}")
        with tempfile.TemporaryDirectory(prefix="szaipa-font-source-") as source_tmp:
            decompressed = decompress_font(args.font, Path(source_tmp))
            font = TTFont(decompressed)
            requested = source_codepoints & set(font.getBestCmap())
            size = build_subset(decompressed, font, args.output, requested)
        print(f"font={args.font.name};core_codepoints={len(requested)};output_bytes={size}")
        return

    if args.output_dir is None or args.output is not None:
        parser.error("--font-dir requires --output-dir and cannot be combined with --output")
    if args.codepoints_file is None or args.css_output is None:
        parser.error("--font-dir requires --codepoints-file and --css-output")
    build_noto_batch(
        args.font_dir,
        args.output_dir,
        args.css_output,
        source_codepoints,
        read_codepoint_set(args.codepoints_file),
    )


if __name__ == "__main__":
    main()
