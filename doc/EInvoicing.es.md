# Vista previa de facturas XRechnung {#xrechnung-invoice-preview}

Mail-Archiver reconoce Adjuntos XML de XRechnung y los renderiza como un
PDF A4 legible directamente en la vista previa del Adjunto. El PDF se genera bajo
demanda y no se almacena en el Archivo.

## Qué se reconoce {#what-is-recognised}

- UBL `Invoice` y `CreditNote` cuyo `CustomizationID` contiene `xrechnung`.
- CII `CrossIndustryInvoice` cuyo
  `GuidelineSpecifiedDocumentContextParameter` contiene `xrechnung`.
- Tanto facturas como notas de crédito.

Solo se renderiza XRechnung. Los documentos Peppol BIS, EN 16931 simple y
ZUGFeRD/Factur-X PDF se dejan intactos; su XML se muestra como antes.

## Dónde encontrarlo {#where-to-find-it}

1. Abra un correo electrónico con un Adjunto XML de XRechnung. La lista de Adjuntos muestra una
   insignia de **XRechnung**.
2. Haga clic en el Adjunto. El modal de vista previa renderiza el PDF generado en línea.
3. **Download PDF** guarda el PDF generado. **Download original XML** guarda el
   Adjunto que fue archivado.
4. El mismo PDF está disponible a través de la REST API.

El PDF es una ayuda de lectura interna, no el original legalmente autoritativo. El
archivo XML enviado sigue siendo el autoritativo.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

El endpoint utiliza la misma autenticación y el mismo alcance de Cuenta que los otros
[endpoints de Adjuntos](API.md#download-attachment). Solo está disponible cuando `Api:AllowAttachmentDownloads`
está habilitado. Un Adjunto que no sea XRechnung devuelve `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Límites {#limits}

- Solo se consideran Adjuntos XML, hasta 5 MiB.
- Se renderizan como máximo 1000 líneas de factura. Si la factura tiene más, el PDF
  indica cuántas líneas solo están presentes en el XML.
- El PDF se genera por solicitud. No hay caché ni trabajo en segundo plano.

## Sin validación {#no-validation}

La vista previa no valida la factura contra XRechnung, EN 16931 o cualquier
otro conjunto de reglas. Solo extrae los campos que muestra. Un archivo XML malformado o
incompleto que aún lleve un identificador XRechnung puede producir un
PDF disperso o ningún PDF en absoluto. Utilice un validador dedicado cuando necesite un
certificado de conformidad.
