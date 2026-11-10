# 📋 Access Log {#access-log}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

В данном руководстве объясняется функциональность журнала доступа (access log) в приложении Mail Archiver, которая позволяет регистрировать и просматривать действия пользователей в системе. Эта функциональность имеет решающее значение для мониторинга, обеспечения безопасности и отладки.

## 📊 What is Logged {#what-is-logged}

Приложение регистрирует различные типы действий пользователей, включая:

1. **Login and Logout** — регистрация операций входа и выхода из системы
2. **Search Operations** — регистрация поисковых запросов электронной почты в системе
3. **Email Opening** — регистрация случаев открытия пользователями содержимого писем
4. **Email Downloading** — регистрация случаев скачивания пользователями писем
5. **Email Restoration** — регистрация случаев восстановления пользователями архивных писем
6. **Account Management** — регистрация действий, связанных с учетными записями
7. **Deletion Operations** — регистрация действий по удалению
8. **Audit Data Export** — регистрация запусков экспорта аудиторских данных (начало и результат, см. [Audit Data Export](AuditExport.md))

## 📝 Action Types and Descriptions {#action-types-and-descriptions}

Регистрируемые действия разделены на следующие типы:

### Login {#login}
- **Description**: Регистрация входа пользователя в систему
- **Details**: Username, timestamp

### Logout {#logout}
- **Description**: Регистрация выхода пользователя из системы
- **Details**: Username, timestamp

### Search {#search}
- **Description**: Регистрация поисковых запросов в системе электронной почты
- **Details**: Username, timestamp, search parameters

### Open {#open}
- **Description**: Регистрация открытия пользователем письма
- **Details**: Username, timestamp, email ID, sender, subject

### Download {#download}
- **Description**: Регистрация скачивания пользователями писем
- **Details**: Username, timestamp, email ID, sender, subject

### Restore {#restore}
- **Description**: Регистрация восстановления пользователями архивных писем
- **Details**: Username, timestamp, email ID, sender, subject

### Account {#account}
- **Description**: Регистрация действий, связанных с учетной записью (создание, изменение и т. д.)
- **Details**: Username, timestamp, account ID

### Deletion {#deletion}
- **Description**: Регистрация операций удаления писем или других ресурсов
- **Details**: Username, timestamp, affected resource
- **Compliance Note**: Операции удаления регистрируются для целей аудита соответствия требованиям

## 🔍 Filter Options {#filter-options}

На странице журнала доступны следующие параметры фильтрации:

1. **Date** — фильтрация по диапазону дат (From-To)
2. **Username** — фильтрация по конкретному пользователю (только для Admin)
3. **Action Type** — фильтрация по конкретному типу действия

## 🖥️ Log Display {#log-display}

Журналы отображаются в формате таблицы со следующими столбцами:

- **Timestamp**: Дата и время действия
- **Username**: Имя задействованного пользователя
- **Type**: Тип действия с цветовой кодировкой
- **Information**: Дополнительные сведения о действии

На мобильных устройствах журналы отображаются в формате карточек.

## 🔐 Access Permissions {#access-permissions}

- **Non-admin users**: Могут просматривать только свои собственные логи
- **Admin users**: Могут просматривать логи всех пользователей и фильтровать их по пользователю

## 📤 Audit Data Export {#audit-data-export}

Администраторы могут открыть специальную страницу [Audit Data Export](AuditExport.md) непосредственно со страницы Logs. Она создает пакеты табличных массовых данных (INDEX.XML + CSV) из архива для внешних инструментов аудита. Каждый запуск экспорта записывает две записи в журнале доступа типа **Audit Data Export** (начало и результат), которые перечисляются и доступны для фильтрации на странице Logs, как и все остальные записи.

Для получения подробной информации см. [Audit Data Export guide](AuditExport.md).
