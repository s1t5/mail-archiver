# 📧 Gmail Best Practices {#gmail-best-practices}

[← Back to Documentation Index](index.md)

При использовании учетных записей Gmail с приложением Mail Archiver необходимо соблюдать несколько специфических конфигураций и правил, чтобы обеспечить оптимальную производительность и избежать потенциальных проблем.

## 🔐 IMAP Configuration with App Passwords {#imap-configuration-with-app-passwords}

Учетные записи Gmail можно интегрировать с приложением Mail Archiver через IMAP, используя App Passwords:

1. Включите двухфакторную аутентификацию в вашей учетной записи Gmail
2. Сгенерируйте App Password специально для приложения Mail Archiver
3. Используйте App Password вместо вашего обычного пароля Gmail при настройке учетной записи в Mail Archiver

## 🏷️ Gmail Label Settings {#gmail-label-settings}

Настоятельно рекомендуется настроить параметры ярлыков Gmail, чтобы предотвратить отображение ярлыка "All Mail" через IMAP:

1. Перейдите в веб-интерфейс Gmail и откройте настройки (иконка шестеренки вверху) → See all settings → Labels tab
2. Найдите ярлык "All Mail" в списке
3. Снимите флажок с опции "Show in IMAP" для "All Mail"

### 📚 Background on Gmail Label Implementation {#background-on-gmail-label-implementation}

Система ярлыков Gmail работает иначе, чем у традиционных почтовых провайдеров. В Gmail:
- Все письма находятся в едином репозитории "All Mail"
- Ярлыки работают как теги, а не как папки
- Ярлык "All Mail" содержит каждое письмо в вашей учетной записи
- Когда письму присваивается ярлык, оно появляется и в исходной папке, и в разделе с ярлыком. Таким образом, все письма в вашем входящем ящике также отображаются в ярлыке "All Mail".

### ⚠️ Issues with IMAP Retrieval {#issues-with-imap-retrieval}

Когда "All Mail" доступен через IMAP, это может вызвать несколько проблем:

1. **Duplicate Processing**: Архиватор может обрабатывать одно и то же письмо несколько раз, так как оно отображается в нескольких папках/ярлыках
2. **Performance Impact**: Извлечение "All Mail" может быть крайне медленным для крупных аккаунтов с тысячами писем
4. **Synchronization Issues**: Могут возникнуть конфликты при попытке определить, к какой папке на самом деле относится письмо

Отключив доступ к "All Mail" через IMAP, вы гарантируете, что письма будут извлекаться только один раз из их основного местоположения, что повышает как производительность, так и точность.

## 🗑️ Retention Policy and Expunge Settings {#retention-policy-and-expunge-settings}

Как упоминалось в основном README, учетные записи Gmail требуют особого внимания к политикам хранения данных (retention policies):

- Функция Gmail Auto-Expunge должна быть отключена в настройках Gmail на вкладке "Forwarding and POP/IMAP"
- Эта настройка критически важна для корректной работы политик хранения с учетными записями Gmail

Чтобы настроить этот параметр:
1. Перейдите в Gmail Settings → See all settings → Forwarding and POP/IMAP tab
2. В разделе "IMAP Access" выберите "Auto-Expunge off - Wait for the client to update the server." и "Move the message to the Bin"

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

Gmail накладывает ежедневный лимит скорости (rate limit) в 2500MB для извлечения данных через IMAP. При архивации больших объемов электронной почты имейте в виду, что вы можете достичь этого лимита, что может временно приостановить извлечение писем до следующего дня.

### 📊 Rate Limit Handling {#rate-limit-handling}

Mail Archiver предоставляет встроенные механизмы обработки лимитов скорости для управления пропускной способностью IMAP в Gmail:

- **Automatic bandwidth tracking**: отслеживает объем загруженных данных на одну учетную запись в день
- **Configurable limits**: установите `BandwidthTracking__DailyLimitMb=2500` для Gmail
- **Automatic pause**: синхронизация приостанавливается при достижении лимита и возобновляется после сброса счетчика
- **Checkpoint system**: прогресс синхронизации сохраняется, и процесс возобновляется с последней позиции

Для получения подробной информации о настройке и использовании см. **[Rate Limit Handling Guide](RateLimitHandling.md)**.

### 🔧 Gmail Configuration Example {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Large Mailbox Considerations {#large-mailbox-considerations}

Для больших почтовых ящиков Gmail (например, 14 ГБ) первоначальная синхронизация займет несколько дней:

| Mailbox Size | Days to Complete (at 2500 MB/day) |
|--------------|-----------------------------------|
| 5 GB | ~2 days |
| 10 GB | ~4 days |
| 14 GB | ~6 days |
| 20 GB | ~8 days |

**Tip**: Включите отслеживание пропускной способности (bandwidth tracking) **до** добавления больших учетных записей Gmail, чтобы обеспечить правильную обработку лимитов скорости с самого начала.

## ✅ Best Practices Summary {#best-practices-summary}

Соблюдение этих рекомендаций поможет обеспечить бесперебойную работу Mail Archiver с вашими учетными записями Gmail, сохраняя целостность данных и оптимальную производительность.
