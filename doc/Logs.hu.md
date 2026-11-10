# 📋 Napló (Access Log) {#access-log}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató elmagyarázza a Mail Archiver alkalmazás naplózási funkcióit, amelyek lehetővé teszik a rendszeren belüli felhasználói tevékenységek rögzítését és megtekintését. Ez a funkcióalapfeltétel a megfigyelés, a biztonság és a hibakeresés céljából is kulcsfontosságú.

## 📊 Mit naplóztat a rendszer {#what-is-logged}

Az alkalmazás különféle felhasználói tevékenységeket naplóztat, beleértve:

1. **Bejelentkezés és kijelentkezés** - A bejelentkezési és kijelentkezési műveletek naplózása
2. **Keresési műveletek** - Az e-mail keresési kérések naplózása a rendszeren belül
3. **E-mail megnyitása** - Naplózza, amikor a felhasználók megnyitják az e-mail tartalmát
4. **E-mail letöltése** - Naplózza, amikor a felhasználók letöltik az e-maileket
5. **E-mail visszaállítása** - Naplózza, amikor a felhasználók visszaállítják az archivált e-maileket
6. **Fiókkezelés** - A fiókkal kapcsolatos műveletek naplózása
7. **Törlési műveletek** - A törlési műveletek naplózása
8. **Audit Data Export** - Az audit adatok exportálási futtatásainak naplózása (kezdés és eredmény, lásd: [Audit Data Export](AuditExport.md))

## 📝 Művelettípusok és leírások {#action-types-and-descriptions}

A naplózott műveletek a következő kategóriákba vannak sorolva:

### Login {#login}
- **Leírás**: Naplózza, amikor egy felhasználó bejelentkezik a rendszerbe
- **Részletek**: Username, timestamp

### Logout {#logout}
- **Leírás**: Naplózza, amikor egy felhasználó kijelentkezik a rendszerből
- **Részletek**: Username, timestamp

### Search {#search}
- **Leírás**: Naplózza az e-mail rendszeren belüli keresési kéréseket
- **Részletek**: Username, timestamp, search parameters

### Open {#open}
- **Leírás**: Naplózza, amikor egy felhasználó megnyit egy e-mailt
- **Részletek**: Username, timestamp, email ID, sender, subject

### Download {#download}
- **Leírás**: Naplózza, amikor a felhasználók letöltik az e-maileket
- **Részletek**: Username, timestamp, email ID, sender, subject

### Restore {#restore}
- **Leírás**: Naplózza, amikor a felhasználók visszaállítják az archivált e-maileket
- **Részletek**: Username, timestamp, email ID, sender, subject

### Account {#account}
- **Leírás**: Naplózza a fiókkal kapcsolatos műveleteket (létrehozás, módosítás stb.)
- **Részletek**: Username, timestamp, account ID

### Deletion {#deletion}
- **Leírás**: Naplózza az e-mailekkel vagy egyéb erőforrásokkal kapcsolatos törlési műveleteket
- **Részletek**: Username, timestamp, affected resource
- **Compliance megjegyzés**: A törlési műveleteket a megfelelőségi auditálási célokból naplózzuk

## 🔍 Szűrési lehetőségek {#filter-options}

A naplóoldal a következő szűrési lehetőségeket kínálja:

1. **Date** - Szűrés időintervallum szerint (From-To)
2. **Username** - Szűrés konkrét felhasználó szerint (csak Adminoknak)
3. **Action Type** - Szűrés konkrét művelet típusa szerint

## 🖥️ Napló megjelenítése {#log-display}

A naplók táblázatos formátumban jelennek meg a következő oszlopokkal:

- **Timestamp**: Az művelet dátuma és időpontja
- **Username**: Az érintett felhasználó neve
- **Type**: A művelet típusa színkódolással
- **Information**: További részletek a műveletről

Mobilalkalmazások esetén a naplók kártyás formátumban jelennek meg.

## 🔐 Hozzáférési jogosultságok {#access-permissions}

- **Nem admin felhasználók**: Csak a saját naplóik megtekintésére jogosultak
- **Admin felhasználók**: Minden felhasználó naplóját megtekinthetik, és felhasználó szerint szűrhetnek

## 📤 Audit Data Export {#audit-data-export}

Az adminisztrátorok közvetlenül a Napló oldalról megnyithatják az erre kifejelekt [Audit Data Export](AuditExport.md) oldalt. Ez táblázatos tömegadat-csomagokat (INDEX.XML + CSV) generál az archívából a külső auditáló eszközök számára. Minden export futtatás két **Audit Data Export** típusú naplóbejegyzést ír (kezdés és eredmény), amelyek a Napló oldalon, mint minden egyéb bejegyzés, listázhatók és szűrhetők.

Részletekért lásd az [Audit Data Export guide](AuditExport.md) dokumentációt.
