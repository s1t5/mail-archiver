# 📊 Manipulação de Limite de Taxa {#rate-limit-handling}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Muitos provedores de e-mail impõem limites de largura de banda no acesso IMAP. Esses limites geralmente variam de algumas centenas de megabytes a vários gigabytes por período de 24 horas. Ao realizar a sincronização de grandes Caixas de correio que excedem esses limites, o processo de sincronização normalmente falharia ou ficaria incompleto.

O Mail Archiver inclui um sistema integrado de manipulação de limite de taxa que gerencia automaticamente essas limitações, garantindo uma Sincronização confiável mesmo para grandes Caixas de correio.

## ⚙️ Como Funciona {#how-it-works}

### Rastreamento Automático de Largura de Banda {#automatic-bandwidth-tracking}

O sistema monitora continuamente a quantidade de dados baixados durante a sincronização IMAP. Quando o limite de largura de banda configurado é atingido:

1. **A Sincronização Pausa Automaticamente**: A operação de sincronização atual é interrompida de forma controlada.
2. **O Progresso é Salvo**: Checkpoints armazenam a posição exata onde a sincronização parou.
3. **Atualizações de Status**: A Conta mostra o status "Rate-Limited" no painel.
4. **Retomada Automática**: Quando o limite é resetado (geralmente após 24 horas), a sincronização é retomada a partir do checkpoint salvo.

### Exemplo: Caixa de correio de 14 GB com Limite Diário de 2,5 GB {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Configuração {#configuration}

### Habilitando o Rastreamento de Largura de Banda {#enabling-bandwidth-tracking}

