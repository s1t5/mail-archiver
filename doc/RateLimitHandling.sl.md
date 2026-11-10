# 📊 Upravljanje omejitvami hitrosti {#rate-limit-handling}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Veliko ponudnikov e-pošte uvaja omejitve pasovne širine pri dostopu prek IMAP. Te omejitve se običajno gibljejo od nekaj stotih megabajtov do več gigabajtov v 24-urnem obdobju. Pri sinhronizaciji velikih predalov, ki presegajo te omejitve, bi proces sinhronizacije običajno neuspel ali pa bi bil nepopoln.

Mail Archiver vključuje vgrajen sistem za upravljanje omejitev hitrosti, ki samodejno upravlja s temi omejitvami in zagotavlja zanesljivo sinhronizacijo tudi pri velikih predalih.

## ⚙️ Kako deluje {#how-it-works}

### Samodejno sledenje pasovni širini {#automatic-bandwidth-tracking}

Sistem nenehno spremlja količino prenesenih podatkov med IMAP sinhronizacijo. Ko je dosegnięta konfigurirana omejitev pasovne širine:

1. **Sinhronizacija se samodejno ustavi**: Trenutna operacija sinhronizacije se varno prekine.
2. **Napredek je shranjen**: Kontrolne točke (checkpoints) shranijo točno točko, kjer se je sinhronizacija ustavila.
3. **Posodobitve statusa**: Račun v nadzorni plošči prikaže status "Rate-Limited".
4. **Samodejni zagon**: Ko se omejitev ponastavi (običajno po 24 urah), se sinhronizacija nadaljuje od shranjene kontrolne točke.

### Primer: 14 GB predal z dnevno omejitvijo 2,5 GB {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Konfiguracija {#configuration}

### Omogočanje sledenja pasovni širini {#enabling-bandwidth-tracking}

