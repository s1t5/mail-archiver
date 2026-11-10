# 📤 Exportação de Dados de Auditoria {#audit-data-export}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

A exportação de dados de auditoria cria pacotes de dados massivos tabulares (ZIP com `INDEX.XML` + tabelas CSV + índice DTD) a partir do Arquivo existente. Esses pacotes seguem a estrutura de índice estabelecida para a entrega de mídia de dados e podem ser importados diretamente por ferramentas comuns de auditoria e análise externa que suportam este formato de índice.

O recurso está disponível como uma **página dedicada acessível a partir da página de Logs** e é restrito a usuários administradores. Cada execução de exportação é registrada no registro de acesso, portanto, o histórico de exportação é seguro contra revisões.

## 🧭 Acesso {#access}

1. Faça login como um usuário administrador
2. Abra a página **Logs**
3. Clique no botão **Audit Data Export** no canto superior direito

Usuários que não são administradores não veem o botão e não podem chamar nenhum dos endpoints de exportação (o servidor os rejeita).

## 📦 Estrutura do Pacote de Exportação {#export-package-structure}

O arquivo ZIP gerado é nomeado `audit-export-<timestamp>.zip` e contém:

| Arquivo | Conteúdo |
|---|---|
| `INDEX.XML` | Arquivo de índice descrevendo o fornecedor de dados, a mídia e todas as definições de tabela/coluna |
| `index.dtd` | O DTD que o arquivo de índice referencia; necessário para ferramentas de importação para validação |
| `emails.csv` | Uma linha por e-mail arquivado no período/Caixa de correio selecionado |
| `attachments.csv` | Opcional; uma linha por Anexo dos e-mails exportados |

### Tabela `emails.csv` {#table-emailscsv}

Colunas (sem linha de cabeçalho; os nomes das colunas vêm de `INDEX.XML`, como de costume para este formato):

| Coluna | Descrição |
|---|---|
| Id | ID interno do e-mail (numérico) |
| MessageId | Cabeçalho Message-ID do e-mail |
| SentDate | Data de envio, ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | Data de recebimento, ISO 8601 UTC |
| IsOutgoing | `1` = enviado, `0` = recebido |
| From | Remetente |
| To | Destinatários |
| Cc | Destinatários em CC |
| Bcc | Destinatários em CCO |
| Subject | Assunto |
| FolderName | Pasta na qual o e-mail está arquivado |
| HasAttachments | `1` = com anexos, `0` = sem anexos |
| AttachmentCount | Número de anexos (numérico) |
| AccountEmail | Endereço de e-mail da Caixa de correio de arquivamento |

### Tabela `attachments.csv` {#table-attachmentscsv}

| Coluna | Descrição |
|---|---|
| EmailId | ID interno do e-mail ao qual o Anexo pertence (chave de junção para `emails.csv`) |
| FileName | Nome do arquivo do Anexo |
| ContentType | Tipo MIME |
| Size | Tamanho em bytes (numérico) |
| Sha256 | Hash SHA-256 do conteúdo (do armazenamento de anexos endereçados por conteúdo) |

### Convenções CSV {#csv-conventions}

- Codificação: UTF-8 **sem** BOM (declarado como `UTF8` em `INDEX.XML`)
- Delimitador de coluna: `;`
- Encapsulador de texto: `"` (duplicado para escape)
- Delimitador de registro: `\n` (explicitamente declarado em `INDEX.XML`)
- Datas: ISO 8601, horários convertidos do fuso horário de exibição configurado para UTC
- Sem linha de cabeçalho; sem números localizados (colunas numéricas contêm apenas inteiros)

## 🖥️ Usando a Página de Exportação {#using-the-export-page}

### Campos do Formulário {#form-fields}

1. **From / To date** - Período para exportar (obrigatório, intervalo máximo configurável, padrão 10 anos)
2. **Mailbox** - Restringir a exportação a uma única Caixa de correio ou exportar todas as Caixas de correio
3. **Attachment metadata** - Quando marcado, `attachments.csv` é adicionado ao pacote
4. **Data supplier name / location / comment** - Escrito no bloco `DataSupplier` de `INDEX.XML`; preenchido automaticamente a partir da seção de configuração `AuditExport` e editável por exportação

### Ciclo de Vida do Trabalho {#job-lifecycle}

