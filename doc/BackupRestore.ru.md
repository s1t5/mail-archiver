# 💾 Руководство по резервному копированию и восстановлению {#backup-and-restore-guide}

[← Назад к индексу документации](index.md)

## 📋 Обзор {#overview}

Данное руководство содержит подробные инструкции по резервному копированию и восстановлению вашей среды Mail Archiver Docker. Регулярное резервное копирование необходимо для защиты ваших архивированных электронных писем и конфигурационных данных.

## ⚠️ Важное предупреждение {#important-disclaimer}

**Все скрипты и команды, представленные в данном руководстве, являются исключительно примерами.** Мы не берем на себя никакой ответственности за их функциональность или пригодность для вашей конкретной среды. Всегда:
- Сначала тестируйте процедуры резервного копирования и восстановления в непроизводственной среде
- Проверяйте полноту и возможность восстановления резервных копий
- Адаптируйте скрипты и команды под вашу конкретную установку и требования
- Храните несколько копий резервных данных в разных местах
- Регулярно тестируйте свои процедуры аварийного восстановления

**Используйте на свой страх и риск.**

## 🎯 Рекомендации по резервному копированию {#backup-recommendations}

### Офлайн-резервное копирование (настоятельно рекомендуется) {#offline-backups-highly-recommended}
Для обеспечения максимальной согласованности и целостности данных **всегда выполняйте резервное копирование при остановленном приложении и базе данных**. Это гарантирует:
- Отсутствие активных транзакций базы данных во время копирования
- Полную согласованность данных во всех файлах
- Отсутствие риска повреждения файлов из-за параллельной записи
- Надежную возможность восстановления

Хотя «горячее» резервное копирование (Методы 2 и 3) удобно для минимизации времени простоя, оно несет в себе более высокий риск несогласованности данных. Для критически важных производственных сред планируйте регулярные офлайн-резервные копии во время окон технического обслуживания.

### Резервное копирование на уровне системы {#system-level-backups}
В дополнение к резервному копированию конкретного приложения **настоятельно рекомендуется** создавать резервные копии всей системной инфраструктуры:
- **Резервное копирование контейнеров LXC**: Если вы используете контейнер LXC, создавайте офлайн-снимки (snapshots) всего контейнера
- **Резервное копирование виртуальных машин**: Для развертывания в VM создавайте полные снимки VM
- **Резервное копирование хост-системы**: Включайте Docker volumes, конфигурации контейнеров и системные настройки
- **Конфигурационные файлы**: Резервно копируйте все конфигурационные файлы Docker и системы отдельно

Резервное копирование на уровне системы обеспечивает дополнительный уровень безопасности и позволяет полностью восстановить среду в случае аварии, включая возможность переноса всей среды на другой хост, если это потребуется.

## 🛠️ Предварительные требования {#prerequisites}

- Работающая установка Mail Archiver Docker (см. [Руководство по установке](Setup.md))
- Доступ к хост-системе Docker
- Достаточное дисковое пространство для резервных копий

## 📦 Что копировать {#what-to-backup}

Среда Mail Archiver Docker состоит из нескольких важных томов данных, которые необходимо резервировать:

### 1. База данных PostgreSQL {#1-postgresql-database}
- **Volume**: `./postgres-data`
- **Содержит**: Все архивированные письма, учетные записи пользователей, почтовые аккаунты и данные приложения
- **Важность**: ⭐⭐⭐ Критично — это ваше основное хранилище данных

### 2. Ключи защиты данных (Data Protection Keys) {#2-data-protection-keys}
- **Volume**: `./data-protection-keys`
- **Содержит**: Ключи ASP.NET Core Data Protection для шифрования cookie и аутентификации
- **Важность**: ⭐⭐ Важно — требуется для существующих пользовательских сессий и безопасной аутентификации

