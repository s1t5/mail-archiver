# Importação de Conta {#account-import}

[← Voltar para o Índice da Documentação](index.md)

## Visão Geral {#overview}

O Mail Archiver suporta a importação de múltiplas contas de e-mail de uma só vez para que o onboarding de um grande número de Caixas de correio não exija a criação de cada Conta individualmente.

Existem dois caminhos de importação em massa, dependendo do provedor de e-mail:

| Provedor | Método de Importação em Massa | Documentação |
|---|---|---|
| **Microsoft 365 (tenant)** | Descoberta de tenant via Microsoft Graph — um registro de app acessa cada Caixa de correio no tenant | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | Importação em massa via CSV — faça o upload de um arquivo CSV com uma linha por Caixa de correio | Esta página (veja abaixo) |

> **Nota:** Contas pessoais da Microsoft (Outlook.com / M365 Family, provedor **MSA**) usam um fluxo de autorização de código de dispositivo interativo e não podem ser importadas em massa. Elas devem ser criadas e autorizadas uma a uma. Veja [MSA Outlook Setup](MSA_Outlook_Setup.md).

---

## Importação em Massa de CSV IMAP {#imap-csv-bulk-import}

### O que faz {#what-it-does}

A importação em massa via CSV permite que um administrador crie centenas de contas de e-mail IMAP em uma única operação ao fazer o upload de um arquivo CSV. Cada linha no arquivo corresponde a uma Caixa de correio. Configurações comuns de servidor (host IMAP, porta, SSL) podem ser especificadas uma vez no formulário de upload e aplicadas a todas as linhas, com sobreposições opcionais por linha.

A importação é **exclusiva para o administrador**. Ela é acessível na página **Mail Accounts** através do menu suspenso **Import** → **CSV Bulk Import**.

### Formato do Arquivo CSV {#csv-file-format}

O arquivo deve ser um CSV codificado em UTF-8 com uma **linha de cabeçalho** e a **vírgula (`,`) como delimitador**. Campos contendo vírgulas ou aspas devem ser envolvidos por aspas duplas (`"..."`).

#### Colunas {#columns}

| Coluna | Obrigatório | Descrição |
|---|---|---|
| `email` | Sim | Endereço da Caixa de correio. Também usado como o nome de usuário de login padrão quando `username` não é fornecido. |
| `password` | Sim | Senha de login IMAP. Armazenada conforme fornecida — espaços iniciais/finais são preservados. |
| `name` | Não | Nome de exibição para a Conta. Se omitido, o padrão é `{prefix} - <{email}>`. |
| `username` | Não | Nome de usuário de login se for diferente do endereço de e-mail. |
| `imap_server` | Não | Host IMAP por linha. Sobrescreve o servidor comum do formulário de upload. |
| `imap_port` | Não | Porta IMAP por linha (1–65535). Sobrescreve a porta comum. |
| `use_ssl` | Não | Alternância de SSL por linha (`true` ou `false`). Sobrescreve a configuração comum. |

#### Exemplo {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- A Linha 1 usa o servidor/porta/SSL comum do formulário de upload.
- A Linha 2 define um nome de usuário personalizado, mas de outra forma usa as configurações comuns.
- A Linha 3 sobrescreve o servidor, a porta e o SSL.

Um arquivo de exemplo para download está disponível na página de importação através do botão **Download example CSV**.

### Formulário de Upload {#upload-form}

| Campo | Propósito |
|---|---|
| **CSV file** | O arquivo para upload. |
| **IMAP server** | Host comum para todas as linhas sem uma sobreposição de `imap_server`. |
| **IMAP port** | Porta comum (padrão 993). |
| **Use SSL** | Alternância de SSL comum (padrão ativado). |
| **Name prefix** | Prefixo para nomes de Conta gerados automaticamente. Padrão: `IMAP`. |
| **Skip existing mailboxes** | Quando ativado, linhas cujo e-mail já existe como uma conta IMAP são ignoradas (contadas como "puladas", não como erros). Quando desativado, são tratadas como falhas. |
| **Account enabled** | Se as Contas criadas devem ser habilitadas para Sincronização em segundo plano. |
| **Delete After Days / Local Retention Days** | Configurações de Retenção opcionais aplicadas a cada Conta criada. Veja [Retention Policies](RetentionPolicies.md). |

### Processamento {#processing}

1. O arquivo CSV é analisado e cada linha é validada (formato de e-mail, senha presente, intervalo de porta, valor de SSL).
2. E-mails duplicados dentro do arquivo são removidos (a primeira ocorrência vence).
3. Contas IMAP existentes com o mesmo endereço de e-mail são detectadas; dependendo da alternância **Skip existing**, elas são ignoradas ou reportadas como falhas.
4. Todas as novas Contas são inseridas em um único lote no banco de dados.
5. Uma entrada no registro de acesso é escrita resumindo a execução.

Nenhum teste de conexão IMAP é realizado durante a importação — as Contas são salvas imediatamente. Problemas de autenticação só aparecerão durante a primeira Sincronização em segundo plano. Isso mantém a importação rápida mesmo para centenas de Caixas de correio.

### Página de Resultado {#result-page}

Após a conclusão da importação, uma página de resultado mostra três tabelas:

- **Created** — e-mail e nome de cada Conta que foi criada.
- **Skipped** — e-mails que eram duplicados (no arquivo ou já existentes) quando **Skip existing** está ativado.
- **Failed** — linhas que não puderam ser processadas, com o número da linha e o motivo (e-mail ausente, senha ausente, porta inválida, sem servidor IMAP, etc.).

Um banner de resumo mostra as contagens: *{created} criadas, {skipped} puladas, {failed} falhas*.

### Limites {#limits}

Os seguintes limites podem ser configurados via `appsettings.json` / variáveis de ambiente (veja [Setup Guide](Setup.md) → *CSV Import Settings*):

| Configuração | Padrão | Descrição |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | Máximo de linhas processadas por upload. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | Tamanho máximo do arquivo enviado. |

### Nota de Segurança {#security-note}

As senhas IMAP são armazenadas no banco de dados da mesma forma que para Contas IMAP criadas individualmente. Esteja ciente de que o arquivo CSV contém todas as senhas das Caixas de correio em texto simples — exclua-o da máquina do cliente após uma importação bem-sucedida.

---

## Importação de Tenant Microsoft 365 {#microsoft-365-tenant-import}

Para Microsoft 365 (Exchange Online), a importação em massa não requer um arquivo CSV. Um único registro de app no Azure AD com as permissões de Graph corretas pode acessar cada Caixa de correio no tenant, para que o Mail Archiver possa descobrir as Caixas de correio automaticamente.

Veja o guia dedicado: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.
