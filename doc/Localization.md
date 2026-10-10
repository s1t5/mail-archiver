# 🌍 Language and Localization

[← Back to Documentation Index](index.md)

## 📋 Overview

The Mail Archiver interface is available in **11 languages**. The language is a
**per-browser preference** stored in a cookie — it is not tied to a user account,
and it never affects the archived mail itself: messages are always stored exactly
as they were received.

## 🌐 Supported languages

| Language | Code |
|----------|------|
| English (default) | `en` |
| English (GB) | `en-GB` |
| Slovenščina | `sl` |
| Deutsch | `de` |
| Italiano | `it` |
| Français | `fr` |
| Español | `es` |
| Nederlands | `nl` |
| Русский | `ru` |
| Magyar | `hu` |
| Polski | `pl` |

**English is the default and the fallback.** Anything a translation does not cover
is shown in English, so a partly translated language never leaves you with an
empty label.

## 🔄 Switching the language

1. Click your **username** in the top-right corner to open the user menu.
2. Under **Language**, pick a language from the list — the page reloads
   immediately in that language.

The choice is written to a cookie that is valid for **one year** and applies to
this browser only. Other users, and the same user in another browser or on
another device, keep their own setting. Because the cookie outlives the session,
the language is still selected after you sign out and back in.

## 🧩 What the language setting does — and does not — affect

- **Translated:** all interface text — menus, buttons, form labels, validation and
  error messages, job status pages and the access log's action descriptions.
- **Not translated:** archived mail. Bodies, subjects and attachments are stored
  and displayed byte-for-byte as received.
- **Search:** the query syntax is the same in every language; see
  [Mail search](Search.md). Changing the language does not change how mail is
  stored or indexed.
- **Timestamps:** dates are displayed in the browser's local time zone and
  formatted for the selected culture. To pin the display time zone instance-wide
  instead, set `TimeZone__DisplayTimeZoneId` (IANA identifier, e.g.
  `Europe/Berlin`) — see [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ For contributors: adding or changing translations

The translated strings live in `Resources/`:

| File | Purpose |
|------|---------|
| `SharedResource.resx` | Neutral resource file — **English**, 1148 keys |
| `SharedResource.<culture>.resx` | One file per language, e.g. `SharedResource.de.resx` |

Rules that follow from how .NET resolves resources here:

1. **Add new keys to the neutral file first**, then to the language files. A key
   that is missing from a translation falls back to the English text — nothing
   breaks, the page just stays partly English.
2. **A key that exists only in a translation file is dead weight.** It is never
   looked up, because lookup starts at the neutral file. (At the time of writing,
   `DeleteAccountAllEmailsButton` and `Sign Out` exist only in some translations.)
3. **`en-GB` is a partial override, by design.** It defines only 245 of the 1148
   keys and inherits the rest from the neutral English file — which is why it
   reads like English except where British wording or date formats differ.
   German, Spanish, French, Hungarian, Italian, Dutch, Polish, Russian and
   Slovenian are currently complete.
4. **Adding a whole language** needs three things: a new
   `SharedResource.<culture>.resx`, an entry in the culture list in `Program.cs`
   (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`),
   and an `<option>` in the language dropdown in
   `Views/Shared/_UserInfo.cshtml`. Without the last two the file is never used.

See [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md)
for the contribution process, coding standards and how to submit a pull request.

## 🔗 Related Documentation

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — query syntax
- [User Management](UserManagement.md) — roles and permissions
