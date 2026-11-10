# 🔄 Руководство по обновлению мажорной версии PostgreSQL {#postgresql-major-version-upgrade-guide}

[← Назад к индексу документации](index.md)

## 📋 Обзор {#overview}

В данном руководстве объясняется, как обновить базу данных PostgreSQL с одной мажорной версии на другую в вашей среде Mail Archiver Docker Compose. Обновление мажорных версий необходимо для использования новых функций PostgreSQL, повышения производительности и получения обновлений безопасности.

## ⚠️ Важное ограничение ответственности {#important-disclaimer}
> **Примечание**: Данная процедура обновления является общим процессом миграции базы данных PostgreSQL и не является специфичной для Mail Archiver. Она предоставляется в качестве рекомендации, основанной на лучших практиках PostgreSQL. Всегда сначала тестируйте процедуру в непроизводственной среде и убедитесь в наличии надлежащих резервных копий перед началом работы.


**Все процедуры, описанные в данном руководстве, должны быть предварительно протестированы в непроизводственной среде.** Обновление мажорных версий включает миграцию данных и несет риски потери или повреждения данных. Всегда:
- Создавайте полную резервную копию перед началом процесса обновления
- Тестируйте процедуру обновления в среде разработки
- Проверяйте всю функциональность приложения после обновления
- Имейте план отката на случай возникновения проблем
- Планируйте обновления на периоды технического обслуживания

**Используйте на свой страх и риск.**

## 🎯 Когда стоит обновляться {#when-to-upgrade}

Рассмотрите возможность обновления PostgreSQL, если:
- Ваша текущая версия приближается к завершению жизненного цикла (end-of-life)
- Обновления безопасности требуют более новой версии
- Вы планируете крупное техническое обслуживание системы

## 🛠️ Предварительные условия {#prerequisites}

- Работающая установка Mail Archiver в Docker (см. [Setup Guide](Setup.md))
- Достаточное дисковое пространство (минимум в 1,5 раза больше текущего размера базы данных)
- Доступ к хост-системе Docker
- Полная резервная копия текущей базы данных (см. [Backup and Restore Guide](BackupRestore.md))
- Окно технического обслуживания для простоя приложения

## 📋 Текущая конфигурация {#current-configuration}

Ваша текущая установка Mail Archiver использует:
- **Database Name**: MailArchiver
- **Database User**: mailuser
- **Data Volume**: `./postgres-data`, сопоставленный с `/var/lib/postgresql/data`

При необходимости эти параметры должны быть скорректированы в следующих примерах команд и скриптов.

## 🔄 Процедуры обновления {#upgrade-procedures}

Этот метод создает логическую резервную копию ваших данных и восстанавливает ее в новую версию PostgreSQL. Это самый безопасный подход, который работает между любыми версиями PostgreSQL.

> **Альтернативные методы**: PostgreSQL также предоставляет другие методы обновления, такие как `pg_upgrade` для более быстрого обновления. Для получения дополнительной информации об альтернативных подходах к обновлению, пожалуйста, обратитесь к [официальной документации по обновлению PostgreSQL](https://www.postgresql.org/docs/current/upgrading.html).

#### Шаг 1: Подготовка среды {#step-1-prepare-the-environment}

```bash
# 1. Stop the application container (keep database running for backup)
docker compose stop mailarchive-app

# 2. Create a backup directory
mkdir -p postgres-backup

# 3. Create a database dump
echo "Export DB content (this can take some time)"
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > postgres-backup/database_dump.sql

# 4. Verify the dump was created successfully
ls -la postgres-backup/database_dump.sql

# 5. Stop the database container
docker compose down
```

#### Шаг 2: Обновление конфигурации Docker Compose {#step-2-update-docker-compose-configuration}

Отредактируйте ваш файл `docker-compose.yml`, чтобы использовать новую версию PostgreSQL:

```yaml
  postgres:
    image: postgres:18  # Changed from old version
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data-new:/var/lib/postgresql  # New data directory
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
```

**Важные изменения:**
- Обновлен `image` с `postgres:17` на `postgres:18` (или вашу целевую версию)
- Изменено сопоставление томов с `./postgres-data:/var/lib/postgresql/data` на `./postgres-data-new:/var/lib/postgresql` (изменение внутреннего пути требуется начиная с версии 18 и далее для postgres)

#### Шаг 3: Запуск нового экземпляра PostgreSQL {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Шаг 4: Восстановление данных в новую базу данных {#step-4-restore-data-to-new-database}

Восстановите дамп базы данных в новый экземпляр PostgreSQL:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
Это может занять некоторое время в зависимости от размера базы данных.
Следите за любыми сообщениями об ошибках во время восстановления.

#### Шаг 5: Тестирование и завершение миграции {#step-5-test-and-complete-migration}
Запустите приложение для проверки подключения:

```bash
docker compose up -d
```

Проверьте функциональность приложения:
- Войдите в веб-интерфейс
- Убедитесь, что поиск по электронной почте работает
- Проверьте доступность существующих данных
- Протестируйте функциональность резервного копирования/восстановления

Если все работает правильно, очистите старые данные:

Остановите контейнеры
```bash
docker compose down
```
Удалите старую директорию данных (только после подтверждения работоспособности новой установки)
```bash
rm -rf postgres-data
```
Удалите директорию резервных копий
```bash
rm -rf postgres-backup
```

Переименуйте новую директорию данных в стандартное имя
```bash
mv postgres-data-new postgres-data
```

Обновите docker-compose.yml, чтобы использовать стандартное имя тома:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Запустите систему полностью
```bash
docker compose up -d
```

## ⚠️ Важные соображения {#important-considerations}

### Планирование простоя {#downtime-planning}

- В зависимости от вашей системы и размера базы данных, эта процедура может занять много времени.
- Планируйте выполнение во время окон технического обслуживания.

### Требования к дисковому пространству {#disk-space-requirements}

Убедитесь в наличии достаточного дискового пространства (приблизительно в два раза больше размера вашей базы данных).

Проверьте доступное место:
```bash
df -h
du -sh postgres-data/
```

### План отката {#rollback-plan}

Всегда готовьтесь к откату:
1. Сохраняйте старую директорию данных до завершения проверки обновления
2. Сохраняйте старую резервную копию `docker-compose.yml`
3. Сначала протестируйте процедуру отката в среде разработки

### Проверка после обновления {#post-upgrade-verification}

После обновления проверьте:
- Подключение приложения к базе данных
- Функциональность поиска и извлечения электронной почты
- Аутентификацию пользователей и управление сессиями
- Функциональность резервного копирования и восстановления
- Метрики производительности базы данных

## 📚 Связанная документация {#related-documentation}

- [Backup and Restore Guide](BackupRestore.md) — Необходимо для создания резервных копий перед обновлением
- [Database Maintenance Guide](DatabaseMaintenance.md) — Обслуживание после обновления
- [Setup Guide](Setup.md) — Конфигурация Docker Compose
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) — Официальная документация к релизам
