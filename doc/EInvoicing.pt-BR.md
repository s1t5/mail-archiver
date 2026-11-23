# Visualização de fatura XRechnung {#xrechnung-invoice-preview}

O Mail-Archiver reconhece Anexos XML XRechnung e os renderiza como um PDF A4 legível diretamente na visualização do Anexo. O PDF é gerado sob demanda e não é armazenado no Arquivo.

## O que é reconhecido {#what-is-recognised}

- UBL `Invoice` e `CreditNote` cujo `CustomizationID` contém `xrechnung`.
- CII `CrossIndustryInvoice` cujo
  `GuidelineSpecifiedDocumentContextParameter` contém `xrechnung`.
- Tanto faturas quanto notas de crédito.

Apenas o XRechnung é renderizado. Documentos Peppol BIS, EN 16931 simples e PDFs ZUGFeRD/Factur-X são ignorados; seu XML é exibido como antes.

## Onde encontrar {#where-to-find-it}

1. Abra um e-mail com um Anexo XML XRechnung. A lista de Anexos mostra um selo **XRechnung**.
2. Clique no Anexo. O modal de visualização renderiza o PDF gerado inline.
3. **Download PDF** salva o PDF gerado. **Download original XML** salva o Anexo que foi arquivado.
4. O mesmo PDF está disponível através da REST API.

O PDF é um auxílio de leitura interno, não o original legalmente autoritativo. O arquivo XML enviado permanece como o autoritativo.

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

O endpoint utiliza a mesma autenticação e escopo de Conta que os outros endpoints de Anexo. Ele só está disponível quando `Api:AllowAttachmentDownloads`
está habilitado. Um Anexo que não seja XRechnung retorna `404`.

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Limites {#limits}

- Apenas Anexos XML são considerados, até 5 MiB.
- No máximo 1000 linhas de fatura são renderizadas. Se a fatura tiver mais, o PDF
  informa quantas linhas estão presentes apenas no XML.
- O PDF é gerado por requisição. Não há cache nem job em segundo plano.

## Sem validação {#no-validation}

A visualização não valida a fatura em relação ao XRechnung, EN 16931 ou qualquer
outro conjunto de regras. Ela apenas extrai os campos que exibe. Um arquivo XML malformado ou
incompleto que ainda carregue um identificador XRechnung pode produzir um PDF esparso ou nenhum PDF. Use um validador dedicado quando precisar de uma
declaração de conformidade.
