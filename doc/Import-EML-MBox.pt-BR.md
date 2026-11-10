# 📥 Importando E-mail na Web UI (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Além da [command line interface](CLI.md) e da
[importação de Conta via CSV](Account-Import.md), o Mail Archiver pode importar um
Arquivo de e-mail existente **através da interface web**. Existem dois formulários de upload:

| Formulário | Arquivo aceito | Resultado |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | Cada mensagem no arquivo é importada para **uma** Pasta que você escolher |
| **Import EML** | `.zip` contendo arquivos `.eml` | A **estrutura de Pastas dentro do ZIP** decide onde cada mensagem será colocada |

Ambos os formulários arquivam as mensagens em uma Conta Mail Archiver existente, ambos funcionam como
**background jobs** e ambos **ignoram duplicatas automaticamente** — importar
o mesmo arquivo duas vezes não cria uma segunda cópia.

> 💡 **Dica**: Se o seu Arquivo já estiver no servidor como um arquivo, prefira o
> [CLI](CLI.md). Isso evita o upload pelo navegador e é a
> melhor escolha para Arquivos de vários gigabytes.

## 🧭 Acesso {#access}

1. Abra **Mail Accounts** no menu principal.
2. Use o menu suspenso **Import** acima da lista de contas e escolha
   **Import MBox** ou **Import EML**.

Os formulários estão disponíveis para **admins** (todas as contas) e **self managers**
(as contas atribuídas a eles); veja
[User Management](UserManagement.md#-user-account-permissions). Apenas contas **habilitadas**
são oferecidas como alvos de importação — uma conta desabilitada não pode ser selecionada.

## 📨 Import MBox {#import-mbox}

MBox é o formato usado pela maioria dos clientes de e-mail para pastas exportadas e por
backups no estilo `mbox`.

| Campo | Significado |
|-------|---------|
| **MBox File** | O arquivo `.mbox` / `.mbx` para upload |
| **Target Account** | A Conta Mail Archiver que recebe as mensagens |
| **Target Folder** | A Pasta para a qual as mensagens são importadas. Escolha uma da lista de pastas existentes da conta ou digite um novo nome — uma pasta que ainda não existe é **criada automaticamente** durante a importação. O padrão é `INBOX`. |

Todas as mensagens no arquivo são importadas para essa única pasta de destino. O arquivo MBox
em si não carrega informações de pasta, portanto a estrutura da Caixa de correio original
não é reconstruída — se você precisar disso, use a rota EML/ZIP.

## 🗂️ Import EML {#import-eml}

A importação EML espera um **Arquivo ZIP** cujos itens são arquivos `.eml`.

- Arquivos `.eml` na **raiz** do ZIP são importados para `INBOX`.
- Arquivos `.eml` em uma **subpasta** são importados para uma pasta com o **mesmo
  nome** dessa subpasta.

Assim, um ZIP estruturado desta forma:

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```

restaura a árvore de pastas junto com as mensagens. Arquivos `.eml` individuais podem ser
enviados envolvendo-os em um ZIP primeiro (`zip emails.zip *.eml`).

## ⚙️ Comportamento compartilhado {#shared-behaviour}

Ambos os formulários se comportam da mesma maneira assim que você pressiona **Start Import**:

- **Limite de tamanho de arquivo** — configurado com `Upload__MaxFileSizeGB` (padrão **10 GB**).
  O formulário mostra o limite atual ao lado do seletor de arquivos.
- **Processamento em segundo plano** — o upload é colocado em uma fila como um job; você não precisa
  manter o navegador aberto.
- **Tratamento de duplicatas** — mensagens que já estão no Arquivo são ignoradas
  e contadas separadamente. As duplicatas são identificadas pelo cabeçalho `Message-ID`.
- **Entrada malformada** — arquivos ilegíveis são ignorados individualmente e relatados na
  página de status em vez de abortar toda a importação.
- **Progresso** — você é levado diretamente para a página de status do job, que mostra o
  ID do job, nome do arquivo, tamanho do arquivo, status, mensagens processadas/totais e as
  contagens em execução. O mesmo job também aparece na página [Jobs](BackgroundJobs.md).
- **Cancelamento** — uma importação em execução pode ser cancelada a partir de sua página de status. As
  mensagens importadas até aquele ponto permanecem no Arquivo.
- **Trilha de auditoria** — a importação é escrita no [access log](Logs.md), e o
  valor de armazenamento por conta é atualizado posteriormente (veja
  [Per-Account Storage](AccountStorage.md)).

### Status do Job {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

Um job finalizado informa quantas mensagens foram importadas, quantas falharam e quantas
foram ignoradas como duplicatas — por exemplo
*"1420 imported, 3 failed, 87 duplicates"*. Em uma importação bem-sucedida, o arquivo enviado
é excluído do servidor; entradas que falharam são mantidas durante a vida útil do
job para que você possa ver o que foi rejeitado.

## ⚠️ Coisas para prestar atenção {#things-to-watch-out-for}

- **Tipo de arquivo incorreto** — o upload é rejeitado a menos que a extensão seja uma de
  `.mbox`, `.eml` ou `.zip`. O seletor do formulário EML só aceita `.zip`, o do formulário MBox
  apenas `.mbox` e `.mbx`.
- **"0 imported, N duplicates"** — isso não é um erro. As mensagens já estão
  no Arquivo, provavelmente de uma importação ou Sincronização anterior da mesma fonte.
- **Arquivos muito grandes** — um arquivo MBox é lido mensagem por mensagem, portanto um
  arquivo de vários gigabytes leva um tempo, mesmo em segundo plano. Acompanhe o progresso na
  página de status em vez de fazer o upload novamente.
- **Espaço livre em disco** — o arquivo enviado é armazenado temporariamente no servidor e
  precisa de espaço ao lado do seu banco de dados de Arquivo.
- **Nomes de Pasta com caracteres especiais** — os nomes das pastas são extraídos dos caminhos do ZIP
  ou da sua entrada; verifique a lista de pastas da conta após a importação se você
  usou caracteres incomuns.

## 🔗 Documentação Relacionada {#related-documentation}

- [CLI commands](CLI.md) — importando de um diretório montado
- [Account Import (CSV)](Account-Import.md) — adicionando muitas contas de uma vez
- [Background Jobs](BackgroundJobs.md) — monitorando e cancelando jobs de importação
- [Exporting a Mail Account](Export-Account.md) — a direção oposta
- [Setup and Parameters](Setup.md) — configurações de `Upload__*`
