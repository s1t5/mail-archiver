# 🌍 Lingua e localizzazione {#language-and-localization}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

L'interfaccia di Mail Archiver è disponibile in **14 lingue**. La lingua è una
**preferenza per singolo browser** memorizzata in un cookie — non è legata a un Account utente
e non influisce mai sulla posta archiviata stessa: i messaggi sono sempre memorizzati esattamente
come sono stati ricevuti.

## 🌐 Lingue supportate {#supported-languages}

| Lingua | Codice |
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

**L'inglese è la lingua predefinita e di fallback.** Qualsiasi cosa non coperta da una traduzione
viene mostrata in inglese, quindi una lingua parzialmente tradotta non ti lascerà mai con un
etichetta vuota.

## 🔄 Cambiare lingua {#switching-the-language}

1. Clicca sul tuo **nome utente** nell'angolo in alto a destra per aprire il menu utente.
2. Sotto **Language**, scegli una lingua dall'elenco — la pagina si ricarica
   immediatamente in quella lingua.

La scelta viene scritta in un cookie che è valido per **un anno** e si applica
solo a questo browser. Altri utenti, e lo stesso utente in un altro browser o su
un altro dispositivo, mantengono le proprie impostazioni. Poiché il cookie sopravvive alla sessione,
la lingua viene mantenuta anche dopo il logout e il successivo login.

## 🧩 Cosa influenza (e cosa non influenza) l'impostazione della lingua {#what-the-language-setting-does-and-does-not-affect}

- **Tradotto:** tutto il testo dell'interfaccia — menu, pulsanti, etichette dei moduli, messaggi di
  validazione ed errore, pagine di stato dei job e le descrizioni delle azioni nel registro di controllo.
- **Non tradotto:** la posta archiviata. Corpi, oggetti e Allegati sono memorizzati
  e visualizzati byte per byte come ricevuti.
- **Ricerca:** la sintassi della query è la stessa in ogni lingua; vedi
  [Mail search](Search.md). Cambiare la lingua non cambia il modo in cui la posta viene
  memorizzata o indicizzata.
- **Timestamp:** le date sono visualizzate nel fuso orario locale del browser e
  formattate per la cultura selezionata. Per fissare il fuso orario di visualizzazione a livello di istanza,
  imposta `TimeZone__DisplayTimeZoneId` (identificatore IANA, ad es.
  `Europe/Berlin`) — vedi [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ Per i collaboratori: aggiungere o modificare le traduzioni {#for-contributors-adding-or-changing-translations}

Le stringhe tradotte risiedono in `Resources/`:

| File | Scopo |
|------|---------|
| `SharedResource.resx` | File di risorse neutro — **English**, 1148 chiavi |
| `SharedResource.<culture>.resx` | Un file per lingua, ad es. `SharedResource.de.resx` |

Regole derivanti da come .NET risolve le risorse in questo contesto:

1. **Aggiungi prima le nuove chiavi al file neutro**, poi ai file linguistici. Una chiave
   mancante in una traduzione utilizza il testo inglese come fallback — nulla si
   rompe, la pagina rimane solo parzialmente in inglese.
2. **Una chiave che esiste solo in un file di traduzione è un peso morto.** Non viene mai
   cercata, perché la ricerca parte dal file neutro. (Al momento della stesura,
   `DeleteAccountAllEmailsButton` e `Sign Out` esistono solo in alcune traduzioni,
   e il file polacco contiene anche chiavi residue del designer: `Bitmap1`, `Icon1`,
   `Name1`.)
3. **`en-GB` è un override parziale, per progettazione.** Definisce solo 245 delle 1148
   chiavi e eredita le restanti dal file inglese neutro — motivo per cui
   appare in inglese, tranne dove differiscono la terminologia britannica o i formati di data.
   Cinese (Semplificato), Tedesco, Spagnolo, Francese, Ungherese, Italiano,
   Giapponese, Olandese, Polacco, Portoghese (Brasile), Russo e Sloveno sono
   attualmente completi.
4. **Aggiungere un'intera lingua** richiede tre cose: un nuovo
   `SharedResource.<culture>.resx`, una voce nell'elenco delle culture in `Program.cs`
   (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`),
   e un `<option>` nel menu a discesa della lingua in
   `Views/Shared/_UserInfo.cshtml`. Senza le ultime due, il file non viene mai utilizzato.

Consulta [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md)
per il processo di contribuzione, gli standard di codifica e come inviare una pull request.

## 🔗 Documentazione correlata {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — sintassi della query
- [User Management](UserManagement.md) — ruoli e Autorizzazione
