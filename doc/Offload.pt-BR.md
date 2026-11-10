# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Voltar para o Índice da Documentação](index.md)

## 📋 Visão Geral {#overview}

Um offload anexa e-mails arquivados mais recentes que um corte em outra Caixa de correio, e deixa tudo o que é mais antigo no Arquivo. Ele existe para migrações onde a nova Caixa de correio deve conter apenas e-mails recentes, enquanto o histórico completo permanece acessível através do Mail Archiver.

Ele difere do [Copy All Emails to Another Mailbox](MailboxMigration.md) de cinco maneiras:

| | Copy All Emails | Offload |
|---|---|---|
| Filtro de data | nenhum, tudo ou nada | corte, com um limite superior opcional |
| Repetível | não, anexa tudo novamente | sim, pula o que o destino já possui |
| Exclusões de Pasta | não | sim, configurável |
| Renomeação de Pasta | não | sim, configurável |
| Pré-visualização | não | dry run relata por pasta sem escrever |

O offload apenas anexa ao destino. Ele nunca deleta do Arquivo e nunca toca na Caixa de correio de origem.

## 🔁 Repetir uma execução é seguro {#repeating-a-run-is-safe}

Antes do primeiro anexo, toda a Caixa de correio de destino é indexada e cada mensagem é verificada contra ela. Uma execução repetida, portanto, não anexa nada que já chegou, e reporta como já presente em vez disso. Isso tem três consequências práticas:

- Uma execução interrompida é recuperada simplesmente executando-a novamente.
- Uma execução finalizada pode ser repetida como uma passagem de verificação. Um resultado correto é nada anexado e tudo já presente.
- Mensagens deletadas no destino voltam na próxima execução, porque não estão mais no índice.

Dois critérios decidem se uma mensagem já está presente, nesta ordem:

1. **Message-ID**, normalizado. Isso faz quase todo o trabalho.
2. **Uma impressão digital (fingerprint)** sobre remetente, destinatários, assunto e hora de envio dentro de dois segundos. Isso cobre as mensagens cujo Message-ID armazenado está ausente ou malformado. Tais mensagens não podem ser restauradas com seu Message-ID original de forma alguma, então sem este segundo critério elas seriam duplicadas em cada execução.

O job reporta quantos matches vieram da impressão digital separadamente, porque é o critério que poderia falhar silenciosamente.

## ⚙️ Configuração {#configuration}

Uma nova seção `Offload`, com padrões que reproduzem o comportamento anterior:

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Chave | Padrão | Propósito |
|---|---|---|
| `MaxConcurrentJobs` | `1` | Quantos jobs de restauração ou offload podem rodar ao mesmo tempo. Um mantém o comportamento estritamente serial que a fila de jobs sempre teve. No máximo um job por Caixa de correio de destino roda independentemente deste valor. |
| `PrefetchMaxMessages` | `500000` | Limite superior de quantas mensagens são indexadas de uma Caixa de correio de destino. Acima disso, a verificação de duplicatas se estreita para uma única pasta e registra que o fez. |
| `ExcludedSourceFolders` | vazio | Pastas de origem nunca enviadas via offload. Verificadas antes da renomeação e cobrem subpastas. |
| `FolderRenameMap` | vazio | Reescreve os segmentos iniciais de um caminho de pasta de origem. |
| `MarkAsSeen` | `true` | Se o e-mail anexado é marcado como lido. |

Ambas as configurações de pasta são enviadas vazias de propósito. Reescrever ou descartar pastas sem ser solicitado surpreenderia qualquer pessoa que já usa o caminho de restauração, então uma execução sem configuração migra tudo, incluindo pastas de spam, e cria um segundo conjunto de pastas especiais ao lado das próprias do destino.

### Exclusões e renomeação de pastas {#folder-exclusions-and-renaming}

Para uma migração de Exchange para Dovecot, as duas configurações geralmente são assim:

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

Sem o mapa de renomeação, cada Caixa de correio migrada termina com ambas `Sent` e `Sent Items`, porque o Dovecot pré-cria suas próprias pastas de uso especial.

Quatro coisas sobre como elas são aplicadas valem a pena saber, porque cada uma é uma forma de ser surpreendido:

