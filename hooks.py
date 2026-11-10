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

import json
import pathlib
import posixpath
import re
import unicodedata
from copy import deepcopy

from mkdocs.plugins import event_priority

# An anchor link: `(Setup.md#-some-anchor)` or `(#-some-anchor)`.
ANCHOR_LINK_RE = re.compile(r"\]\((?P<path>[^)\s]*?)#(?P<anchor>[^)\s]+)\)")

# ATX heading: `## Some heading`.
HEADING_RE = re.compile(r"^#{1,6}\s+(?P<text>.+?)\s*#*\s*$", re.MULTILINE)

# MkDocs/pymdownx drops anything that is not a word char, space or dash.
INVALID_SLUG_CHAR_RE = re.compile(r"[^\w\- ]", re.UNICODE)
SPACE_RE = re.compile(r" ", re.UNICODE)

# Collected once per build: every spelling of a page -> the ids it contains.
_PAGE_IDS: dict[str, set[str]] = {}

# Translation notices, loaded once. Missing file or PyYAML means no notice.
_NOTICES: dict[str, dict[str, dict[str, str]]] = {}
_NOTICES_FILE = pathlib.Path(__file__).resolve().parent / "doc" / "i18n" / "notices.yml"
if _NOTICES_FILE.exists():
    try:
        import yaml

        _NOTICES = yaml.safe_load(_NOTICES_FILE.read_text(encoding="utf-8")) or {}
    except Exception:  # pragma: no cover - a broken notice must not break the build
        _NOTICES = {}


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


def on_config(config):
    """Inject navigation translations from doc/i18n/nav.yml."""
    nav_file = pathlib.Path(__file__).resolve().parent / "doc" / "i18n" / "nav.yml"
    if not nav_file.exists():
        return config
    try:
        import yaml

        data = yaml.safe_load(nav_file.read_text(encoding="utf-8")) or {}
    except Exception:  # pragma: no cover - a broken file must not break the build
        return config
    try:
        plugin = config.plugins["i18n"]
    except (KeyError, TypeError):
        return config
    for language in getattr(plugin.config, "languages", []):
        translations = data.get(language.locale)
        if translations:
            language.nav_translations = translations
    return config


def _english_url(page, locale: str) -> str:
    """Relative URL of the English page, seen from a localized page."""
    abs_url = getattr(page, "abs_url", "/") or "/"
    if not abs_url.endswith("/"):
        abs_url += "/"
    english = abs_url.replace(f"/{locale}/", "/", 1)
    base = posixpath.dirname(abs_url.rstrip("/")) or "/"
    relative = posixpath.relpath(english, base)
    if not relative.endswith("/"):
        relative += "/"
    return relative


def _locale_from_url(page, config) -> str:
    """Return the build locale from a page URL.

    Fallback pages keep ``page.file.locale == "en"`` even when they are rendered
    into a localized build, so the file locale alone is not enough.
    """
    abs_url = getattr(page, "abs_url", "") or ""
    try:
        languages = config.plugins["i18n"].config.languages
    except (KeyError, TypeError):
        return "en"
    for language in languages:
        if getattr(language, "default", False):
            continue
        if f"/{language.locale}/" in abs_url:
            return language.locale
    return "en"


def _inject_notice(markdown: str, page, config) -> str:
    """Add the machine-translation notice to a localized page."""
    locale = _locale_from_url(page, config)
    if locale == "en" or not _NOTICES:
        return markdown
    source_path = getattr(page.file, "src_path", "") or ""
    translated = source_path.endswith(f".{locale}.md")
    kind = "translated" if translated else "fallback"
    notice = (_NOTICES.get(kind) or {}).get(locale)
    if not notice:
        return markdown
    link = (
        f'<a href="{_english_url(page, locale)}">'
        f'{notice.get("link_text", "English")}</a>'
    )
    body = notice.get("body", "").replace("{link}", link)
    marker = "info" if translated else "warning"
    block = f'!!! {marker} "{notice.get("title", "Translation")}"\n    {body}'
    lines = markdown.splitlines()
    for index, line in enumerate(lines):
        if line.startswith("# "):
            lines[index + 1:index + 1] = ["", block]
            return "\n".join(lines)
    return block + "\n\n" + markdown


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

    markdown = ANCHOR_LINK_RE.sub(replace, markdown)
    return _inject_notice(markdown, page, config)


# --------------------------------------------------------------------------
# Per-language search index
# --------------------------------------------------------------------------

def _lunr_language(locale: str) -> str:
    """Lunr knows base languages, not full locales such as pt-BR."""
    return locale.split("-", 1)[0].lower()


@event_priority(-100)
def on_post_build(config, **kwargs) -> None:
    """Split the shared search index into one index per language.

    ``mkdocs-static-i18n`` builds a single ``search/search_index.json`` that
    contains every language. Material loads the index relative to the current
    page, so a page under ``/de/`` asks for ``/de/search/search_index.json``.
    Writing one index per language there keeps every search inside its own
    locale.
    """
    site_dir = pathlib.Path(config["site_dir"])
    index_path = site_dir / "search" / "search_index.json"
    if not index_path.exists():
        return
    try:
        data = json.loads(index_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return

    try:
        languages = config.plugins["i18n"].config.languages
    except (KeyError, TypeError):
        return
    build = [language.locale for language in languages if language.build]
    if len(build) < 2:
        return
    try:
        default = next(
            language.locale for language in languages if language.default
        )
    except StopIteration:
        return

    docs = data.get("docs", [])
    prefixes = {
        f"{locale}/": locale for locale in build if locale != default
    }

    for prefix, locale in prefixes.items():
        localized = []
        for doc in docs:
            location = doc.get("location", "")
            if location.startswith(prefix):
                copy = deepcopy(doc)
                copy["location"] = location[len(prefix):]
                localized.append(copy)
        if not localized:
            continue
        index_config = deepcopy(data.get("config", {}))
        index_config["lang"] = [_lunr_language(locale)]
        target = site_dir / locale / "search" / "search_index.json"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(
            json.dumps(
                {"config": index_config, "docs": localized},
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )

    # The root index keeps only the default language.
    data["docs"] = [
        doc
        for doc in docs
        if not any(doc.get("location", "").startswith(prefix) for prefix in prefixes)
    ]
    data.setdefault("config", {})["lang"] = [_lunr_language(default)]
    index_path.write_text(
        json.dumps(data, ensure_ascii=False),
        encoding="utf-8",
    )
