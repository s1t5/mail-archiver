# XRechnung invoice preview {#xrechnung-invoice-preview}

Mail-Archiver は XRechnung XML 添付ファイルを認識し、添付ファイルプレビュー内で直接、読みやすい A4 PDF としてレンダリングします。PDF はオンデマンドで生成され、アーカイブには保存されません。

## 認識されるもの {#what-is-recognised}

- `CustomizationID` に `xrechnung` を含む UBL `Invoice` および `CreditNote`。
- `GuidelineSpecifiedDocumentContextParameter` に `xrechnung` を含む CII `CrossIndustryInvoice`。
- 請求書（invoices）とクレジットノート（credit notes）の両方。

XRechnung のみがレンダリングされます。Peppol BIS、プレーンな EN 16931 ドキュメント、および ZUGFeRD/Factur-X PDF は対象外であり、それらの XML は従来通り表示されます。

## 確認方法 {#where-to-find-it}

1. XRechnung XML 添付ファイルを含むメールを開きます。添付ファイルリストに **XRechnung** バッジが表示されます。
2. 添付ファイルをクリックします。プレビューモーダル内で生成された PDF がインラインでレンダリングされます。
3. **Download PDF** をクリックすると、生成された PDF が保存されます。**Download original XML** をクリックすると、アーカイブされた添付ファイルが保存されます。
4. 同じ PDF は REST API を通じて利用可能です。

この PDF は内部的な閲覧補助ツールであり、法的に権威のある原本ではありません。提出された XML ファイルが引き続き権威を持ちます。

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

このエンドポイントは、他の [attachment endpoints](API.md#download-attachment) と同じ認証およびアカウントスコープを使用します。これは `Api:AllowAttachmentDownloads` が有効な場合にのみ利用可能です。XRechnung 以外の添付ファイルの場合は `404` が返されます。

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## Limits {#limits}

- 最大 5 MiB までの XML 添付ファイルのみが対象となります。
- 最大 1000 行の請求書明細（invoice lines）までがレンダリングされます。請求書にそれ以上の行がある場合、PDF には XML にのみ存在する行数が記載されます。
- PDF はリクエストごとに生成されます。キャッシュやバックグラウンドジョブはありません。

## No validation {#no-validation}

プレビューは、XRechnung、EN 16931、またはその他のルールセットに対して請求書の検証（バリデーション）は行いません。表示するフィールドを抽出するだけです。XRechnung の識別子を保持していても、不正な形式または不完全な XML ファイルの場合、不完全な PDF が生成されるか、あるいは PDF が全く生成されないことがあります。適合性に関する証明が必要な場合は、専用のバリデーターを使用してください。
