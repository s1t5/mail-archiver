# 🔒 Security Policy

The Mail-Archiver project takes security seriously. This document describes how to report
security vulnerabilities and how they are handled.

## 📌 Reporting a Vulnerability

If you believe you have found a security vulnerability in Mail-Archiver, **please do not open
a public GitHub issue**. Instead, report it responsibly to:

📧 **Email:** mail@s1t5.dev

### Information to include

To help us understand and reproduce the issue quickly, please provide as much of the following as
possible:

- Affected **version** or commit / branch (a release tag, or a Docker image tag)
- Step-by-step instructions to **reproduce** the issue
- A minimal **proof of concept** (if applicable)
- The **impact** you believe the vulnerability has (e.g. data exposure, privilege escalation, DoS)
- Any relevant logs, configuration snippets, or environment details (please redact secrets)

### PGP-encrypted reports (optional)

You may encrypt your report using the maintainer's OpenPGP public key, available on
[keys.openpgp.org](https://keys.openpgp.org/search?q=mail@s1t5.dev):

### Response and disclosure

- We will acknowledge your report on a **best-effort** basis and as quickly as we reasonably can.
  There is **no guaranteed response time or SLA**.
- We treat all incoming vulnerability reports in confidence and coordinate a **responsible
  disclosure** of the issue — including credit to the reporter — once a fix is available.
- Please do not publicly disclose the vulnerability before a coordinated release is published.

## ✅ Supported Versions

| Version / Branch       | Security Fixes |
| ---------------------- | -------------- |
| Latest release (tag)   | ✅ Supported   |
| Older releases         | ❌ Not supported |

## 🎯 Scope

### In scope

- Source code in this repository (the ASP.NET Core MVC app, EF Core migrations and helpers,
  background services, CLI import).
- The provided `docker-compose.yml`.
- Default values shipped in `appsettings.json`.

### Out of scope

- Vulnerabilities in upstream third-party dependencies (MailKit, Microsoft.Graph,
  Npgsql, EF Core, etc.). Please report those **upstream** to the respective maintainers.
- Issues arising from insecure production deployments — e.g. a misconfigured reverse proxy,
  an exposed database port, weak OAuth client secrets, or a missing HTTPS termination. See the
  configuration guides under `doc/` for hardening guidance.
- Social engineering, physical attacks, or denial-of-service via sheer network volume against
  infrastructure we do not control.

## 🔐 Secure Configuration

Security in Mail-Archiver depends strongly on how it is deployed. Please review the relevant
guides before exposing the application publicly:

- **Setup & hardening:** [doc/Setup.md](doc/Setup.md)
- **Reverse proxy / TLS:** [doc/ReverseProxy.md](doc/ReverseProxy.md)
- **OpenID Connect authentication:** [doc/OIDC_Implementation.md](doc/OIDC_Implementation.md)
- **Microsoft 365 app registration:** [doc/AZURE_APP_REGISTRATION_M365.md](doc/AZURE_APP_REGISTRATION_M365.md)

Important defaults to be aware of:

- `appsettings.json` ships with **default Docker-oriented credentials**. Override the connection
  string, DataProtection key path, and any other secrets in production via environment variables,
  user secrets, or a secure secret store.
- `appsettings.Development.json` is **gitignored** and intended only for local development.
- Enforce **HTTPS** in production. Do not expose the Kestrel HTTP port directly; terminate TLS at
  a reverse proxy (see `doc/ReverseProxy.md`).

## 🚫 What We Will Not Do

- We do **not** offer a bug bounty or monetary rewards — Mail-Archiver is a community project.
- We do **not** reverse-engineer or audit deployments we do not control.
- We will **not** honor reports that are spam, marketing, or clearly unrelated to security.

## 📄 License

Mail-Archiver is licensed under the **GNU General Public License v3.0** — see the
[LICENSE](LICENSE) file. By submitting a security report you agree that any coordinated fix and
advisory may be published under the same license.