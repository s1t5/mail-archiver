#!/usr/bin/env python3
"""Render the API reference page from the OpenAPI specification.

`doc/assets/openapi-v1.json` is the specification as published by the
application itself. This script turns it into Markdown so the reference page
cannot drift away from the actual API — the specification is the single source
of truth, the page is derived output.

Usage:
    python3 tools/gen_api_reference.py            # write doc/API-Reference.md
    python3 tools/gen_api_reference.py --check    # exit 1 if the page is stale

The --check mode is what CI runs, so a pull request that changes the
specification without regenerating the page fails the build.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SPEC_PATH = REPO_ROOT / "doc" / "assets" / "openapi-v1.json"
OUTPUT_PATH = REPO_ROOT / "doc" / "API-Reference.md"

METHOD_ORDER = ["get", "post", "put", "patch", "delete"]
HTTP_METHODS = set(METHOD_ORDER)

# Content types the ASP.NET serializer advertises but that nobody should send.
NOISE_CONTENT_TYPES = {"text/plain", "text/json"}


def ref_name(schema: dict) -> str | None:
    """Return the schema name for a $ref, or None for inline schemas."""
    ref = schema.get("$ref")
    if isinstance(ref, str) and "/" in ref:
        return ref.rsplit("/", 1)[-1]
    return None


def type_name(schema: dict) -> str:
    """Human readable type for a schema fragment.

    ASP.NET emits unions like ["integer", "string"] for int bindings; the first
    entry is the meaningful one.
    """
    name = ref_name(schema)
    if name:
        return f"[{name}](#{name.lower()})"

    if "allOf" in schema and len(schema["allOf"]) == 1:
        return type_name(schema["allOf"][0])

    declared = schema.get("type")
    if isinstance(declared, list):
        declared = declared[0] if declared else None
    if declared is None:
        return "any"
    if declared == "array":
        return f"{type_name(schema.get('items', {}))}[]"
    fmt = schema.get("format")
    return f"{declared} ({fmt})" if fmt else declared


def escape(text: str) -> str:
    """Escape the pipe so table cells do not break."""
    return str(text).replace("|", "\\|").replace("\n", " ").strip()


def cell(value: str) -> str:
    """Empty cells render badly; use an em dash."""
    value = escape(value)
    return value if value else "—"


def render_parameters(parameters: list[dict]) -> list[str]:
    if not parameters:
        return []
    lines = [
        "| Name | In | Type | Required | Default |",
        "| ---- | -- | ---- | -------- | ------- |",
    ]
    for param in parameters:
        schema = param.get("schema", {})
        default = schema.get("default")
        lines.append(
            "| `{name}` | {where} | {type} | {required} | {default} |".format(
                name=escape(param.get("name", "")),
                where=cell(param.get("in", "")),
                type=cell(type_name(schema)),
                required="yes" if param.get("required") else "no",
                default=cell("" if default is None else str(default)),
            )
        )
    return lines


def render_responses(responses: dict) -> list[str]:
    if not responses:
        return []
    lines = [
        "| Status | Content type | Schema |",
        "| ------ | ------------ | ------ |",
    ]
    for status in sorted(responses, key=lambda s: (len(s), s)):
        response = responses[status]
        content = response.get("content", {})
        types = [t for t in content if t not in NOISE_CONTENT_TYPES] or list(content)
        if not types:
            lines.append(f"| {escape(status)} | — | no body |")
            continue
        for content_type in types:
            schema = content[content_type].get("schema", {})
            lines.append(
                f"| {escape(status)} | `{escape(content_type)}` | {cell(type_name(schema))} |"
            )
    return lines


def render_request_body(body: dict) -> list[str]:
    if not body:
        return []
    content = body.get("content", {})
    types = [t for t in content if t not in NOISE_CONTENT_TYPES] or list(content)
    lines = []
    for content_type in types:
        schema = content[content_type].get("schema", {})
        required = " (required)" if body.get("required") else ""
        lines.append(f"Request body{required}: `{escape(content_type)}` → {type_name(schema)}")
    return lines


def render_endpoint(path: str, method: str, operation: dict) -> list[str]:
    anchor = path.strip("/").replace("/", "").replace("{", "").replace("}", "").lower()
    lines = [f"### `{method.upper()}` {path} {{ #{anchor} }}", ""]

    summary = (operation.get("summary") or "").strip()
    description = (operation.get("description") or "").strip()
    if summary:
        lines += [summary, ""]
    if description:
        lines += [description, ""]

    lines += render_request_body(operation.get("requestBody", {}))
    # Each block needs a trailing blank line, otherwise consecutive tables are
    # merged into one by the Markdown renderer.
    for block in (
        render_parameters(operation.get("parameters", [])),
        render_responses(operation.get("responses", {})),
    ):
        if block:
            lines += block + [""]
    return lines


def render_schema(name: str, schema: dict) -> list[str]:
    lines = [f"### {name} {{ #{name.lower()} }}", ""]
    description = (schema.get("description") or "").strip()
    if description:
        lines += [description, ""]

    properties = schema.get("properties")
    if not properties:
        lines += [f"Type: `{type_name(schema)}`", ""]
        return lines

    required = set(schema.get("required", []))
    lines += [
        "| Property | Type | Required |",
        "| -------- | ---- | -------- |",
    ]
    for prop_name in sorted(properties):
        prop = properties[prop_name]
        lines.append(
            "| `{name}` | {type} | {required} |".format(
                name=escape(prop_name),
                type=cell(type_name(prop)),
                required="yes" if prop_name in required else "no",
            )
        )
    lines.append("")
    return lines


def build(spec: dict) -> str:
    info = spec.get("info", {})
    title = info.get("title", "API")
    version = info.get("version", "")

    out: list[str] = [
        "# API Reference",
        "",
        "!!! warning \"Generated page — do not edit\"",
        "",
        "    This page is rendered from the OpenAPI specification at",
        "    `doc/assets/openapi-v1.json`. Edit the specification or the",
        "    generator (`tools/gen_api_reference.py`), not this file.",
        "    CI fails if the two drift apart.",
        "",
        f"Specification: `{title}`" + (f", version `{version}`" if version else ""),
        "",
        "For authentication, pagination, error handling and rate limiting see",
        "[the API guide](API.md). This page is the endpoint and data-type reference.",
        "",
    ]

    # ---- endpoint overview ------------------------------------------------
    out += ["## Endpoints", ""]
    endpoints: list[tuple[str, str, dict]] = []
    for path in sorted(spec.get("paths", {})):
        for method in METHOD_ORDER:
            operation = spec["paths"][path].get(method)
            if operation:
                endpoints.append((path, method, operation))

    if endpoints:
        out += ["| Method | Path |", "| ------ | ---- |"]
        for path, method, _ in endpoints:
            anchor = path.strip("/").replace("/", "").replace("{", "").replace("}", "").lower()
            out.append(f"| `{method.upper()}` | [`{path}`](#{anchor}) |")
        out.append("")
    else:
        out += ["_The specification declares no endpoints._", ""]

    # ---- endpoint detail --------------------------------------------------
    for path, method, operation in endpoints:
        out += render_endpoint(path, method, operation)

    # ---- schemas ----------------------------------------------------------
    schemas = spec.get("components", {}).get("schemas", {})
    out += ["## Data types", ""]
    if schemas:
        for name in sorted(schemas):
            out += render_schema(name, schemas[name])
    else:
        out += ["_The specification declares no schemas._", ""]

    return "\n".join(out).rstrip() + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="exit non-zero if the generated page differs from the file on disk",
    )
    args = parser.parse_args()

    if not SPEC_PATH.exists():
        print(f"error: specification not found at {SPEC_PATH}", file=sys.stderr)
        return 1

    spec = json.loads(SPEC_PATH.read_text(encoding="utf-8"))
    rendered = build(spec)

    if args.check:
        current = OUTPUT_PATH.read_text(encoding="utf-8") if OUTPUT_PATH.exists() else ""
        if current != rendered:
            print(
                f"error: {OUTPUT_PATH.relative_to(REPO_ROOT)} is out of date.\n"
                "       run: python3 tools/gen_api_reference.py",
                file=sys.stderr,
            )
            return 1
        print(f"ok: {OUTPUT_PATH.relative_to(REPO_ROOT)} matches the specification")
        return 0

    OUTPUT_PATH.write_text(rendered, encoding="utf-8")
    print(f"wrote {OUTPUT_PATH.relative_to(REPO_ROOT)} ({len(rendered)} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
