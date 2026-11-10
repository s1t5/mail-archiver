# 🌍 Nyelv és lokalizáció {#language-and-localization}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

A Mail Archiver felülete **14 nyelven** érhető el. A nyelv **böngészőre vonatkozó preferencia**, amelyet egy cookie tárol — nem kötődik a felhasználói fiókhoz, és soha nem befolyásolja magát a learchivált leveleket: az üzenetek mindig pontosan úgy tárolódnak, ahogy kapták őket.

## 🌐 Támogatott nyelvek {#supported-languages}

| Nyelv | Kód |
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

**Az angol az alapértelmezett és a visszahivatkozási nyelv (fallback).** Bármit, amit egy fordítás nem fed le, angolul jelenít meg, így egy részben lefordított nyelv sosem hagy magára üres címkét.

## 🔄 A nyelv váltása {#switching-the-language}

1. Kattintson a **felhasználónevére** a jobb felső sarokban a felhasználói menü megnyitásához.
2. A **Language** menüpont alatt válasszon egy nyelvet a listából — az oldal azonnal újratöltődik az adott nyelven.

A választ egy cookie-be mentjük, amely **egy évig** érvényes, és csak erre a böngészőre vonatkozik. Más felhasználók, illetve ugyanazon felhasználó egy másik böngészőben vagy eszközön, saját beállításokat kezelnek. Mivel a cookie túlél a munkameneten (session), a nyelv kijelölve marad a kijelentkedés és a újrajelentkezés után is.

## 🧩 Mit befolyásol a nyelvi beállítás — és mit nem {#what-the-language-setting-does-and-does-not-affect}

- **Fordított:** minden felületi szöveg — menük, gombok, űrlap címkék, validációs és hibaüzenetek, feladatállapot-oldalak és az hozzáférési napló műveleti leírásai.
- **Nem fordított:** learchivált levelek. A levelek tartalma, tárgya és csatolmányai byte-ról byte ugyanúgy tárolódnak és jelennek meg, ahogy kapták őket.
- **Keresés:** a lekérdezési szintaxis minden nyelven azonos; lásd: [Mail search](Search.md). A nyelv megváltoztatása nem változtatja meg a levelek tárolásának vagy indexelésének módját.
- **Időbélyegletek:** a dátumok a böngésző helyi időzónájában jelennek meg, és a kiválasztott kultúrának megfelelően vannak formázva. Ha az egész példány (instance) szintű megjelenítési időzónát szeretné rögzíteni, állítsa be a `TimeZone__DisplayTimeZoneId` beállítást (IANA azonosító, pl. `Europe/Berlin`) — lásd: [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ A hozzájárulóknak: fordítások hozzáadása vagy módosítása {#for-contributors-adding-or-changing-translations}

A fordított 문자 (strings) a `Resources/` fájlokban találhatóak:

| Fájl | Cél |
|------|---------|
| `SharedResource.resx` | Semleges erőforrásfájl — **English**, 1148 kulcs |
| `SharedResource.<culture>.resx` | Egy fájl minden nyelvre, pl. `SharedResource.de.resx` |

A .NET erőforrásfeloldási mechanizmusából fakadó szabályok:

1. **Először az új kulcsokat a semleges fájlhoz (neutral file) kell adni**, majd a nyelvspecifikus fájlokhoz. Ha egy kulcs hiányzik egy fordításból, az angol szövegre tér vissza — semmi nem romlik el, az oldal egyszerűen csak részben angol marad.
2. **A kulcs, amely csak egy fordításfájlban létezik, felesleges terhet jelent.** Erre sosem kerül sor, mert a keresés a semleges fájltól indul. (A leírás időpontjában `DeleteAccountAllEmailsButton` és `Sign Out` csak egyes fordításokban léteznek, a lengyel fájl pedig felesleges tervezői kulcsokat is tartalmaz: `Bitmap1`, `Icon1`, `Name1`.)
3. **A `en-GB` tervezés szerinti részleges felülbírálás.** Ez csak a 1148 kulcs közül 245-öt határoz meg, és a tö 나머it a semleges angol fájltól örökli — ezért olvasható angolul, kivéve ott, ahol a brit szóhasználat vagy a dátumformátum eltér. A kínai (egyszerűsített), német, spanyol, francia, magyar, olasz, japán, holland, lengyel, portugál (brazil), orosz és szlovén nyelvek jelenleg teljes egészében elérhetőek.
4. **Egy teljes nyelv hozzáadása** három dolgot igényel: egy új `SharedResource.<culture>.resx`-ot, egy bejegyzést a kultúra listában a `Program.cs` fájlban (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`), és egy `<option>`-et a nyelvválasztóban a `Views/Shared/_UserInfo.cshtml` fájlban. Ezek nélkül szemben a fájl sosem kerül felhasználásra.

Lásd a [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md) dokumentációt a hozzájárulási folyamatról, a kódolási szabványokról és arról, hogyan küldhető be pull request.

## 🔗 Kapcsolódó dokumentáció {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — lekérdezési szintaxis
- [User Management](UserManagement.md) — szerepkörök és jogosultságok
