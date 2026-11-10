# 💾 Exibição de Armazenamento por Conta {#per-account-storage-display}

[← Voltar para o Índice da Documentação](index.md)

## 📋 Visão Geral {#overview}

A tabela "Account Overview" do Dashboard e a tabela "Show All" do MailAccounts incluem uma coluna de **Armazenamento**. Esta coluna mostra quanto espaço de banco de dados cada conta está usando — incluindo **todos os campos de cada e-mail arquivado** mais **todos os anexos**.

## ⚙️ Como funciona {#how-it-works}

Os valores de armazenamento são **armazenados em cache** na tabela de banco de dados `AccountStorageCache`. Isso significa que as páginas web leem valores pré-computados em vez de executar uma consulta dispendiosa toda vez que você abre o Dashboard ou a lista de contas. O cache é mantido atualizado por um serviço de segundo plano independente (`AccountStorageRefreshService`).

Para cada conta, o serviço solicita ao PostgreSQL que some `pg_column_size` para cada coluna das linhas da conta na tabela `ArchivedEmails`. Isso abrange **todos os campos de um e-mail** (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, etc.), incluindo a compressão do PostgreSQL e o armazenamento out-of-line (TOAST). O tamanho de um valor out-of-line é lido de seu ponteiro TOAST, portanto, o cálculo apenas varre a própria tabela e nunca carrega os corpos dos e-mails. A parte de anexos é adicionada separadamente como a soma lógica de `EmailAttachment.Size`.

O serviço de atualização atualiza o cache em três situações:

1. **Preenchimento inicial (Startup backfill)** — Após uma nova instalação ou após a atualização para a versão que introduz este recurso, cada conta começa sem um valor em cache. O serviço percorre todas as contas uma a uma e computa o primeiro valor.
2. **Atualização diária** — Uma vez por dia no horário configurado (padrão `02:30`), o serviço recomputa todas as contas como uma rede de segurança.
3. **Atualização imediata** — Após uma sincronização de e-mail, importação de EML, importação de MBOX ou exclusão de retenção, a conta afetada é recomputada imediatamente para que o valor exibido permaneça atualizado.

## 🤔 Por que "0 B" é exibido inicialmente {#why-0-b-is-shown-at-first}

Quando o recurso é ativado pela primeira vez, ou quando uma nova conta é criada, a linha de cache para essa conta ainda não existe. O serviço de atualização roda em segundo plano e computa as contas **uma a uma**, com um pequeno atraso entre cada conta para que arquivos muito grandes não sobrecarreguem o banco de dados.
Até que o serviço tenha processado uma conta, a interface exibe **"0 B"** como um marcador de posição. Isso é completamente normal. Assim que o serviço de segundo plano terminar essa conta, o valor real aparecerá na próxima vez que a página for carregada ou atualizada.
Para arquivos existentes grandes, o preenchimento inicial pode levar vários minutos ou mais. O serviço rastreia seu progresso em uma tabela dedicada (`AccountStorageBackfillState`), portanto, se a aplicação for reiniciada no meio do preenchimento, ela simplesmente continua de onde parou — nenhuma conta é computada duas vezes.

## 🔧 Configuração {#configuration}

O recurso pode ser ajustado em `appsettings.json` ou via variáveis de ambiente. Consulte a seção **Account Storage Settings** em [Setup.md](Setup.md) para todas as opções disponíveis, incluindo:

- `AccountStorage__Enabled`: Ativa ou desativa o serviço de atualização (`true` por padrão).
- `AccountStorage__DailyExecutionTime`: Tempo para a atualização completa diária (`02:30` por padrão).
- `AccountStorage__BackfillDelayMs`: Pausa entre contas durante o preenchimento inicial (`5000` ms por padrão).
- `AccountStorage__RefreshBatchDelayMs`: Pausa entre contas durante a atualização diária (`1000` ms por padrão).
- `AccountStorage__CommandTimeoutSeconds`: Timeout de comando do banco de dados para o cálculo de armazenamento por conta (`300` segundos por padrão). Aumente isso para arquivos muito grandes onde a varredura `pg_column_size` leva mais tempo.

> 💡 **Dica**: Você **não** precisa ativar `DatabaseMaintenance__Enabled` para que este recurso funcione. O serviço de atualização de armazenamento roda de forma independente.
