# 🌍 言語とローカライゼーション {#language-and-localization}

[← ドキュメントインデックスに戻る](index.md)

## 📋 概要 {#overview}

Mail Archiverのインターフェースは**14言語**で利用可能です。言語設定は**ブラウザごとの設定**としてCookieに保存されます。これはユーザーアカウントに紐付いているものではなく、アーカイブされたメール自体に影響を与えることもありません。メッセージは常に受信された通りの状態で保存されます。

## 🌐 サポートされている言語 {#supported-languages}

| 言語 | コード |
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

**英語がデフォルトであり、フォールバック（代替）として機能します。** 翻訳がカバーしていない項目は英語で表示されるため、翻訳が一部しか行われていない言語であっても、ラベルが空欄になることはありません。

## 🔄 言語の切り替え {#switching-the-language}

1. 右上の**ユーザー名**をクリックしてユーザーメニューを開きます。
2. **Language**の下からリスト内の言語を選択します。ページは即座にその言語でリロードされます。

選択内容は**1年間**有効なCookieに書き込まれ、このブラウザにのみ適用されます。他のユーザーや、別のブラウザまたは別のデバイスを使用している同一ユーザーは、それぞれ独自の言語設定を保持します。Cookieの有効期限はセッションよりも長いため、サインアウトして再度サインインした後も言語設定は維持されます。

## 🧩 言語設定が影響するもの、および影響しないもの {#what-the-language-setting-does-and-does-not-affect}

- **翻訳されるもの:** すべてのインターフェーステキスト — メニュー、ボタン、フォームのラベル、バリデーションおよびエラーメッセージ、ジョブステータスページ、およびアクセスログのアクション説明。
- **翻訳されないもの:** アーカイブされたメール。本文、件名、添付ファイルは、受信時と同じバイト単位で保存および表示されます。
- **検索:** クエリ構文はすべての言語で共通です。詳細は [Mail search](Search.md) を参照してください。言語を変更しても、メールの保存方法やインデックス作成の方法は変わりません。
- **タイムスタンプ:** 日付はブラウザのローカルタイムゾーンで表示され、選択されたカルチャに合わせてフォーマットされます。インスタンス全体で表示タイムゾーンを固定するには、`TimeZone__DisplayTimeZoneId` (IANA識別子、例: `Europe/Berlin`) を設定してください。詳細は [Setup and Parameters](Setup.md#-timezone-settings) を参照してください。

## 🛠️ コントリビューターの方へ: 翻訳の追加または変更 {#for-contributors-adding-or-changing-translations}

翻訳文字列は `Resources/` に格納されています。

| ファイル | 目的 |
|------|---------|
| `SharedResource.resx` | 中立リソースファイル — **English**、1148個のキー |
| `SharedResource.<culture>.resx` | 言語ごとのファイル、例: `SharedResource.de.resx` |

ここでの .NET によるリソース解決方法に基づいたルールは以下の通りです：

1. **まず中立ファイルに新しいキーを追加し**、その後に言語ファイルに追加してください。翻訳ファイルにキーが欠落している場合は、英語のテキストにフォールバックされます。これにより、ページが壊れることはなく、一部が英語のまま表示されるだけとなります。
2. **翻訳ファイルにのみ存在するキーは無意味です。** ルックアップは中立ファイルから始まるため、それらは参照されません。（執筆時点では、`DeleteAccountAllEmailsButton` と `Sign Out` は一部の翻訳にのみ存在し、ポーランド語のファイルには設計時の残骸であるキー `Bitmap1`、`Icon1`、`Name1` も含まれています。）
3. **`en-GB` は設計上、部分的なオーバーライドです。** これは1148個のキーのうち245個のみを定義しており、残りは中立的な英語ファイルから継承しています。そのため、イギリス英語の表現や日付形式が異なる部分を除き、英語のように見えます。中国語（簡体字）、ドイツ語、スペイン語、フランス語、ハンガリー語、イタリア語、日本語、オランダ語、ポーランド語、ポルトガル語（ブラジル）、ロシア語、スロベニア語は、現在完全な状態です。
4. **言語全体を追加する場合**には、3つの要素が必要です：新しい `SharedResource.<culture>.resx`、`Program.cs` 内のカルチャリストへのエントリ (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`)、および `Views/Shared/_UserInfo.cshtml` の言語ドロップダウンにおける `<option>`。これら2つがないと、ファイルは使用されません。

コントリビューションのプロセス、コーディング標準、およびプルリクエストの送信方法については、[CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md) を参照してください。

## 🔗 関連ドキュメント {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — クエリ構文
- [User Management](UserManagement.md) — ロールと権限