### 2a. Файлы оригинальных сообщений (только при использовании `RawMessages.Storage=filesystem`) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volume**: `./raw-messages` (сторона хоста для `/app/RawMessages`)
- **Содержит**: Оригинальный `.eml` каждого письма в формате gzip, записываемый, когда установлен `RawMessages:Enabled` и бэкенд хранилища — `filesystem`
- **Важность**: ⭐⭐⭐ Критично **при использовании** — база данных хранит только контрольную сумму, размер и относительный путь, поэтому восстановление без этой директории оставит оригиналы, которые невозможно будет предоставить пользователю
- Резервируйте эту директорию и дамп базы данных **вместе**. Восстановление базы данных без файлов не является ошибкой, которую архив может исправить: загрузки будут отвечать `404`, а контрольные суммы невозможно вычислить из чего-либо другого. Восстановление файлов без базы данных столь же бесполезно, так как на них ничего не ссылается.
- Если вы используете `RawMessages.Storage=postgres` (по умолчанию), эта директория не существует, и оригиналы находятся внутри `./postgres-data`, как и раньше.
- См. [Хранение оригинальных сообщений](RawMessages.md) для получения информации о самой функции и способах переключения бэкендов.

### 3. Конфигурация Docker Compose {#3-docker-compose-configuration}
- **File**: `docker-compose.yml`
- **Содержит**: Конфигурацию контейнеров, переменные окружения и настройки
- **Важность**: ⭐⭐ Важно — содержит конфигурацию вашего приложения

## 🔄 Процедуры резервного копирования {#backup-procedures}

### Метод 1: Полное резервное копирование (Рекомендуется) {#method-1-complete-backup-recommended}

Этот метод копирует все данные при остановленных контейнерах для обеспечения согласованности данных.

```bash
# 1. Stop the containers
docker compose down

# 2. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 3. Copy all data volumes
cp -r postgres-data "$BACKUP_DIR/"
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Copy docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

# 7. Start the containers again
docker compose up -d

echo "Backup completed: $BACKUP_DIR.tar.gz"
```

### Метод 2: «Горячее» резервное копирование (Минимальный простой) {#method-2-hot-backup-minimal-downtime}

Этот метод создает полную резервную копию при работающей системе с минимальным временем простоя.

**Примечание:** Замените `mailuser` и `MailArchiver` на ваши фактические учетные данные базы данных из `docker-compose.yml` (проверьте переменные окружения `POSTGRES_USER` и `POSTGRES_DB`).

```bash
# 1. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 2. Backup the database
# Replace 'mailuser' and 'MailArchiver' with your actual POSTGRES_USER and POSTGRES_DB
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > "$BACKUP_DIR/database.sql"

# 3. Backup data protection keys (these change rarely, can be done while running)
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Backup docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

echo "Hot backup completed: $BACKUP_DIR.tar.gz"
```

### Метод 3: Скрипт автоматического резервного копирования {#method-3-automated-backup-script}

Для регулярного автоматического резервного копирования мы предоставляем готовый к использованию скрипт. Скрипт доступен в [backup.sh](attachments/backup.sh).

**Возможности:**
- ✅ Автоматическое обнаружение учетных данных базы данных из docker-compose.yml
- ✅ «Горячее» резервное копирование (минимальный простой)
- ✅ Механизм lock-файла для предотвращения параллельных запусков бэкапа
- ✅ Проверка свободного места на диске перед началом
- ✅ Автоматическая очистка старых резервных копий

**Конфигурация:**

Перед использованием скрипта отредактируйте раздел конфигурации в верхней части файла:

```bash
# Required settings:
MAILARCHIVER_DIR="/path/to/your/mailarchiver"   # Mail Archiver installation directory
BACKUP_BASE_DIR="/path/to/your/backups"         # Where to store backups

# Optional settings (with defaults):
RETENTION_DAYS=30                               # How many days to keep backups
LOG_FILE="/var/log/mailarchiver-backup.log"     # Log file location
MIN_DISK_SPACE_MB=1000                          # Minimum free disk space required
LOCK_FILE="/var/lock/mailarchiver-backup.lock"  # Lock file to prevent concurrent runs
```

**Настройка:**

