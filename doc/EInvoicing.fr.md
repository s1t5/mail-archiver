# Aperçu de la facture XRechnung {#xrechnung-invoice-preview}

Mail-Archiver reconnaît les Pièces jointes XML XRechnung et les affiche sous la forme d'un PDF A4 lisible directement dans l'aperçu de la Pièce jointe. Le PDF est généré à la demande et n'est pas stocké dans l'Archive.

## Ce qui est reconnu {#what-is-recognised}

- UBL `Invoice` et `CreditNote` dont le `CustomizationID` contient `xrechnung`.
- CII `CrossIndustryInvoice` dont le
  `GuidelineSpecifiedDocumentContextParameter` contient `xrechnung`.
- Les factures et les avoirs.

Seules les XRechnung sont affichées. Les documents Peppol BIS, EN 16931 simple et ZUGFeRD/Factur-X PDF ne sont pas traités ; leur XML est affiché comme auparavant.

## Où le trouver {#where-to-find-it}

1. Ouvrez un e-mail avec une Pièce jointe XML XRechnung. La liste des Pièces jointes affiche un badge **XRechnung**.
2. Cliquez sur la Pièce jointe. La fenêtre modale d'aperçu affiche le PDF généré en ligne.
3. **Download PDF** enregistre le PDF généré. **Download original XML** enregistre la Pièce jointe qui a été archivée.
4. Le même PDF est disponible via l'API REST.

Le PDF est une aide à la lecture interne, pas l'original juridiquement faisant foi. Le fichier XML soumis reste l'élément faisant foi.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

Le point de terminaison utilise la même authentification et le même périmètre de Compte que les autres [points de terminaison de Pièce jointe](API.md#download-attachment). Il n'est disponible que lorsque `Api:AllowAttachmentDownloads`
est activé. Une Pièce jointe non-XRechnung renvoie `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Limites {#limits}

- Seules les Pièces jointes XML sont prises en compte, jusqu'à 5 MiB.
- Au plus 1000 lignes de facture sont affichées. Si la facture en contient davantage, le PDF indique combien de lignes ne sont présentes que dans le XML.
- Le PDF est généré par requête. Il n'y a pas de cache ni de tâche de fond.

## Pas de validation {#no-validation}

L'aperçu ne valide pas la facture par rapport à XRechnung, EN 16931 ou tout autre ensemble de règles. Il extrait uniquement les champs qu'il affiche. Un fichier XML mal formé ou incomplet qui porte tout de même un identifiant XRechnung peut produire un PDF incomplet ou aucun PDF du tout. Utilisez un validateur dédié lorsque vous avez besoin d'une déclaration de conformité.
