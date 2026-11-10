# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

O Mail Archiver fornece um **servidor MCP (Model Context Protocol)** opcional que expõe o e-mail arquivado para agentes de IA por meio do transporte Streamable HTTP. Ele espelha a [REST API](API.md) de apenas leitura: mesmos dados, mesmo modelo de permissão por usuário, mesmo registro de acesso — apenas empacotado como ferramentas MCP em vez de endpoints REST, para que um agente LLM possa descobri-los e chamá-los diretamente.

O objetivo é permitir que um agente de IA leia e-mails arquivados **sem nunca expor credenciais da Caixa de correio ou falar com o provedor de e-mail.** Uma chave de API com escopo por usuário é uma capacidade para *ler o Arquivo*, não uma credencial para *ser a Caixa de correio* — ela herda apenas as permissões da Caixa de correio de seu proprietário, não concede nenhum caminho de escrita e pode ser revogada instantaneamente sem tocar na Conta de e-mail subjacente.

> ⚠️ **O servidor MCP é desativado por padrão.** Ele deve ser explicitamente habilitado na configuração (veja [Enabling the MCP server](#enabling-the-mcp-server)). Quando desativado, o endpoint `/mcp` retorna `404 Not Found`.

O servidor MCP é **apenas leitura por design**: ele expõe ferramentas para listar contas, listar pastas, pesquisar mensagens, ler uma mensagem e baixar um Anexo. Ele nunca cria, modifica ou deleta dados.

## Sumário {#table-of-contents}

- [Habilitando o servidor MCP](#enabling-the-mcp-server)
- [Autenticação](#authentication)
- [Transporte e endpoint](#transport-and-endpoint)
- [Tools](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [Rate limiting](#rate-limiting)
- [Access logging](#access-logging)
- [Conectando um agente](#connecting-an-agent)
- [Orientações de segurança para exposição à internet](#security-guidance-for-internet-exposure)

## Habilitando o servidor MCP {#enabling-the-mcp-server}

O servidor MCP é configurado na seção `Mcp` de `appsettings.json` (ou as variáveis de ambiente equivalentes, ex: `Mcp__Enabled=true`):

```json
"Mcp": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "DefaultPageSize": 20,
  "MaxResults": 100,
  "MaxAttachmentBytes": 10000000,
  "AllowMessageDownloads": true,
  "MaxMessageBytes": 10000000,
  "RateLimitPerMinute": 120
}
```

| Configuração | Padrão | Descrição |
| --- | --- | --- |
| `Enabled` | `false` | Interruptor principal. Quando `false`, o endpoint `/mcp` retorna `404`. |
| `AllowAttachmentDownloads` | `true` | Quando `false`, `get_attachment` retorna um erro em vez de bytes de anexo. Metadados sobre anexos ainda são retornados por `get_email`. |
| `DefaultPageSize` | `20` | Tamanho da página usado por `search_emails` quando o chamador omite `pageSize`. |
| `MaxResults` | `100` | Limite superior para o tamanho da página `search_emails`; valores maiores são limitados. |
| `MaxAttachmentBytes` | `10000000` | Tamanho máximo de anexo (em bytes) que `get_attachment` retornará inline como base64; anexos maiores são recusados. |
| `AllowMessageDownloads` | `true` | Quando `false`, `get_email_eml` e `get_email_raw` retornam um erro em vez da mensagem. Uma mensagem completa carrega seus anexos, portanto, **tanto** este interruptor quanto `AllowAttachmentDownloads` devem estar `true`; desativar qualquer um deles desativa ambas as ferramentas. |
| `MaxMessageBytes` | `10000000` | Tamanho máximo de mensagem (em bytes) que `get_email_eml` e `get_email_raw` retornarão inline; mensagens maiores são recusadas. |
| `RateLimitPerMinute` | `120` | Orçamento de requisição de janela fixa por chave de API por minuto (veja [Rate limiting](#rate-limiting)). |

## Autenticação {#authentication}

O servidor MCP autentica-se com as **mesmas chaves de API por usuário da API REST**, enviadas como um bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

As chaves de API são criadas e gerenciadas na interface web em **API Keys**. Uma chave herda as permissões exatas do usuário que a possui (um admin vê todas as contas; um usuário restrito vê apenas suas contas atribuídas). As chaves podem ser revogadas ou expiradas independentemente das contas de e-mail subjacentes. Consulte a [seção de autenticação da REST API](API.md#authentication) para obter o ciclo de vida completo da chave e o modelo de permissão — as chaves MCP não são um repositório de credenciais separado.

Quando o servidor MCP está desativado, ou quando nenhum bearer token/um bearer token inválido é apresentado, o endpoint retorna `404` / `401` respectivamente (ele nunca redireciona para a página de login).

## Transporte e endpoint {#transport-and-endpoint}

O servidor MCP utiliza o transporte **Streamable HTTP** e é montado em:

```
POST /mcp
```

Ele é executado em modo **stateless** (uma requisição JSON-RPC por requisição HTTP), que é o modo recomendado para servidores que não precisam de requisições de servidor para cliente, como sampling ou elicitation. As ferramentas são descobertas via a requisição padrão MCP `tools/list` e chamadas via `tools/call`.

## Ferramentas {#tools}

### list_accounts {#list_accounts}

Lista as contas de e-mail que a chave de API atual tem permissão para acessar. Retorna
`id`, `name`, `emailAddress`, `provider`, `isEnabled` e `lastSync`.

**Parâmetros:** nenhum.

**Resultado:** array de objetos de Conta. Use o `id` retornado com `list_folders`
e `search_emails`.

### list_folders {#list_folders}

Lista a árvore de pastas de uma única Conta de e-mail, com contagens de e-mails por Pasta.

**Parâmetros:**

| Nome | Tipo | Obrigatório | Descrição |
| --- | --- | --- | --- |
| `accountId` | integer | sim | O id da Conta de e-mail (de `list_accounts`). |

**Resultado:** array de objetos de Pasta, cada um com `name`, `fullPath`, `totalCount`,
`level` e `children` recursivo. Retorna uma lista vazia se a Conta não
existir ou se a chave não estiver autorizada para ela.

### search_emails {#search_emails}

Pesquisa e-mails no Arquivo por consulta de texto completo, intervalo de datas, Conta, Pasta e direção.

**Parâmetros:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `q` | string | no | Consulta de pesquisa de texto completo (assunto, corpo, de/para). |
| `from` | string (ISO-8601) | no | Filtro de data/hora UTC enviado em ou após. |
| `to` | string (ISO-8601) | no | Filtro de data/hora UTC enviado em ou antes de. |
| `accountId` | integer | no | Restringir a um único id de Conta de e-mail. |
| `folder` | string | no | Restringir a um único caminho completo de Pasta (ex: `INBOX`). |
| `direction` | string | no | `incoming` ou `outgoing`. |
| `page` | integer | no | Número da página baseado em 1 (padrão `1`). |
| `pageSize` | integer | no | Tamanho da página; limitado a `[1, MaxResults]`. `0` usa `DefaultPageSize`. |
| `sortBy` | string | no | `sentdate` (padrão), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | no | `asc` ou `desc` (padrão `desc`). |

**Resultado:** um objeto paginado com `items` (array de resumos de e-mail), `page`,
`pageSize`, `totalItems`, `totalPages`. Cada resumo contém `id`, `accountId`,
`subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` e
`folderName`. Passe um `id` retornado para `get_email` para obter o corpo completo e a lista de Anexo.

### get_email {#get_email}

Busca o conteúdo completo de um único e-mail arquivado.

**Parâmetros:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | O id do e-mail arquivado (de `search_emails`). |

**Resultado:** um único objeto de e-mail com todos os campos de resumo mais `cc`, `bcc`,
`receivedDate`, `messageId`, `textBody`, `htmlBody` e `attachments` (array de
`{ id, fileName, contentType, size }`) e `hasRawMessage` (se
`get_email_raw` pode retornar o original). Retorna `null` se o e-mail não
existir ou se a chave não estiver autorizada para sua Conta.

### get_attachment {#get_attachment}

Baixa um único Anexo de e-mail como um blob base64.

**Parâmetros:**

| Nome | Tipo | Obrigatório | Descrição |
| --- | --- | --- | --- |
| `id` | integer | yes | O id do e-mail Arquivado ao qual o Anexo pertence. |
| `attachmentId` | integer | yes | O id do Anexo (de `attachments` de `get_email`). |

**Resultado:** um objeto com `id`, `emailId`, `fileName`, `contentType`, `size`
e `base64Data` (os bytes brutos do Anexo, codificados em base64). A ferramenta retorna um
erro quando `Mcp:AllowAttachmentDownloads=false`, quando o Anexo excede
`Mcp:MaxAttachmentBytes`, ou quando a chave não está autorizada para a
Conta do e-mail.

### get_email_eml {#get_email_eml}

Retorna um e-mail como uma mensagem RFC 5322 completa (`.eml`), reconstruída a partir dos campos do Arquivo: os cabeçalhos armazenados (`Received`, `Authentication-Results`, `X-Spam-*`, …), corpos e Anexos. Funciona para cada e-mail no Arquivo, mas o resultado não é byte-idêntico ao que foi recebido, portanto as assinaturas DKIM não serão verificadas contra ele — use [`get_email_raw`](#get_email_raw) para isso. Espelha `GET /api/v1/emails/{id}/eml`.

**Parâmetros:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | O id do e-mail no Arquivo (de `search_emails`). |

**Resultado:** um objeto com `emailId`, `kind` (`eml`), `source` (`null`), `sha256`, `size`, `encoding` e `content`. `content` é o texto da mensagem quando `encoding` é `utf-8`, e a mensagem codificada em base64 quando `encoding` é `base64` (mensagens que não são UTF-8 válidas, para que nenhum byte seja alterado). A ferramenta retorna um erro quando `Mcp:AllowMessageDownloads=false`, quando a mensagem excede `Mcp:MaxMessageBytes`, ou quando a chave não está autorizada para a Conta do e-mail.

### get_email_raw {#get_email_raw}

Retorna a mensagem original exatamente como recebida, para análise de spam,
verificações DKIM/SPF/ARC ou perícia forense. Disponível apenas quando a original foi armazenada no momento do
Arquivo (`RawMessages:Enabled`, veja
[Original Message Storage Settings](Setup.md#-original-message-storage-settings));
`get_email` o reporta como `hasRawMessage`. Espelha
`GET /api/v1/emails/{id}/raw`.

**Parâmetros:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | O id do e-mail arquivado (de `search_emails`). |

**Resultado:** o mesmo objeto que `get_email_eml` com `kind` `raw`, `source` definido
como a original foi capturada (`imap`, `graph`, `eml-file` — cópias exatas —
ou `serialized`, reescrito pelo parser com quebras de linha normalizadas para CRLF)
e `sha256` o checksum registrado no momento do Arquivo. Retorna os mesmos erros que
`get_email_eml`, e um erro quando nenhum original é armazenado para o e-mail.

Os bytes são lidos de onde quer que tenham sido armazenados — o banco de dados ou um arquivo,
dependendo de `RawMessages:Storage` no momento do Arquivo (veja
[Storage backend](RawMessages.md#storage-backend)). Com
`RawMessages:FileSystem:VerifyChecksumOnRead` (padrão `true`) o arquivo é
re-hasheado antes de ser retornado; uma mensagem cujo conteúdo não corresponde mais ao
checksum registrado é recusada com um erro em vez de ser retornada.

## Rate limiting {#rate-limiting}

O servidor MCP possui sua própria política de limite de taxa de janela fixa (`"Mcp"`), particionada pelo prefixo não secreto da chave de API (primeiros 11 caracteres do token `ma_...`), revertendo para o IP do cliente quando nenhum bearer token estiver presente. O orçamento é configurado via `Mcp:RateLimitPerMinute` (padrão `120` por minuto). Quando excedido, o endpoint retorna `429 Too Many Requests` com um cabeçalho `Retry-After`. Isso é independente da política de limite de taxa da REST API.

## Registro de acesso {#access-logging}

Cada chamada de ferramenta é registrada através do mesmo `IAccessLogService` que a REST API,
com o nome de usuário proprietário e o tipo de acesso correspondente (`Search`, `Open`,
`Download`). A atividade do MCP é, portanto, visível na página **Logs** ao lado da
atividade da REST API e da web-UI, para um único Registro de auditoria.

## Conectando um agente {#connecting-an-agent}

O servidor MCP utiliza o transporte padrão MCP Streamable HTTP, portanto, qualquer cliente MCP que suporte HTTP pode se conectar. A URL é a URL base da aplicação mais `/mcp`, por exemplo, `http://localhost:5179/mcp`, e a chave de API é fornecida por meio do cabeçalho `Authorization: Bearer ma_...`.

Para agentes configurados via JSON padrão do cliente MCP, uma entrada se parece com:

```json
{
  "mcpServers": {
    "mail-archiver": {
      "url": "https://archive.example.com/mcp",
      "headers": {
        "Authorization": "Bearer ma_<your-api-key>"
      }
    }
  }
}
```

## Orientações de segurança para exposição à internet {#security-guidance-for-internet-exposure}

O servidor MCP foi projetado de modo que, com os padrões e o endurecimento abaixo aplicados, o e-mail em Arquivo não possa deixar o sistema sem uma chave de API válida por usuário que tenha sido explicitamente emitida para uma Conta. Os pontos abaixo são uma **leitura obrigatória antes de expor `/mcp` à internet**.

### Obrigatório (bloquear exploração / exfiltração de dados) {#mandatory-block-exploitation-data-exfiltration}

1. **Apenas HTTPS.** Termine o TLS na borda (proxy reverso). Uma chave de API do tipo bearer sobre HTTP simples pode ser interceptada e reutilizada.
2. **Mantenha `Mcp:Enabled=false` a menos que você realmente o utilize.** Quando desativado, `/mcp` retorna `404` e nenhum caminho de código é alcançável; este é o padrão seguro e o estado seguro para implantações que não precisam de acesso de agentes de IA.
3. **O limite de taxa (rate limiting) é aplicado no endpoint.** A política `"Mcp"` (`Mcp:RateLimitPerMinute`, padrão `120`) é aplicada no endpoint `/mcp` mapeado. Ele é particionado pelo prefixo não secreto da chave de API (primeiros 11 caracteres do token `ma_...`) ou pelo IP do cliente quando nenhum token bearer está presente. **Reduza `RateLimitPerMinute`** para exposição à internet (ex: `30`–`60`) — chamadas de ferramentas MCP são mais pesadas que chamadas REST e o endpoint não deve ser deixado com um orçamento alto quando clientes não confiáveis podem alcançá-lo.
4. **O `X-Forwarded-For` de proxy reverso deve ser confiado com cuidado.** O limitador de taxa recorre ao `RemoteIpAddress`, que sob um proxy é o IP do proxy, a menos que o `UseForwardedHeaders` receba o IP real do cliente. Certifique-se de que seu proxy configure `X-Forwarded-For` corretamente e que apenas seu proxy possa configurá-lo; caso contrário, um invasor pode forjar a partição do limite de taxa.
5. **Não coloque `/mcp` atrás de um cache de CDN.** As respostas MCP são JSON-RPC por requisição e nunca devem ser armazenadas em cache — o cache quebraria o protocolo e vazaria o resultado de um usuário para outro. Marque `/mcp` como não cacheável na borda (`Cache-Control: no-store`).

### Higiene da chave de API (bloquear acesso não autorizado) {#api-key-hygiene-block-unauthorized-access}

6. **Emita chaves dedicadas e de curta duração para agentes MCP.** Uma única chave `ma_...` é válida tanto para `/api` quanto para `/mcp`; não há escopo por protocolo hoje. Portanto:
   - Crie uma **chave separada por agente** (não reutilize uma chave de script de relatório para um agente LLM externo).
   - Sempre defina um `ExpiresAt` curto e rotacione-o; revogue imediatamente quando o agente for desativado.
   - Prefira chaves pertencentes a um **usuário restrito (não administrador)** cujas únicas Contas atribuídas sejam as que o agente precisa. Uma chave de administrador concede visibilidade a *todas* as contas de e-mail; uma chave restrita limita o raio de alcance.
7. **Trate a chave de API como uma senha.** Ela é uma credencial bearer. Nunca a incorpore em código do lado do cliente, nunca a registre em logs, nunca a envie para um repositório. Coloque-a no armazenamento de segredos do cliente MCP e rotacione em caso de suspeita de comprometimento.
8. **Auditoria na página de Logs.** Cada chamada de ferramenta é registrada em `AccessLogs` (`Search`, `Open`, `Download`) sob o usuário proprietário. Revise periodicamente a atividade do MCP em busca de padrões inesperados (picos de volume, leituras fora do horário, downloads em massa).

### Modelo de confiança para dados retornados (bloquear danos a jusante) {#trust-model-for-returned-data-block-downstream-harm}

9. **Corpos de e-mail e Anexos são conteúdos não confiáveis**, exatamente como chegaram na Caixa de correio. Eles podem conter malware, links de phishing ou HTML/JavaScript ativos.
   - `get_email` retorna `htmlBody` **bruto e não sanitizado**. **Não** o renderize para um humano sem sanitização (DOMPurify / `HtmlSanitizer` / um sandbox). Renderizar HTML arquivado bruto é um vetor de XSS armazenado.
   - `get_attachment` retorna os bytes brutos com o `contentType` original. **Não** abra ou execute automaticamente Anexos; trate o campo `contentType` como metadados não confiáveis, não como uma instrução segura.
   - `get_email_eml` e `get_email_raw` retornam a mensagem completa, incluindo cada Anexo. As mesmas regras se aplicam a cada uma de suas partes.
10. **Não permita que o agente realize ações de escrita baseadas no conteúdo do e-mail sem confirmação.** Um e-mail malicioso pode conter texto de injeção de prompt visando o LLM consumidor. Aplique Permissões e confirmação na própria lógica do agente — o prompt do sistema não é uma fronteira de segurança.

### Recomendações de defesa em profundidade {#defense-in-depth-recommendations}

11. **Restrinja `/mcp` na camada de rede** quando possível (ex: permitir apenas o IP / intervalo de VPN do host do agente no proxy reverso), além da autenticação por chave de API. Este é o controle individual mais eficaz para exposição à internet.
12. **Reduza `Mcp:AllowAttachmentDownloads` para `false`** se seus agentes não precisarem de bytes de Anexos — a exfiltração de Anexos é o caminho de dados de maior volume. Metadados (nome do arquivo, tipo de conteúdo, tamanho) ainda são retornados via `get_email`.
13. **Reduza `Mcp:MaxAttachmentBytes`** para o menor valor que seus agentes realmente precisem (padrão `10000000` ≈ 10 MB). Isso limita a maior resposta única que uma chave de API pode buscar por requisição. O mesmo vale para `Mcp:MaxMessageBytes`, ou defina `Mcp:AllowMessageDownloads` como `false` quando os agentes não precisarem de mensagens completas.
14. **Reduza `Mcp:MaxResults` e `Mcp:DefaultPageSize`** para limitar o maior resultado de busca que uma chave de API pode buscar por requisição (padrão `100` / `20`).
15. **Monitore `AccessLogs` e rejeições de limite de taxa** (`429`) operacionalmente. Um pico repentino de um único prefixo de chave é um sinal para revogar essa chave.
