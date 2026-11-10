# Mailcow példány archiválás {#mailcow-instance-archiving}

[← Vissza a dokumentáció indexhez](index.md)

## Áttekintés {#overview}

A Mail Archiver központilag archiválhatja egy [Mailcow](https://mailcow.email) példány postafiókjait anélkül, hogy gyűjtést kellene végeznie a postafiók tulajdonosai személyes jelszavairól.

Az adminisztrátor egyszer csatlakozik a példányhoz az API kulcs segítségével. A Mail Archiver ezután felsorolja a postafiókokat, és minden archiválásra kiválasztott fiókhoz egy **különleges alkalmazás-jelszavat (app password)** hoz létre, amely `imap_access` korlátozott. Ezt az alkalmazás-jelszavat az meglévő IMAP szolgáltató használja a postafiók archiválásához, és bármikor visszavonható.

Az adminisztrátorok az instance managerből érhetik el a példányt a **Add Account → Mailcow** menüpontból, vagy egy olyan fiók műveleti menüjéből, amelyet már a Mailcow kezel. A menedzser kizárólag adminisztrátorok számára elérhető, mivel a tárolt API kulcs minden postafiók hitelesítő adatait kezelheti a példányban.

## Fiókok létrehozása a Mailcow-ból {#creating-accounts-from-mailcow}

A **Mail Accounts → Add Account** menüpontban az adminisztrátorok a **Mailcow** szolgáltatót választhatják ki. A űrlap ilyenkor megjeleníti a csatlakoztatott példányokat és egy **Manage mailboxes** műveletet. A postafiókok kiválasztása ott normál IMAP archiváló fiókokat hoz létre különleges alkalmazás-jelszavakkal; külön Mailcow fiókot nem tárol. Ha még nincs példány, a **Manage Mailcow instances** megnyitja az instance managert, ahonnan lehet csatlakozni.

## Mev موجود fiók összekapcsolása {#linking-an-existing-account}

Egy IMAP fiók szerkesztési felületén az adminisztrátorok átváltabilirják a szolgáltatót **Mailcow**-ra, és kiválaszthatnak egy példányt. A fiók megőrzi az IMAP szolgáltatót, de átkerül a Mailcow kezelésébe: a Mail Archiver létrehoz egy különleges alkalmazás-jelszavat, ellenőrzi azt IMAP-on keresztül, majd lecseréli a fiók tárolt jelszavát. A fiók ezután a fióklistában Mailcow művelettel jelenik meg.

A Mailcow kezeléséből a másik szolgáltató kiválasztásával és mentéssel lehet kilépni. Ilyenkor az alkalmazás-jelszavát visszavonják, a helyi jelszavát pedig törlik. Visszaváltáskor IMAP-ra, egy új IMAP jelszót kell megadni; a Microsoft 365-re vagy importálásra korlátozott fiókra való váltás esetén a saját hitelesítő adatait használja. A nem adminisztrátorok továbbra is szerkeszthetik a Mailcow által kezelt fiók hitelesítés nélküli beállításait, de nem tudják összekapcsolni vagy lecsatlakoztatni azt.

## Előfeltételek {#prerequisites}

### A hitelesítési adatok titkosítása engedélyezett legyen {#credential-encryption-must-be-enabled}

A Mail Archiver a Mailcow API kulcsot és minden generált alkalmazás-jelszavát titkosítva tárolja a meglévő hitelesítés-titkosítási funkció segítségével. Állítsa be

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

mielőtt csatlakozik egy példányhoz. Amíg a titkosítás nincs engedélyezve, a példány létrehozása és a postafiókok aktiválása is megtagadható, így a hitelesítési adatok soha nem kerülhetnek törzstáblázatba (plaintext) ezen funkción keresztül. Tartsa a kulcsot a adatbázison kívül, és vegye magát a biztonságos mentésbe — nélküle a tárolt alkalmazás-jelszavakat nem lehet feltörni, és a postafiókok nem biztosíthatók meg. Részletes információért lásd a [Setup](Setup.md#-security-settings) részt.

### Mailcow API hozzáférés {#mailcow-api-access}

Hozzon létre egy API kulcsot a Mailcow-ban a **Configuration → Access → API** menüpont alatt. A kulcsnak engedélyezve kell være, hogy

- felsorolja a postafiókokat (`get/mailbox/all`),
- felsorolja, létrehozza és törli az alkalmazás-jelszavakat (`get/app-passwd/all`, `add/app-passwd`, `delete/app-passwd`).

A Mailcow API-t a Mail Archiver host IP-címére korlátozza a Mailcow API beállításaiban. A Mailcow alkalmazás-jelszavazó végpontjához szükséges, hogy az instance számára engedélyezve legyen az **App Passwords** funkció.

### Hálózati hozzáférés {#network-access}

- **HTTPS** a Mailcow forrásra (például `https://mail.example.com`).
- **IMAP over TLS** az IMAP hostra, általában a `993` portra. A Plaintext IMAP nem támogatott a biztosítás (provisioning) során.

## Lépésről lépésre történő használat {#step-by-step-usage}

### Egy példány csatlakoztatása {#connecting-an-instance}

1. Jelentkezzen be a Mail Archiverbe adminisztrátorként.
2. Nyissa meg a **Mail Accounts** részt, és kattintson a **Mailcow instances** gombra.
3. Kattintson a **Connect Mailcow instance** gombra.
4. Adja meg a csatlakozási adatokat:
   - **Account name** — egy megjelenítési név a példány számára a Mail Archiverben.
   - **Mailcow URL** — csak a HTTPS forrás, például `https://mail.example.com`. Az elérési utak, lekérdezési paraméterek és beágyazott hitelesítők nem elfogadottak.
   - **API key** — a Mailcow-ban létrehozott kulcs.
   - **IMAP host** — a archiváláshoz használt hostnév, például `mail.example.com`. Csak a hostnévet adja meg, protokoll vagy port nélkül.
   - **IMAP TLS port** — alapértelmezett értéke `993`.
5. Kattintson a **Connect** gombra. A Mail Archiver ellenőrzi az API kulcsot a postafiókok felsorolásával, mielőtt mentené a példányt. Sikeres esetben megnyílik a postafiók-választó.

Az API kulcs titkosítva kerül tárolásra, és soha nem jelenik meg újra az űrlapban vagy hibaüzenetben. Az HTTP átirányítások letiltva vannak az API hívásoknál, így a kulcs nem továbbíthatható más forrás felé.

### Postafiókok aktiválása {#activating-mailboxes}

1. A postafiók oldalon opcionálisan szűrje a listát a keresőmezővel. A szűrő az e-mail címet és a megjelenítési nevet is figyelembe veszi, és nem érzékeny a kis- és nagybetűkre.
2. Válassza ki a archiválandó postafiókokat. A **Select all** és a **Clear selection** csak a jelenleg látható (szűrt) postafiókokra vonatkozik, így a szűrés biztonságos módja egy részcsoport kiválasztásának.
3. Kattintson az **Activate archiving** gombra.

Minden kiválasztott postafiók esetében a Mail Archiver:

1. újra ellenőrzi az e-mail címet a Mailcow aktuális postafiók-listájához képest, és figyelmen kívül hagyja a letiltott fiókokat,
2. létrehoz egy `{instance name} – {mailbox display name}` nevű archiváló fiókot,
3. generál egy véletlenszerű, `imap_access` korlátozott, `MailArchiver-<unique id>` nevű alkalmazás-jelszavat,
4. ellenőrzi a hitelesítő adatokat egy TLS-alapú IMAP bejelentkezéssel,
5. és csak ezután engedélyezi a szinkronizálást.

Ha az IMAP ellenőrzés sikertelen, a fiók letiltva marad, és megpróbálható újra.

### Postafiók állapotjelzők {#mailbox-status-badges}

A választási oldal jelzi minden postafiókot, hogy egyértelmű legyen, miért érhető el vagy nem egy jelölőnégyzet:

| Jelző | Jelentés | Választható |
| --- | --- | --- |
| **Available** | Listázva van a Mailcow-ban, de még nem biztosított | igen |
| **Active** | Biztosítva és szinkronizál | nem |
| **Pending** | A biztosítás megindult, de nem fejeződött be (újra megpróbálható) | igen |
| **Revoked** | Az alkalmazás-jelszavát visszavonták; újra aktiválható | igen |
| **Existing** | Egy ilyen címmel már létezik egy független IMAP fiók | nem |
| **Disabled** | A postafiók letiltva van a Mailcow-ban | nem |

### A függőben lévő postafiók újra megpróbálása {#retrying-a-pending-mailbox}

A generált hitelesítő adat és az egyedi alkalmazás-jelszó név a felhívás **előtt** megmarad. Ha egy kérés bizonytalanul sikertelenül zárul le — például egy timeout az alkalmazás-jelszó létrehozása után —, a postafiók **Pending** állapotban marad. Frissítse az oldalt, és válassza ki újra: a Mail Archiver először az alkalmazás nevét keresi, és csak akkor hoz létre új alkalmazás-jelszavat, ha nincs már létező. Az újrapróbálás nem hoz létre duplikált hitelesítő adatokat.

Egy példány biztosítását és visszavonását PostgreSQL tanácsadó zárak (advisory locks) használja a sorrendiség biztosítására, így két egyszerre futó művelet nem tudja ugyanazt a postafiókot kétszer biztosítani.

### Hozzáférés visszavonása {#revoking-access}

Kattintson a **Revoke app password** gombra egy postafiók mellett. Ez:

- letiltja a jövőbeni szinkronizálást,
- törli a Mailcow-ban kezelt alkalmazás-jelszavat,
- és törli a helyi jelszavat.

**Az archivált levelek megmaradnak.** Csak a kezelt alkalmazás-jelszavát távolítják el — a postafiókhoz tartozó egyéb alkalmazás-jelszavakat soha nem érintik. A már futó szinkronizálási műveletek a meglévő kapcsolatukkal befejeződhetnek.

Ha a távoli kérés sikertelen, a fiók letiltva marad, és a visszavonás újra megpróbálható.

Egy archiváló fiók törlése a **Mail Accounts** részből nem távolítja el csendben a távoli hitelesítő adatot. A kötés továbbra is szerepel a **Remaining app passwords** alatt a példány oldalán, így explicit módon visszavonható.

### Egy példány szerkesztése {#editing-an-instance}

Nyissa meg a **Mailcow instances** részt, és kattintson az **Edit** gombra. Az API kulcs mező mindig üres marad — a tárolt kulcs soha nem jelenik meg. Hagyja üresen a jelenlegi kulcs megőrzéséhez, vagy írjon be újat a felváltásához. A megváltoztatott csatlakozási adatokat ellenőrizni kell a Mailcow-val a mentés előtt.

### Egy példány törlése {#deleting-an-instance}

Nyissa meg a **Mailcow instances** részt, és kattintson a **Delete** gombra.

Egy példány törlése eltávolítja annak titkosított API kulcsát és a postafiók-kötéseit. Az **archiváló fiókok és azok archivált levelei megmaradnak**, de a fiókok letiltásra kerülnek.

A törlés **megtagadható, ha bármilyen kezelt alkalmazás-jelszó még aktív** a Mailcow-ban: a példány eltávolítása törölné azt az API kulcsot, amely a visszavonáshoz szükséges. Előbb minden kezelt alkalmazás-jelszavót vonjon vissza a postafiók oldalán.

## Audit naplózás {#audit-logging}

Minden adminisztrációs művelet bekerül a hozzáférési naplóba az elkövető felhasználó adataival:

- példány csatlakoztatása,
- példány frissítése,
- példány törlése,
- postafiókok aktiválása (a biztosított és a sikertelen fiókok számával),
- alkalmazás-jelszó visszavonása.

## Postafiók limit {#mailbox-limit}

Egyetlen aktiválási futás maximum `Mailcow__MaxSelectedMailboxes` postafiókot biztosíthat (alapértelmezett `1000`). Ez védi a rendszert a véletlen tömeges biztosítástól, valamint a túlzott Mailcow API és IMAP terheléstől. Nagyobb példányok esetén futtassa a műveletet többször.

Minden postafiók több API körforgást és egy IMAP ellenőrzést igényel, ezek pedig egymás után kerülnek sorra. Több száz postafiók biztosítása egyetlen kérésben meghaladhatja a reverse proxy vagy a böngésző timeout értékeit; ilyenkor bontsa fel a választást kisebb csoportokra.

## Biztonsági megjegyzések {#security-notes}

- Az API kulcs a szerver oldalon marad. `X-API-Key` fejlécként küldi el, törli a naplókból, és soha nem tér vissza a böngészőbe.
- A Mailcow alkalmazás-jelszó válaszai visszaismételhetik a törzstáblázat (plaintext) jelszavakat a `log` mezőkben. A Mail Archiver szándékosan nem naplózza a választesteket, és nem jeleníti meg az API hibákat a böngészőben; a hibákat helyi üzenetként jelenti vissza.
- Az HTTP 200 választ nem tekinti sikernek: a módosításokhoz a Mailcow minden eredményhez tartozó `type: success` szükséges.
- Az IMAP jogosultság nem inherently (természetesen) csak olvasható. A biztosított fiókoknak nincs szerver oldali törlési szabályzata beállítva, az adminisztrátorok pedig megőrzik a fiók beállításainak ellenőrzését. Ez a funkció üzeneteket archivál, nem pedig a Mailcow konfigurációját vagy mentéseit.

### Hostnévek és privát hálózatok {#hostnames-and-private-networks}

A Mailcow URL-t és az IMAP hostot az adminisztrátor adja meg, és ezek privát címzetsávra is mutathatnak. Ez szándékos és támogatott: a Mail Archiver gyakran a levelező szerver mellett kerül kihelyezésre, így az `mail.example.lan`, `192.168.10.20` vagy `10.0.0.5` normális értékek. **Egyetlen privát, loopback vagy link-local címzés sem blokkolható.**

A bizalmi határ az adminisztrátori szerepkör, nem pedig a hálózati cím. Az `Mail Accounts` elérhető az önkezelők számára is, akik már konfiguráltak egy tetszőleges IMAP szervert a saját fiókaikhoz; a Mailcow rész pedig kizárólag adminisztrátorok számára elérhető, az adminisztrátor pedig már rendelkezik korlátozás nélküli hozzáféréssel minden archiváló fiókhoz a telepítésben. Ezért az IMAP host korlátozása nem zárna be új jogosultsági határt, miközben megsértené az elvárt on-premise telepítést.

Két korlátozás továbbra is érvényes:

- A Mailcow URL egy tisztán HTTPS forrás kell, hogy legyen. Az elérési utak, lekérdezési paraméterek, felhasználói adatok és átirányítások nem elfogadottak, így az API kulcs nem továbbíthatható más forrás felé.
- Az IMAP host egy olyan hostnév kell, amely nem tartalmaz protokollot vagy portot, a biztosítás pedig mindig TLS-sel (`SslOnConnect`) történik.

Ha korlátozni szeretné, hogy mely hostokhoz férhet hozzá a Mail Archiver, azt a hálattan (például kimenő tűzfal szabályok vagy egy DNS szabály a konténerek számára) szintjén kényszerítse ki, ne pedig az alkalmazás bemeneti validációjára támaszkodva.

## Hibaelhárítás {#troubleshooting}

| Tünet | Ok és megoldás |
| --- | --- |
| *Encryption required* a csatlakozás vagy aktiváláskor | `Security__CredentialEncryptionKey` nincs beállítva vagy érvénytelen. Konfiguráljon egy base64 kódolt 32 byte-os kulcsot, majd indítsa újra. |
| *HTTPS origin required* | Az URL tartalmaz elérési utat, lekérdezési paramétert vagy beágyazott hitelesítő adatot. Adja meg a tisztán definiált forrást, pl. `https://mail.example.com`. |
| *IMAP host invalid* | Az IMAP host egy olyan hostnév kell, amely nem tartalmaz protokollot vagy portot. |
| *Instance already connected* | Egy másik példány létezik ugyanazzal az URL-rel. Szeressen át arra a példányra. |
| *API request failed (401/403)* | Az API kulcs hiányoznak a szükséges jogosultságokból, vagy az IP korlátozás nem tartalmazza a Mail Archiver hostját. |
| *API request rejected* | A Mailcow egy `error`/`danger`/`warning` sort adott vissza. Ellenőrizze a Mailcow naplókat; a Mail Archiver nem továbbítja a választesteket. |
| *IMAP verification failed* | Rossz IMAP host vagy port, egy nem megbízható tanúsítvány, vagy az alkalmazás-jelszó funkció letiltva van a Mailcow-ban. |
| *IMAP account exists* | Egy független IMAP fiók már archiválja ezt az e-mail címet. Használja azt a fiókot a második archiváló létrehozása helyett. |
| *Revoke previous password* | Ezen postafiók archiváló fiókja törlve lett. Előbb vonja vissza a maradék alkalmazás-jelszavát, majd aktiválja újra. |
| A postafiók **Pending** állapotban marad | A Mailcow API timeoutot kaptál. Frissítse az oldalt, és aktiválja újra a postafiókot; nem lesz duplikált alkalmazás-jelszó. |
| A törlés megtagadásra kerül | A kezelt alkalmazás-jelszavak még aktívak. Előbb vonja vissza őket a postafiók oldalán. |

## Felhasznált API végpontok {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Adatbázis-változások {#database-changes}

A migráció csak a `MailcowInstances` és `MailcowMailboxes` táblákat adja hozzá az indexekkel és idegen kulcsokkal együtt. A meglévő postafiók táblák és az archivált adatok megmaradnak. Egy archiváló fiók törlése a kötés `MailAccountId` értékét `NULL`-ra állítja, így a távoli hitelesítő adat később is visszavonható marad.
