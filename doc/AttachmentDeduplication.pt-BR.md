# 🧬 Deduplicação de Anexo {#attachment-deduplication}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Os anexos de e-mail são armazenados como payloads binários no banco de dados. O **mesmo arquivo** é frequentemente anexado a muitos e-mails (logotipos de empresas em assinaturas, PDFs encaminhados, newsletters, contratos, ...). Sem a deduplicação, cada anexo mantém sua própria cópia privada desses bytes, o que desperdiça uma grande quantidade de espaço no banco de dados.

A **deduplicação de anexo** armazena cada payload *único* exatamente uma vez em uma tabela endereçada por conteúdo (indexada pelo hash SHA-256 dos bytes) e permite que todos os anexos idênticos referenciem essa única cópia compartilhada. Isso normalmente reduz significativamente a pegada de armazenamento de Arquivos com muitos anexos.

## ⚙️ Como funciona {#how-it-works}

### Novos anexos {#new-attachments}
Sempre que um novo e-mail é arquivado (IMAP, Microsoft 365 / Graph, importação de EML, importação de MBOX, ...) o payload do anexo é transformado em hash e armazenado uma vez. Se o mesmo payload já existir, o novo anexo simplesmente referencia a linha de conteúdo existente. Isso acontece de forma transparente na camada de dados, de modo que todos os caminhos de arquivamento são cobertos automaticamente.

### Dados existentes (migração) {#existing-data-migration}
Para instalações que já contêm anexos arquivados **antes** desta funcionalidade ser introduzida, uma **migração de segundo plano robusta e retomável** converte os payloads inline existentes em linhas de conteúdo compartilhadas:

- Ela processa os anexos **lote por lote**, cada lote em sua própria transação.
- O progresso é persistido em uma tabela de estado dedicada, de modo que a migração **sobrevive a um reinício da aplicação** e simplesmente continua de onde parou.
- Ela é **idempotente** – anexos já migrados nunca são tocados novamente, mesmo após uma falha ou reinício.
- Ela é executada em segundo plano e não bloqueia o uso normal da aplicação.

Quando a migração termina, ela registra uma mensagem de conclusão, por exemplo:

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

No próximo início, ela detecta que não há nada a fazer e permanece ociosa. Caso algum anexo não deduplicado permaneça (por exemplo, após a restauração de um backup antigo), ela retoma e os migra automaticamente — o sistema sempre converge para um estado totalmente deduplicado.

### Coleta de lixo (sempre ativa) {#garbage-collection-always-on}
Quando e-mails (e, portanto, anexos) são excluídos, um payload compartilhado pode ficar sem referência. Uma coleta de lixo integrada remove essas linhas de conteúdo órfãs.

> 🧹 **A limpeza de órfãos SEMPRE é executada**, independentemente da funcionalidade `DatabaseMaintenance`. Ela é executada uma vez logo após a migração e depois periodicamente (a cada `OrphanCleanupIntervalHours`, padrão 12 h). Quando `DatabaseMaintenance__Enabled=true`, a mesma limpeza é executada adicionalmente antes do `VACUUM` diário para que o espaço liberado seja fisicamente recuperado.

### Uso de disco após a migração (importante) {#disk-usage-after-the-migration-important}

> 📈 **O valor "Storage" no dashboard pode subir brevemente logo após a migração** (ex: 3,5 GB → 5,3 GB). Isso é esperado e não é um erro.
>
> O dashboard reporta o tamanho **físico** do banco de dados (`pg_database_size`). Quando a migração move um payload para o armazenamento compartilhado, ela define a antiga cópia inline como `NULL`, mas o PostgreSQL (MVCC) mantém esses bytes antigos como *dead tuples* até que o arquivo da tabela seja reescrito — portanto, por um momento, o banco de dados contém tanto a cópia antiga quanto a nova.
>
> Para corrigir isso automaticamente, a aplicação executa um `VACUUM FULL` de **uma única vez** na tabela de anexos **assim que a migração for concluída e enquanto nenhum job de sincronização estiver em execução**. Isso reescreve a tabela e devolve o espaço liberado ao sistema operacional; depois disso, o tamanho reportado cai para **abaixo** do original. Ele é executado exatamente uma vez (rastreado via um marcador `ReclaimedAt`), é seguro para reinícios e é adiado enquanto uma sincronização estiver em progresso (ele tenta novamente automaticamente quando ocioso).

>
> ⚠️ O `VACUUM FULL` bloqueia a tabela de anexos durante sua duração (sem leituras/escritas de anexos nesse intervalo) e precisa de algum espaço livre em disco para a reescrita. Se você quiser acioná-lo manualmente em vez disso, execute:
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 Configuração {#configuration}


Estas configurações são opcionais; os padrões são sensatos para a maioria das instalações. Veja também o [Guia de Configuração](Setup.md#-attachment-deduplication-settings).

| Configuração | Padrão | Descrição |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Número de anexos existentes migrados por transação. Valores maiores migram mais rápido, mas aumentam a carga de memória/DB por lote. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Pausa opcional (ms) entre lotes de migração para limitar a carga no DB em sistemas ocupados. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Atraso (s) após o início do app antes de a migração começar (permite que a migração de esquema termine primeiro). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Intervalo (h) da coleta de lixo de órfãos sempre ativa. |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | Tempo limite de comando do banco de dados (em segundos) para as operações de lote de migração. O padrão é 5 minutos. Se um lote ainda sofrer timeout, o serviço tenta novamente automaticamente com metade do tamanho do lote. |

Exemplo (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ FAQ {#faq}

**Posso desativar a deduplicação?**
Não. É um recurso central de armazenamento e está sempre ativo. Você só pode ajustar o tamanho do lote e os parâmetros de agendamento acima.

**Meus dados estão seguros durante a migração?**
Sim. Cada lote é uma única transação e o trabalho é idempotente. Uma falha ou reinício no meio da migração reverte o lote inacabado e ele é reprocessado no próximo início; nenhum dado é perdido ou duplicado.

**Isso torna o arquivamento mais lento?**
O overhead de hash por anexo é insignificante. A migração única de dados existentes é executada em segundo plano e pode ser limitada com `DelayBetweenBatchesMs`, se necessário.

**A migração continua sofrendo timeout e reiniciando no mesmo cursor. O que posso fazer?**
Isso pode acontecer em bancos de dados muito grandes (centenas de milhares de e-mails) onde um único lote de 200 anexos com hash SHA-256 excede o tempo limite de comando padrão. O serviço agora tenta novamente automaticamente com metade do tamanho do lote quando ocorre um timeout. Se você ainda vir timeouts, pode diminuir manualmente o tamanho do lote e/ou aumentar o tempo limite do comando:

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**Quanto espaço eu vou economizar?**
Isso depende inteiramente de quantos anexos duplicados seu Arquivo contém. Arquivos com muitas assinaturas repetidas, logotipos ou arquivos encaminhados são os que mais se beneficiam.

---
