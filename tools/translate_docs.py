#!/usr/bin/env python3
"""Translate the Mail Archiver documentation with an OpenAI-compatible endpoint.

The English pages under ``doc/`` are the single source of truth. Translations
are written next to them as ``doc/<Page>.<locale>.md``, the suffix layout
``mkdocs-static-i18n`` expects. The language list is derived from the
``Resources/SharedResource.*.resx`` files, so a new UI language automatically
becomes a documentation target. ``en-GB`` is skipped because the source is
already English.

Configuration is read from the environment:

``LLM_BASE_URL``
    Base URL of the chat-completions API, for example
    ``https://api.openai.com/v1`` or ``https://llm.example.com/v1``.
``LLM_API_KEY``
    API key. When it is missing the script prints a notice and exits 0, so a
    fork without the secret does not fail the documentation build.
``LLM_MODEL``
    Model name, for example ``gpt-4o-mini``.
``LLM_TIMEOUT``
    Request timeout in seconds, default 180.

Usage::

    python tools/translate_docs.py --all
    python tools/translate_docs.py --lang de --force
    python tools/translate_docs.py --check
    python tools/translate_docs.py --nav
    python tools/translate_docs.py --selftest
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

try:
    import yaml
except ImportError:  # pragma: no cover - PyYAML ships with MkDocs
    yaml = None

ROOT = Path(__file__).resolve().parent.parent
DOC = ROOT / "doc"
I18N = DOC / "i18n"
MANIFEST = I18N / "translations.json"
GLOSSARY = I18N / "glossary.yml"
NAV = I18N / "nav.yml"
RESOURCES = ROOT / "Resources"

EXCLUDED_SOURCES = {"API-Reference.md"}
DEFAULT_BASE_URL = "https://api.openai.com/v1"
DEFAULT_MODEL = "gpt-4o-mini"
MAX_CHUNK_CHARS = 6000
MAX_RETRIES = 4
MAX_VALIDATION_RETRIES = 3

LANGUAGE_NAMES = {
    "de": "German",
    "es": "Spanish",
    "fr": "French",
    "hu": "Hungarian",
    "it": "Italian",
    "ja": "Japanese",
    "nl": "Dutch",
    "pl": "Polish",
    "pt-BR": "Brazilian Portuguese",
    "ru": "Russian",
    "sl": "Slovenian",
    "zh": "Simplified Chinese",
}

PLACEHOLDER_RE = re.compile(r"⟦X(\d+)⟧")
FENCE_RE = re.compile(r"^(\s*)(`{3,}|~{3,})")


# --------------------------------------------------------------------------
# Discovery
# --------------------------------------------------------------------------

def discover_languages() -> list[str]:
    """Return the documentation target locales, derived from the resx files."""
    languages = set()
    for path in RESOURCES.glob("SharedResource.*.resx"):
        code = path.name[len("SharedResource."):-len(".resx")]
        if code and code != "en-GB":
            languages.add(code)
    return sorted(languages)


def discover_sources() -> list[Path]:
    """Return the English Markdown pages that are translation sources."""
    sources = []
    for path in sorted(DOC.glob("*.md")):
        if path.name in EXCLUDED_SOURCES:
            continue
        if re.search(r"\.[a-z]{2}(?:-[A-Z]{2})?\.md$", path.name):
            continue
        sources.append(path)
    return sources


def target_path(source: Path, locale: str) -> Path:
    return source.with_name(f"{source.stem}.{locale}.md")


# --------------------------------------------------------------------------
# Manifest
# --------------------------------------------------------------------------

def load_manifest() -> dict[str, Any]:
    if not MANIFEST.exists():
        return {"version": 1, "languages": {}}
    try:
        data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {"version": 1, "languages": {}}
    data.setdefault("version", 1)
    data.setdefault("languages", {})
    return data


def save_manifest(data: dict[str, Any]) -> None:
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(
        json.dumps(data, indent=2, ensure_ascii=False, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def sha256(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


# --------------------------------------------------------------------------
# Markdown helpers
# --------------------------------------------------------------------------

def first_heading(text: str) -> str:
    for line in text.splitlines():
        match = re.match(r"^#\s+(.*)$", line)
        if match:
            return match.group(1).strip()
    return ""


def slugify(title: str) -> str:
    """Reproduce the anchor MkDocs emits for a heading."""
    try:
        from markdown.extensions.toc import slugify_unicode

        return slugify_unicode(title, "-")
    except Exception:
        text = re.sub(r"[^\w\s-]", "", title.lower(), flags=re.UNICODE)
        return re.sub(r"\s+", "-", text).strip("-")


def extract_headings(text: str) -> list[tuple[int, str]]:
    """Return (level, anchor) for every heading outside code fences."""
    headings: list[tuple[int, str]] = []
    in_fence = False
    fence_marker = ""
    for line in text.splitlines():
        fence = FENCE_RE.match(line)
        if fence:
            marker = fence.group(2)[:3]
            if not in_fence:
                in_fence = True
                fence_marker = marker
            elif marker == fence_marker:
                in_fence = False
                fence_marker = ""
            continue
        if in_fence:
            continue
        match = re.match(r"^(#{1,6})\s+(.*)$", line)
        if match:
            headings.append((len(match.group(1)), slugify(match.group(2).strip())))
    return headings


def split_chunks(text: str) -> list[str]:
    """Split a page at level-2 headings, never inside a code fence."""
    lines = text.splitlines(keepends=True)
    chunks: list[str] = []
    current: list[str] = []
    in_fence = False
    fence_marker = ""

    for line in lines:
        fence = FENCE_RE.match(line)
        if fence:
            marker = fence.group(2)[:3]
            if not in_fence:
                in_fence = True
                fence_marker = marker
            elif marker == fence_marker:
                in_fence = False
                fence_marker = ""
        if not in_fence and re.match(r"^##\s+", line) and current:
            chunks.append("".join(current))
            current = [line]
        else:
            current.append(line)
    if current:
        chunks.append("".join(current))

    result: list[str] = []
    for chunk in chunks:
        if len(chunk) <= MAX_CHUNK_CHARS:
            result.append(chunk)
        else:
            result.extend(_split_large(chunk))
    return result


def _split_large(text: str) -> list[str]:
    """Split an oversized chunk at level-3 headings, then at blank lines."""
    parts = re.split(r"(?m)(?=^###\s+)", text)
    if len(parts) > 1:
        result: list[str] = []
        for part in parts:
            if len(part) <= MAX_CHUNK_CHARS:
                result.append(part)
            else:
                result.extend(_split_paragraphs(part))
        return result
    return _split_paragraphs(text)


def _split_paragraphs(text: str) -> list[str]:
    paragraphs = re.split(r"(?m)(?=\n\s*\n)", text)
    result: list[str] = []
    buffer = ""
    for paragraph in paragraphs:
        if buffer and len(buffer) + len(paragraph) > MAX_CHUNK_CHARS:
            result.append(buffer)
            buffer = paragraph
        else:
            buffer += paragraph
    if buffer:
        result.append(buffer)
    # Hard split anything that is still too large.
    final: list[str] = []
    for part in result:
        while len(part) > MAX_CHUNK_CHARS:
            final.append(part[:MAX_CHUNK_CHARS])
            part = part[MAX_CHUNK_CHARS:]
        if part:
            final.append(part)
    return final


def mask(text: str) -> tuple[str, dict[str, str]]:
    """Replace opaque Markdown fragments with placeholders."""
    store: dict[str, str] = {}

    def keep(value: str) -> str:
        key = f"⟦X{len(store)}⟧"
        store[key] = value
        return key

    text = re.sub(r"(?ms)^(`{3,}|~{3,}).*?^\1\s*$", lambda m: keep(m.group(0)), text)
    text = re.sub(r"`[^`\n]+`", lambda m: keep(m.group(0)), text)
    text = re.sub(r"\]\(([^)\s]+)\)", lambda m: "](" + keep(m.group(1)) + ")", text)
    text = re.sub(r"<[^>\n]+>", lambda m: keep(m.group(0)), text)
    return text, store


def normalize_placeholders(text: str) -> str:
    return re.sub(r"⟦\s*X\s*(\d+)\s*⟧", r"⟦X\1⟧", text)


def unmask(text: str, store: dict[str, str]) -> str:
    for key, value in store.items():
        text = text.replace(key, value)
    return text


def placeholders(text: str) -> set[str]:
    return {f"⟦X{m.group(1)}⟧" for m in PLACEHOLDER_RE.finditer(text)}


def count_fences(text: str) -> int:
    return len(re.findall(r"(?m)^\s*(?:`{3,}|~{3,})", text)) // 2


def count_headings(text: str, level: int) -> int:
    return len(re.findall(rf"(?m)^#{{{level}}}\s+\S", text))


def count_tables(text: str) -> int:
    return len(re.findall(r"(?m)^\s*\|?[\s:-]*-[\s:|-]*\|?\s*$", text))


def count_admonitions(text: str) -> int:
    return len(re.findall(r"(?m)^\s*(?:!!!|\?\?\?)\s*\w+", text))


def count_links(text: str) -> int:
    return len(re.findall(r"\]\(", text))


def validate(source: str, translated: str) -> list[str]:
    """Return structural differences that make a translation unusable."""
    problems: list[str] = []
    if count_fences(source) != count_fences(translated):
        problems.append(
            f"code fences: {count_fences(source)} -> {count_fences(translated)}"
        )
    for level in range(1, 7):
        expected = count_headings(source, level)
        actual = count_headings(translated, level)
        if expected != actual:
            problems.append(f"heading level {level}: {expected} -> {actual}")
    if count_tables(source) != count_tables(translated):
        problems.append(f"tables: {count_tables(source)} -> {count_tables(translated)}")
    if count_admonitions(source) != count_admonitions(translated):
        problems.append(
            f"admonitions: {count_admonitions(source)} -> {count_admonitions(translated)}"
        )
    if count_links(source) != count_links(translated):
        problems.append(f"links: {count_links(source)} -> {count_links(translated)}")
    return problems


def add_heading_ids(source: str, translated: str) -> str:
    """Give every translated heading the anchor of its English original.

    This keeps every inbound anchor link working across languages, because
    ``mkdocs-static-i18n`` rewrites page paths but not fragments.
    """
    anchors = [anchor for _, anchor in extract_headings(source)]
    lines = translated.splitlines()
    index = 0
    in_fence = False
    fence_marker = ""
    result: list[str] = []

    for line in lines:
        fence = FENCE_RE.match(line)
        if fence:
            marker = fence.group(2)[:3]
            if not in_fence:
                in_fence = True
                fence_marker = marker
            elif marker == fence_marker:
                in_fence = False
                fence_marker = ""
        if not in_fence and re.match(r"^#{1,6}\s+", line):
            line = re.sub(r"\s*\{#[^}]*\}\s*$", "", line)
            if index < len(anchors) and anchors[index]:
                line = f"{line} {{#{anchors[index]}}}"
            index += 1
        result.append(line)
    return "\n".join(result)


# --------------------------------------------------------------------------
# Prompt and API
# --------------------------------------------------------------------------

def build_system_prompt(locale: str, glossary: dict[str, str]) -> str:
    language = LANGUAGE_NAMES.get(locale, locale)
    lines = [
        f"You translate technical documentation from English into {language}.",
        "Preserve the Markdown structure exactly: headings, lists, tables, admonitions, blockquotes and code fences.",
        "Placeholders such as ⟦X0⟧ are opaque. Keep every placeholder unchanged and in place.",
        "Never translate link targets, anchor fragments, product names, CLI flags, configuration keys, code identifiers or file paths.",
        "Do not add, remove or reorder sections, links, table rows or code blocks.",
        "Return only the translated Markdown. Do not wrap the answer in a code fence and do not add commentary.",
    ]
    if glossary:
        lines.append("Use these glossary translations exactly:")
        for term, translated in glossary.items():
            lines.append(f"- {term} -> {translated}")
    return "\n".join(lines)


def load_glossary(locale: str) -> dict[str, str]:
    if yaml is None or not GLOSSARY.exists():
        return {}
    try:
        data = yaml.safe_load(GLOSSARY.read_text(encoding="utf-8")) or {}
    except Exception:
        return {}
    terms = data.get("terms", {})
    result: dict[str, str] = {}
    for term, values in terms.items():
        if isinstance(values, dict) and locale in values:
            result[str(term)] = str(values[locale])
    return result


class TransientError(RuntimeError):
    """A response worth retrying, for example a non-JSON body from a proxy."""


def call_chat_completion(
    system: str,
    user: str,
    model: str,
    base_url: str,
    api_key: str,
    timeout: float,
) -> str:
    payload = {
        "model": model,
        "temperature": 0,
        "stream": False,
        "messages": [
            {"role": "system", "content": system},
            {"role": "user", "content": user},
        ],
    }
    request = urllib.request.Request(
        base_url.rstrip("/") + "/chat/completions",
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            "Authorization": f"Bearer {api_key}",
        },
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        raw = response.read().decode("utf-8", errors="replace")
        status = response.status
        content_type = response.headers.get("Content-Type", "")
    try:
        body = json.loads(raw)
    except json.JSONDecodeError as exc:
        preview = " ".join(raw.split())[:300]
        raise TransientError(
            f"non-JSON response (HTTP {status}, {content_type}): {preview!r}"
        ) from exc
    try:
        content = body["choices"][0]["message"]["content"]
    except (KeyError, IndexError, TypeError) as exc:
        raise RuntimeError(f"unexpected response shape: {body}") from exc
    return content.strip("\n")


def request_with_retry(
    system: str,
    user: str,
    model: str,
    base_url: str,
    api_key: str,
    timeout: float,
) -> str:
    last_error: Exception | None = None
    for attempt in range(MAX_RETRIES):
        try:
            return call_chat_completion(
                system, user, model, base_url, api_key, timeout
            )
        except urllib.error.HTTPError as exc:
            last_error = exc
            if exc.code not in (429, 500, 502, 503, 504):
                raise
            retry_after = exc.headers.get("Retry-After") if exc.headers else None
            delay = int(retry_after) if retry_after and retry_after.isdigit() else 2 ** attempt
            preview = ""
            try:
                preview = " ".join(
                    exc.read().decode("utf-8", errors="replace").split()
                )[:200]
            except Exception:
                pass
            print(
                f"  HTTP {exc.code} {preview!r}, retrying in {delay}s",
                file=sys.stderr,
            )
            time.sleep(delay)
        except TransientError as exc:
            last_error = exc
            delay = 2 ** attempt
            print(f"  {exc}, retrying in {delay}s", file=sys.stderr)
            time.sleep(delay)
        except (urllib.error.URLError, TimeoutError, OSError) as exc:
            last_error = exc
            delay = 2 ** attempt
            print(f"  request failed ({exc}), retrying in {delay}s", file=sys.stderr)
            time.sleep(delay)
    raise RuntimeError(f"request failed after {MAX_RETRIES} attempts: {last_error}")


# --------------------------------------------------------------------------
# Translation
# --------------------------------------------------------------------------

def translate_chunk(
    chunk: str,
    locale: str,
    glossary: dict[str, str],
    page_title: str,
    model: str,
    base_url: str,
    api_key: str,
    timeout: float,
) -> str:
    system = build_system_prompt(locale, glossary)
    masked, store = mask(chunk)
    user = f"Page: {page_title}\n\n<source>\n{masked}\n</source>"
    feedback = ""
    last_problem = "unknown"
    for attempt in range(MAX_VALIDATION_RETRIES):
        prompt = user if not feedback else f"{user}\n\n{feedback}"
        raw = request_with_retry(system, prompt, model, base_url, api_key, timeout)
        raw = normalize_placeholders(raw)
        missing = set(store) - placeholders(raw)
        if missing:
            last_problem = "missing placeholders: " + ", ".join(sorted(missing))
            feedback = (
                "The previous answer dropped or changed these placeholders: "
                + ", ".join(sorted(missing))
                + ". Repeat the translation and keep every placeholder."
            )
            continue
        translated = unmask(raw, store)
        problems = validate(chunk, translated)
        if not problems:
            return translated
        last_problem = "; ".join(problems)
        feedback = (
            "The previous answer changed the Markdown structure ("
            + "; ".join(problems)
            + "). Repeat the translation without changing the structure."
        )
    raise RuntimeError(
        f"translation failed validation after {MAX_VALIDATION_RETRIES} attempts: {last_problem}"
    )


def translate_file(
    source_path: Path,
    locale: str,
    manifest: dict[str, Any],
    model: str,
    base_url: str,
    api_key: str,
    timeout: float,
    force: bool,
) -> str:
    source = source_path.read_text(encoding="utf-8")
    digest = sha256(source)
    key = source_path.name
    entry = manifest["languages"].get(locale, {}).get(key)
    target = target_path(source_path, locale)

    if not force and target.exists() and entry and entry.get("source_sha256") == digest:
        return "unchanged"

    title = first_heading(source)
    chunks = split_chunks(source)
    translated_chunks = []
    for number, chunk in enumerate(chunks, 1):
        print(f"  {key} [{locale}] chunk {number}/{len(chunks)}", flush=True)
        translated_chunks.append(
            translate_chunk(
                chunk, locale, load_glossary(locale), title, model, base_url, api_key, timeout
            )
        )
    result = add_heading_ids(
        source, "\n\n".join(part.strip("\n") for part in translated_chunks)
    )
    target.write_text(result.rstrip() + "\n", encoding="utf-8")

    manifest["languages"].setdefault(locale, {})[key] = {
        "source_sha256": digest,
        "model": model,
        "translated_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    return "translated"


def remove_orphans(languages: list[str], sources: list[Path], manifest: dict[str, Any]) -> int:
    """Delete translations whose English source no longer exists."""
    names = {path.name for path in sources}
    removed = 0
    for locale in languages:
        entries = manifest["languages"].setdefault(locale, {})
        for name in list(entries):
            if name in names:
                continue
            orphan = DOC / f"{Path(name).stem}.{locale}.md"
            if orphan.exists():
                orphan.unlink()
                removed += 1
            del entries[name]
    return removed


# --------------------------------------------------------------------------
# Navigation
# --------------------------------------------------------------------------

def collect_nav_titles(nav: Any) -> list[str]:
    titles: list[str] = []

    def walk(node: Any) -> None:
        if isinstance(node, list):
            for item in node:
                walk(item)
        elif isinstance(node, dict):
            for key, value in node.items():
                if isinstance(value, str) and value.endswith(".md"):
                    titles.append(key)
                elif isinstance(value, list):
                    titles.append(key)
                    walk(value)

    walk(nav)
    return titles


def load_nav_titles() -> list[str]:
    if yaml is None:
        return []
    text = (ROOT / "mkdocs.yml").read_text(encoding="utf-8")
    match = re.search(r"(?ms)^nav:\n(.*?)(?=^\S)", text)
    if not match:
        return []
    try:
        nav = yaml.safe_load("nav:\n" + match.group(1))["nav"]
    except Exception:
        return []
    return collect_nav_titles(nav)


def translate_nav(
    languages: list[str],
    model: str,
    base_url: str,
    api_key: str,
    timeout: float,
) -> None:
    titles = load_nav_titles()
    if not titles:
        print("No navigation titles found, skipping --nav.", file=sys.stderr)
        return
    if yaml is None:
        print("PyYAML is not installed, skipping --nav.", file=sys.stderr)
        return
    data = yaml.safe_load(NAV.read_text(encoding="utf-8")) if NAV.exists() else {}
    data = data or {}
    for locale in languages:
        language = LANGUAGE_NAMES.get(locale, locale)
        system = (
            f"You translate user interface labels from English into {language}. "
            "Return a JSON object with exactly the same keys as the input. "
            "Do not add commentary or code fences."
        )
        user = json.dumps(titles, ensure_ascii=False)
        raw = request_with_retry(system, user, model, base_url, api_key, timeout)
        raw = re.sub(r"^```(?:json)?\s*|\s*```$", "", raw.strip())
        try:
            translated = json.loads(raw)
        except json.JSONDecodeError:
            print(f"  {locale}: navigation answer was not JSON, keeping English", file=sys.stderr)
            continue
        data[locale] = {title: str(translated.get(title, title)) for title in titles}
        print(f"  {locale}: {len(data[locale])} navigation labels", flush=True)
    NAV.parent.mkdir(parents=True, exist_ok=True)
    NAV.write_text(
        yaml.safe_dump(data, allow_unicode=True, sort_keys=True) if yaml else "{}",
        encoding="utf-8",
    )


# --------------------------------------------------------------------------
# Self test
# --------------------------------------------------------------------------

def selftest() -> None:
    sample = """# Title

