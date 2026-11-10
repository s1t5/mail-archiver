# ⚙️ Background Jobs {#background-jobs}

[← Voltar para o Índice da Documentação](index.md)

## 📋 Visão Geral {#overview}

A maior parte do trabalho no Mail Archiver não acontece enquanto você espera. Sincronizar uma Caixa de correio, importar um arquivo MBOX, restaurar milhares de e-mails ou exportar um Arquivo pode levar de minutos a horas. Essas tarefas são executadas como **background jobs** — você as inicia e elas continuam rodando mesmo se você fechar o navegador.

A página **Background Jobs** mostra o que está sendo executado no momento, o que terminou e o que deu errado. É o lugar para consultar quando algo demora mais do que o esperado ou quando uma sincronização termina com erros.

> **ℹ️ Página de Administrador.** Background Jobs requer direitos de administrador e é acessível pelo menu principal. Usuários comuns não a veem — eles ainda podem iniciar tarefas (por exemplo, uma restauração em lote), mas o monitoramento acontece aqui.

## 🗂️ Tipos de Job {#job-types}

A página agrupa os jobs por tipo. Cada seção mostra até os **20 mais recentes**, com jobs em execução e na fila ordenados no topo — para que um job antigo não seja ocultado por novos.

| Seção | O que abrange | Iniciado de |
|---|---|---|
| **Synchronization Jobs** | Buscar novos e-mails do servidor de e-mail | Agendamento automático, ou *Sync now* em uma Conta |
| **MBox Import Jobs** | Importar arquivos `.mbox` | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import Jobs** | Importar arquivos `.eml` / arquivos ZIP | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Batch Restore Jobs** | Copiar e-mails selecionados de volta para uma Caixa de correio | Archive → select emails → Batch Restore |
| **Account Export Jobs** | Exportar uma Conta inteira (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Selected Emails Export Jobs** | Exportar um conjunto selecionado de e-mails | Archive → select emails → Export |
| **Email Deletion Jobs** | Excluir e-mails do servidor ou do Arquivo, incluindo exclusão em massa baseada em filtro | Política de Retenção, exclusão manual, ou Archive → *Delete all matches* |
| **Account Deletion Jobs** | Remover uma Conta de e-mail inteira | Mail Accounts → Delete |
| **Offload** | Mover e-mails de um intervalo de datas para outra Conta | Mail Accounts → Offload |

## 📊 Lendo uma Linha de Job {#reading-a-job-row}

Cada linha possui o mesmo conjunto de colunas:

| Coluna | Significado |
|---|---|
| **Job ID** | Identificador curto — cite este ID ao relatar um problema |
| **Account** | A Caixa de correio na qual o job trabalha |
| **Status** | Estado atual, veja a tabela abaixo |
| **Progress** | O quanto o job avançou, com processados/total quando conhecido |
| **Created** | Quando o job foi colocado na fila |
| **Started** | Quando ele realmente começou a ser executado |
| **Completed** | Quando ele terminou (vazio enquanto ainda estiver em execução) |
| **Duration** | Tempo decorrido |

Alguns tipos de job adicionam contadores à coluna de progresso:

- **Success** / **Failed** — e-mails processados com sucesso e sem sucesso
- **Skipped (duplicates)** — e-mails já presentes no Arquivo, não importados duas vezes
- **Recovered** — mensagens buscadas novamente após o servidor informar que estavam ausentes
- **Malformed (skipped)** — mensagens que não puderam ser analisadas e foram ignoradas

### Valores de Status {#status-values}

| Status | Significado |
|---|---|
| **Queued** | Aguardando para iniciar — outro job está rodando, ou o worker está ocupado |
| **Running** | Atualmente em execução |
| **Completed** | Terminado com sucesso |
| **CompletedWithErrors** | Terminado, mas alguns itens falharam (importações MBox) |
| **Failed** | Terminou com um erro |
| **Cancelled** | Interrompido por um usuário |
| **Rate Limited** | *Sync pausada devido ao limite de largura de banda. Será retomada automaticamente.* |
| **Timed Out** | A sincronização excedeu o limite de tempo configurado e foi interrompida |

> **ℹ️ "Rate Limited" e "Timed Out" são estados exclusivos de sincronização.** Eles parecem falhas, mas não são: uma sincronização com limite de taxa retoma sozinha assim que a cota de largura de banda permitir, e uma sincronização com timeout continua de seu checkpoint na próxima execução. Veja [Rate Limit Handling](RateLimitHandling.md) e [Mail Synchronization](Synchronization.md).

### Por que o progresso pode ficar em 0% {#why-progress-can-sit-at-0}

O progresso é medido por **Pastas concluídas**, não por e-mails individuais:

> *O progresso é medido por Pastas concluídas. Uma pasta grande (ex: INBOX) mantém a barra em 0% até que ela termine.*

Em uma Caixa de correio onde a INBOX contém a maior parte dos e-mails, a barra pode permanecer em 0% por muito tempo e depois saltar. Isso é esperado — o job está trabalhando, apenas ainda não terminou sua primeira pasta grande.

## 🔄 A Página se Atualiza Sozinha {#the-page-refreshes-itself}

Enquanto a página estiver aberta, ela recarrega a cada **30 segundos**, para que você possa deixá-la em uma aba e acompanhar o progresso sem clicar em nada. O rótulo mostra quando os dados foram atualizados pela última vez.

O recarregamento é cancelado no momento em que você clica em um link ou botão — caso contrário, seu clique cairia em uma página que está sendo substituída por baixo dela.

**Configurável:** `Jobs:RefreshSeconds` em `appsettings.json` (padrão `30`).
Defina como `0` para desativar o recarregamento automático. Cada recarregamento reconstrói a página a partir de todas as fontes de jobs, portanto, um intervalo muito curto em uma instalação com muitas Contas é uma carga real — e essa carga se multiplica com cada aba aberta.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Cancelando um Job {#cancelling-a-job}

Jobs que suportam cancelamento mostram um botão **Cancel Job**, que pergunta:

> *Are you sure you want to cancel this job?*

Jobs de importação usam a redação **Cancel Import** / *Are you sure you want to cancel this import job?*

O cancelamento está disponível para jobs de **sincronização, restauração em lote, exportação de e-mails selecionados e exclusão de e-mails**. O job para assim que o item atual for finalizado; o status torna-se **Cancelled**.

> **⚠️ Cancelar não é um rollback.** E-mails já importados, restaurados, exportados ou excluídos permanecem assim. Cancelar impede que o job faça *mais* — não desfaz o que ele já fez. Para um job de exclusão, isso significa que os e-mails já removidos sumiram.

Jobs de Conta e de importação MBox não podem ser cancelados nesta página; pare-os na própria página de importação da Conta.

## ⚠️ Falhas de Sincronização e "Acknowledge Failures" {#sync-failures-and-acknowledge-failures}

Quando uma sincronização termina com o status **Failed**, a linha do job oferece a opção **Acknowledge Failures**. Esta é a única ação na página que altera o comportamento futuro, por isso vale a pena entender.

Cada execução de sincronização começa de onde a última parou, usando um **checkpoint**. Se uma sincronização falha para alguns e-mails, o checkpoint permanece antes desses e-mails para que a próxima execução tente buscá-los novamente. Esse é o padrão seguro — nada é ignorado silenciosamente.

Mas se as falhas forem permanentes (uma mensagem corrompida, uma pasta que o servidor se recusa a fornecer), cada sincronização subsequente tentará novamente e a sincronização continuará falhando no mesmo lugar.

**Acknowledge Failures** resolve esse impasse avançando o checkpoint para além dos e-mails que falharam. A confirmação é explícita sobre as consequências:

> *This will advance the sync timestamp to the completion time of this job, so the next sync will be a normal incremental sync. The failed emails will not be retried. Continue?*

Em caso de sucesso, você verá: *"Sync timestamp has been advanced. The next sync will be incremental."*

> **⚠️ Só confirme após analisar as falhas.** Após o reconhecimento, esses e-mails **não** serão tentados novamente — eles estarão ausentes do Arquivo, a menos que você os busque de outra forma. Verifique os detalhes da falha primeiro e, se a causa for uma configuração incorreta, corrija-a antes de confirmar. Veja [Mail Synchronization](Synchronization.md) para detalhes sobre o comportamento do checkpoint.

O reconhecimento é registrado no log de acesso como *SyncAcknowledgeFailures*, com o número de e-mails que falharam e a Conta, para que possa ser rastreado posteriormente.

## 👁️ O Que Você Não Pode Fazer Aqui {#what-you-cannot-do-here}

- **Sem botão de repetição.** Um job que falhou não é reiniciado por esta página — inicie a operação novamente de onde você a começou (sincronização, importação, exportação).
- **Sem edição.** Os parâmetros do job são fixos no momento da criação.
- **Apenas os últimos 20 por tipo.** Jobs mais antigos não são listados. Para registros de longo prazo, o log de acesso e o log da aplicação são as fontes — veja [Access Logging](Logs.md) e [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Solução de Problemas {#troubleshooting}

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| Job permanece em **Queued** | Outro job do mesmo worker está rodando | Aguarde; verifique outras seções para um job de longa duração |
| Progresso travado em 0% | Uma pasta grande ainda está sendo processada | Normal — veja "Por que o progresso pode ficar em 0%" |
| Sincronização repetidamente **Failed** na mesma Conta | Erros permanentes em e-mails individuais | Inspecione as falhas, corrija a causa e depois confirme |
| Sincronização mostra **Rate Limited** | Cota de largura de banda atingida | Nada — ela retoma automaticamente |
| Sincronização mostra **Timed Out** | Caixa de correio maior que o limite de tempo | Ela continuará do checkpoint na próxima execução; veja [Synchronization](Synchronization.md) |
| **CompletedWithErrors** em uma importação MBox | Algumas mensagens malformadas | Os contadores mostram quantas; o restante foi importado |
| Job desaparece após o recarregamento | Saiu da janela dos "últimos 20" | Verifique o log da aplicação para o status final |
| Página recarrega enquanto você trabalha | Atualização automática a cada 30 s | Defina `Jobs:RefreshSeconds` para `0` |

## 📚 Documentação Relacionada {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — checkpoints, sincronizações com timeout
- [Rate Limit Handling](RateLimitHandling.md) — cotas de largura de banda e retomada automática
- [Access Logging](Logs.md) — quem fez o quê, incluindo ações de jobs
- [Batch Restore](MailboxMigration.md) — movendo e-mails entre Caixas de correio
- [Date-Windowed Offload](Offload.md) — jobs de offload
- [Docker Compose Logs](DockerComposeLogs.md) — lendo o log da aplicação
