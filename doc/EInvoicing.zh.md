# XRechnung 发票预览 {#xrechnung-invoice-preview}

Mail-Archiver 会识别 XRechnung XML 附件，并将其直接在附件预览中渲染为可读的 A4 PDF。该 PDF 是按需生成的，不会存储在存档中。

## 识别内容 {#what-is-recognised}

- `CustomizationID` 包含 `xrechnung` 的 UBL `Invoice` 和 `CreditNote`。
- `GuidelineSpecifiedDocumentContextParameter` 包含 `xrechnung` 的 CII `CrossIndustryInvoice`。
- 发票和贷记单（credit notes）。

仅渲染 XRechnung。Peppol BIS、纯 EN 16931 文档以及 ZUGFeRD/Factur-X PDF 会保持原样；它们的 XML 将按原样显示。

## 如何查找 {#where-to-find-it}

1. 打开一封带有 XRechnung XML 附件的电子邮件。附件列表会显示 **XRechnung** 徽章。
2. 点击附件。预览模态框会在行内渲染生成的 PDF。
3. **Download PDF** 保存生成的 PDF。**Download original XML** 保存被存档的附件。
4. 同一个 PDF 也可通过 REST API 获取。

该 PDF 仅作为内部阅读辅助工具，并非具有法律效力的原始文件。提交的 XML 文件仍具有权威性。

## REST API {#rest-api}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}/invoice-pdf
```

该端点使用与其他 [attachment endpoints](API.md#download-attachment) 相同的身份验证和账户作用域。仅在启用 `Api:AllowAttachmentDownloads` 时可用。非 XRechnung 附件会返回 `404`。

```bash
curl -H "Authorization: Bearer <api-key>" \
     -o rechnung.pdf \
     "https://archive.example.org/api/v1/emails/123/attachments/456/invoice-pdf"
```

## 限制 {#limits}

- 仅考虑大小不超过 5 MiB 的 XML 附件。
- 最多渲染 1000 行发票明细。如果发票行数更多，PDF 会说明有多少行仅存在于 XML 中。
- PDF 是按请求生成的。没有缓存，也没有后台作业。

## 无校验 {#no-validation}

预览不会根据 XRechnung、EN 16931 或任何其他规则集对发票进行校验。它仅提取其显示的字段。格式错误或不完整的 XML 文件（即使仍带有 XRechnung 标识符）可能会生成稀疏的 PDF 或根本不生成 PDF。当您需要合规性声明时，请使用专门的校验器。