Intro with `code` and [a link](Setup.md#-overview).

## Section one

Text.

```bash
echo hi
```

## Section two

| A | B |
|---|---|
| 1 | 2 |
"""
    chunks = split_chunks(sample)
    assert len(chunks) == 3, chunks
    masked, store = mask(sample)
    assert "⟦X" in masked
    assert unmask(masked, store) == sample
    assert validate(sample, sample) == []
    assert count_fences(sample) == 1
    assert count_headings(sample, 2) == 2
    assert count_tables(sample) == 1
    result = add_heading_ids(sample, sample)
    assert "{#section-one}" in result
    assert "{#section-two}" in result
    print("selftest OK")


# --------------------------------------------------------------------------
# Entry point
# --------------------------------------------------------------------------

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lang", help="translate one locale only")
    parser.add_argument("--all", action="store_true", help="translate every resx locale")
    parser.add_argument("--force", action="store_true", help="ignore the manifest")
    parser.add_argument("--check", action="store_true", help="report stale translations")
    parser.add_argument("--nav", action="store_true", help="translate navigation labels")
    parser.add_argument("--selftest", action="store_true", help="run offline checks")
    parser.add_argument(
        "--max-seconds",
        type=int,
        default=0,
        help="stop before the next page after this many seconds (0 means no limit)",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.selftest:
        selftest()
        return 0

    sources = discover_sources()
    languages = [args.lang] if args.lang else discover_languages()
    manifest = load_manifest()

    if args.check:
        stale = []
        for locale in languages:
            for source in sources:
                digest = sha256(source.read_text(encoding="utf-8"))
                entry = manifest["languages"].get(locale, {}).get(source.name)
                target = target_path(source, locale)
                if not target.exists() or not entry or entry.get("source_sha256") != digest:
                    stale.append(f"{locale}: {source.name}")
        if stale:
            print("Stale translations:")
            for item in stale:
                print(f"  {item}")
            return 1
        print("All translations are current.")
        return 0

    api_key = os.environ.get("LLM_API_KEY", "").strip()
    base_url = os.environ.get("LLM_BASE_URL", DEFAULT_BASE_URL).strip()
    model = os.environ.get("LLM_MODEL", DEFAULT_MODEL).strip()
    timeout = float(os.environ.get("LLM_TIMEOUT", "180"))

    if not api_key:
        print(
            "LLM_API_KEY is not set. Skipping translation; "
            "the existing translations stay untouched.",
            file=sys.stderr,
        )
        return 0

    if not (args.all or args.lang or args.nav):
        args.all = True

    started = time.monotonic()
    changed = 0
    failures: list[str] = []
    stopped_early = False
    if args.all or args.lang:
        removed = remove_orphans(languages, sources, manifest)
        if removed:
            print(f"Removed {removed} orphaned translation files.")

        for locale in languages:
            print(f"[{locale}]")
            for source in sources:
                if args.max_seconds and time.monotonic() - started >= args.max_seconds:
                    print(
                        f"Time budget of {args.max_seconds}s reached, "
                        f"stopping before {source.name}.",
                        file=sys.stderr,
                    )
                    stopped_early = True
                    break
                try:
                    state = translate_file(
                        source, locale, manifest, model, base_url, api_key, timeout, args.force
                    )
                except RuntimeError as exc:
                    failures.append(f"{locale}/{source.name}: {exc}")
                    print(f"  FAILED {source.name} [{locale}]: {exc}", file=sys.stderr)
                    save_manifest(manifest)
                    continue
                if state == "translated":
                    changed += 1
                save_manifest(manifest)
            save_manifest(manifest)
            if stopped_early:
                break

    if args.nav:
        print("[navigation]")
        translate_nav(languages, model, base_url, api_key, timeout)

    save_manifest(manifest)
    if failures:
        print(
            f"{len(failures)} page(s) failed and stay untranslated:",
            file=sys.stderr,
        )
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
    print(f"Done. {changed} file translations written.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        raise SystemExit(130)