1. **Exclusões são correspondidas primeiro**, contra o nome de origem. Se a renomeação acontecesse primeiro, uma exclusão em `Deleted Items` nunca seria acionada uma vez que já tivesse se tornado `Trash`.
2. **A renomeação reescreve o prefixo de caminho correspondente mais longo**, em limites de segmento. `Sent Items` tornando-se `Sent` também transforma `Sent Items/2019` em `Sent/2019`, e deixa `Sent Items Archive` intacta.
3. **A correspondência é insensível a maiúsculas/minúsculas**, e tanto `/` quanto `\` contam como separadores.
4. **Duas pastas de origem podem colapsar em uma de destino.** `Sent Items` e `Sent` tornando-se `Sent` é aceitável e não abre a pasta de destino duas vezes.

Alterar o mapa de renomeação entre duas execuções não duplica nada, porque a verificação de duplicatas cobre toda a Caixa de correio de destino em vez de uma pasta resolvida.

## 🖥️ Executando pela interface {#running-it-from-the-interface}

Na página de detalhes de uma Conta, **Offload to Another Mailbox** abre um formulário com a Caixa de correio de destino, a pasta raiz de destino, se deve preservar a estrutura de pastas, a janela de data, um interruptor de dry run e se deve marcar o e-mail anexado como lido. As exclusões configuradas e o mapa de renomeação são exibidos apenas para leitura, para que uma execução nunca seja uma surpresa.

O job é colocado na fila e seu progresso e relatório por pasta aparecem na página de status do job e em **Jobs**. O relatório por pasta nomeia as pastas de origem, portanto é mostrado apenas ao usuário que iniciou o job e aos administradores.

A Caixa de correio de destino pode ser uma conta IMAP desabilitada, por exemplo, para provisionar um alvo de migração antes de entrar em produção; a execução falha de forma evidente na conexão se o destino for inalcançável. Contas do Microsoft 365 não podem ser alvos de offload; o caminho de restauração do Graph permanece inalterado.

### Quem pode executar {#who-may-run-it}

Administradores podem fazer offload entre quaisquer duas contas. Um self-manager pode fazer offload **apenas entre as contas atribuídas a ele**, em ambas as extremidades: a origem, porque a página da conta já é delimitada dessa forma, e o destino, porque a lista de destino é restringida ao mesmo conjunto e a solicitação é verificada contra ela novamente quando o job é iniciado. Um self-manager com uma única conta atribuída, portanto, não tem um alvo elegível e é informado disso.

O escopo vem de `IAccountAccessResolver`, o mesmo resolver que a API REST e o servidor MCP usam, portanto há uma definição de quem pode usar qual Caixa de correio. A decisão em si reside em `OffloadTargetEligibility`, que tanto a lista de destino quanto a verificação de solicitação chamam, portanto o que o formulário oferece e o que o servidor aceita não podem divergir.

## ⌨️ Executando pela linha de comando {#running-it-from-the-command-line}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argumento | Significado |
|---|---|
| `--source-account-id` | Conta para ler e-mails arquivados. Obrigatório. |
| `--target-account-id` | Conta IMAP para anexar. Obrigatório, e deve ser diferente da origem. |
| `--since` | Limite inferior inclusivo na data de envio, `YYYY-MM-DD`. Obrigatório. |
| `--until` | Limite superior inclusivo opcional. |
| `--target-folder` | Pasta raiz na Caixa de correio de destino. Padrão é `INBOX`. |
| `--preserve-folders` | Recriar a estrutura de pastas de origem abaixo da raiz. |
| `--dry-run` | Reportar o que aconteceria, sem anexar nada. |
| `--no-mark-seen` | Anexar sem a flag Seen. |

Códigos de saída:

| Código | Significado |
|---|---|
| `0` | Tudo foi anexado ou já estava presente. |
| `1` | Pelo menos uma mensagem falhou, ou a própria execução falhou. |
| `2` | A invocação estava errada: argumentos incorretos ou ausentes, conta desconhecida, destino não é IMAP, origem igual ao destino. |

Uma invocação lida com uma Caixa de correio, o que torna uma frota de Caixas de correio programável a partir de um CSV de origem, destino e corte.

> **Executando várias ao mesmo tempo.** A regra de um job por destino só existe dentro de um único processo, portanto duas invocações de linha de comando simultâneas contra a *mesma* Caixa de correio de destino poderiam cada uma pegar seu índice de duplicatas antes que a outra comece a escrever, e poderiam então ambas anexar o mesmo e-mail. Particione o trabalho para que cada invocação simultânea tenha um destino diferente, que é o caso normal quando cada linha de um CSV é uma Caixa de correio diferente.

## 🗓️ Como o corte é interpretado {#how-the-cutoff-is-interpreted}

- O filtro é na **data de envio**, não em quando uma mensagem foi arquivada. O timestamp de arquivamento é o tempo da execução de arquivamento e não tem significado para um corte.
- Uma janela relativa como "os últimos doze meses" é **resolvida para uma data fixa quando o job é criado**. Um job pode rodar por muito tempo e ser repetido depois, e ele tem que selecionar o mesmo e-mail toda vez; uma janela relativa armazenada sofreria derivação.
- Os cortes são interpretados no **fuso horário de exibição configurado**, o mesmo que a tela de busca mostra, e o limite inferior é inclusivo a partir de 00:00 daquele dia. Um limite superior inclui o dia inteiro. Janelas relativas ("os últimos N meses") também são resolvidas nesse fuso horário de exibição, correspondendo a como `SentDate` é armazenado.

### Limitação conhecida {#known-limitation}

- Quando o índice de duplicatas atinge `Offload:PrefetchMaxMessages`, o escopo é reduzido e registrado em vez de ser tentado novamente contra uma única pasta. O fallback de `restrictToFolder` que o construtor de índice oferece não é usado atualmente pelo caminho de offload; aumentar o limite é o remédio suportado.

## 📋 Uma migração, de ponta a ponta {#a-migration-end-to-end}

1. Crie as Caixas de correio de destino. Para muitas de uma vez, veja a importação em massa de CSV em [Account Import](Account-Import.md).
2. Configure `Offload:ExcludedSourceFolders` e `Offload:FolderRenameMap`. Ambos são vazios por padrão, então pular isso migra pastas de spam e cria pastas especiais duplicadas.
3. Confirme se cada Caixa de correio de origem está totalmente arquivada antes de fazer o offload.
4. **Faça um dry run de tudo.** Verifique as contagens e os nomes de pastas de destino resolvidos antes de qualquer coisa ser escrita.
5. Execução real. Comece com `MaxConcurrentJobs` em 2 e aumente apenas após observar como o servidor de destino lida com isso; o Dovecot limita conexões simultâneas por usuário e por IP.
6. Repita a execução para verificar. Espere nada anexado e tudo já presente.
7. Desabilite as contas de origem.

O throughput é limitado por `BatchOperation:PauseBetweenEmailsMs`, que é 50 ms por padrão e, portanto, permite no máximo vinte anexos por segundo antes que a latência do servidor seja contada.

## 🔍 O que os contadores significam {#what-the-counters-mean}

| Contador | Significado |
|---|---|
| Anexado | Escrito no destino. Em um dry run, o que teria sido escrito. |
| Já presente | O destino já o possuía, por qualquer critério. |
| de total por impressão digital | Quantos foram reconhecidos pelo segundo critério em vez de pelo Message-ID. |
| Pasta excluída | Pulado porque a pasta de origem está excluída. |
| Falhou | Não pôde ser anexado. |

Um aviso é reportado se o índice de duplicatas não cobriu toda a Caixa de correio de destino, o que acontece acima de `PrefetchMaxMessages` ou quando uma pasta não pôde ser lida. Nesse estado, uma execução repetida pode anexar e-mails que já estão presentes nas partes que não foram indexadas.

## ⚠️ Notas e limites {#notes-and-limits}

- O e-mail restaurado agora carrega seu **tempo de entrega original** como a data interna do IMAP, extraído da cadeia `Received` da mensagem e caindo para a data de envio. Anteriormente, ele carregava o tempo da execução de arquivamento, o que quebrava a ordenação do lado do servidor por chegada e qualquer regra baseada em idade no destino.
- Como uma execução repetida pula o que já está presente, uma data interna errada não pode ser corrigida executando novamente: as mensagens são reconhecidas e puladas. Acerte isso antes de uma execução de produção, ou delete as mensagens afetadas no destino e execute novamente.
- Microsoft 365 não é suportado como alvo de offload.
- A detecção de duplicatas só pode reconhecer o que consegue ver na Caixa de correio de destino. E-mails que chegaram ao destino por outra rota são capturados apenas na medida em que os dois critérios por acaso coincidem com eles.
