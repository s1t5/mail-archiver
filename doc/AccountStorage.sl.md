# 💾 Prikaz shrambe po računu {#per-account-storage-display}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Tabela "Account Overview" v nadzorni plošči in tabela "Show All" v MailAccounts vključujeta stolpec **Storage**. Ta stolpec prikazuje, koliko prostora v bazi podatkov uporablja vsak račun — vključno z **vsemi polji vsake arhivirane pošte** in **vsega prilog**.

## ⚙️ Kako deluje {#how-it-works}

Vrednosti shrambe so **predhodno shranjene (cached)** v tabeli baze podatkov `AccountStorageCache`. To pomeni, da spletne strani berejo že izračunane vrednosti namesto da bi ob vskem odpiranju nadzorne plošče ali seznama računov izvajale zahtevno poizvedbo. Predhodno shranjene vrednosti posodablja neodvisna storitev v ozadju (`AccountStorageRefreshService`).

Za vsak račun storitev zahteva od PostgreSQL, da izračuna `pg_column_size` za vsak stolpec vrstovnega zapisa računa v tabeli `ArchivedEmails`. To zajema **vsa polja pošte** (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, itd.), vključno s PostgreSQL kompresijo in shranjevanjem zunaj vrstice (TOAST). Velikost vrednosti zunaj vrstice se bere iz njenega TOAST kazalnika, zato izračun skenira le samo tabelo in nikoli ne nalaga telesa pošte. Del z prilogami se doda ločeno kot logična suma `EmailAttachment.Size`.

Storitev za osveževanje posodobi predhodno shranjene vrednosti v treh primerih:

1. **Startup backfill** — Po novi namestitvi ali po posodobitvi na različico, ki uvaja to funkcijo, vsak račun začne brez predhodno shranjene vrednosti. Storitev nato eno za drugo obdeluje vse račune in izračuna prvo vrednost.
2. **Daily refresh** — Enkrat dnevno ob nastavljenem času (privzeto `02:30`) storitev ponovno izračuna vse račune kot varnostno neto.
3. **Immediate refresh** — Po sinhronizaciji pošte, uvozu EML, uvozu MBOX ali izbrisu zaradi politike zadržanja podatkov (retention deletion) se prizadani račun takoj ponovno izračuna, da ostane prikazana vrednost aktualna.

## 🤔 Zakaj se sprva prikaže "0 B" {#why-0-b-is-shown-at-first}

Ko je funkcija prvič vključena ali ko je ustvarjen nov račun, vrstica za predhodno shranjeno vrednost tega računa še ne obstaja. Storitev za osveževanje deluje v ozadju in račune izračunava **eno za drugo**, z majhnim časovnim zamikom med vsakim računom, da zelo veliki arhivi ne preobremenijo baze podatkov.
Dokler storitev računa ni obdelala, uporabniški vmesnik prikazuje **"0 B"** kot nadomestno vrednost. To je popolnoma normalno. Takoj ko storitev v ozadju zaključi z računom, se prava vrednost pri naslednji nalaganju ali osvežitvi strani prikaže.
Pri velikih obstoječih arhivih lahko backfill traja več minut ali celo dlje. Storitev sledi svojemu napredku v namenski tabeli (`AccountStorageBackfillState`), zato, če aplikacija med postopkom backfill prekine, preprosto nadaljuje tam, kjer je prekinila — noben račun se ne izračuna dvakrat.

## 🔧 Konfiguracija {#configuration}

Funkcijo je mogoče prilagoditi v `appsettings.json` ali prek okoljskih spremenljivk. Za vse razpoložljive možnosti, vključno s tem, poglejte razdelek **Account Storage Settings** v datoteki [Setup.md](Setup.md):

- `AccountStorage__Enabled`: Vklopi ali izklopi storitev osveževanja (privzeto `true`).
- `AccountStorage__DailyExecutionTime`: Čas za dnevno popolno osveževanje (privzeto `02:30`).
- `AccountStorage__BackfillDelayMs`: Premak med računi med startup backfill (privzeto `5000` ms).
- `AccountStorage__RefreshBatchDelayMs`: Premak med računi med dnevnim osveževanjem (privzeto `1000` ms).
- `AccountStorage__CommandTimeoutSeconds`: Časovni omejitev (timeout) ukazov baze podatkov za izračun shrambe na račun (privzeto `300` sekund). Povečajte to vrednost za zelo velike arhive, kjer skeniranje `pg_column_size` traja dlje.

> 💡 **Nasvet**: Za delovanje te funkcije **ni** potrebno vključiti `DatabaseMaintenance__Enabled`. Storitev osveževanja shrambe deluje neodvisno.
