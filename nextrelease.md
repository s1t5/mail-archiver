## ✨ New Features
- **Pluggable Storage for Original Messages** - `RawMessages:Storage` selects where the original `.eml` is kept: `postgres` (default, in the database) or `filesystem` (gzip files next to the archive, metadata and checksum stay in the database). A migration command moves existing messages between the two backends in either direction, verifying every checksum before it touches a copy.

## ⚙️ Improvements
- **EML Export Serves the Stored Original** - The Export EML button on the email detail page returns the original message byte for byte when one is stored, instead of always rebuilding it from the archived fields. Emails without an original still export as before, and the filename and response headers say which of the two forms was served. A checksum mismatch aborts the export rather than quietly returning the reconstruction.
- **Configurable OIDC Response Mode** - `OAuth__ResponseMode` accepts `query`, `fragment` or `form_post`. The default stays `form_post`; providers that do not support it, such as Synology SSO Server, work with `query`.

## 🐛 Bug Fixes
- **OAuth-Only Mode Is Now Enforced Server-Side** - With `OAuth__DisablePasswordLogin=true` the server now rejects every password POST to `/Auth/Login` instead of only hiding the login form. Before this fix a direct POST with a valid local password still signed the user in and bypassed the configured sign-in policy.
- **Bootstrap Password Always Forces a Change** - Signing in with the admin password from `Authentication:Password` now forces a password change on every installation, not only on a fresh one. The previous condition also required an empty archive, so existing installations kept a working admin login with the shipped default password.
- **Raw Message Storage Migration Was Never Applied** - `20261106090000_RawMessageStorageBackends` was missing its `[Migration]` attribute, so EF Core never discovered it and the `StorageType`/`StoragePath` columns were never created. Databases migrated with 2610.1 could not store filesystem-backed raw messages.
- **Build Output No Longer Nests Recursively** - The test project's `bin`/`obj` output was copied into the web app's output directory on every build, nesting one level deeper each time. The output tree grew to tens of gigabytes and made builds and test runs slow or memory-starved.
- **OIDC Login Failures Show a Readable Message** - A missing or unusable claim no longer ends in an HTTP 500. The user is returned to the login page with a localized explanation and the reason is logged at error level.
- **OIDC Name Claim Fallback** - The display name is now also read from the `username` claim, which some providers return as the only name-like value. Empty or non-string claims no longer stop the fallback chain.
