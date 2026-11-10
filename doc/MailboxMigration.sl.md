# 📥 Vodnik za migracijo poštovnega predala {#mailbox-migration-guide}

[← Nazaj na indeks dokumentacije](index.md)

Ta vodnik pojasnjuje, kako migrirati poštovni predal iz enega ponudnika e-pošte v drugega z uporabo aplikacije Mail Archiver.

## 📋 Pregled {#overview}

Aplikacija Mail Archiver omogoča funkcionalnost za migracijo e-poštnih sporočil iz izvornog poštovnega predala v ciljni poštovni predal. To je posebej uporabno pri menjavi ponudnikov e-pošte, hkrati pa ohranjate svojo zgodovino e-pošte. Struktura map izvornih poštovnih predal je med migracijo prav tako ohranjena.

> **Migrirate samo nedavne pošte?** Postopek, opisan spodaj, kopira *celotno* arhivirano poštovni predal, njegovo ponovno izvajanje pa vse doda še enkrat. Če bi novo poštovni predal skulle vsebovati le pošto, ki je novejša od določenega datuma, medtem ko celotna zgodovina ostane v arhivu, uporabite namesto tega [date-windowed offload](Offload.md). Ta sprejme časovno obdobje, lahko izključi in preimenuje mape, ponuja poskusni zagon (dry run) in je varen za ponavljanje, saj preskoči vse, kar ciljni predal že vsebuje. Na voljo je tudi preko ukazne vrstice, kar omogoča avtomatizacijo celotne flote poštovnih predal.

## 🚀 Postopek migracije {#migration-process}

Za migracijo svojega poštovnega predala sledite tem korakom:

1. **Dodajte izvorni račun**
   - Pojdite v razdelek "Email Accounts"
   - Kliknite "Add Account"
   - Vnesite podatke in凭凭证 (credentials) svojega izvornega računa
   - Shranite račun

2. **Sinhronizirajte izvorni račun**
   - Omogočite aplikaciji, da sinhronizira vse e-poštne sporočile iz izvornenga računa
   - Počakajte, da se postopek sinhronizacije zaključi

3. **Dodajte ciljni račun**
   - V razdelek "Email Accounts" kliknite "Add Account"
   - Vnesite podatke in凭凭证 (credentials) svojega ciljnega računa
   - Shranite račun

4. **Sinhronizirajte ciljni račun**
   - Če je ciljni račun nov, ga sinhronizirajte, da zagotovite pravilno nastavitev
   - Počakajte, da se postopek sinhronizacije zaključi

5. **Zaženite migracijo**
   - V razdelek "Email Accounts" poiščite izvorni račun
   - Pojdite na podrobnosti računa
   - Izberite možnost "Copy All Emails to Another Mailbox"
   - Izberite ciljni račun in ciljno mapo
   - Omogočite možnost **"Preserve Folder Structure"** – izbrana ciljna mapa nato služi kot korenina, pod katero se rekurzivno ponovno ustvari celotna struktura map izvornenga računa
   - Če ta možnost ni omogočena, bodo vsa e-poštna sporočila postavljena neposredno v izbrano ciljno mapo (ravno, brez podmap)
   - Začnite postopek migracije

6. **Spremljajte migracijo**
   - Če je število e-poštnih sporočil veliko, bo migracija potekala kot delovni proces v ozadju
   - Status in napredovanje migracije lahko vidite v zavihku "Jobs"
   - Počakajte, da se migracija uspešno zaključi

7. ** onemogočite izvorni račun**
   - Po uspešnem prenosu se vrnite k izvornemu računu
   - V nastavitvah računa nastavite izvorni račun na "Disabled"
   - To zagotovi, da se v prihodnosti ne bo več arhiviral

## ⚠️ Pomembne opombe {#important-notes}

- Ta postopek ni ponovljiv: ponovno izvajanje doda vsako sporočilo še enkrat. [date-windowed offload](Offload.md) je ponovljiva alternativa.
- Zagotovite, da ima izvorni račun dovolj dovoljenj za branje vseh e-poštnih sporočil
- Prepričajte se, da ima ciljni račun dovolj prostora za migrirana e-poštna sporočila
- Postopek migracije lahko traja nekaj časa, odvisno od števila sporočil in hitrosti povezave
- Priporočljivo je opraviti migracijo v času manjše obremenitve, da se zmanjša vpliv na e-poštno storitev
- Redno preverjajte status migracije v zavihku "Jobs", da zagotovite napredovanje procesa
- Po migraciji preverite, ali so bila vsa e-poštna sporočila uspešno prenesena v ciljni račun

## ✅ Najboljše prakse {#best-practices}

- Pred migracijo celotnega poštovnega predala vedno preizkusite postopek migracije z majhnim podobsetom e-poštnih sporočil
- Pred začetkom migracije poskrbite za varnostno kopijo pomembnih e-poštnih sporočil
- Čas migracije načrtujte v času, ko je uporaba e-pošte minimalna
- N closely spremljajte napredovanje migracije in bodite pripravljeni na reševanje morebitnih težav
- Po končani migraciji preverite, ali so bila vsa e-poštna sporočila pravilno prenesena in dostopna