V svoj `appsettings.json` dodajte naslednji razdelek:

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Možnosti konfiguracije {#configuration-options}

| Možnost | Opis | Z умолчанию | Primer |
|--------|-------------|---------|---------|
| `Enabled` | Omogoči/onemogoči sledenje pasovni širini | `false` | `true` |
| `DailyLimitMb` | Dnevna omejitev prenosov v megabajtih | `2500` | `2500` |
| `WarningThresholdPercent` | Stopnja v odstotkih, pri kateri se v dnevnikih izpišejo opozorila | `80` | `80` |
| `PauseHoursOnLimit` | Število ur pavziranja, ko je dosegnięta omejitev | `24` | `24` |

### Nastavitev pravilne omejitve {#setting-the-correct-limit}

Za določitev ustrezne vrednosti `DailyLimitMb`:

1. Preverite dokumentacijo ali pogoje uporabe vašega ponudnika e-pošte.
2. Poiščite omejitve IMAP pasovne širine ali kvote prenosov.
3. Če uporabljate poslovni e-poštni sistem, kontaktirajte svojega e-poštnega administratorja.
4. Če niste prepričani, začnite s konservativno vrednostjo (npr. 2000 MB).
5. Med prvotnimi sinhronizacijami spremljajte dnevnik za opozorila o omejitvah hitrosti.

## 👤 Uporabniška izkušnja {#user-experience}

### Indikatorji na nadzorni plošči {#dashboard-indicators}

Ko je omejitev hitrosti aktivna, račun prikaže:

- **Status**: "Rate-Limited" 
- **Informacijski pas**: Razloži situacijo in približen čas ponastavitve.
- **Napredovanje sinhronizacije**: Prikazuje e-pošto, ki je bila obdelana pred dosego omejitve.

### Samodejno vedenje {#automatic-behavior}

1. **Brez potrebe po ročnem poseganju**: Sistem vse upravlja samodejno.
2. **Ponovni zagon aplikacije**: Če se aplikacija ponovno zagoni, je stanje omejitve hitrosti shranjeno v bazi podatkov.
3. **Več računov**: Vsak račun samostojno sledi svoji pasovni širini.

### Kaj je shranjeno {#what-gets-saved}

Ko pride do omejitve hitrosti, sistem shrani:

- **Kontrolne točke sinhronizacije**: Točno točno v vsaki mapi, kjer se je sinhronizacija ustavila.
- **Uporabo pasovne širine**: Skupno število prenesenih bajtov za tekoči dan.
- **Stanje omejitve**: Ali je omejitev dosegnięta in kdaj se bo ponastavila.

## 🗄️ Vzdrževanje baze podatkov {#database-maintenance}

`DatabaseMaintenanceService` samodejno čisti stare podatke:

- **Zapiski pasovne širine**: Izbrišejo se po 7 dneh.
- **Kontrolne točke sinhronizacije**: Ob zaključene sinhronizacije se izbrišejo po 30 dneh.
- **Nezaključene kontrolne točke**: Ostanejo shranjene, dokler se sinhronizacija ne zaključi.

## ✅ Najboljše prakse {#best-practices}

### Za velike predale {#for-large-mailboxes}

1. **Omogočite sledenje pasovni širini**: Nujno za predale, ki so večji od dnevne omejitve.
2. **Načrtujte prvotno sinhronizacijo**: Sinhronizacija velikega predala lahko traja več dni.
3. **Spremljajte napredovanje**: Preverite nadzorno ploščo za status omejitve hitrosti.
4. **Ne prekinjajte**: Naj sistem samodejno opravi nadaljevanje prek prek avtomatskega ponovnega zagona.

### Za administratorje {#for-administrators}

1. **Nastavite ustrezne omejitve**: Uskladi `DailyLimitMb` z dejanskimi omejitvami vašega ponudnika e-pošte.
2. **Spremljajte dnevnike**: Sporočila o opozorilih se pojavijo pri 80 % dosegane omejitve.
3. **Prostor v bazi podatkov**: Sledenje pasovni širini porabi minimalno dodatno shrambo.
4. **Pred spremembami naredite varnostno kopijo**: Pred spreminjanjem nastavitev omejitev hitrosti vedno naredite varnostno kopijo.

### Učinkovitost {#performance-considerations}

- ✅ **Vpliv na pomnilnik**: Negateljljiv – sledi le bajtom, ne shranjuje e-pošte.
- ✅ **Vpliv na bazo podatkov**: Minimalen – majhni zapisi se samodejno čistijo.
- ✅ **Hitrost sinhronizacije**: Brez vpliva na učinkovitost med aktivno sinhronizacijo.

## 🛡️ Začasne napake FETCH / Zaticavanje na strani strežnika {#transient-fetch-errors-server-side-throttling}

Poleg zgoraj opisovanega sledenja pasovni širini na podlagi bajtov, Mail Archiver zazna in reagira tudi na **zaticavanje na posamezno sporočilo** (per-message throttling), ki ga signalizira sam IMAP strežnik. Nekateri ponudniki glasbenega IMAP strežnika uporabljajo zatikanje na nivo računa ali IP, ki je **neodvisno od konfiguriranih pavziranja `BatchOperation`**, saj se zatikanje izvaja na strani strežnika na podlagi splošnega obsega zahtev med trenutno sejo.

### Simptomi {#symptoms}

Strežnik vrne odgovor `NO` (ali `BAD`) na posamezen ukaz `FETCH` za telo sporočila, s sporočili, kot so:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- odgovori, ki vsebujejo `throttle`, `rate limit` ali podobne oznake.

Avtentikacija, izpis map in samo iskanje sporočil običajno še vedno delujeta — neuspešno pa je le dejansko pridobivanje celotnega sporočila.

### Samodejno upravljanje {#automatic-handling}

Ko se zazna takšna začasna napaka, Mail Archiver uporabi strategijo ponovnih poskusov za posamezno sporočilo:

1. **Ponovni poskus z eksponencialnim zakasnjenjem**: Do **3 ponovnih poskusov** za isto sporočilo, z zakasnitvami **5 s → 15 s → 60 s**.
2. **Obnovitev povezave**: Pred vsakim ponovnim poskusom se preverijo in po potrebi obnovijo stanje povezave, avtentikacije in izbira mape.
3. **Ponastavitev ob uspehu**: Uspešen FETCH ponastavi števec zaporednih neuspehov.
4. **Varno zaustavitev sinhronizacije**: Če se **10 zaporednih sporočil** še vedno ne uspeva zaradi začasnih napak po vseh ponovnih poskusih, se trenutna sinhronizacija varno ustavi — z uporabo istega mehanizma kot pri pavzanju zaradi omejitve pasovne širine:
   - Žig časa `LastSync` **ni** posodobljen.
   - Če je `BandwidthTracking` omogočen, so kontrolne točke ohranjene, da se naslednja sinhronizacija nadaljuje od zadnjega uspešno obdelanega sporočila.
   - Naloga je označena kot omejena s hitrostjo.
   - Naslednja načrtovana sinhronizacija bo potekala samodejno.

### Zmanjšanje posledic {#mitigation}

Če redno naletite na zatikanje na strani strežnika, zmanjšajte hitrost zahtev s prilagajanjem nastavitev `BatchOperation`:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Upoštevajte, da nekateri ponudniki zatikajo na podlagi **skupne števila bajtov** ali **skupne število FETCH zahtev v časovnem oknu**, namesto hitrosti na sekundo. V tem primeru:

- Omogočite `BandwidthTracking` (glejte zgoraj) in konfigurirajte `DailyLimitMb`, da ustreza dokumentirani omejitvi ponudnika. Sinhronizacija se bo nato čisto ustavila, ko bo dnevni proračun izčrpan, in se naslednji dan samodejno nadaljevala.
- Prvotne sinhronizacije zelo velikih predalov lahko trajajo več dni, kar je predvideno vedenje.

### Kaj velja za začasno napako {#what-counts-as-a-transient-error}

Le odgovori IMAP `NO`/`BAD` s besedilom ali kodami odgovorov, ki so povezani z zatikanjem, se obravnavajo kot začasni. Druge napake (nepravilna sporočila, težave z razkodiranjem UTF-8 itd.) se še vedno takoj štejejo kot `FailedEmails` in **ne sprožijo** logike ponovnih poskusov/pavziranja.

## 📚 Sorodna dokumentacija {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Postopki čiščenja baze podatkov
- [GmailBestPractices.md](GmailBestPractices.md) - Konfiguracija specifična za Gmail
