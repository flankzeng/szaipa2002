#!/usr/bin/env python3
"""Build local web-font subsets without changing the repository's old glyphs.

Single-font mode keeps the original Phase 1 workflow. Batch Noto mode builds:

* one core per used weight from modern source text plus the read-only database
  codepoint snapshot;
* small GB2312 buffer shards for future common Chinese, loaded only when a new
  character is not already in the core;
* the generated @font-face sections in the public and Staff CSS manifests.

The database snapshot contains Unicode codepoints only. No database value or
credential is used by this script.
"""

from __future__ import annotations

import argparse
import hashlib
import re
import shutil
import stat
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
FONT_URL_PATTERN = re.compile(
    r"(?P<url>\.\./fonts/(?P<file>[^?\"')]+\.woff2))"
    r"(?:\?v=(?P<version>[^\"')\s]+))?(?=[\"')])"
)
FONT_VERSION_LENGTH = 12
FONT_FACE_BLOCK_PATTERN = re.compile(r"@font-face\s*\{.*?\}\s*", re.DOTALL)
FONT_FAMILY_PATTERN = re.compile(r'font-family:\s*"(?P<family>[^"]+)";')


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
PUBLIC_MANIFEST_FAMILIES = frozenset((*PUBLIC_SANS, *PUBLIC_SERIF))
STAFF_MANIFEST_FAMILIES = frozenset((*PUBLIC_SANS, *STAFF_SERIF))

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


def font_content_version(font_path: Path) -> str:
    return hashlib.sha256(font_path.read_bytes()).hexdigest()[:FONT_VERSION_LENGTH]


def version_font_urls(css_path: Path, *, check_only: bool) -> int:
    """Refresh or verify content versions for every local WOFF2 URL in one CSS manifest."""
    text = css_path.read_text(encoding="utf-8")
    font_dir = css_path.parent.parent / "fonts"
    failures: list[str] = []
    matched_files: set[str] = set()

    def replace(match: re.Match[str]) -> str:
        file_name = match.group("file")
        if Path(file_name).name != file_name or "/" in file_name or "\\" in file_name:
            raise ValueError(f"Unsafe font URL in {css_path}: {file_name}")

        font_path = font_dir / file_name
        if not font_path.is_file():
            raise FileNotFoundError(f"Font referenced by {css_path} does not exist: {font_path}")

        expected = font_content_version(font_path)
        actual = match.group("version")
        matched_files.add(file_name)
        if check_only:
            if actual != expected:
                failures.append(f"{file_name}: expected ?v={expected}, found {actual or 'no version'}")
            return match.group(0)
        return f"{match.group('url')}?v={expected}"

    refreshed = FONT_URL_PATTERN.sub(replace, text)
    if not matched_files:
        raise RuntimeError(f"No local WOFF2 URLs found in {css_path}")
    if failures:
        raise RuntimeError("Font CSS version check failed: " + "; ".join(failures))
    if not check_only and refreshed != text:
        css_path.write_text(refreshed, encoding="utf-8")

    action = "verified" if check_only else "versioned"
    print(f"font_urls={len(matched_files)};action={action};css={css_path}")
    return len(matched_files)


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


def manifest_names_for_role(role: FaceRole) -> tuple[str, ...]:
    family_pair = (role.core_family, role.buffer_family)
    if family_pair == PUBLIC_SANS:
        return ("public", "staff")
    if family_pair == PUBLIC_SERIF:
        return ("public",)
    if family_pair == STAFF_SERIF:
        return ("staff",)
    raise ValueError(f"Unknown font manifest role: {family_pair!r}")


def validate_manifest_pair_paths(
    public_path: Path,
    staff_path: Path,
    *,
    require_existing: bool,
) -> None:
    if public_path.resolve() == staff_path.resolve():
        raise ValueError("Public and Staff font manifests must use different paths")
    if require_existing:
        missing = [str(path) for path in (public_path, staff_path) if not path.is_file()]
        if missing:
            raise FileNotFoundError(
                "Both tracked font manifest templates must exist before rebuilding Noto subsets: "
                + ", ".join(missing)
            )


