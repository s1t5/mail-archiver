# 🌍 语言与本地化 {#language-and-localization}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

Mail Archiver 界面支持 **14 种语言**。语言是存储在 Cookie 中的**基于浏览器的偏好设置** —— 它不与用户帐户绑定，且永远不会影响存档的邮件本身：邮件始终按接收时的原样存储。

## 🌐 支持的语言 {#supported-languages}

| 语言 | 代码 |
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

**English 是默认语言和回退语言。** 任何翻译未覆盖的内容都将以英文显示，因此部分翻译的语言不会导致标签为空。

## 🔄 切换语言 {#switching-the-language}

1. 点击右上角的**用户名**以打开用户菜单。
2. 在 **Language** 下，从列表中选择一种语言 —— 页面会立即以该语言重新加载。

该选择会被写入一个有效期为**一年**的 Cookie 中，且仅适用于当前浏览器。其他用户，以及同一用户在其他浏览器或设备上的设置，将保持各自的设置。由于 Cookie 的有效期长于会话，因此在您退出并重新登录后，语言设置仍然有效。

## 🧩 语言设置影响（及不影响）的内容 {#what-the-language-setting-does-and-does-not-affect}

- **已翻译：** 所有界面文本 —— 菜单、按钮、表单标签、验证和错误消息、任务状态页面以及访问日志的操作描述。
- **未翻译：** 存档邮件。正文、主题和附件按接收时的字节逐一存储和显示。
- **搜索：** 查询语法在每种语言中都是相同的；请参阅 [Mail search](Search.md)。更改语言不会改变邮件的存储或索引方式。
- **时间戳：** 日期按浏览器的本地时区显示，并针对所选文化进行格式化。若要将显示时区固定为全实例统一，请设置 `TimeZone__DisplayTimeZoneId`（IANA 标识符，例如 `Europe/Berlin`）—— 参见 [Setup and Parameters](Setup.md#-timezone-settings)。

## 🛠️ 致贡献者：添加或更改翻译 {#for-contributors-adding-or-changing-translations}

翻译字符串存储在 `Resources/` 中：

| 文件 | 用途 |
|------|---------|
| `SharedResource.resx` | 中性资源文件 —— **English**，包含 1148 个键 |
| `SharedResource.<culture>.resx` | 每种语言一个文件，例如 `SharedResource.de.resx` |

以下是基于 .NET 解析资源方式的规则：

1. **先向中性文件添加新键**，然后再添加到语言文件。如果翻译中缺少某个键，则会回退到英文文本 —— 这不会导致程序崩溃，页面只是会保持部分英文显示。
2. **仅存在于翻译文件中的键是冗余的。** 因为查找过程是从中性文件开始的，所以这些键永远不会被查找。（在编写本文时，`DeleteAccountAllEmailsButton` 和 `Sign Out` 仅存在于某些翻译中，且波兰语文件还携带了残留的设计师键：`Bitmap1`、`Icon1`、`Name1`。）
3. **设计上，`en-GB` 是部分覆盖。** 它仅定义了 1148 个键中的 245 个，并从英文中性文件继承其余部分 —— 这就是为什么除了英式用语或日期格式不同外，它读起来很像英文。目前，中文（简体）、德语、西班牙语、法语、匈牙利语、意大利语、日语、荷兰语、波兰语、葡萄牙语（巴西）、俄语和斯洛文尼亚语已实现完整翻译。
4. **添加完整语言**需要三件事：一个新的 `SharedResource.<culture>.resx`、在 `Program.cs` 的文化列表中添加条目（`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`），以及在 `Views/Shared/_UserInfo.cshtml` 的语言下拉菜单中添加 `<option>`。如果没有后两项，该文件将永远不会被使用。

有关贡献流程、编码标准以及如何提交 Pull Request，请参阅 [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md)。

## 🔗 相关文档 {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — 查询语法
- [User Management](UserManagement.md) — 角色与权限
