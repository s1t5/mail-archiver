# 🌍 Jezik in lokalizacija {#language-and-localization}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Vmesnik Mail Archiverja je na voljo v **14 jezikih**. Jezik je **nastavitev na nivoju brskalnika**, ki je shranjena v piškotku — ni povezan z uporabniškim računom in nikoli ne vpliva na arhivirano pošto: sporočila so vedno shranjena točno tako, kot so bila prejeta.

## 🌐 Podprti jeziki {#supported-languages}

| Jezik | Koda |
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
| Português (Brasil) | `pt-BR` |
| 日本語 | `ja` |
| 简体中文 | `zh` |

**Angleščina je privzeta in službi kot nadgrajevanje (fallback).** Vse, česar prevod ne pokriva, se prikaže v angleščini, zato vas delno preveden jezik nikoli ne pusti z praznim oznakim.

## 🔄 Menjava jezika {#switching-the-language}

1. Kliknite svoj **username** v zgornjem desnem kotu, da odprete uporabniški meni.
2. Pod **Language** izberite jezik s seznama — stran se takoj ponovno naloži v izbranem jeziku.

Izbira je zapisana v piškotek, ki velja za **eno leto** in velja le za ta brskalnik. Drugi uporabniki in isti uporabnik v drugem brskalniku ali na drugi napravi ohranijo svoje nastavitve. Ker piškotek traja dlje kot uporabniška vaja, bo jezik še vedno izbran tudi po odjavi in ponovni prijavi.

## 🧩 Na kaj nastavitev jezika vpliva — in česa ne — {#what-the-language-setting-does-and-does-not-affect}

- **Prevedeno:** ves besedilo v vmesniku — meniji, gumbi, oznake obrazcev, sporočila o validaciji in napakah, strani s statusom opravil in opisi dejanj v dnevniku dostopa.
- **Ne je prevedeno:** arhivirana pošta. Vsebina, zadeve in priloge so shranjene in prikazane bit za bit, kot so bile prejete.
- **Iskanje:** sintaksa poiskavo je enaka v vsakem jeziku; poglejte [Mail search](Search.md). Menjava jezika ne spremeni načina, kako je pošta shranjena ali indeksirana.
- **Žični časovni žig (Timestamps):** datumi so prikazani v lokalnem časovnem pasu brskalnika in oblikovani za izbrano kulturo. Če želite časovno pas za prikaz določiti na ravni celotne instance, nastavite `TimeZone__DisplayTimeZoneId` (IANA identifikator, npr. `Europe/Berlin`) — poglejte [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ Za sodelovalce: dodajanje ali spreminjanje prevodov {#for-contributors-adding-or-changing-translations}

Prevedeni nizovi se nahajajo v `Resources/`:

| Datoteka | Namen |
|------|---------|
| `SharedResource.resx` | Nevtralna datoteka virov — **English**, 1148 ključev |
| `SharedResource.<culture>.resx` | Ena datoteka na jezik, npr. `SharedResource.de.resx` |

Pravila, ki izhajajo iz načina, kako .NET tukaj razrešuje vire:

1. **Najprej dodajte nove ključe v nevtralno datoteko**, nato pa v datoteke jezikov. Ključ, ki ga v prevodu ni, se preklopi na angleško besedilo — nič ne zavira, stran pa ostane delno v angleščini.
2. **Ključ, ki obstaja le v datoteki prevoda, je nepotreben breme.** Ni nikoli iskan, saj se iskanje začne pri nevtralni datoteki. (Ob času pisanja `DeleteAccountAllEmailsButton` in `Sign Out` obstajata le v nekaterih prevodih, polska datoteka pa vsebuje tudi preostale ključe oblikovalca: `Bitmap1`, `Icon1`, `Name1`.)
3. **`en-GB` je po zasnovi delno prepričanje (override).** Definira le 245 od 1148 ključev in ostale črpi iz nevtralne angleške datoteke — zato besedilo zveni kot angleščina, razen tam, kjer se razlikuje britanski slog ali formati datumov. Kitajski (poenostavljen), nemški, španski, francozski, madžarski, italijanski, japonski, nizozemski, poljski, portugalski (Brazil) in ruski ter slovenščina so trenutno popolni.
4. **Dodajanje celotnega jezika** zahteva tri stvari: novo `SharedResource.<culture>.resx`, vnos v seznam kultur v `Program.cs` (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`) in `<option>` v spustnem meniju jezika v `Views/Shared/_UserInfo.cshtml`. Brez zadnjih dveh datoteka nikoli ni v uporabi.

Za postopek prispevanja, standarde kodiranja in navodila za oddajo pull requesta si oglejte [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md).

## 🔗 Sorodna dokumentacija {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — sintaksa poiskave
- [User Management](UserManagement.md) — vloge in dovoljenja
