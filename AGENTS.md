# AGENTS.md — Mail-Archiver

## Stack & Structure

- **Single .NET 10 ASP.NET Core MVC app** (`MailArchiver.csproj`). One solution file (`MailArchiver.sln`).
- **PostgreSQL** via EF Core (`Npgsql.EntityFrameworkCore.PostgreSQL`). Schema: `mail_archiver`.
- **Provider-based email services**: IMAP (`MailKit`), M365 (`Microsoft.Graph`), IMPORT. Routed by `ProviderEmailServiceFactory`.
- **several languages** localized via `.resx` in `Resources/`. `SharedResource` is the marker class for `IStringLocalizer`.

## Run & Build

- **Run locally**: `dotnet run` → http://localhost:5179 (or https://localhost:7095). Uses `Properties/launchSettings.json`.
- **Build / publish**: `dotnet publish -c Release -o out` (used by Dockerfile).
- **Run tests**: `dotnet test` (xUnit test project at `tests/MailArchiver.Tests`). No CI step exists yet, run manually before finishing work that touches tested helpers.
- **Docker**: `docker compose up -d` spins up the app + PostgreSQL. Dockerfile exposes port 5000. `ASPNETCORE_ENVIRONMENT=Production` inside container.

## Database & Migrations

- Migrations run **automatically on startup** (Program.cs). There is also a custom `EnsureMigrationsHistoryTableExists` helper for completely fresh databases.
- Migration naming convention: `YYYYMMDDHHMMSS_<FeatureName>.cs`, using the UTC timestamp and a short PascalCase description of the change instead of a version number (e.g., `20261019130558_AddMailcowInstances.cs`). Existing migrations keep their historical names.
- Migration style: use raw SQL with `DO $$` blocks checking `information_schema`/`pg_indexes` existence before creating/dropping. Use schema-qualified names: `mail_archiver."TableName"`. Include `COMMENT ON` for tables/columns. Implement both `Up()` and `Down()`.
- On startup the app also executes `CREATE EXTENSION IF NOT EXISTS citext;`.

## Architecture Quirks

- **Singleton hosted services**: several background services (e.g., `BatchRestoreService`, `MBoxImportService`, `EmlImportService`, `ExportService`, `EmailDeletionService`, `DatabaseMaintenanceService`) are registered as **both** `AddSingleton<T>()` and `AddHostedService<T>()` pointing to the **same instance**. Do not change this pattern.
- `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);` is set at the top of `Program.cs`.
- `System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);` is required for MailKit/MimeKit encoding support.
- Attachment deduplication is handled by `AttachmentDeduplicationInterceptor` (EF interceptor) and `AttachmentDeduplicationBackgroundService` (resumable background migration for existing data).

## CLI Commands

The app supports headless import via command-line arguments (processed before the web pipeline starts):

```bash
dotnet run -- --import-mbox --file /path/to/archive.mbox --account-id 1 --folder INBOX
dotnet run -- --import-eml --file /path/to/archive.zip --account-id 1
```

- `--folder` is optional (defaults to `INBOX`).
- File paths must be under directories listed in `LocalImport.AllowedPaths` (configurable in `appsettings.json`).

## Configuration

- `appsettings.Development.json` is **gitignored** (`.gitignore: *.Development.json`). It exists locally but is not tracked. For local dev, create it if needed (e.g., pointing to a local PostgreSQL instance).
- `appsettings.json` contains default Docker-oriented connection strings and credentials.
- `DataProtection:KeyPath` defaults to `/app/DataProtection-Keys` in containers; in dev it can be `./DataProtection-Keys`.
- **New configuration parameters** added to `appsettings.json` must also be documented in `doc/setup.md`.

## Contributing

- Third-party code changes **require prior coordination via email** (`mail@s1t5.dev`) before opening PRs. See `CONTRIBUTING.md`.
- Documentation lives in `doc/`.

## Release Notes (nextrelease.md)

After every bugfix, extension, or improvement, create or update an entry in `nextrelease.md` in the project root. If the file does not exist, create it. The entries should be always in english! Keep the description short!

Categorize each change into one of the following sections and follow this schema:

```markdown
## ✨ New Features
- **Title** - Short description of the new feature

## ⚙️ Improvements
- **Title** - Short description of the improvement

## 🐛 Bug Fixes
- **Title** - Short description of the bug fix
```

Example:

```markdown
## ✨ New Features
- **Bandwidth Tracking & Rate Limiting** - Automatic detection and handling of daily download quotas (e.g. 2500 MB/day limit)
- **Sync Checkpoint System** - Resumable synchronization with persistent checkpoints

## ⚙️ Improvements
- **Enhanced IMAP Sync Logic** with integrated rate limiting
- **Database Maintenance** for bandwidth and checkpoint data
- **Folder Panel** in archive page can now be collapsed

## 🐛 Bug Fixes
- **Dark Mode for Folder Panel**
```

## CI / Automation

- GitHub Actions only publish Docker images to Docker Hub and GHCR (`push-to-github-packages.yaml`, `push-dev-to-registries.yaml`). There is no CI build, test, or lint step.

## What is NOT configured

- No `.editorconfig`, no `global.json`, no `Directory.Build.props`, no `dotnet-tools.json`, no StyleCop/ruleset files.
- No type-checker or linter beyond the C# compiler. Formatting follows the existing code style.