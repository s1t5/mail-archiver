# 💾 Per-Account Storage Display

[← Back to Documentation Index](Index.md)

## 📋 Overview

The Dashboard "Account Overview" table and the MailAccounts "Show All" table include a **Storage** column. This column shows how much database space each account is using — including **all fields of every archived mail** plus **all attachments**.

## ⚙️ How it works

Storage values are **cached** in the `AccountStorageCache` database table. This means the web pages read pre-computed values instead of running an expensive query each time you open the Dashboard or the account list. The cache is kept up-to-date by an independent background service (`AccountStorageRefreshService`).

For each account, the service asks PostgreSQL to calculate the size of every row in the `ArchivedEmails` table using `pg_column_size`. This single call covers **all fields of a mail** at once (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, etc.), including PostgreSQL compression and out-of-line storage (TOAST). The attachment portion is added separately as the logical sum of `EmailAttachment.Size`.

The refresh service updates the cache in three situations:

1. **Startup backfill** — After a new installation or after updating to the version that introduces this feature, every account starts with no cached value. The service walks through all accounts one by one and computes the first value.
2. **Daily refresh** — Once per day at the configured time (default `02:30`) the service recomputes all accounts as a safety net.
3. **Immediate refresh** — After a mail sync, EML import, MBOX import, or retention deletion, the affected account is recomputed right away so the displayed value stays current.

## 🤔 Why "0 B" is shown at first

When the feature is first enabled, or when a new account is created, the cache row for that account does not yet exist. The refresh service runs in the background and computes accounts **one by one**, with a small delay between each account so that very large archives do not overload the database.
Until the service has processed an account, the UI shows **"0 B"** as a placeholder. This is completely normal. As soon as the background service finishes that account, the real value appears the next time the page is loaded or refreshed.
For large existing archives the backfill can take several minutes or longer. The service tracks its progress in a dedicated table (`AccountStorageBackfillState`), so if the application is restarted in the middle of the backfill it simply continues where it left off — no account is computed twice.

## 🔧 Configuration

The feature can be tuned in `appsettings.json` or via environment variables. See the **Account Storage Settings** section in [Setup.md](Setup.md) for all available options, including:

- `AccountStorage__Enabled`: Turn the refresh service on or off (`true` by default).
- `AccountStorage__DailyExecutionTime`: Time for the daily full refresh (`02:30` by default).
- `AccountStorage__BackfillDelayMs`: Pause between accounts during the startup backfill (`5000` ms by default).
- `AccountStorage__RefreshBatchDelayMs`: Pause between accounts during the daily refresh (`1000` ms by default).

> 💡 **Tip**: You do **not** need to enable `DatabaseMaintenance__Enabled` for this feature to work. The storage refresh service runs independently.
