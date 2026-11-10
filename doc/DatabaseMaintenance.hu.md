# 🔧 Adatbázis karbantartási útmutató {#database-maintenance-guide}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató elmagyarázza, hogyan kell konfigurálni és használni a Mail Archiver automatikus adatbázis-karbantartási funkcióját. Az alkalmazás tartalmaz egy beépített háttérszolgáltatást, amely automatikusan végrehajtja a PostgreSQL adatbázis-karbantartási műveleteket az optimális teljesítmény fenntartása és az adatbázis "duzzadása" (bloat) megelőzése érdekében.

## ⚠️ Fontos megjegyzés {#important-notice}

**A PostgreSQL adatbázisok nem zsugorodnak össze automatikusan a törölt adatok után.** Amikor e-maileket vagy postafiókokat töröl, az adatbázis a helyet újrahasználhatóként jelöli meg, de az adatbázis fájl tényleges mérete változatlan marad. Ezt a jelenséget "adatbázis duzzadásnak" (database bloat) nevezik.

A Mail Archiver tartalmaz egy automatikus karbantartási szolgáltatást, amely napi szinten futtat VACUUM ANALYZE műveleteket a következőképpen:
- A tárhely visszaszerzése az adatbázison belüli újrahasznosításhoz
- A lekérdezési statisztikák frissítése az optimális teljesítmény érdekében
- A teljesítmény idővel történő romlásának megelőzése
- Az adatbázis egészségének megőrzése

## 🚀 Automatikus karbantartás (Ajánlott) {#automatic-maintenance-recommended}

### Konfiguráció {#configuration}

Engedélyezze az automatikus adatbázis-karbantartást a `appsettings.json`-ben vagy környezeti változókon keresztül a `docker-compose.yml`-ban:

#### 1. opció: appsettings.json használatával {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### 2. opció: docker-compose.yml használatával (Ajánlott) {#option-2-via-docker-composeyml-recommended}

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Enable automatic database maintenance
      - DatabaseMaintenance__Enabled=true
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30
```

### Konfigurációs opciók {#configuration-options}

| Paraméter | Típus | Alapértelmezett | Leírás |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Az automatikus karbantartás engedélyezése vagy kikapcsolása |
| `DailyExecutionTime` | string | `"02:00"` | A karbantartás futtatásának időpontja (HH:mm formátum, 24 órás) |
| `TimeoutMinutes` | integer | `30` | A karbantartási műveletekhez megengedett maximális időperc |

### Hogyan működik {#how-it-works}

Engedélyezés esetén a karbantartási szolgáltatás:

1. **Automatikusan elindul** az alkalmazással együtt
2. **Vár** a konfigurált futtatási időpontig
3. **Végrehajtja a VACUUM ANALYZE** parancsot az egész adatbázison
4. **Logolja a műveletet** az alkalmazás hozzáférési logjában a folyamat időtartamával együtt
5. **Újra ütemez** a következő napra

A karbantartási művelet:
- ✅ Downtime (leállás) nélkül fut
- ✅ Nem akadályozza a normál alkalmazásműködést
- ✅ Teljes logolást és monitorozást biztosít
- ✅ Visszaszerzi a tárhelyet az adatbázason belüli újrahasznosításhoz
- ✅ Frissíti a lekérdezési statisztikákat a jobb teljesítmény érdekében

### Az időpont kiválasztása {#choosing-the-execution-time}

Válasszon egy olyan időpontot, amikor a rendszer legalacsonyabb aktivitási szinten van:
- **Ajánlott**: 02:00 és 04:00 között (éjszakai órák)
- **Fontos megfontolni**: Az időzóna beállítása
- **Kerülje**: A csúcsidőszakokat vagy a mentési ablakokat

Példa konfigurációk:

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Az automatikus karbantartás monitorozása {#monitoring-automatic-maintenance}

#### 1. Alkalmazás logjai {#1-application-logs}

Ellenőrizze az alkalmazás logjait a karbantartási tevékenységekért:

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Hozzáférési log (UI) {#2-access-log-ui}

Minden karbantartási művelet a alkalmazás hozzáférési logjában található meg:

1. Navigáljon a UI-ben a **Logs** → **Access Log** menüpontba
2. Szűrje a típust a következőre: **Database Maintenance**
3. Tekintse meg a futtatási időpontokat, az időtartamot és esetleges hibákat

## 🔧 Manuális karbantartás (Haladó) {#manual-maintenance-advanced}

### VACUUM FULL a tárhely visszaszerzéséhez {#vacuum-full-for-disk-space-recovery}

**⚠️ Alkalmazás leállását igényel**

A VACUUM FULL tömöríti az adatbázist és visszajuttatja a tárhelyet az operációs rendszernek, de kizárólagos zárolást (exclusive locks) igényel.

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**Mikor használja:**
- Nagy postafiókok törlése után
- Jelentős tárhely visszaszerzése érdekében
- Időközi karbantartási ablakokban

**Fontos:**
- A táblaméretnek megfelelő szabad tárhelyet igényel
- Nagy adatbázisok esetén órákig tarthat
- Blokkol minden adatbázis-műveletet
- Csak a kevésbé aktív időszakokban kell ütemezni

A részletes manuális folyamatokért lásd a [PostgreSQL VACUUM Dokumentációt](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Kapcsolódó dokumentáció {#related-documentation}

- [Beállítási útmutató](Setup.md) - Kezdő konfiguráció és környezeti változók
- [Mentési és visszaállítási útmutató](BackupRestore.md) - Adatbázis mentési folyamatok
- [PostgreSQL VACUUM Dokumentáció](https://www.postgresql.org/docs/current/sql-vacuum.html) - Hivatalos PostgreSQL dokumentáció

## 📝 Összegzés {#summary}

✅ **Engedélyezze az automatikus karbantartást** a kézi adatbázis-optimalizálás elkerülése érdekében
✅ **Monitorozza a hozzáférési logok segítségével** a sikeres műveletek ellenőrzéséhez
✅ **Csak akkor használjon manuális VACUUM FULL parancsot**, ha tárhely-visszaszerzésre van szükség
✅ **Ütemezze a VACUUM FULL parancsot** karbantartási ablakokba a nagy törlések után
✅ **Tartsa naprakész mentéseket** bármilyen manuális karbantartási művelet előtt

Az automatikus karbantartási szolgáltatás kezeli a napi adatbázis-optimalizálást, így a legtöbb esetben szükségét veszi ki a manuális beavatkozásnak.