1. **Скопируйте скрипт в нужное вам место:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Отредактируйте конфигурацию:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Настройте параметры
   ```

3. **Сделайте скрипт исполняемым:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Протестируйте скрипт вручную:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Проверьте резервную копию:**
   - Проверьте директорию бэкапа на наличие созданного файла `.tar.gz`
   - Изучите лог-файл на наличие ошибок или предупреждений

**Планирование автоматических бэкапов:**

Чтобы настроить автоматическое резервное копирование через cron:

```bash
# Edit crontab
crontab -e

# Add a line to run backup daily at 2 AM
0 2 * * * /usr/local/bin/mailarchiver-backup.sh

# Or weekly on Sunday at 3 AM
0 3 * * 0 /usr/local/bin/mailarchiver-backup.sh

# Or multiple times (e.g., daily at 2 AM and 2 PM)
0 2,14 * * * /usr/local/bin/mailarchiver-backup.sh
```

## 🔙 Процедуры восстановления {#restore-procedures}

Процедуры восстановления соответствуют методам резервного копирования. Выберите подходящий метод восстановления в зависимости от того, какой метод копирования вы использовали:

- **Метод 1 (Полное резервное копирование)**: Используйте процедуру "Full Restore from Complete Backup". Этот метод используется, когда у вас есть полная копия, включающая директорию `postgres-data`.
- **Метод 2 (Горячее резервное копирование)** или **Метод 3 (Скрипт автоматического бэкапа)**: Используйте процедуру "Restore from SQL Dump" (обрабатывает ключи защиты данных и базу данных). Этот метод используется, когда у вас есть «горячая» копия, включающая SQL-дамп базы данных и ключи защиты данных.

Для удобства мы также предоставили автоматизированный скрипт восстановления, который может выполнить все процедуры автоматически. Вы можете использовать этот скрипт вместо ручного выполнения шагов, описанных ниже.

### Автоматизированный скрипт восстановления {#automated-restore-script}

Скрипт восстановления доступен в файле [restore.sh](attachments/restore.sh). Он обеспечит пошаговое руководство и запросит метод резервного копирования, путь к tar-файлу бэкапа и директорию установки, а затем автоматически выполнит процесс восстановления.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

Скрипт проведет вас через процесс восстановления и автоматически выполнит все шаги на основе выбранного вами метода резервного копирования.

### Полное восстановление из полной копии (Ручной метод) {#full-restore-from-complete-backup-manual-method}

Этот метод восстанавливает все данные из архива полной резервной копии. Его следует использовать, если у вас есть полная копия, включающая директорию `postgres-data` (созданную первым методом).

```bash
# 1. Stop the containers
docker compose down

# 2. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 3. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 4. Remove current data (make sure to backup first if needed!)
rm -rf postgres-data
rm -rf data-protection-keys

# 5. Restore data volumes
cp -r "$BACKUP_PATH/postgres-data" ./
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Start the containers
docker compose up -d

echo "Restore completed from: $BACKUP_FILE"
```

### Восстановление из SQL-дампа (Ручной метод) {#restore-from-sql-dump-manual-method}

Этот метод восстанавливает базу данных и ключи защиты данных из «горячей» копии (Методы два и три).

**Примечание:** В командах ниже используются примеры учетных данных (`mailuser` и `MailArchiver`). Вы должны заменить их на фактические значения из вашего файла `docker-compose.yml`. Проверьте переменные окружения `POSTGRES_USER` и `POSTGRES_DB` в сервисе `postgres`.

```bash
# 1. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 2. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 3. Stop the containers
docker compose down

# 4. Backup current data protection keys (optional, for safety)
# cp -r data-protection-keys data-protection-keys.backup

# 5. Restore data protection keys
rm -rf data-protection-keys
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Extract database credentials from docker-compose.yml
# Check your docker-compose.yml for the actual values and replace in the commands below:
# - POSTGRES_USER (default: mailuser)
# - POSTGRES_DB (default: MailArchiver)
DB_USER="mailuser"  # Replace with your POSTGRES_USER
DB_NAME="MailArchiver"  # Replace with your POSTGRES_DB

# 8. Start the database container
docker compose up -d postgres

