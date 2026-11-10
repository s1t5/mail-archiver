# 📧 Gmail 最佳实践 {#gmail-best-practices}

[← 返回文档索引](index.md)

在使用 Mail Archiver 应用程序配合 Gmail 账号时，应遵循一些特定的配置和最佳实践，以确保最佳性能并避免潜在问题。

## 🔐 使用应用专用密码进行 IMAP 配置 {#imap-configuration-with-app-passwords}

可以使用带有应用专用密码（App Passwords）的 IMAP 将 Gmail 账号集成到 Mail Archiver 应用程序中：

1. 在您的 Gmail 账号上启用两步验证（2-Factor Authentication）
2. 专门为 Mail Archiver 应用程序生成一个应用专用密码
3. 在 Mail Archiver 中配置账号时，使用该应用专用密码而非您的常规 Gmail 密码

## 🏷️ Gmail 标签设置 {#gmail-label-settings}

强烈建议调整您的 Gmail 标签设置，以防止 "All Mail"（所有邮件）标签通过 IMAP 暴露：

1. 进入您的 Gmail Web 界面并打开设置（顶部的齿轮图标）→ 查看所有设置 → 标签（Labels）选项卡
2. 在列表中找到 "All Mail" 标签
3. 取消勾选 "All Mail" 的 "在 IMAP 中显示"（Show in IMAP）选项

### 📚 关于 Gmail 标签实现的背景知识 {#background-on-gmail-label-implementation}

Gmail 的标签系统与传统的电子邮件提供商不同。在 Gmail 中：
- 所有电子邮件都存在于一个单一的 "All Mail" 仓库中
- 标签的作用是标签（Tags）而非文件夹（Folders）
- "All Mail" 标签包含了您账号中的每一封邮件
- 当一封邮件被贴上标签时，它会同时出现在原文件夹和被标记的部分。因此，您收件箱中的所有邮件也会出现在 "All Mail" 标签中。

### ⚠️ IMAP 获取问题 {#issues-with-imap-retrieval}

当 "All Mail" 通过 IMAP 暴露时，可能会导致以下问题：

1. **重复处理**：由于同一封邮件出现在多个文件夹/标签中，归档程序可能会多次处理同一封邮件
2. **性能影响**：对于拥有数千封邮件的大型账号，获取 "All Mail" 可能会极其缓慢
4. **同步问题**：在尝试确定一封邮件实际属于哪个文件夹时，可能会产生冲突

通过禁用对 "All Mail" 的 IMAP 访问，您可以确保邮件仅从其主要位置被获取一次，从而提高性能和准确性。

## 🗑️ 保留策略与自动清除设置 {#retention-policy-and-expunge-settings}

如主 README 中所述，Gmail 账号在保留策略方面需要特别注意：

- 必须在 Gmail 设置的 "转发和 POP/IMAP" 选项卡下禁用 Gmail 的 Auto-Expunge（自动清除）功能
- 此设置对于保留策略在 Gmail 账号上正确运行至关重要

配置此设置的步骤：
1. 进入 Gmail 设置 → 查看所有设置 → 转发和 POP/IMAP 选项卡
2. 在 "IMAP 访问" 部分，选择 "Auto-Expunge off - Wait for the client to update the server."（关闭自动清除 - 等待客户端更新服务器）以及 "Move the message to the Bin"（将邮件移至回收站）

## ⚠️ Gmail IMAP 速率限制 {#gmail-imap-rate-limiting}

Gmail 对 IMAP 获取设置了每日 2500MB 的速率限制。在归档大量邮件时，请注意您可能会达到此限制，这可能会导致邮件获取暂时暂停，直到第二天。

### 📊 速率限制处理 {#rate-limit-handling}

Mail Archiver 提供内置的速率限制处理功能，以管理 Gmail 的 IMAP 带宽限制：

- **自动带宽追踪**：监控每个账号每天下载的数据量
- **可配置限制**：为 Gmail 设置 `BandwidthTracking__DailyLimitMb=2500`
- **自动暂停**：达到限制时暂停同步，并在重置后恢复
- **检查点系统**：保存同步进度，并从上次位置恢复

有关详细的配置和用法，请参阅 **[速率限制处理指南](RateLimitHandling.md)**。

### 🔧 Gmail 配置示例 {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 大型邮箱注意事项 {#large-mailbox-considerations}

对于大型 Gmail 邮箱（例如 14 GB），初始同步将需要多天时间：

| 邮箱大小 | 完成所需天数 (按 2500 MB/天计算) |
|--------------|-----------------------------------|
| 5 GB | ~2 天 |
| 10 GB | ~4 天 |
| 14 GB | ~6 天 |
| 20 GB | ~8 天 |

**提示**：在添加大型 Gmail 账号**之前**启用带宽追踪，以确保从一开始就能进行正确的速率限制处理。

## ✅ 最佳实践总结 {#best-practices-summary}

遵循这些最佳实践将有助于确保 Mail Archiver 与您的 Gmail 账号顺畅运行，同时保持数据完整性和最佳性能。
