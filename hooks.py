"""MkDocs build hook for the Mail-Archiver documentation.

## Why this exists

The Markdown files in `doc/` were written for GitHub's renderer. There, a
heading such as

    ## 🧬 Attachment Deduplication Settings

produces the anchor `#-attachment-deduplication-settings` — GitHub keeps the
emoji, and the emoji degrades into a leading dash.

MkDocs strips the emoji before slugifying, so the same heading becomes
`#attachment-deduplication-settings`. Every cross-document anchor link written
for GitHub (e.g. `Setup.md#-attachment-deduplication-settings`) would therefore
land on nothing once the docs are served from GitHub Pages.

## What this hook does

`on_page_markdown` rewrites such anchor links **in memory, at build time**, to
the slug MkDocs actually emits. Files on disk are never modified, so the same
Markdown keeps working on GitHub *and* on the generated site.
"""

import re
import unicodedata

# An anchor link: `(Setup.md#-some-anchor)` or `(#-some-anchor)`.
ANCHOR_LINK_RE = re.compile(r"\]\((?P<path>[^)\s]*?)#(?P<anchor>[^)\s]+)\)")

# ATX heading: `## Some heading`.
HEADING_RE = re.compile(r"^#{1,6}\s+(?P<text>.+?)\s*#*\s*$", re.MULTILINE)

# MkDocs/pymdownx drops anything that is not a word char, space or dash.
INVALID_SLUG_CHAR_RE = re.compile(r"[^\w\- ]", re.UNICODE)
SPACE_RE = re.compile(r" ", re.UNICODE)

# Collected once per build: every spelling of a page -> the ids it contains.
_PAGE_IDS: dict[str, set[str]] = {}


def _mkdocs_slug(heading: str) -> str:
    """Reproduce the slug MkDocs emits for `heading`.

    MkDocs uses Python-Markdown's `toc` extension, whose default slugifier is
    `markdown.extensions.toc.slugify_unicode` (emoji are dropped, spaces become
    dashes, no leading dash is left behind).
    """
    try:
        from markdown.extensions.toc import slugify_unicode
    except ImportError:  # pragma: no cover
        text = unicodedata.normalize("NFC", heading).strip().lower()
        return SPACE_RE.sub("-", INVALID_SLUG_CHAR_RE.sub("", text))

    slug = slugify_unicode(heading, "-")
    # Guard against an empty slug for emoji-only headings.
    return slug or SPACE_RE.sub("-", INVALID_SLUG_CHAR_RE.sub("", heading.strip().lower()))


def _github_slug(heading: str) -> str:
    """Reproduce GitHub's heading anchor for `heading` (keeps emoji)."""
    text = unicodedata.normalize("NFC", heading).strip().lower()
    # Keep letters, digits, marks, emoji and the connector chars GitHub keeps.
    kept = [
        ch
        for ch in text
        if ch in "-_ " or unicodedata.category(ch).startswith(("L", "N", "M", "S"))
    ]
    slug = "".join(kept)
    slug = re.sub(r"[\uFE0E\uFE0F\u200B-\u200D]", "", slug)  # drop VS / ZWJ
    slug = SPACE_RE.sub("-", slug.strip())
    return re.sub(r"-{2,}", "-", slug)


def _collect_ids() -> None:
    """Scan every source file once and record the slugs MkDocs will emit."""
    import pathlib

    doc_dir = pathlib.Path(__file__).resolve().parent / "doc"
    if not doc_dir.is_dir():
        return

    for md in doc_dir.rglob("*.md"):
        try:
            text = md.read_text(encoding="utf-8")
        except OSError:
            continue

        ids = {_mkdocs_slug(m.group("text")) for m in HEADING_RE.finditer(text)}
        ids.discard("")
        if not ids:
            continue

        rel = md.relative_to(doc_dir).as_posix()
        stem = rel.rsplit(".", 1)[0]
        keys = {rel, md.name, stem, stem + "/", stem + ".md"}
        for key in keys:
            _PAGE_IDS.setdefault(key, set()).update(ids)


def _variants(anchor: str) -> list[str]:
    """Candidate MkDocs slugs for a GitHub anchor."""
    stripped = anchor.lstrip("-_")
    raw = [
        stripped,
        stripped.replace("_", "-"),
        re.sub(r"[-_]{2,}", "-", stripped),
        anchor.replace("_", "-"),
        re.sub(r"[-_]{2,}", "-", anchor),
    ]
    seen: set[str] = set()
    out: list[str] = []
    for c in raw:
        if c and c not in seen:
            seen.add(c)
            out.append(c)
    return out


def on_page_markdown(markdown: str, page, config, files, **kwargs) -> str:
    """Rewrite GitHub-style anchor links to the ids MkDocs emits."""
    if not _PAGE_IDS:
        _collect_ids()

    # Ids valid on the page currently being rendered (same-page `#anchor` links).
    src = getattr(page.file, "src_path", "")
    local_ids: set[str] = set()
    for key in (src, src.rsplit("/", 1)[-1], src.rsplit(".", 1)[0]):
        local_ids |= _PAGE_IDS.get(key, set())

    def replace(match: "re.Match[str]") -> str:
        path, anchor = match.group("path"), match.group("anchor")
        valid = local_ids if path == "" else _PAGE_IDS.get(path)
        if valid is None or anchor in valid:
            return match.group(0)
        for candidate in _variants(anchor):
            if candidate in valid:
                return f"]({path}#{candidate})"
        return match.group(0)

    return ANCHOR_LINK_RE.sub(replace, markdown)