Adicione a seguinte seção ao seu `appsettings.json`:

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Opções de Configuração {#configuration-options}

| Opção | Descrição | Padrão | Exemplo |
|--------|-------------|---------|---------|
| `Enabled` | Habilitar/desabilitar rastreamento de largura de banda | `false` | `true` |
| `DailyLimitMb` | Limite diário de download em megabytes | `2500` | `2500` |
| `WarningThresholdPercent` | Porcentagem do limite para disparar avisos nos logs | `80` | `80` |
| `PauseHoursOnLimit` | Horas para pausar quando o limite é atingido | `24` | `24` |

### Definindo o Limite Correto {#setting-the-correct-limit}

Para determinar o valor apropriado de `DailyLimitMb`:

1. Verifique a documentação ou os termos de serviço do seu provedor de e-mail.
2. Procure por limites de largura de banda IMAP ou cotas de download.
3. Entre em contato com o administrador de e-mail se estiver usando um sistema de e-mail corporativo.
4. Comece com um valor conservador se não tiver certeza (ex: 2000 MB).
5. Monitore os logs em busca de avisos de limite de taxa durante as sincronizações iniciais.

## 👤 Experiência do Usuário {#user-experience}

### Indicadores do Painel {#dashboard-indicators}

Quando o limite de taxa está ativo, a Conta exibe:

- **Status**: "Rate-Limited" 
- **Banner de Informação**: Explica a situação e o tempo estimado de reset.
- **Progresso da Sincronização**: Mostra os e-mails processados antes de o limite ser atingido.

### Comportamento Automático {#automatic-behavior}

1. **Sem Intervenção Manual Necessária**: O sistema lida com tudo automaticamente.
2. **Reinicializações do Aplicativo**: Se o aplicativo for reiniciado, o estado do limite de taxa é preservado no banco de dados.
3. **Múltiplas Contas**: Cada Conta rastreia sua largura de banda de forma independente.

### O Que é Salvo {#what-gets-saved}

Quando ocorre um limite de taxa, o sistema salva:

- **Checkpoints de Sincronização**: Posição exata em cada Pasta onde a sincronização parou.
- **Uso de Largura de Banda**: Total de bytes baixados para o dia atual.
- **Estado do Limite**: Se o limite foi atingido e quando ele será resetado.

## 🗄️ Manutenção do Banco de Dados {#database-maintenance}

O `DatabaseMaintenanceService` limpa automaticamente os dados antigos:

- **Registros de Largura de Banda**: Excluídos após 7 dias.
- **Checkpoints de Sincronização**: Checkpoints concluídos são excluídos após 30 dias.
- **Checkpoints Incompletos**: Preservados até que a sincronização seja concluída.

## ✅ Melhores Práticas {#best-practices}

### Para Grandes Caixas de correio {#for-large-mailboxes}

1. **Habilite o Rastreamento de Largura de Banda**: Essencial para Caixas de correio maiores que o limite diário.
2. **Planeje a Sincronização Inicial**: Uma Caixa de correio grande pode levar vários dias para sincronizar totalmente.
3. **Monitore o Progresso**: Verifique o painel para o status do limite de taxa.
4. **Não Interrompa**: Deixe a retomada automática lidar com a continuação.

### Para Administradores {#for-administrators}

1. **Defina Limites Apropriados**: Corresponda o `DailyLimitMb` aos limites reais do seu provedor de e-mail.
2. **Monitore os Logs**: Mensagens de aviso aparecem em 80% do limite.
3. **Espaço no Banco de Dados**: O rastreamento de largura de banda usa armazenamento adicional mínimo.
4. **Backup Antes de Alterações**: Sempre faça um Backup antes de modificar as configurações de limite de taxa.

### Considerações de Desempenho {#performance-considerations}

- ✅ **Impacto na Memória**: Irrelevante - apenas rastreia bytes, não armazena e-mails.
- ✅ **Impacto no Banco de Dados**: Mínimo - registros pequenos são limpos automaticamente.
- ✅ **Velocidade de Sincronização**: Sem impacto de desempenho durante a sincronização ativa.

## 🛡️ Erros FETCH Transitórios / Throttling no Lado do Servidor {#transient-fetch-errors-server-side-throttling}

Além do rastreamento de largura de banda baseado em bytes descrito acima, o Mail Archiver também detecta e reage ao **throttling por mensagem** sinalizado pelo próprio servidor IMAP. Alguns provedores de IMAP hospedados aplicam throttling por Conta ou por IP que é **independente das pausas de `BatchOperation` configuradas**, porque o throttle é aplicado no lado do servidor com base no volume total de solicitações durante a sessão atual.

### Sintomas {#symptoms}

O servidor retorna uma resposta `NO` (ou `BAD`) para um único comando `FETCH` de um corpo de mensagem, com mensagens como:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- respostas contendo `throttle`, `rate limit` ou marcadores similares.

A autenticação, listagem de Pasta e a própria busca de mensagens geralmente ainda funcionam — apenas a recuperação real da mensagem completa falha.

### Manipulação Automática {#automatic-handling}

Quando tal erro transitório é detectado, o Mail Archiver aplica uma estratégia de tentativa de reenvio por mensagem:

1. **Tentativa com backoff exponencial**: Até **3 tentativas** para a mesma mensagem, com atrasos de **5 s → 15 s → 60 s**.
2. **Recuperação de Conexão**: Antes de cada tentativa, o estado da conexão, autenticação e seleção de Pasta são validados e restaurados, se necessário.
3. **Reset em caso de sucesso**: Um FETCH bem-sucedido reseta o contador de falhas consecutivas.
4. **Pausa de sincronização controlada**: Se **10 mensagens consecutivas** ainda falharem com erros transitórios após todas as tentativas, a execução atual da sincronização é pausada de forma controlada — o mesmo mecanismo usado pela pausa de limite de largura de banda:
   - O timestamp `LastSync` **não** é atualizado.
   - Se `BandwidthTracking` estiver habilitado, os checkpoints são preservados para que a próxima execução da sincronização retome da última mensagem processada com sucesso.
   - O trabalho é marcado como com limite de taxa.
   - A próxima sincronização agendada será retomada automaticamente.

### Mitigação {#mitigation}

Se você atingir regularmente o throttling no lado do servidor, diminua a taxa de solicitações ajustando suas configurações de `BatchOperation`:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Note que alguns provedores aplicam throttle com base em **total de bytes** ou **total de FETCHs por janela de tempo**, em vez de taxa por segundo. Nesses casos:

- Habilite `BandwidthTracking` (veja acima) e configure `DailyLimitMb` para corresponder ao limite documentado do provedor. A sincronização então pausará de forma limpa quando o orçamento diário for esgotado e retomará automaticamente no dia seguinte.
- Sincronizações iniciais de Caixas de correio muito grandes podem levar vários dias, o que é o comportamento pretendido.

### O Que Conta como um Erro Transitório {#what-counts-as-a-transient-error}

Apenas respostas IMAP `NO`/`BAD` com texto ou códigos de resposta relacionados a throttling são tratadas como transitórias. Outros erros (mensagens malformadas, problemas de decodificação UTF-8, etc.) ainda são contados como `FailedEmails` imediatamente e **não** disparam a lógica de tentativa/pausa.

## 📚 Documentação Relacionada {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Procedimentos de limpeza do banco de dados
- [GmailBestPractices.md](GmailBestPractices.md) - Configuração específica para Gmail
