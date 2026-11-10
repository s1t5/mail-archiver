# API REST Somente Leitura (v1) {#read-only-rest-api-v1}

O Mail Archiver fornece uma API REST opcional, de **somente leitura**, para acesso programático ao e-mail arquivado. Ela é projetada para casos de uso de automação e integração (relatórios, exportações de e-discovery, monitoramento) sem passar pela interface web.

O objetivo é permitir que um script ou agente de IA leia o e-mail arquivado **sem nunca expor as credenciais da Caixa de correio ou falar com o provedor de e-mail.** Uma chave de API com escopo por usuário é uma capacidade para *ler o Arquivo*, não uma credencial para *ser a Caixa de correio* — ela herda apenas as permissões de Caixa de correio do proprietário, não concede caminho de escrita e pode ser revogada instantaneamente sem tocar na Conta de e-mail subjacente.

> ⚠️ **A API é desativada por padrão.** Ela deve ser explicitamente habilitada na configuração (veja [Habilitando a API](#enabling-the-api)). Quando desativada, cada rota `/api/*` retorna `404 Not Found`.

A API é **somente leitura por design**: ela expõe contas, pastas, busca de mensagens, detalhes de mensagens e downloads de Anexo. Ela nunca cria, modifica ou exclui dados.

## Sumário {#table-of-contents}

- [Habilitando a API](#enabling-the-api)
- [Autenticação](#authentication)
  - [Formato da chave de API](#api-key-format)
  - [Ciclo de vida da chave](#key-lifecycle)
  - [Modelo de Permissão](#permission-model)
  - [Racional de segurança](#security-rationale)
- [Convenções](#conventions)
  - [URL base e versionamento](#base-url-and-versioning)
  - [Paginação](#pagination)
  - [Erros](#errors)
  - [Limitação de taxa](#rate-limiting)
- [Endpoints](#endpoints)
  - [Listar contas](#list-accounts)
  - [Listar pastas](#list-folders)
  - [Pesquisar mensagens](#search-messages)
  - [Obter mensagem](#get-message)
  - [Baixar anexo](#download-attachment)
  - [Baixar mensagem (.eml)](#download-message-eml)
  - [Baixar mensagem original](#download-original-message)
  - [Estatísticas](#stats)
- [Tipos de dados](#data-types)
- [Registro de acesso](#access-logging)
- [OpenAPI e Swagger UI](#openapi-and-swagger-ui)
- [Referência da API](API-Reference.md) — referência de endpoint e tipo de dado gerada

## Habilitando a API {#enabling-the-api}

A API é configurada sob a seção `Api` de `appsettings.json` (ou as
variáveis de ambiente equivalentes, por exemplo, `Api__Enabled=true`):

```json
"Api": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "AllowMessageDownloads": true,
  "EnableSwaggerUi": true,
  "DefaultPageSize": 20,
  "MaxPageSize": 100,
  "RateLimitPerMinute": 120
}
```


### Ciclo de vida da chave {#key-lifecycle}

1. **Criar** — um usuário cria uma chave com um `Name` descritivo e uma expiração opcional.
   A resposta mostra a chave em texto simples uma única vez.
2. **Usar** — envie a chave como um bearer token. Cada requisição aceita atualiza o `LastUsedAt`
   da chave (as gravações são limitadas a no máximo uma vez a cada ~5 minutos para
   evitar uma gravação em cada chamada).
3. **Expirar** — se `ExpiresAt` estiver definido e no passado, a chave é rejeitada.
4. **Revogar** — o usuário proprietário ou um administrador revoga a chave
   (`RevokedAt` está definido); ela é imediatamente e permanentemente rejeitada.

Uma chave é **ativa** quando não é revogada, não está expirada **e** seu usuário
proprietário ainda está ativo. Uma chave inativa por qualquer um desses motivos é rejeitada com `401`.

### Modelo de Permissão {#permission-model}

Uma chave de API herda as permissões exatas do usuário que a possui. O
principal autenticado carrega as mesmas claims que um login web normal emite, portanto,
todas as autorizações existentes

| Configuração | Padrão | Descrição |
| --- | --- | --- |
| `Enabled` | `false` | Interruptor mestre. Quando `false`, todas as rotas `/api/*` retornam `404` e o documento OpenAPI / Swagger UI não são mapeados. |
| `AllowAttachmentDownloads` | `true` | Quando `false`, o endpoint de download de Anexo retorna `403`. Metadados sobre Anexos ainda são retornados pelo endpoint de detalhes da mensagem. |
| `AllowMessageDownloads` | `true` | Quando `false`, os endpoints de mensagem completa (`/eml` e `/raw`) retornam `403`. Uma mensagem completa carrega seus Anexos, portanto, **tanto** este interruptor quanto `AllowAttachmentDownloads` devem ser `true`; desativar qualquer um deles desativa ambos os endpoints. |
| `EnableSwaggerUi` | `true` | Quando `true` (e `Enabled` é `true`), monta o Swagger UI em `/apidocs` e o documento OpenAPI em `/apidocs/spec/v1.json`. Ambos requerem uma **sessão web (cookie)** autenticada — eles não fazem parte de `/api/` e não são alcançáveis com uma chave de API. |
| `DefaultPageSize` | `20` | Tamanho da página usado quando uma requisição omite `pageSize`. |
| `MaxPageSize` | `100` | Limite superior; valores de `pageSize` maiores são limitados a este. |
| `RateLimitPerMinute` | `120` | Orçamento de requisição de janela fixa por chave de API por minuto (veja [Rate limiting](#rate-limiting)). |

## Autenticação {#authentication}

A API autentica com **chaves de API por usuário** enviadas como um bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` aceita **apenas** chaves de API. Cookies de navegador não são honrados, portanto a
API não possui superfície de CSRF. Falhas de autenticação retornam `401` com um
cabeçalho `WWW-Authenticate: Bearer` e um corpo [problem+json](#errors) — a API
**nunca** redireciona para a página de login.

As chaves são gerenciadas via autoatendimento na interface web em **API Keys** (cada usuário
gerencia suas próprias chaves; administradores podem visualizar e revogar as chaves de qualquer usuário).

### Formato da chave de API {#api-key-format}

Uma chave é a string `ma_` seguida por 43 caracteres base64 seguros para URL codificando 32
bytes aleatórios (256 bits de entropia):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Armazenamento:

- **`KeyPrefix`** — os primeiros ~11 caracteres (ex: `ma_8Kf3pQ2`), armazenados em
  texto simples. Usado para identificar uma chave na interface do usuário e nos logs. Não é um segredo.
- **`KeyHash`** — o SHA-256 codificado em hex da chave completa, armazenado sob um
  **índice único**. A chave em texto simples nunca é armazenada.

A chave completa é mostrada ao usuário **exatamente uma vez**, no momento da criação. Ela não pode
ser recuperada posteriormente; uma chave perdida deve ser revogada e substituída.

### Ciclo de vida da chave {#key-lifecycle}

1. **Criar** — um usuário cria uma chave com um `Name` descritivo e uma expiração opcional.
   A resposta mostra a chave em texto simples uma vez.
2. **Usar** — envie a chave como um bearer token. Cada requisição aceita atualiza o `LastUsedAt`
   da chave (as gravações são limitadas a no máximo uma vez a cada ~5 minutos para
   evitar uma gravação em cada chamada).
3. **Expirar** — se `ExpiresAt` estiver definido e no passado, a chave é rejeitada.
4. **Revogar** — o usuário proprietário ou um administrador revoga a chave
   (`RevokedAt` é definido); ela é imediatamente e permanentemente rejeitada.

Uma chave está **ativa** quando não está revogada, não está expirada **e** seu usuário proprietário
ainda está ativo. Uma chave inativa por qualquer um desses motivos é rejeitada com `401`.

### Modelo de Permissão {#permission-model}

Uma chave de API herda as permissões exatas do usuário que a possui. O
principal autenticado carrega as mesmas reivindicações que um login web normal emite, portanto,
toda a lógica de autorização existente se aplica sem alterações:

- **Administradores** (`IsAdmin`) veem todas as contas de e-mail e todas as mensagens.
- **Usuários regulares** veem apenas as contas de e-mail atribuídas a eles
  (`UserMailAccount`). Contas e mensagens fora desse conjunto são tratadas como
  **inexistentes**: requisições para elas retornam `404`, não `403`, portanto a API
  não vaza a existência de contas que um chamador não pode ver.

### Justificativa de segurança {#security-rationale}

- **Bypass de 2FA.** Chaves de API ignoram a autenticação de dois fatores por design — isso é
  inerente a tokens não interativos (o mesmo ocorre com tokens de acesso pessoal
  no GitHub/GitLab). Isso é mitigado por: chaves estarem **desativadas por padrão**,
  revogáveis, opcionalmente expiráveis, limitadas às permissões do usuário e cada
  acesso ser registrado no [access log](#access-logging).
- **Sem cookies em `/api`.** A autenticação por cookie não é aceita em rotas `/api`,
  eliminando preocupações de CSRF para a API.
- **Transporte.** Sempre sirva a API via HTTPS em produção para que os bearer tokens
  não sejam expostos no cabo.

## Convenções {#conventions}

### Base URL e versionamento {#base-url-and-versioning}

Todos os endpoints estão sob `/api/v1`. A versão está no caminho para que futuras
alterações disruptivas possam ser lançadas como `/api/v2` sem perturbar os clientes existentes.

As respostas são `application/json`, a menos que indicado (downloads de anexo retornam o
content type do próprio anexo). Todos os timestamps são UTC, ISO 8601
(`2026-06-11T09:30:00Z`).

### Paginação {#pagination}

Endpoints de listagem retornam um envelope de paginação:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` é baseado em 1. Páginas fora do intervalo retornam um array `items` vazio (não é um
  erro).
- `pageSize` tem como padrão `Api:DefaultPageSize` e é limitado a
  `[1, Api:MaxPageSize]`.

### Erros {#errors}

Erros usam [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
`application/problem+json`:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| Status | Quando |
| --- | --- |
| `400 Bad Request` | Parâmetros malformados (ex: data impossível de processar, `sortBy` inválido). |
| `401 Unauthorized` | Chave ausente, malformada, expirada, revogada ou de outra forma inválida. Inclui `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Downloads de anexo solicitados enquanto `AllowAttachmentDownloads=false`, ou downloads de mensagem enquanto `AllowMessageDownloads=false`. |
| `404 Not Found` | O recurso não existe **ou** está fora das contas permitidas do chamador. Também retornado para cada rota `/api/*` quando `Api:Enabled=false`. |
| `429 Too Many Requests` | Limite de taxa excedido. |
| `500 Internal Server Error` | Erro inesperado do servidor (problem+json, nunca a página de erro HTML). |

### Limite de taxa {#rate-limiting}

As solicitações são limitadas por uma política de janela fixa de `Api:RateLimitPerMinute`
solicitações por minuto (padrão `120`), particionadas pelo prefixo da chave de API (recorrendo ao IP do cliente quando nenhuma chave está presente). Exceder o orçamento retorna `429`.

## Endpoints {#endpoints}

> Todos os exemplos assumem que a API está habilitada e que `$KEY` possui uma chave válida.

### List accounts {#list-accounts}

```
GET /api/v1/accounts
```

Retorna as contas de e-mail visíveis para o chamador. Campos de credenciais
(IMAP/OAuth username, password, client secret, …) **nunca** são incluídos.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/accounts
```

```json
[
  {
    "id": 1,
    "name": "Support Mailbox",
    "emailAddress": "support@example.com",
    "provider": "IMAP",
    "isEnabled": true,
    "lastSync": "2026-06-11T08:15:00Z"
  }
]
```

### List folders {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Retorna a árvore de pastas para uma Conta como uma estrutura aninhada. Retorna `404` se a Conta não existir ou não estiver visível para o chamador.

```json
[
  {
    "name": "INBOX",
    "fullPath": "INBOX",
    "totalCount": 4213,
    "level": 0,
    "children": [
      { "name": "Work", "fullPath": "INBOX/Work", "totalCount": 512, "level": 1, "children": [] }
    ]
  }
]
```

### Pesquisar mensagens {#search-messages}

```
GET /api/v1/emails
```

Pesquisa mensagens arquivadas com as mesmas capacidades da interface web.

| Parâmetro de consulta | Tipo | Descrição |
| --- | --- | --- |
| `q` | string | Consulta de texto completo sobre assunto, corpo, de, para, cc, bcc. Suporta sintaxe de campo, ex: `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Data de envio mais antiga (inclusive), `YYYY-MM-DD`. |
| `to` | date | Data de envio mais recente (inclusive), `YYYY-MM-DD`. |
| `accountId` | int | Restringir a uma Conta (deve estar visível para o chamador). |
| `folder` | string | Restringir a uma Pasta pelo caminho completo. |
| `direction` | string | `incoming` ou `outgoing`. |
| `page` | int | Número da página baseado em 1 (padrão `1`). |
| `pageSize` | int | Resultados por página (padrão `Api:DefaultPageSize`, limitado a `Api:MaxPageSize`). |
| `sortBy` | string | Um de `sentDate` (padrão), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` ou `desc` (padrão `desc`). |

Retorna um [envelope paginado](#pagination) de [`EmailSummaryDto`](#emailsummarydto).
Registra uma entrada de [registro de acesso](#access-logging) do tipo `Search`.

```bash
curl -H "Authorization: Bearer $KEY" \
  "https://host/api/v1/emails?q=subject:invoice&from=2026-01-01&pageSize=50"
```

```json
{
  "items": [
    {
      "id": 9876,
      "accountId": 1,
      "subject": "Invoice #4711",
      "from": "billing@vendor.com",
      "to": "support@example.com",
      "sentDate": "2026-03-02T11:04:00Z",
      "isOutgoing": false,
      "hasAttachments": true,
      "folderName": "INBOX"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 3,
  "totalPages": 1
}
```

### Obter mensagem {#get-message}

```
GET /api/v1/emails/{id}
```

Retorna a mensagem completa, incluindo os corpos. O corpo utiliza a mesma cadeia de fallback da interface web: `OriginalBodyHtml` (bytes brutos) → `BodyUntruncatedHtml` →
`HtmlBody`, e o equivalente para o corpo de texto. Retorna `404` se a mensagem
não existir ou pertencer a uma Conta fora das Permissões do chamador. Registra uma entrada de log de acesso do tipo `Open`.

Retorna um [`EmailDetailDto`](#emaildetaildto).

### Download anexo {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Realiza o streaming dos bytes do anexo com seu tipo de conteúdo e nome de arquivo armazenados. Retorna
`404` se a mensagem ou o anexo não existirem ou não forem visíveis para o
chamador. Retorna `403` (problem+json) quando `Api:AllowAttachmentDownloads=false`.
Registra uma entrada de registro de auditoria do tipo `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Download message (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Retorna a mensagem como `message/rfc822`, reconstruída a partir dos campos arquivados:
endereços, os cabeçalhos brutos armazenados (`Received`, `Authentication-Results`,
`X-Spam-*`, …), corpos e anexos. Funciona para cada e-mail arquivado. É a
mesma reconstrução que a exportação de Conta da interface web produz, portanto, **não** é
byte-idêntica ao que foi recebido: assinaturas DKIM não serão verificadas contra ela.
Use [`/raw`](#download-original-message) para isso.

Retorna `404` se a mensagem não existir ou não for visível para o solicitante, e
`403` (problem+json) quando `Api:AllowMessageDownloads=false`. Registra uma entrada de log de acesso
do tipo `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Baixar mensagem original {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Retorna a mensagem original conforme recebida (`message/rfc822`), para análise de spam,
verificação de DKIM ou verificações forenses. O original só é armazenado quando
`RawMessages:Enabled` foi definido no momento em que o e-mail foi arquivado; veja
[Configurações de Armazenamento de Mensagem Original](Setup.md#-original-message-storage-settings).
Se um e-mail possui um, isso é indicado por `hasRawMessage` em
[`EmailDetailDto`](#emaildetaildto).

| Cabeçalho de resposta | Significado |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 dos bytes retornados, registrado quando a mensagem foi armazenada |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (cópias exatas) ou `serialized` (reescrito pelo parser, finais de linha normalizados para CRLF) |
| `X-Raw-Message-Storage` | `postgres` ou `filesystem` — de onde os bytes foram lidos (veja [Storage backend](RawMessages.md#storage-backend)) |

Os bytes podem residir no banco de dados ou em um arquivo, dependendo de
`RawMessages:Storage` no momento em que a mensagem foi arquivada; o endpoint
resolve isso por mensagem, portanto, ambos os tipos são servidos da mesma forma. Com
`RawMessages:FileSystem:VerifyChecksumOnRead` (padrão `true`), o arquivo é
re-hasheado antes de ser enviado, e uma mensagem cujo conteúdo não corresponde mais ao
checksum registrado **não** é servida.

Retorna `404` se a mensagem não existir ou não estiver visível para o chamador,
`404` com o título do problema `No original message is stored for this email.`
quando ela existe sem um original, e `500` com o título
`Stored original message failed its checksum.` quando o conteúdo armazenado não corresponde mais ao
checksum registrado no momento do arquivamento. Retorna `403` (problem+json) quando
`Api:AllowMessageDownloads=false`. Registra uma entrada de log de acesso do tipo `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Stats {#stats}

```
GET /api/v1/stats
```

Retorna estatísticas agregadas de Arquivo visíveis ao chamador: total de e-mails arquivados, contas de e-mail visíveis, anexos nesses e-mails e o tamanho total do banco de dados PostgreSQL em megabytes. Para usuários que não são administradores, as contagens são limitadas às contas de e-mail atribuídas a eles (o mesmo escopo que o painel web aplica); administradores veem totais globais.

Para chaves de API que não são de administrador, `databaseSizeInMB` é uma string vazia: o tamanho do banco de dados descreve toda a instância, não o escopo da chave. E-mails, Contas e Anexos são sempre limitados às contas que a chave pode acessar.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/stats
```

```json
{
  "emails": 160132,
  "accounts": 4,
  "attachments": 43540,
  "databaseSizeInMB": "5432"
}
```

### StatsDto {#statsdto}

| Campo | Tipo | Notas |
| --- | --- | --- |
| `emails` | int | Mensagens arquivadas visíveis para o chamador |
| `accounts` | int | Contas de Caixa de correio visíveis para o chamador |
| `attachments` | int | Anexos nessas mensagens visíveis |
| `databaseSizeInMB` | string | Tamanho total do banco de dados PostgreSQL em MB; vazio para chaves que não são de administrador |

## Tipos de dados {#data-types}

### MailAccountDto {#mailaccountdto}

| Campo | Tipo | Notas |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

Nenhum campo de credencial é serializado.

### FolderNodeDto {#foldernodedto}

| Campo | Tipo | Notas |
| --- | --- | --- |
| `name` | string | Nome da folha |
| `fullPath` | string | Caminho completo |
| `totalCount` | int | Mensagens na Pasta |
| `level` | int | Profundidade de aninhamento (0 = raiz) |
| `children` | FolderNodeDto[] | Sub-pastas |

### EmailSummaryDto {#emailsummarydto}

| Campo | Tipo |
| --- | --- |
| `id` | int |
| `accountId` | int |
| `subject` | string |
| `from` | string |
| `to` | string |
| `sentDate` | datetime |
| `isOutgoing` | bool |
| `hasAttachments` | bool |
| `folderName` | string |

### EmailDetailDto {#emaildetaildto}

Tudo de [`EmailSummaryDto`](#emailsummarydto) mais:

| Campo | Tipo | Notas |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Resolvido via cadeia de fallback do corpo |
| `htmlBody` | string | Resolvido via cadeia de fallback do corpo |
| `attachments` | AttachmentDto[] | Apenas metadados |
| `hasRawMessage` | bool | `true` quando a mensagem original é armazenada e [`/raw`](#download-original-message) a retorna |

### AttachmentDto {#attachmentdto}

| Campo | Tipo | Notas |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytes |

Os bytes do Anexo são obtidos a partir do [download endpoint](#download-attachment),
não deste DTO.

## Registro de acesso {#access-logging}

Todo acesso a dados através da API é registrado no mesmo registro de acesso usado pela interface web (visível em **Logs**), atribuído ao usuário proprietário da chave:

| Endpoint | Tipo registrado |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI e Swagger UI {#openapi-and-swagger-ui}

Quando a API está habilitada e `Api:EnableSwaggerUi=true`:

- O documento OpenAPI v1 é servido em **`/apidocs/spec/v1.json`**.
- O Swagger UI é servido em **`/apidocs`**.

Ambos os caminhos estão fora de `/api/` e, portanto, são protegidos pela autenticação normal da web (cookie) — uma sessão de navegador logada é necessária para visualizá-los, e eles não podem ser acessados com uma chave de API.

### Referência de endpoint neste site {#endpoint-reference-on-this-site}

Os endpoints e tipos de dados também são publicados aqui como uma página gerada:
**[API Reference](API-Reference.md)**. Ela é renderizada a partir do mesmo documento OpenAPI — uma cópia é mantida em `doc/assets/openapi-v1.json` — pelo `tools/gen_api_reference.py`. O CI falha quando a página e a especificação perdem a Sincronização, portanto, a referência sempre descreve a API que realmente é enviada.

!!! note "Regenerando"
    A página é um output gerado. Altere a API (ou o gerador), então execute `python3 tools/gen_api_reference.py` e faça o commit do resultado.

    Atualizar a própria especificação fornecida significa executar novamente a geração de documento em tempo de build descrita em `tools/README.md`.
