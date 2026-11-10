# 🚨 Экстренное восстановление учетной записи {#emergency-account-recovery}

[← Назад к индексу документации](index.md)

## 📋 Обзор {#overview}

Данное руководство содержит подробные инструкции по восстановлению доступа к приложению Mail Archiver в случае, если вы забыли пароль администратора. Эта экстренная процедура позволяет восстановить доступ к вашей системе без потери каких-либо данных.

> 📝 **Требование к версии**: Этот метод восстановления доступен в Mail Archiver версии 2601.1 и выше.

## ⚠️ Важное уведомление по безопасности {#important-security-notice}

Этот метод восстановления следует использовать только в чрезвычайных ситуациях, когда у вас нет доступа к учетной записи администратора. Он требует временного изменения конфигурации вашего приложения и должен быть отменен сразу после восстановления пароля.

## 🛠️ Шаги по восстановлению {#recovery-steps}

### 1. Создание экстренной учетной записи администратора {#1-create-emergency-administrator-account}

Измените переменные окружения вашего `appsettings.json` или Docker, чтобы создать временную экстренную учетную запись администратора:

**Для Docker Compose (рекомендуется):**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Emergency admin account
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempPass123!
      # ... other settings remain unchanged
```

**Для прямого изменения appsettings.json:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Перезапуск приложения/контейнера {#2-restart-the-applicationcontainer}

После изменения конфигурации перезапустите ваше приложение:

**Для Docker Compose:**
```bash
docker compose down
docker compose up -d
```

**Для прямого запуска приложения:**
```bash
# Restart your application service
```

### 3. Вход с экстренной учетной записью {#3-login-with-emergency-account}

1. Перейдите в веб-интерфейс Mail Archiver
2. Войдите, используя экстренные учетные данные:
   - **Username**: `emergency_admin` (или то, что вы установили)
   - **Password**: `TempPass123!` (или то, что вы установили)

### 4. Сброс исходного пароля администратора {#4-reset-original-administrator-password}

1. Перейдите в раздел "Users" в главном меню
2. Найдите вашу исходную учетную запись администратора в списке пользователей
3. Нажмите "Edit" для этого пользователя
4. Измените пароль на новый надежный пароль
5. Нажмите "Save Changes"

### 5. Восстановление исходной конфигурации {#5-restore-original-configuration}

Верните изменения конфигурации к исходному имени пользователя администратора:

**Для Docker Compose:**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Original admin account
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword
      # ... other settings
```

**Для прямого appsettings.json:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Повторный перезапуск приложения {#6-restart-application-again}

Перезапустите приложение с восстановленной конфигурацией:

**Для Docker Compose:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Пример: Полный процесс восстановления через Docker Compose {#example-complete-docker-compose-recovery-process}

Ниже приведен полный пример процесса экстренного восстановления с использованием Docker Compose:

### Шаг 1: Экстренная конфигурация {#step-1-emergency-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # EMERGENCY ADMIN ACCOUNT
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempEmergencyPass2026!
      
      # Other settings remain unchanged...
      - MailSync__IntervalMinutes=15
      - TimeZone__DisplayTimeZoneId=Europe/Berlin
      # ... rest of configuration
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Шаг 2: Перезапуск и вход {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Шаг 3: Сброс исходного пароля {#step-3-reset-original-password}
1. Войдите в веб-интерфейс с экстренными учетными данными
2. Перейдите в раздел Users
3. Отредактируйте исходного пользователя-админа и установите новый пароль
4. Сохраните изменения

### Шаг 4: Восстановление нормальной конфигурации {#step-4-restore-normal-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # RESTORED ORIGINAL ADMIN ACCOUNT
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword2026!
      
      # ... rest of configuration unchanged
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Шаг 5: Финальный перезапуск {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Утерян ключ шифрования учетных данных {#lost-credential-encryption-key}

Если `Security__CredentialEncryptionKey` (шифрование учетных данных при хранении) утерян, сохраненные
учетные данные почтовых аккаунтов больше не могут быть расшифрованы. Приложение **не**
аварийно завершает работу: затронутые значения рассматриваются как "отсутствие учетных данных", и в лог записывается предупреждение. Для восстановления:

1. Восстановите ключ из вашего бэкапа / менеджера секретов и перезапустите приложение.
   Учетные данные снова заработают немедленно, изменения данных не требуются.
2. Если ключ утерян безвозвратно, зашифрованные учетные данные не могут быть восстановлены.
   Затронутые аккаунты легко идентифицировать: их синхронизация не удается, и в лог
   приложения записывается предупреждение о расшифровке (а в интерфейсе это отображается
   как проблема синхронизации).
3. Откройте каждый затронутый аккаунт через **Mail Accounts → Edit** и заново введите его
   данные: пароль IMAP, client secret M365 или повторно авторизуйте аккаунт (MSA). Сохранение
   формы повторно зашифрует учетные данные с помощью текущего настроенного ключа — ручные
   изменения в базе данных не требуются.

> 💡 Ключ никогда не хранится в базе данных, поэтому одних только бэкапов базы данных
> недостаточно — всегда делайте бэкап ключа отдельно.

## 🔒 Рекомендации по безопасности {#security-best-practices}

1. **Используйте сложные временные пароли**: Сделайте ваш экстренный пароль сложным и уникальным
2. **Смените его немедленно**: Сбросьте экстренный пароль, как только восстановите доступ
