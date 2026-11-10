# 📋 Access Log {#access-log}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南介绍了 Mail Archiver 应用程序中的访问日志（Access Log）功能，该功能支持对系统内的用户活动进行记录和查看。此功能对于监控、安全和调试至关重要。

## 📊 记录内容 {#what-is-logged}

应用程序记录各种类型的用户活动，包括：

1. **登录与登出** - 记录登录和登出操作
2. **搜索操作** - 记录系统内的电子邮件搜索请求
3. **打开邮件** - 记录用户打开邮件内容的行为
4. **下载邮件** - 记录用户下载邮件的行为
5. **邮件恢复** - 记录用户恢复存档邮件的行为
6. **账户管理** - 记录与账户相关的操作
7. **删除操作** - 记录删除行为
8. **审计数据导出** - 记录审计数据导出运行情况（开始与结果，参见 [Audit Data Export](AuditExport.md)）

## 📝 操作类型与描述 {#action-types-and-descriptions}

记录的操作分为以下类型：

### Login {#login}
- **描述**: 记录用户登录系统时
- **详情**: Username, timestamp

### Logout {#logout}
- **描述**: 记录用户登出系统时
- **详情**: Username, timestamp

### Search {#search}
- **描述**: 记录电子邮件系统内的搜索请求
- **详情**: Username, timestamp, search parameters

### Open {#open}
- **描述**: 记录用户打开电子邮件时
- **详情**: Username, timestamp, email ID, sender, subject

### Download {#download}
- **描述**: 记录用户下载电子邮件时
- **详情**: Username, timestamp, email ID, sender, subject

### Restore {#restore}
- **描述**: 记录用户恢复存档电子邮件时
- **详情**: Username, timestamp, email ID, sender, subject

### Account {#account}
- **描述**: 记录与账户相关的操作（创建、修改等）
- **详情**: Username, timestamp, account ID

### Deletion {#deletion}
- **描述**: 记录对电子邮件或其他资源的删除操作
- **详情**: Username, timestamp, affected resource
- **合规性说明**: 删除操作会被记录以用于合规性审计

## 🔍 筛选选项 {#filter-options}

日志页面提供以下筛选选项：

1. **Date** - 按日期范围（From-To）进行筛选
2. **Username** - 按特定用户进行筛选（仅限 Admin）
3. **Action Type** - 按特定操作类型进行筛选

## 🖥️ 日志显示 {#log-display}

日志以表格形式显示，包含以下列：

- **Timestamp**: 操作的日期和时间
- **Username**: 相关用户的名称
- **Type**: 带有颜色编码的操作类型
- **Information**: 关于操作的附加详情

对于移动设备，日志将以卡片格式显示。

## 🔐 访问权限 {#access-permissions}

- **非 Admin 用户**: 只能查看自己的日志
- **Admin 用户**: 可以查看所有用户的日志并按用户进行筛选

## 📤 Audit Data Export {#audit-data-export}

Admin 可以直接从 Logs 页面打开专门的 [Audit Data Export](AuditExport.md) 页面。该页面会从存档中创建用于外部审计工具的表格化大规模数据包（INDEX.XML + CSV）。每次导出运行都会写入两条类型为 **Audit Data Export** 的访问日志条目（开始与结果），这些条目与所有其他条目一样，可以在 Logs 页面上进行列出和筛选。

详情请参阅 [Audit Data Export guide](AuditExport.md)。
