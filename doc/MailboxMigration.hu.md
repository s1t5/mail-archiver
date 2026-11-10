# 📥 Mailbox Migration Guide {#mailbox-migration-guide}

[← Vissza a dokumentáció indexéhez](index.md)

Ez a útmutató elmagyarázza, hogyan lehet egy postafalakat egy e-mail szolgáltatóról másikra migrálni a Mail Archiver alkalmazás használatával.

## 📋 Áttekintés {#overview}

A Mail Archiver alkalmazás funkciókat biztosít az e-mailek forrás postafalakból cél postafalakba történő migráálásához. Ez különösen hasznos, amikor e-mail szolgáltatót váltunk, miközben meg akarjuk őrizni az e-mail előzményeket. A forrás postafalak mappastruktúrája a migráción keresztül is megmarad.

> **Csak a friss leveleket migrálja?** Az alább leírt folyamat az *egész* archivált postafalakot másolja át, és ha kétszer futtatja, minden adatot másodszor is hozzáad. Ha az új postafalakban csak egy meghatározott időpont utáni leveleket szeretné tárolni, miközben a teljes előzmény az archívumban marad, akkor használja helyette a [date-windowed offload](Offload.md) funkciót. Ez fogad egy időintervallumot, kizárhat és átnevezhet mappákat, kínál tesztfuttatást (dry run), és biztonságosan ismételhető, mivel átugorja azokat az adatokat, amelyek már a célpostafalakban szerepelnek. Emellett parancssorból is irányítható, ami lehetővé teszi a postafalak flottájának szkriptelését.

## 🚀 Migrációs folyamat {#migration-process}

Kövesse az alábbi lépéseket a postafalak migrázásához:

1. **A forrás fiók hozzáadása**
   - Navigáljon az "Email Accounts" részhez
   - Kattintson az "Add Account" gombra
   - Adja meg a forrás fiók adatait és hitelesítő adatait
   - Mentse el a fiókot

2. **A forrás fiók szinkronizálása**
   - Engedélyezze az alkalmazás számára az összes e-mail szinkronizálását a forrás fiókból
   - Várja meg a szinkronizálási folyamat befejeződését

3. **A cél fiók hozzáadása**
   - Az "Email Accounts" részben kattintson az "Add Account" gombra
   - Adja meg a cél fiók adatait és hitelesítő adatait
   - Mentse el a fiókot

4. **A cél fiók szinkronizálása**
   - Ha a cél fiók új, szinkronizálja azt, hogy biztosítsa a megfelelő beállítást
   - Várja meg a szinkronizálási folyamat befejeződését

5. **A migráció elindítása**
   - Az "Email Accounts" részben azonosítsa meg a forrás fiókot
   - Menjen a fiók részleteihez
   - Válassza a "Copy All Emails to Another Mailbox" opciót
   - Válassza ki a cél fiókot és egy cél mappát
   - Engedélyezze a **"Preserve Folder Structure"** opciót – ilyenkor a kiválasztott cél mappa szolgál gyökérként, amely alá a teljes forrás mappastruktúra rekurzív módon újraépül
   - Ha ez az opció nincs engedélyezve, az összes e-mail közvetlenül a kiválasztott cél mappába kerül (lapos formában, almappák nélkül)
   - Indítsa el a migrációs folyamatot

6. **A migráció figyelése**
   - Ha nagy számú e-mail érintett, a migráció háttérfolyamatként zajlik le
   - A migráció állapota és haladása a "Jobs" fülön tekinthető meg
   - Várja meg, amíg a migráció sikeresen befejeződik

7. **A forrás fiók letiltása**
   - A sikeres átvitel után térjen vissza a forrás fiókhoz
   - Állítsa a forrás fiókot "Disabled" állapotba a fiókbeállításokban
   - Ez biztosítja, hogy a fiókot a jövőben már ne archiválják

## ⚠️ Fontos megjegyzések {#important-notes}

- Ez a folyamat nem ismételhető: ha újra futtatja, minden üzenetet másodszor is hozzáad. A [date-windowed offload](Offload.md) a tekrázható alternatíva.
- Győződjön meg róla, hogy a forrás fiók rendelkezik a megfelelő jogosultságokkal az összes e-mail olvasásához
- Ellenőrizze, hogy a cél fiók elegendő tárhelyrel rendelkezik-e az átmigrált e-mailekhez
- A migrációs folyamat időt vehet igénybe az e-mailek számától és a csatlakozási sebességtől függően
- Javasolt a migráció elvégzése a forgalom alacsonyabb időszakaiban, hogy minimalizálja a hatást az e-mail szolgáltatásra
- Rendszeresen ellenőrizze a migráció állapotát a "Jobs" fülön, hogy biztosítsa a folyamat haladását
- A migráció után ellenőrizze, hogy minden e-mail sikeresen átkerült a cél fiókba

## ✅ Best Practices {#best-practices}

- Mindig tesztelje a migráciási folyamatot e-mailek kis részletével, mielőtt az egész postafalakot migrálná
- Tartson mag себе biztonsági mentést a fontos e-mailekről a migráció megkezdése előtt
- Időzítsen a migrációt olyan időpontra, amikor az e-mail használat minimális
- Kövesse szorosan a migráció haladását, és készüljen fel bármilyen felmerülő probléma kezelésére
- A migráció befejeztével ellenőrizze, hogy minden e-mail helyesen került átra és elérhető
