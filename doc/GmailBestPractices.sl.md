# 📧 Gmail Best Practices {#gmail-best-practices}

[← Naz nazaj na indeks dokumentacije](index.md)

Pri uporabi računov Gmail z aplikacijo Mail Archiver je treba upoštevati nekaj specifičnih konfiguracij in najboljših praks, da zagotovite optimalno delovanje in preprečite morebitne težave.

## 🔐 IMAP konfiguracija z gesli za aplikacije {#imap-configuration-with-app-passwords}

Račune Gmail lahko integrirate z aplikacijo Mail Archiver z uporabo IMAP in gesli za aplikacije (App Passwords):

1. Omogočite dvostopenjsko avtentikacijo na svojem računu Gmail
2. Generirajte geslo za aplikacijo (App Password) posebej za aplikacijo Mail Archiver
3. Pri konfiguraciji računa v Mail Archiver uporabite geslo za aplikacijo namesto svojega običajnega gesla za Gmail

## 🏷️ Nastavitve Gmail etiket {#gmail-label-settings}

Močno se priporoča prilagoditev nastavitev etiket Gmail, da se prepreči izpostavitev etikete "All Mail" prek IMAP:

1. Pojdite na spletni vmesnik Gmail in odprite nastavitve (ikona z malim zobnikom na vrhu) → See all settings → Labels tab
2. V seznamu poiščite etiketo "All Mail"
3. Odklíčite možnost "Show in IMAP" za "All Mail"

### 📚 Osnovne informacije o implementaciji etiket Gmail {#background-on-gmail-label-implementation}

Sistem etiket Gmail deluje drugače kot pri tradicionalnih ponudnikih e-pošte. V Gmailu:
- Vsi e-maili obstajajo v enem samem skladišču "All Mail"
- Etikete delujejo kot oznake namesto map
- Etiketa "All Mail" vsebuje vsako e-pošto na vašem računu
- Ko je e-pošta označena z etiketo, se pojavi tako v prvotni mapi kot v razdelku z etiketami. Tako se vse e-pošte v vašem predvokalu (inbox) pojavijo tudi v etiketi All Mail.

### ⚠️ Težave pri pridobivanju prek IMAP {#issues-with-imap-retrieval}

Ko je "All Mail" izpostavljen prek IMAP, lahko povzroči več težav:

1. **Dvojno obdelovanje**: Arhivator lahko isto e-pošto obdeluje večkrat, saj se pojavlja v več mapah/etiketah
2. **Vpliv na zmogljivost**: Pridobivanje "All Mail" lahko traja izjemno dolgo pri velikih računih z milijoni e-poštnih sporočil
4. **Težave s sinhronizacijo**: Konflikti se lahko pojavijo pri poskusu določitve, v katero mapo e-pošta dejansko pripada

Z onemogočanjem dostopa prek IMAP do "All Mail" zagotovite, da so e-poštna sporočila pridobljena le enkrat iz njihove primarne lokacije, kar izboljša tako zmogljivost kot natančnost.

## 🗑️ Politika zadržanja in nastavitve za izbris (Expunge) {#retention-policy-and-expunge-settings}

Kot je navedeno v glavnem README, zahtevajo računi Gmail posebne pozornosti glede politik zadržanja podatkov:

- Funkcija Auto-Expunge v Gmailu mora biti onemogočena v nastavitvah Gmail v zavihku "Forwarding and POP/IMAP"
- Ta nastavitev je ključna za pravilno delovanje politik zadržanja pri računih Gmail

Za konfiguracijo te nastavitve:
1. Pojdite na Gmail Settings → See all settings → Forwarding and POP/IMAP tab
2. V razdelku "IMAP Access" izberite "Auto-Expunge off - Wait for the client to update the server." in "Move the message to the Bin"

## ⚠️ Gmail IMAP omejitev hitrosti (Rate Limiting) {#gmail-imap-rate-limiting}

Gmail postavlja dnevno omejitev hitrosti v višini 2500 MB za pridobivanje prek IMAP. Pri arhiviranju velikih količin e-pošte imejte v mislih, da lahko dosežete to omejitev, kar lahko začasno ustavi pridobivanje e-pošte do naslednjega dneva.

### 📊 Obdelava omejitev hitrosti {#rate-limit-handling}

Mail Archiver nudi vgrajeno obdelavo omejitev hitrosti za upravljanje omejitev IMAP pasovne širine Gmaila:

- **Samodejno sledenje pasovni širini**: Spremlja predate podatke na račun na dan
- **Konfigurabilne omejitve**: Nastavite `BandwidthTracking__DailyLimitMb=2500` za Gmail
- **Samodejni premak**: Sinhronizacija se ustavi, ko je dosežena omejitev, in nadaljuje po ponastavitvi
- **Sistem kontrolnih točk**: Napredovanje sinhronizacije je shranjeno in se nadaljuje od zadnjega položaja

Za podrobno konfiguracijo in uporabo poglejte **[Rate Limit Handling Guide](RateLimitHandling.md)**.

### 🔧 Primer konfiguracije Gmail {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Upostevanje velikih poštnih predalov {#large-mailbox-considerations}

Pri velikih Gmail poštnih predalih (npr. 14 GB) bo začetna sinhronizacija trajala več dni:

| Velikost poštnega predala | Dnevi za dokončanje (pri 2500 MB/dan) |
|---------------------------|------------------------------------------|
| 5 GB                      | ~2 dni                                   |
| 10 GB                     | ~4 dni                                   |
| 14 GB                     | ~6 dni                                   |
| 20 GB                     | ~8 dni                                   |

**Nasvet**: Omogočite sledenje pasovni širini **pred** dodajanjem velikih Gmail računov, da zagotovite pravilno obdelavo omejitev hitrosti že od začetka.

## ✅ Povzetek najboljših praks {#best-practices-summary}

Slediti tem najboljšim praksam bo pomagalo zagotoviti nemoteno delovanje Mail Archiverja z vašimi Gmail računi, hkrati pa ohraniti integriteto podatkov in optimalno zmogljivost.