def write_manifest_pair(
    public_path: Path,
    public_text: str,
    staff_path: Path,
    staff_text: str,
    *,
    refresh_versions: bool,
) -> None:
    """Validate both manifests before replacing either tracked destination."""
    validate_manifest_pair_paths(public_path, staff_path, require_existing=False)

    public_path.parent.mkdir(parents=True, exist_ok=True)
    staff_path.parent.mkdir(parents=True, exist_ok=True)
    temporary_paths: list[Path] = []
    destination_modes: list[int] = []
    backup_paths: list[Path | None] = []
    preserve_backups = False
    try:
        for destination, text in ((public_path, public_text), (staff_path, staff_text)):
            destination_mode = (
                stat.S_IMODE(destination.stat().st_mode) if destination.exists() else 0o644
            )
            destination_modes.append(destination_mode)
            with tempfile.NamedTemporaryFile(
                mode="w",
                encoding="utf-8",
                prefix=f".{destination.stem}-",
                suffix=destination.suffix,
                dir=destination.parent,
                delete=False,
            ) as temporary:
                temporary_path = Path(temporary.name)
                temporary_paths.append(temporary_path)
                temporary.write(text)

        for temporary_path in temporary_paths:
            version_font_urls(temporary_path, check_only=not refresh_versions)
        for temporary_path, destination_mode in zip(temporary_paths, destination_modes):
            temporary_path.chmod(destination_mode)

        destinations = (public_path, staff_path)
        for destination in destinations:
            if not destination.exists():
                backup_paths.append(None)
                continue
            with tempfile.NamedTemporaryFile(
                prefix=f".{destination.stem}-backup-",
                suffix=destination.suffix,
                dir=destination.parent,
                delete=False,
            ) as backup:
                backup_path = Path(backup.name)
                backup_paths.append(backup_path)
            shutil.copy2(destination, backup_path)

        replaced_count = 0
        try:
            for temporary_path, destination in zip(temporary_paths, destinations):
                temporary_path.replace(destination)
                replaced_count += 1
        except Exception as replacement_error:
            rollback_errors: list[str] = []
            for index in reversed(range(replaced_count)):
                destination = destinations[index]
                backup_path = backup_paths[index]
                try:
                    if backup_path is None:
                        destination.unlink(missing_ok=True)
                    else:
                        backup_path.replace(destination)
                except Exception as rollback_error:
                    rollback_errors.append(f"{destination}: {rollback_error}")
            if rollback_errors:
                preserve_backups = True
                backup_locations = ", ".join(
                    str(path) for path in backup_paths if path is not None and path.exists()
                )
                raise RuntimeError(
                    "Font manifest replacement and rollback both failed; "
                    f"backups preserved at: {backup_locations}; rollback errors: "
                    + "; ".join(rollback_errors)
                ) from replacement_error
            raise

        temporary_paths.clear()
    finally:
        for temporary_path in temporary_paths:
            temporary_path.unlink(missing_ok=True)
        if not preserve_backups:
            for backup_path in backup_paths:
                if backup_path is not None:
                    backup_path.unlink(missing_ok=True)


def update_generated_css_pair(
    public_path: Path,
    public_blocks: list[str],
    staff_path: Path,
    staff_blocks: list[str],
) -> None:
    validate_manifest_pair_paths(public_path, staff_path, require_existing=True)

    with tempfile.TemporaryDirectory(prefix="szaipa-font-manifests-") as temporary_directory:
        temporary_root = Path(temporary_directory)
        public_temporary = temporary_root / public_path.name
        staff_temporary = temporary_root / staff_path.name
        shutil.copyfile(public_path, public_temporary)
        shutil.copyfile(staff_path, staff_temporary)
        update_generated_css(public_temporary, public_blocks)
        update_generated_css(staff_temporary, staff_blocks)
        public_text = public_temporary.read_text(encoding="utf-8")
        staff_text = staff_temporary.read_text(encoding="utf-8")

    write_manifest_pair(
        public_path,
        public_text,
        staff_path,
        staff_text,
        refresh_versions=True,
    )


