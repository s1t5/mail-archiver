# 🌍 Langue et localisation {#language-and-localization}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

L'interface de Mail Archiver est disponible en **14 langues**. La langue est une
**préférence par navigateur** stockée dans un cookie — elle n'est pas liée à un Compte utilisateur,
et elle n'affecte jamais les e-mails archivés eux-mêmes : les messages sont toujours stockés exactement
tels qu'ils ont été reçus.

## 🌐 Langues prises en charge {#supported-languages}

| Langue | Code |
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

**L'anglais est la langue par défaut et de repli.** Tout ce qu'une traduction ne couvre pas
est affiché en anglais, de sorte qu'une langue partiellement traduite ne vous laisse jamais avec un
libellé vide.

## 🔄 Changer de langue {#switching-the-language}

1. Cliquez sur votre **nom d'utilisateur** dans le coin supérieur droit pour ouvrir le menu utilisateur.
2. Sous **Language**, choisissez une langue dans la liste — la page se recharge
   immédiatement dans cette langue.

Le choix est écrit dans un cookie qui est valide pendant **un an** et s'applique
uniquement à ce navigateur. Les autres utilisateurs, et le même utilisateur dans un autre navigateur ou sur
un autre appareil, conservent leurs propres paramètres. Comme le cookie survit à la session,
la langue est toujours sélectionnée après votre déconnexion et votre reconnexion.

## 🧩 Ce que le paramètre de langue affecte — et n'affecte pas {#what-the-language-setting-does-and-does-not-affect}

- **Traduit :** tout le texte de l'interface — menus, boutons, libellés de formulaires, messages de validation et
  d'erreur, pages de statut des tâches et descriptions d'actions du journal d'audit.
- **Non traduit :** les e-mails archivés. Les corps, les objets et les Pièces jointes sont stockés
  et affichés bit par bit tels qu'ils ont été reçus.
- **Recherche :** la syntaxe de requête est la même dans toutes les langues ; voir
  [Mail search](Search.md). Changer la langue ne change pas la manière dont les e-mails sont
  stockés ou indexés.
- **Horodatages :** les dates sont affichées dans le fuseau horaire local du navigateur et
  formatées selon la culture sélectionnée. Pour fixer le fuseau horaire d'affichage pour toute l'instance,
  définissez `TimeZone__DisplayTimeZoneId` (identifiant IANA, par exemple
  `Europe/Berlin`) — voir [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ Pour les contributeurs : ajouter ou modifier des traductions {#for-contributors-adding-or-changing-translations}

Les chaînes traduites se trouvent dans `Resources/` :

| Fichier | Utilité |
|------|---------|
| `SharedResource.resx` | Fichier de ressources neutre — **English**, 1148 clés |
| `SharedResource.<culture>.resx` | Un fichier par langue, par exemple `SharedResource.de.resx` |

Règles découlant de la manière dont .NET résout les ressources ici :

1. **Ajoutez d'abord les nouvelles clés au fichier neutre**, puis aux fichiers de langue. Une clé
   manquante dans une traduction bascule sur le texte anglais — rien ne
   se casse, la page reste simplement partiellement en anglais.
2. **Une clé qui n'existe que dans un fichier de traduction est un poids mort.** Elle n'est jamais
   recherchée, car la recherche commence par le fichier neutre. (Au moment de la rédaction,
   `DeleteAccountAllEmailsButton` et `Sign Out` n'existent que dans certaines traductions,
   et le fichier polonais contient également des clés de conception restantes : `Bitmap1`, `Icon1`,
   `Name1`.)
3. **`en-GB` est un remplacement partiel, par conception.** Il ne définit que 245 des 1148
   clés et hérite du reste du fichier anglais neutre — c'est pourquoi il
   se lit comme de l'anglais, sauf là où la formulation britannique ou les formats de date diffèrent.
   Le chinois (simplifié), l'allemand, l'espagnol, le français, le hongrois, l'italien,
   le japonais, le néerlandais, le polonais, le portugais (Brésil), le russe et le slovène sont
   actuellement complets.
4. **L'ajout d'une langue entière** nécessite trois choses : un nouveau
   `SharedResource.<culture>.resx`, une entrée dans la liste des cultures dans `Program.cs`
   (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`),
   et un `<option>` dans le menu déroulant des langues dans
   `Views/Shared/_UserInfo.cshtml`. Sans ces deux derniers, le fichier n'est jamais utilisé.

Consultez [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md)
pour le processus de contribution, les normes de codage et la manière de soumettre une pull request.

## 🔗 Documentation connexe {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — syntaxe de requête
- [User Management](UserManagement.md) — rôles et Autorisation