# 9. Wait for database to be ready
echo "Waiting for database to be ready..."
sleep 10

# 10. Verify database is ready (adjust -U and -d parameters if needed)
docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME"

# 11. Drop and recreate the database (WARNING: This deletes all current data!)
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "DROP DATABASE IF EXISTS \"$DB_NAME\";"
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "CREATE DATABASE \"$DB_NAME\";"

# 12. Restore the database from SQL dump
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_PATH/database.sql"

# 13. Start all containers
docker compose up -d

# 14. Clean up extracted backup
rm -rf "$BACKUP_PATH"

echo "Restore completed from: $BACKUP_FILE"
```


## ⚠️ Важные примечания {#important-notes}

### Настройки подключения к базе данных {#database-connection-settings}
Убедитесь, что настройки подключения к вашей базе данных в `docker-compose.yml` совпадают при резервном копировании и восстановлении:
- `POSTGRES_USER` (по умолчанию: mailuser)
- `POSTGRES_PASSWORD` (по умолчанию: masterkey)
- `POSTGRES_DB` (по умолчанию: MailArchiver)

### Ключи защиты данных (Data Protection Keys) {#data-protection-keys}
Директория `data-protection-keys` используется для:
- Аутентификации пользователей и управления сессиями
- Шифрования cookie
- Безопасной обработки данных

Без этих ключей существующие пользовательские сессии будут аннулированы, и пользователям придется войти в систему заново.

### Переменные окружения {#environment-variables}
Все настройки приложения хранятся в переменных окружения в `docker-compose.yml`. Убедитесь, что вы:
- Сделали резервную копию файла `docker-compose.yml`
- Задокументировали любые пользовательские настройки
- Используете ту же конфигурацию при восстановлении

## 🔒 Рекомендации по безопасности {#security-best-practices}

1. **Шифруйте бэкапы**: Рассмотрите возможность шифрования архивов резервных копий, особенно если они содержат конфиденциальные данные электронной почты.

2. **Безопасное хранение**: Храните резервные копии в безопасном месте:
   - Используйте отдельный сервер или систему хранения
   - Внедрите контроль доступа
   - Рассмотрите возможность использования удаленного или облачного хранилища

3. **Регулярное тестирование**: Регулярно тестируйте свои процедуры резервного копирования и восстановления, чтобы убедиться, что они работают правильно.

4. **Мониторинг размера бэкапов**: Отслеживайте размер резервных копий, чтобы гарантировать наличие достаточного дискового пространства.

5. **Срок хранения бэкапов**: Определите и внедрите политику хранения резервных копий (например, хранить ежедневные бэкапы в течение 7 дней, еженедельные — 4 недели, ежемесячные — 12 месяцев).

6. **Смена паролей по умолчанию**: Пароли по умолчанию в конфигурации небезопасны и должны быть изменены для использования в производственной среде.


## 🖥️ Резервное копирование Proxmox {#proxmox-backup}

При запуске среды Mail Archiver Docker в контейнере Proxmox LXC рекомендуется использовать режим "stop" для резервного копирования всего контейнера, чтобы обеспечить согласованность данных.

### Почему рекомендуется режим Stop {#why-stop-mode-is-recommended}

1. **Согласованность данных**: Остановка контейнера гарантирует, что все транзакции базы данных завершены, а данные записаны на диск перед созданием резервной копии.
2. **Предотвращение повреждения данных**: Выполнение резервного копирования при активном контейнере может привести к повреждению данных, особенно при операциях с базой данных.
3. **Целостность файловой системы**: Остановка контейнера гарантирует завершение всех операций с файловой системой, предотвращая частичную запись во время бэкапа.

### Примечания по Proxmox Backup {#proxmox-backup-notes}

- Всегда проверяйте наличие достаточного дискового пространства для резервных копий.
- Регулярно тестируйте процедуру восстановления, чтобы убедиться в валидности бэкапов.
- Рассмотрите возможность настройки расписания резервного копирования в веб-интерфейсе Proxmox для автоматизации.
- Отслеживайте логи резервного копирования на наличие ошибок или проблем.