1. Clicar em **Start Export** valida o período e a Caixa de correio, cria um trabalho e retorna à página
2. O trabalho é executado em segundo plano (exportações na fila são processadas uma após a outra); a tabela de trabalhos mostra o progresso em tempo real (e-mails processados/total com uma barra de progresso) via polling automático
3. Quando a exportação termina, o botão **Download** aparece na linha do trabalho
4. Trabalhos em execução ou na fila podem ser **cancelados**

### Histórico de Exportação {#export-history}

A tabela de histórico abaixo do formulário lista as exportações mais recentes com data/hora, usuário, período, Caixa de correio, contagem de e-mails, status e tamanho do arquivo. O histórico é **persistido no banco de dados** (tabela `AuditExportJobs`), portanto, sobrevive a reinicializações da aplicação e é seguro contra revisões.

Os arquivos de download são mantidos por uma janela de Retenção configurável (padrão 30 dias, veja `AuditExport__RetentionDays`); após isso, os arquivos ZIP são excluídos pela limpeza diária, enquanto as linhas de histórico permanecem.

> 📝 **Nota**: Os arquivos de exportação são armazenados dentro do container da aplicação. Se você precisar mantê-los a longo prazo, faça o download e armazene-os em seu repositório de evidências de auditoria.

## 🔐 Auditoria {#auditing}

Cada exportação produz duas entradas de registro de acesso do novo tipo **Audit Data Export**:

1. **Entrada de início** - quando a exportação é colocada na fila (com ID do trabalho, período, Caixa de correio, opções como JSON)
2. **Entrada de resultado** - quando a exportação é concluída, falha ou é cancelada (com ID do trabalho, período, Caixa de correio, contagem de e-mails, resultado e tamanho do pacote como JSON)

As entradas aparecem na página de Logs como todas as outras entradas de registro de acesso e podem ser filtradas pelo novo tipo. O menu suspenso de filtro de tipo as identifica automaticamente.

## ⚙️ Configuração {#configuration}

A seção `AuditExport` em `appsettings.json` (variáveis de ambiente usam a sintaxe de sublinhado duplo, ex: `AuditExport__RetentionDays`):

| Configuração | Descrição | Padrão |
|---|---|---|
| `DataSupplierName` | Valor padrão para o campo de formulário do nome do fornecedor de dados | vazio |
| `DataSupplierLocation` | Valor padrão para o campo de formulário da localização do fornecedor de dados | vazio |
| `Comment` | Valor padrão para o campo de formulário de comentário | vazio |
| `OutputDirectory` | Diretório para os arquivos ZIP gerados (caminhos relativos resolvem em relação à raiz de conteúdo do app) | `exports/audit` |
| `RetentionDays` | Dias até que os arquivos de exportação concluídos sejam excluídos | `30` |
| `MaxRangeYears` | Intervalo máximo permitido entre a data de início e fim do período | `10` |

## 🗄️ Banco de Dados {#database}

O histórico de exportação é armazenado na tabela `mail_archiver."AuditExportJobs"` (criada pela migração `MigrateV2609_1`, aplicada automaticamente na inicialização). Nenhuma tabela existente é modificada por este recurso.

## 🔍 Solução de Problemas {#troubleshooting}

| Problema | Causa / Solução |
|---|---|
| Botão de download ausente após reinicialização | Os arquivos de exportação residem no container; se o container foi recriado ou a janela de Retenção passou, o ZIP sumiu enquanto a linha de histórico permanece. Execute a exportação novamente. |
| "The period must not exceed X years" | Reduza o período ou aumente `AuditExport__MaxRangeYears`. |
| O trabalho permanece em *Running* por muito tempo | Arquivos grandes são transmitidos em lotes de 1000 e-mails; o progresso é atualizado continuamente. Períodos muito grandes podem levar algum tempo — verifique o contador de processados/total. |
| Ferramenta de importação relata problemas de codificação | O pacote é UTF-8 sem BOM com a flag `UTF8` declarada e delimitador de registro explícito; certifique-se de que a ferramenta lê o `INDEX.XML` ao lado dos arquivos de dados (extraia o ZIP inteiro, não abra arquivos individualmente de dentro do ZIP). |

## 🔗 Documentação Relacionada {#related-documentation}

- [Access Logging](Logs.md)
- [Installation, Setup and Parameters](Setup.md)