def split_existing_manifest(source_path: Path, public_path: Path, staff_path: Path) -> None:
    """Split complete existing face blocks without rebuilding or rewriting font files."""
    source_text = source_path.read_text(encoding="utf-8")
    if GENERATED_BEGIN not in source_text or GENERATED_END not in source_text:
        raise ValueError(f"Generated font markers are missing from {source_path}")

    before_generated, remainder = source_text.split(GENERATED_BEGIN, 1)
    generated_text, public_tail = remainder.split(GENERATED_END, 1)
    if before_generated.strip():
        raise ValueError(f"Unexpected content before the generated font section in {source_path}")

    matches = list(FONT_FACE_BLOCK_PATTERN.finditer(generated_text))
    if not matches:
        raise ValueError(f"No generated @font-face blocks found in {source_path}")

    for previous, current in zip(matches, matches[1:]):
        if generated_text[previous.end() : current.start()].strip():
            raise ValueError(f"Unexpected content between generated font faces in {source_path}")
    if generated_text[matches[-1].end() :].strip():
        raise ValueError(f"Unexpected content after generated font faces in {source_path}")

    preamble = generated_text[:matches[0].start()].strip()
    public_blocks: list[str] = []
    staff_blocks: list[str] = []
    source_blocks: list[str] = []
    public_sans_blocks: list[str] = []
    for match in matches:
        block = match.group(0).strip()
        family_match = FONT_FAMILY_PATTERN.search(block)
        if family_match is None:
            raise ValueError(f"Generated @font-face block has no family in {source_path}")
        family = family_match.group("family")
        source_blocks.append(block)
        if family in PUBLIC_MANIFEST_FAMILIES:
            public_blocks.append(block)
        if family in STAFF_MANIFEST_FAMILIES:
            staff_blocks.append(block)
        if family in PUBLIC_SANS:
            public_sans_blocks.append(block)
        if family not in PUBLIC_MANIFEST_FAMILIES | STAFF_MANIFEST_FAMILIES:
            raise ValueError(f"Unknown generated font family in {source_path}: {family}")

    if set(public_blocks) | set(staff_blocks) != set(source_blocks):
        raise RuntimeError("Split font manifests do not cover every original generated face")
    if set(public_blocks) & set(staff_blocks) != set(public_sans_blocks):
        raise RuntimeError("Only public Noto Sans faces may be shared by both manifests")

    def generated_manifest(blocks: list[str]) -> str:
        body = "\n\n".join((preamble, *blocks))
        return f"{GENERATED_BEGIN}\n{body}\n{GENERATED_END}"

    public_text = generated_manifest(public_blocks) + public_tail
    staff_text = (
        generated_manifest(staff_blocks)
        + "\n\n/* Staff uses shared public Noto Sans plus its isolated Serif 400/600/700 faces. */\n"
    )
    write_manifest_pair(
        public_path,
        public_text,
        staff_path,
        staff_text,
        refresh_versions=False,
    )
    print(
        f"source_faces={len(source_blocks)};public_generated_faces={len(public_blocks)};"
        f"staff_generated_faces={len(staff_blocks)};shared_faces={len(public_sans_blocks)};"
        f"public_css={public_path};staff_css={staff_path}"
    )


