# 📧 Gmail Best Practices {#gmail-best-practices}

[← Back to Documentation Index](index.md)

Ao usar contas Gmail com o aplicativo Mail Archiver, existem várias configurações específicas e boas práticas que devem ser seguidas para garantir o desempenho ideal e evitar problemas potenciais.

## 🔐 IMAP Configuration with App Passwords {#imap-configuration-with-app-passwords}

Contas Gmail podem ser integradas com o aplicativo Mail Archiver usando IMAP com App Passwords:

1. Ative a Autenticação de 2 Fatores em sua conta Gmail
2. Gere um App Password especificamente para o aplicativo Mail Archiver
3. Use o App Password em vez de sua senha regular do Gmail ao configurar a Conta no Mail Archiver

## 🏷️ Gmail Label Settings {#gmail-label-settings}

É fortemente recomendado ajustar suas configurações de rótulo do Gmail para evitar que o rótulo "All Mail" seja exposto via IMAP:

1. Vá para a interface web do Gmail e abra as configurações (ícone de engrenagem no topo) → Ver todas as configurações → aba Labels
2. Encontre o rótulo "All Mail" na lista
3. Desmarque a opção "Show in IMAP" para "All Mail"

### 📚 Background on Gmail Label Implementation {#background-on-gmail-label-implementation}

O sistema de rótulos do Gmail funciona de forma diferente dos provedores de e-mail tradicionais. No Gmail:
- Todos os e-mails existem em um único repositório "All Mail"
- Rótulos atuam como tags em vez de pastas
- O rótulo "All Mail" contém todos os e-mails em sua Conta
- Quando um e-mail é rotulado, ele aparece tanto na Pasta original quanto na seção rotulada. Portanto, todos os e-mails em sua caixa de entrada também aparecem no rótulo "All Mail".

### ⚠️ Issues with IMAP Retrieval {#issues-with-imap-retrieval}

Quando o "All Mail" é exposto via IMAP, isso pode causar vários problemas:

1. **Duplicate Processing**: O arquivador pode processar o mesmo e-mail várias vezes, pois ele aparece em múltiplas pastas/rótulos
2. **Performance Impact**: Recuperar o "All Mail" pode ser extremamente lento para Contas grandes com milhares de e-mails
4. **Synchronization Issues**: Conflitos podem surgir ao tentar determinar a qual pasta um e-mail realmente pertence

Ao desativar o acesso IMAP ao "All Mail", você garante que os e-mails sejam recuperados apenas uma vez de sua localização primária, melhorando tanto o desempenho quanto a precisão.

## 🗑️ Retention Policy and Expunge Settings {#retention-policy-and-expunge-settings}

Como mencionado no README principal, as contas Gmail requerem atenção especial em relação às políticas de Retenção:

- O recurso Auto-Expunge do Gmail deve ser desativado nas configurações do Gmail sob a aba "Forwarding and POP/IMAP"
- Esta configuração é crucial para que as políticas de Retenção funcionem corretamente com as contas Gmail

Para configurar esta definição:
1. Vá para Gmail Settings → Ver todas as configurações → aba Forwarding and POP/IMAP
2. Na seção "IMAP Access", selecione "Auto-Expunge off - Wait for the client to update the server." e "Move the message to the Bin"

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

O Gmail impõe um limite de taxa diário de 2500MB para recuperação IMAP. Ao arquivar grandes volumes de e-mails, esteja ciente de que você pode atingir esse limite, o que pode pausar temporariamente a recuperação de e-mails até o dia seguinte.

### 📊 Rate Limit Handling {#rate-limit-handling}

O Mail Archiver fornece tratamento de limite de taxa integrado para gerenciar os limites de largura de banda IMAP do Gmail:

- **Automatic bandwidth tracking**: Monitora os dados baixados por Conta por dia
- **Configurable limits**: Defina `BandwidthTracking__DailyLimitMb=2500` para Gmail
- **Automatic pause**: A Sincronização pausa quando o limite é atingido e retoma após o reset
- **Checkpoint system**: O progresso da Sincronização é salvo e retomado da última posição

Para configuração e uso detalhados, consulte o **[Rate Limit Handling Guide](RateLimitHandling.md)**.

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

Para grandes Caixas de correio do Gmail (ex: 14 GB), a Sincronização inicial levará vários dias:

| Mailbox Size | Days to Complete (at 2500 MB/day) |
|--------------|-----------------------------------|
| 5 GB | ~2 days |
| 10 GB | ~4 days |
| 14 GB | ~6 days |
| 20 GB | ~8 days |

**Dica**: Ative o rastreamento de largura de banda **antes** de adicionar grandes contas Gmail para garantir o tratamento adequado do limite de taxa desde o início.

## ✅ Best Practices Summary {#best-practices-summary}

Seguir estas boas práticas ajudará a garantir o funcionamento suave do Mail Archiver com suas contas Gmail, mantendo a integridade dos dados e o desempenho ideal.
