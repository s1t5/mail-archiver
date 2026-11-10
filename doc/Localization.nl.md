# 🌍 Taal en lokalisatie {#language-and-localization}

[← Terug naar de documentatie-index](index.md)

## 📋 Overzicht {#overview}

De Mail Archiver-interface is beschikbaar in **14 talen**. De taal is een
**per-browser voorkeur** die in een cookie wordt opgeslagen — het is niet gekoppeld aan een gebruikersaccount,
en het heeft nooit invloed op de gearchiveerde e-mail zelf: berichten worden altijd precies
opgeslagen zoals ze zijn ontvangen.

## 🌐 Ondersteunde talen {#supported-languages}

| Taal | Code |
|----------|------|
| Engels (standaard) | `en` |
| Engels (GB) | `en-GB` |
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

**Engels is de standaard en de fallback.** Alles wat een vertaling niet dekt,
wordt in het Engels weergegeven, zodat een gedeeltelijk vertaalde taal je nooit
met een leeg label achterlaat.

## 🔄 De taal wijzigen {#switching-the-language}

1. Klik op je **gebruikersnaam** in de rechterbovenhoek om het gebruikersmenu te openen.
2. Kies onder **Language** een taal uit de lijst — de pagina wordt
   onmiddellijk in die taal opnieuw geladen.

De keuze wordt opgeslagen in een cookie die **één jaar** geldig is en alleen geldt voor
deze browser. Andere gebruikers, en dezelfde gebruiker in een andere browser of op
een ander apparaat, behouden hun eigen instelling. Omdat de cookie de sessie overleeft,
is de taal nog steeds geselecteerd nadat je bent uitgelogd en opnieuw bent ingelogd.

## 🧩 Wat de taalinstelling wel — en niet — beïnvloedt {#what-the-language-setting-does-and-does-not-affect}

- **Vertaald:** alle interface-tekst — menu's, knoppen, formulierlabels, validatie en
  foutmeldingen, statuspagina's van taken en de actiebeschrijvingen in het auditlogboek.
- **Niet vertaald:** gearchiveerde e-mail. Berichten, onderwerpen en bijlagen worden
  byte-voor-byte opgeslagen en weergegeven zoals ontvangen.
- **Zoeken:** de zoekopdrachtsyntaxis is in elke taal hetzelfde; zie
  [Mail search](Search.md). Het wijzigen van de taal verandert niets aan de manier waarop e-mail wordt
  opgeslagen of geïndexeerd.
- **Tijdstempels:** datums worden weergegeven in de lokale tijdzone van de browser en
  geformatteerd voor de geselecteerde cultuur. Om de weergavetijdzone instance-breed vast te leggen, stel je `TimeZone__DisplayTimeZoneId` in (IANA-identifier, bijv.
  `Europe/Berlin`) — zie [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ Voor bijdragers: vertalingen toevoegen of wijzigen {#for-contributors-adding-or-changing-translations}

De vertaalde strings staan in `Resources/`:

| Bestand | Doel |
|------|---------|
| `SharedResource.resx` | Neutraal resourcebestand — **Engels**, 1148 keys |
| `SharedResource.<culture>.resx` | Eén bestand per taal, bijv. `SharedResource.de.resx` |

Regels die voortvloeien uit de manier waarop .NET resources hier oplost:

1. **Voeg eerst nieuwe keys toe aan het neutrale bestand**, en dan pas aan de taalbestanden. Een key
   die ontbreekt in een vertaling valt terug op de Engelse tekst — er gaat niets
   stuk, de pagina blijft gewoon gedeeltelijk in het Engels.
2. **Een key die alleen in een vertaalbestand bestaat, is overbodig.** Deze wordt nooit
   opgezocht, omdat de zoekopdracht begint bij het neutrale bestand. (Op het moment van schrijven,
   bestaan `DeleteAccountAllEmailsButton` en `Sign Out` alleen in sommige vertalingen,
   en het Poolse bestand bevat ook overgebleven designer keys: `Bitmap1`, `Icon1`,
   `Name1`.)
3. **`en-GB` is een gedeeltelijke override, bij ontwerp.** Het definieert slechts 245 van de 1148
   keys en erft de rest van het neutrale Engelse bestand — wat verklaart waarom het
   als Engels leest, behalve waar Britse bewoordingen of datumformaten afwijken.
   Chinees (Vereenvoudigd), Duits, Spaans, Frans, Hongaars, Italiaans,
   Japans, Nederlands, Pools, Portugees (Brazilië), Russisch en Sloveens zijn
   momenteel volledig.
4. **Het toevoegen van een volledige taal** vereist drie dingen: een nieuwe
   `SharedResource.<culture>.resx`, een vermelding in de cultuurlijst in `Program.cs`
   (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`),
   en een `<option>` in het taaldropdownmenu in
   `Views/Shared/_UserInfo.cshtml`. Zonder de laatste twee wordt het bestand nooit gebruikt.

Zie [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md)
voor het bijdrageproces, codestandaarden en hoe je een pull request indient.

## 🔗 Gerelateerde documentatie {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — zoekopdrachtsyntaxis
- [User Management](UserManagement.md) — rollen en machtigingen
