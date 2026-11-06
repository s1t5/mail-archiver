## ✨ New Features
- **Pluggable Storage for Original Messages** - `RawMessages:Storage` selects where the original `.eml` is kept: `postgres` (default, in the database) or `filesystem` (gzip files next to the archive, metadata and checksum stay in the database). A migration command moves existing messages between the two backends in either direction, verifying every checksum before it touches a copy.

## ⚙️ Improvements
- **EML Export Serves the Stored Original** - The Export EML button on the email detail page returns the original message byte for byte when one is stored, instead of always rebuilding it from the archived fields. Emails without an original still export as before, and the filename and response headers say which of the two forms was served. A checksum mismatch aborts the export rather than quietly returning the reconstruction.
- **Configurable OIDC Response Mode** - `OAuth__ResponseMode` accepts `query`, `fragment` or `form_post`. The default stays `form_post`; providers that do not support it, such as Synology SSO Server, work with `query`.

## 🐛 Bug Fixes
- **OIDC Login Failures Show a Readable Message** - A missing or unusable claim no longer ends in an HTTP 500. The user is returned to the login page with a localized explanation and the reason is logged at error level.
- **OIDC Name Claim Fallback** - The display name is now also read from the `username` claim, which some providers return as the only name-like value. Empty or non-string claims no longer stop the fallback chain.
