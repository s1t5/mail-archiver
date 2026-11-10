# Arhiviranje Mailcow instance {#mailcow-instance-archiving}

[← Nazaj na indeks dokumentacije](index.md)

## Pregled {#overview}

Mail Archiver lahko centralno arhivira poštne predali instance [Mailcow](https://mailcow.email), ne da bi zbiral osebne gesle lastnikov poštnih predalov.

Administrator enkrat poveže instanto s svojim API ključem. Mail Archiver nato izlista poštne predale in za vsak poštni predal, izbran za arhiviranje, ustvari **namenski app password**, omejen na `imap_access`. Tega app passworda uporablja obstoječi IMAP ponudnik za arhiviranje poštnega predala, lahko pa ga kadar koli ponovno prekličete.

Administratorji dostopajo do upravljavca inštanc prek **Add Account → Mailcow** ali iz menija dejavnosti računa, ki ga Mailcow že upravlja. Upravljavec je na voljo le administratorjem, ker shranjeni API ključ lahko upravlja s pooblaščenji za vsak poštni predal v instanti.

## Ustvarjanje računov iz Mailcow {#creating-accounts-from-mailcow}

V **Mail Accounts → Add Account** lahko administratorji izberejo **Mailcow** kot ponudnika. Obrazec nato ponudi povezane instance in dejavnost **Manage mailboxes**. Izbira poštnih predalov tam ustvari običajne IMAP arhivske račune z namenskimi app passwords; ne shranjuje se ločen Mailcow račun. Če nobena instanta še ne obstaja, **Manage Mailcow instances** odpre upravljavca inštanc, kjer je mogoč povezovanje.

## Povezovanje obstoječega računa {#linking-an-existing-account}

V pogledu za urejanje IMAP računa lahko administratorji preklopijo ponudnika na **Mailcow** in izberejo instanto. Račun ohrani IMAP ponudnika, vendar se preلناči v upravljanje Mailcow: Mail Archiver ustvari namenski app password, ga preveri prek IMAP in nadomesti shranjeno geslo računa. Račun se nato v seznamu računov prikaže z dejavnostjo Mailcow.

Če želite zapustiti upravljanje Mailcow, izberite drugega ponudnika in shranite. App password bo preklican in lokalno geslo bo izbrisano. Ob ponovnem preklopu na IMAP vnesite novo IMAP geslo; preklop na Microsoft 365 ali račun samo za uvoz bo zagotovil svoje pooblaščenje. Ne-administratorji lahko še vedno urejajo nastavitve računa, ki ga upravlja Mailcow, ki niso povezane s pooblaščenji, vendar ga ne morejo povezati ali odpojiti.

## Predpogoji {#prerequisites}

### Šifriranje pooblaščenj mora biti omogočeno {#credential-encryption-must-be-enabled}

Mail Archiver shranjuje Mailcow API ključ in vsak ustvarjen app password šifriran na disku z uporabo obstoječe funkcije šifriranja pooblaščenj. Nastavite

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

pred povezovanjem instance. Ustvarjanje instance in aktivacija poštnih predalov sta ob izklopljenem šifriranju zavržena, zato pooblaščenj ne morejo biti nikoli zapisana kot nešifrirana besedila prek te funkcije. Ključ shranite zunaj baze podatkov in ga vključite v svojo varno varnostno kopijo — brez njega shranjenih app passwordov ni mogoče dešifrirati in poštnih predalov ni mogoče pripraviti. Več podrobnosti najdete v razdelku [Setup](Setup.md#-security-settings).

### Dostop do Mailcow API {#mailcow-api-access}

V Mailcow ustvarite API ključ pod **Configuration → Access → API**. Ključu mora biti dovoljeno:

- izlistati poštne predale (`get/mailbox/all`),
- izlistati, ustvariti in izbrisati app passwords (`get/app-passwd/all`, `add/app-passwd`,
  `delete/app-passwd`).

V nastavitvah Mailcow API omejite dostop na IP naslov gostitelja Mail Archiver. Mailcowov endpoint za app-password zahteva, da je funkcija **App Passwords** za instanto omogočena.

### Omrežni dostop {#network-access}

- **HTTPS** do izvornine Mailcow (na primer `https://mail.example.com`).
- **IMAP over TLS** do IMAP gostitelja, običajno port `993`. IMAP v obliki nešifrirane besedila ni podprt za pripravo predalov.

## Korak za korakom {#step-by-step-usage}

### Povezovanje instance {#connecting-an-instance}

1. Prijavite se v Mail Archiver kot administrator.
2. Odprite **Mail Accounts** in kliknite **Mailcow instances**.
3. Kliknite **Connect Mailcow instance**.
4. Vnesite podrobnosti o povezavi:
   - **Account name** — ime za prikaz instance v Mail Archiver.
   - **Mailcow URL** — le HTTPS izvornina, na primer `https://mail.example.com`. Poti, nizovni zahtevki in vključena pooblaščenja bodo zavrženi.
   - **API key** — ključ, ustvarjen v Mailcow.
   - **IMAP host** — ime gostitelja, ki se uporablja za arhiviranje, na primer `mail.example.com`. Vnesite samo ime gostitelja, brez sheme ali porta.
   - **IMAP TLS port** — privzeto `993`.
5. Kliknite **Connect**. Mail Archiver preveri API ključ z izlistanjem poštnih predalov pred shranjenjem instance. Če je povezovanje uspešno, se odpre izbira poštnih predalov.

API ključ je shranjen šifriran in se nikoli ne prikaže v obrazcu ali v sporočilu o napaki. Preusmeritve HTTP so za API klice onemogočene, da ključ ne bi bil preusmerjen na drugo izvornino.

### Aktivacija poštnih predalov {#activating-mailboxes}

1. Na strani poštnih predalov po želji filtrirajte seznam s poljem za iskanje. Filter se ujema z naslovom in imenom za prikaz ter je neobazljiv za velike in male črke.
2. Izberite poštne predale za arhiviranje. **Select all** in **Clear selection** vplivata le na trenutno vidne (filtrirane) poštne predale, zato je filtriranje varen način za izbiro podskupa.
3. Kliknite **Activate archiving**.

Za vsak izbrani poštni predal Mail Archiver:

1. ponovno preveri naslov glede na trenutni seznam poštnih predalov v Mailcow in ignorira onemogočene poštne predale,
2. ustvari arhivski račun z imenom `{instance name} – {mailbox display name}`,
3. generira naključen app password z imenom `MailArchiver-<unique id>`, omejen na `imap_access`,
4. preveri pooblaščenje z IMAP prijavo prek TLS,
5. in šele nato omogoči sinhronizacijo.

Če IMAP preverjanje ne uspe, račun ostane onemogočen in ga je mogoče ponovno poskusiti.

### Znaki statusa poštnega predala {#mailbox-status-badges}

Stran za izbiro vsak poštni predal označi, da je jasno, zakaj je polje za izbiro na voljo ali ne:

| Badge | Meaning | Selectable |
| --- | --- | --- |
| **Available** | Listed in Mailcow, not yet provisioned | yes |
| **Active** | Provisioned and synchronizing | no |
| **Pending** | Provisioning started but not finished (retryable) | yes |
| **Revoked** | App password was revoked; can be activated again | yes |
| **Existing** | An independent IMAP account with this address already exists | no |
| **Disabled** | The mailbox is disabled in Mailcow | no |

### Ponovni poskus za čakajoč poštni predal {#retrying-a-pending-mailbox}

Generirano pooblaščenje in edinstveno ime app passworda sta shranjena **pred** klicem Mailcow API. Če zahteva zavrne na nedoločen način — na primer zaradi časovnega iztaknjaja po ustvarjanju app passworda — poštni predal ostane vidna kot **Pending**. Osvežite stran in ga ponovno izberite: Mail Archiver najprej poišče ime app passworda in ustvari nov app password le, če nobene ni. Ponovni poskus ne povzroča kopiranja pooblaščenj.

Priprava in preklic za eno instanto sta zaporedna z uporabo PostgreSQL advisory zaklepov, zato dve hkrati opravljeni operaciji ne smeta pripraviti tega istega poštnega predala dvakrat.

### Preklic dostopa {#revoking-access}

Kliknite **Revoke app password** ob poštnem predalu. To:

- onemogoči prihodnjo sinhronizacijo,
- izbriše upravljani app password v Mailcow,
- in izbriše lokalno geslo.

**Arhivirana pošta ostane.** Odstranjen bo le upravljani app password — ostali app passwordi, ki pripadajo poštnemu predalu, ostanejo nedotaknjeni. Že tekoče operacije sinhronizacije lahko končajo s svojo obstojočo povezavo.

Če zahteva na strani strežnika ne uspe, račun ostane onemogočen in preklic lahko ponovite.

Izbrisati arhivski račun v **Mail Accounts** ne odstrani tiho pooblaščenja na strani strežnika. Veza ostane navedena pod **Remaining app passwords** na strani instance, da jo lahko lahko iz explicitno prekličete.

### Urejanje instance {#editing-an-instance}

Odprite **Mailcow instances** in kliknite **Edit**. Polje za API ključ je vedno prazno — shranjeni ključ se nikoli ne prikaže. Pustite ga prazno, da ohranite trenutni ključ, ali vnesite nov, da ga zamenjate. Spremenjene podrobnosti o povezavi se pred shranjenjem preverijo v Mailcow.

### Brisanje instance {#deleting-an-instance}

Odprite **Mailcow instances** in kliknite **Delete**.

Brisanje instance odstrani njen šifriran API ključ in njegove vezi s poštnimi predali. **Arhivski računi in njihova arhivirana pošta ostanejo**, vendar so računi onemogočeni.

Brisanje je **zavrženo, če je v Mailcow še vedno aktiven kateri koli upravljani app password**: odstranitev instance bi izbrisala API ključ, ki je potreben za njegov preklic. Najprej prekličite vse upravjane app passworde na strani poštnih predalov.

## Zapisovanje revizijskih dnevnikov {#audit-logging}

Vsaka administratorska operacija je zapisana v dnevnik dostopa z uporabnikom, ki jo je izvedel:

- povezovanje instance,
- posodabljanje instance,
- brisanje instance,
- aktivacija poštnih predalov (s številom pripravljenih in številom neuspešnih),
- preklic app passworda.

## Omejitev poštnih predalov {#mailbox-limit}

Ena izvedena aktivacija lahko pripravi največ `Mailcow__MaxSelectedMailboxes` poštnih predalov (privzeto `1000`). To ščiti pred nenadno masovno pripravo in prekomernim obremenjenjem Mailcow API in IMAP. Pri večjih instancah operacijo izvedite večkrat.

Vsak poštni predal zahteva več klicov API in IMAP preverjanje, obdeluje pa se zaporedno. Priprava več sto poštnih predalov v enem zahtevku lahko preseže časovne omejitve obratnega posrednika ali brskalnika; izbiro razdelite v skupine.

## Varnostni opombe {#security-notes}

- API ključ ostane na strani strežnika. Pošilja se kot `X-API-Key` glava, je iztaknjen iz dnevnikov in se nikoli ne vrne brskalniku.
- Odgovori Mailcow za app-password lahko v svojih poljih `log` odteglijo nešifrirana gesla. Mail Archiver namerno ne beleži telesa odgovorov in ne prikazuje napak API brskalniku; napake so poročane kot lokalizirana sporočila.
- Odgovor HTTP 200 ni obravnavan kot uspešen: spremembe zahtevajo Mailcowovo `type: success` za vsak rezultat.
- IMAP dovoljenje ni v svojem bistvu samo za branje. Pripravljeni računi nimajo konfigurirane politike brisanja na strani strežnika, administratorji pa ohranjajo nadzor nad nastavitvami računa. Ta funkcija arhivira sporočila, ne pa Mailcow konfiguracijo ali varnostne kopije.

### Imena gostiteljev in zasebna omrežja {#hostnames-and-private-networks}

URL Mailcow in IMAP gostitelj sta vnesa administratorja in sta lahko usmerjena na zasebni adresni prostor. To je namerno in podprto: Mail Archiver je običajno nameščen poleg poštnega strežnika, zato so `mail.example.lan`, `192.168.10.20` ali `10.0.0.5` običajne vrednosti. **Žadna zasebna, loopback ali link-local naslova niso blokirani.**

Meja zaupanja je vloga administratorja, ne pa omrežni naslov. `Mail Accounts` je na voljo upravljavcem, ki lahko že konfigurirajo poljuben IMAP strežnik za svoje račune; razdelek Mailcow je omejen na administratorje, administrator pa že ima neomejen dostop do vsakega arhivskega računa v namestitvi. Z omejitvijo IMAP gostitelja torej ne bi zaprli meje privilegijev, tem bo pa pokvarilo načrtovano on-premise namestitev.

Še dve omejitvi veljata:

- URL Mailcow mora biti čista HTTPS izvornina. Poti, nizovni zahtevki, podatki o uporabniku in preusmeritve so zavrženi, zato API ključ ne more biti preusmerjen na drugo izvornino.
- IMAP gostitelj mora biti ime gostitelja brez sheme ali porta, priprava pa vedno povezuje prek TLS (`SslOnConnect`).

Če morate omejiti, katere gostitelje lahko Mail Archiver doseže, to izsilite na omrežni plasti — z regole požarni zidave za odhodni promet ali z DNS politiko za kontejner — namesto da se zanašate na validacijo vhodnih podatkov v aplikaciji.

## Reševanje težav {#troubleshooting}

| Simptom | Vzrok in rešitev |
| --- | --- |
| *Encryption required* ob povezovanju ali aktivaciji | `Security__CredentialEncryptionKey` ni nastavljen ali je neveljavno. Konfigurirajte 32-ovojni ključ v obliki base64 in ponovno zaženite. |
| *HTTPS origin required* | URL vsebuje pot, nizovni zahtevek ali vključena pooblaščenja. Vnesite čisto izvornino, npr. `https://mail.example.com`. |
| *IMAP host invalid* | IMAP gostitelj mora biti ime gostitelja brez sheme ali porta. |
| *Instance already connected* | Obstaja druga instanta z isto URL naslovom. Namesto tega uredite to instanto. |
| *API request failed (401/403)* | API ključ nima potrebnih dovoljenj ali omejitev IP ne vključuje gostitelja Mail Archiver. |
| *API request rejected* | Mailcow je vrnil `error`/`danger`/`warning` vrstico. Preverite dnevnike Mailcow; Mail Archiver ne posreduje telesa odgovorov. |
| *IMAP verification failed* | Napačen IMAP gostitelj ali port, nezaupanja vredno certifikat ali je funkcija app-password v Mailcow onemogočena. |
| *IMAP account exists* | Samostojen IMAP račun že arhivira ta naslov. Namesto ustvarjanja drugega arhivskega računa uporabite ta račun. |
| *Revoke previous password* | Arhivski račun za ta poštni predal je bil izbrisan. Najprej prekličite preostali app password, nato pa ponovno aktivirajte. |
| Poštni predal ostane **Pending** | Časovni iztakaj Mailcow API. Osvežite stran in ponovno aktivirajte poštni predal; ne ustvari se noben duplikat app passworda. |
| Brisanje je zavrženo | Upravljani app passwordi so še vedno aktivni. Najprej jih prekličite na strani poštnih predalov. |

## Uporabljeni API endpointi {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Spremembe v bazi podatkov {#database-changes}

Migracija doda le tabeli `MailcowInstances` in `MailcowMailboxes` skupaj z njimi indeksi in zvetnimi ključi. Obstoječe tabele poštnih predalov in arhivirani podatki ostanejo. Odstranitev arhivskega računa nastavi `MailAccountId` vezi na `NULL`, da lahko je lahko pooblaščanje na strani strežnika kasneje še vedno prekličeno.