def build_noto_batch(
    font_dir: Path,
    output_dir: Path,
    public_css_output: Path,
    staff_css_output: Path,
    source_codepoints: set[int],
    database_codepoints: set[int],
) -> None:
    validate_manifest_pair_paths(public_css_output, staff_css_output, require_existing=True)
    missing_sources = [
        str(font_dir / spec.source_name)
        for spec in SITE_NOTO_FONTS
        if not (font_dir / spec.source_name).is_file()
    ]
    if missing_sources:
        raise FileNotFoundError(
            "Noto batch source fonts are missing; no output was changed: "
            + ", ".join(missing_sources)
        )

    core_requested = source_codepoints | database_codepoints
    buffer_requested = collect_gb2312_codepoints() - core_requested
    buffer_shards = split_shards(buffer_requested)
    generated_comment = (
        "/*\n"
        " * Local Noto cores use the repository's original outlines. Current source and\n"
        " * read-only database characters are in core; GB2312-only future characters\n"
        " * fall through to small same-outline buffer shards.\n"
        " */"
    )
    css_blocks = {
        "public": [generated_comment],
        "staff": [generated_comment],
    }
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
                for manifest_name in manifest_names_for_role(role):
                    css_blocks[manifest_name].append(font_face(role.core_family, core_name, role.weight))

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
                    for manifest_name in manifest_names_for_role(role):
                        css_blocks[manifest_name].append(
                            font_face(role.buffer_family, shard_name, role.weight, shard)
                        )

            print(
                f"font={source.name};core_codepoints={len(core)};core_bytes={core_bytes};"
                f"db_supported={len(supported_database)};db_outside_source={len(database_codepoints - available)};"
                f"gb_shards={shard_count};gb_bytes={shard_bytes}",
                flush=True,
            )

    update_generated_css_pair(
        public_css_output,
        css_blocks["public"],
        staff_css_output,
        css_blocks["staff"],
    )
    print(
        f"database_codepoints={len(database_codepoints)};source_codepoints={len(source_codepoints)};"
        f"gb2312_codepoints={len(collect_gb2312_codepoints())};total_bytes={total_bytes};"
        f"public_css={public_css_output};staff_css={staff_css_output}"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    source_group = parser.add_mutually_exclusive_group(required=True)
    source_group.add_argument("--font", type=Path)
    source_group.add_argument(
        "--font-dir",
        type=Path,
        help="Build all Noto faces used by the modern public and Staff pages.",
    )
    source_group.add_argument(
        "--refresh-css-versions",
        type=Path,
        metavar="CSS",
        help="Add or refresh content hashes on every local WOFF2 URL without rebuilding fonts.",
    )
    source_group.add_argument(
        "--check-css-versions",
        type=Path,
        metavar="CSS",
        help="Fail unless every local WOFF2 URL carries the current content hash.",
    )
    source_group.add_argument(
        "--split-css-manifests",
        type=Path,
        metavar="CSS",
        help="Split the existing combined CSS into public and Staff manifests without rebuilding fonts.",
    )
    parser.add_argument("--source-root", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--codepoints-file", type=Path)
    parser.add_argument("--public-css-output", type=Path)
    parser.add_argument("--staff-css-output", type=Path)
    args = parser.parse_args()

    if args.refresh_css_versions is not None or args.check_css_versions is not None:
        css_path = args.refresh_css_versions or args.check_css_versions
        version_font_urls(css_path, check_only=args.check_css_versions is not None)
        return

    if args.split_css_manifests is not None:
        if args.public_css_output is None or args.staff_css_output is None:
            parser.error("--split-css-manifests requires --public-css-output and --staff-css-output")
        split_existing_manifest(
            args.split_css_manifests,
            args.public_css_output,
            args.staff_css_output,
        )
        return

    require_font_tools()
    if args.source_root is None:
        parser.error("font build modes require --source-root")
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
    if args.codepoints_file is None or args.public_css_output is None or args.staff_css_output is None:
        parser.error(
            "--font-dir requires --codepoints-file, --public-css-output, and --staff-css-output"
        )
    build_noto_batch(
        args.font_dir,
        args.output_dir,
        args.public_css_output,
        args.staff_css_output,
        source_codepoints,
        read_codepoint_set(args.codepoints_file),
    )


if __name__ == "__main__":
    main()
