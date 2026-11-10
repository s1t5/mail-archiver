# 🧬 Deduplikacija prilogov {#attachment-deduplication}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Priloge v e-pošti so shranjene kot binarni podatki v bazi podatkov. **Ista datoteka** je zelo pogosto prilagojena številnim e-poštnim sporočilom (logotipi podjetij v podpisih, posredovani PDF-ji, novice, pogodbe, ...). Brez deduplikacije vsaka priloga ohranja svojo zasebno kopijo teh bajtov, kar zapravlja veliko prostora v bazi podatkov.

**Deduplikacija prilog** vsako *unikatno* polno (payload) shrani točno enkrat v tabelo z adresiranjem po vsebini (ključ je SHA-256 hash bajtov) in omogoči vsem identičnim prilogam, da se referencirajo na to edino skupno kopijo. To običajno znatno zmanjša porabo prostora za arhiviranje prilog.

## ⚙️ Kako deluje {#how-it-works}

### Nove priloge {#new-attachments}
Vsakič, ko se arhivira novo e-poštno sporočilo (IMAP, Microsoft 365 / Graph, uvoz EML, uvoz MBOX, ...) se polno priloge zhasira in shrani enkrat. Če ta ista polno že obstaja, se nova priloga preprosto referencira na obstoječ trdek v vrstici vsebine. To se zgodi transparentno na ravni podatkov, zato je vsaka pot arhiviranja samodejno pokrita.

### Obstoječi podatki (migracija) {#existing-data-migration}
Za namesti, ki že vsebujejo priloge, arhivirane **pred** uvedbo te funkcije, **robustna, ponovljiva migracija v ozadju** pretvori obstoječe vključene polne v skupne vrstice vsebine:

- Obdeluje priloge **serijo po seriji** (batch by batch), vsako serijo v svojem lastnem poslu (transaction).
- Napredovanje je shranjeno v namenski tabeli stanja, zato migracija **preživi ponovni zagon aplikacije** in preprosto nadaljuje tam, kjer je bila ustavljena.
- Je **idempotentna** – že migrirane priloge se nikoli več ne dotikajo, tudi po napaki ali ponovnem zagonu.
- Poteka v ozadju in ne blokira normalne uporabe aplikacije.

Ko se migracija konča, zabeleži sporočilo o dokončanju, npr.:

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

Ob naslednjem zagonu zazna, da ni več nič česa opraviti, in ostane v stanju mirovanja. Če bi kdaj ostale kakšne ne-deduplicirane priloge (na primer po obnovitvi starejšje varnostne kopije), migracijo samodejno nadaljuje in jih pretvori — sistem se vedno konča v popolnoma dedupliciranem stanju.

### Čiščenje smeti (vedno vklopljeno) {#garbage-collection-always-on}
Ko so e-poštna sporočila (in s tem tudi priloge) izbrisana, se lahko skupna polna postane neobstoječa (unreferenced). Vgrajeno čiščenje smeti (garbage collection) odstrani te sirotne vrstice vsebine.

> 🧹 **Čiščenje sirotnih podatkov vedno teče**, neodvisno od funkcije `DatabaseMaintenance`. Poteka enkrat takoj po migraciji in nato občasno (vsak `OrphanCleanupIntervalHours`, privzeto vsakih 12 h). Ko `DatabaseMaintenance__Enabled=true`, se isto čiščenje dodatno izvede pred dnevnim `VACUUM`, da se osvobojeni prostor fizično povrne.

### Poraba diska po migraciji (pomembno) {#disk-usage-after-the-migration-important}

> 📈 **Vrednost "Storage" na nadzorni plošči lahko takoj po migraciji kratko naraste** (npr. 3,5 GB → 5,3 GB). To je pričakovano in ni napaka.
>
> Nadzorna plošča poročila o **fizični** velikosti baze podatkov (`pg_database_size`). Ko migracija premakne polno v skupno shranjevanje, staro vključeno kopijo postavi na `NULL`, vendar PostgreSQL (MVCC) ohranja te stare bajte kot *mrtve okvire* (dead tuples), dokler datoteka tabele ni ponovno prebrzana — zato baza na trenutek vsebuje tako staro kot novo kopijo.
>
> Da bi to popravili samodejno, aplikacija opravi **enkratno** `VACUUM FULL` na tabeli prilog **ko je migracija končana in medtem, ko ne tečejo nobeni sinhronizacijske naloge**. To ponovno zapiše tabelo in vrne osvobojen prostor operacijskemu sistemu; nato poročana velikost pade **pod** prvotno. To se izvede točno enkrat (sledi se prek oznake `ReclaimedAt`), je varno za ponovni zagon in se odloži, dokler poteka sinhronizacija (samodejno poskusi ponovno, ko je sistem v stanju mirovanja).

>
> ⚠️ `VACUUM FULL` za čas trajanja zaključi tabelo prilog (med tem ni branja/pisanja prilog) in potrebuje nekaj prostega prostora na disku za ponovni zapis. Če želite to sprožiti ročno, zaženite:
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 Konfiguracija {#configuration}


Te nastavitve so neobvezne; privzete vrednosti so primerne za večino namestitev. Poglejte tudi [Vodnik za postavitev](Setup.md#-attachment-deduplication-settings).

| Nastavitev | Privzeto | Opis |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Število obstoječih prilog, migriranih v enem poslu. Večje vrednosti migrirajo hitreje, vendar povečajo obremenitev pomnilnika/baze na serijo. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Neprijetna pavza (ms) med serijami migracije za omejitev obremenitev baze na preobremenjenih sistemih. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Zavoka (s) po zagonu aplikacije, preden se migracija začne (da se najprej dokonča migracija sheme). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Interval (h) vedno delujočega čiščenja sirotnih podatkov. |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | Čas izteka ukaz za bazo podatkov (v sekundah) za operacije serij migracije. Privzeto je 5 minut. Če serija še vedno izteče časovno, servis samodejno poskusi s polovico velikosti serije. |

Primer (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ FAQ {#faq}

**Ali lahko onemogočim deduplikacijo?**
Ne. To je ključna funkcija shranjevanja in je vedno aktivna. Lahko lahko prilagodite le velikost serije in parametre načrtovanja zgoraj.

**Ali so moji podatki varni med migracijo?**
Da. Vsaka serija je en samostojen posel in delo je idempotentno. Napaka ali ponovni zagon sredi migracije ponastavi nekončano serijo in se ta ponovno obdeluje ob naslednjem zagonu; podatki se ne izgubijo in ne duplicirajo.

**Ali to upočasni arhiviranje?**
Obremenitev zaradi hesiranja posamezne priloge je zanemarljiva. Enkratna migracija obstoječih podatkov poteka v ozadju in jo je po potrebi mogoče omejiti s `DelayBetweenBatchesMs`.

**Migracija nenehno izteče časovno in se ponovno začenja na istem mestu. Kaj lahko storim?**
To se lahko zgodi pri zelo velikih bazah podatkov (stot Thousands e-poštnih sporočil), kjer ena serija 200 prilog s SHA-256 hesiranjem preseže privzeto časovno omejitev ukaza. Servis zdaj ob izteku časa samodejno poskusi s polovico velikosti serije. Če še vedno opazite iztekanje časa, lahko ročno znižate velikost serije in/ali povečate časovno omejitev ukaza:

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**Koliko prostora bom prihranil?**
To je popolnoma odvisno od tega, koliko dupliciranih prilog vsebuje vaš arhiv. Najvešjo korist imajo arhiivi z veliko ponovljenih podpisov, logotipov ali posredovanih datotek.

---
