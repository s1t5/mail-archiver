# 🛠️ Guia de Configuração do Mail Archiver {#mail-archiver-setup-guide}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia fornece instruções detalhadas para configurar o aplicativo Mail Archiver usando Docker Compose.

## 🛠️ Pré-requisitos {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Passos de Instalação {#installation-steps}

1. Instale os pré-requisitos em seu sistema.

2. Crie um arquivo `docker-compose.yml` com o seguinte conteúdo:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;

      # Authentication Settings
      - Authentication__Username=admin
      - Authentication__Password=secure123!
      - Authentication__SessionTimeoutMinutes=60
      - Authentication__CookieName=MailArchiverAuth
      - Authentication__CookieSameSite=Lax

      # REST API Settings (read-only, disabled by default)
      - Api__Enabled=false
      - Api__AllowAttachmentDownloads=true
      - Api__AllowMessageDownloads=true
      - Api__EnableSwaggerUi=true
      - Api__DefaultPageSize=20
      - Api__MaxPageSize=100
      - Api__RateLimitPerMinute=120

      # MCP Server Settings (Model Context Protocol for AI agents, read-only, disabled by default)
      - Mcp__Enabled=false
      - Mcp__AllowAttachmentDownloads=true
      - Mcp__DefaultPageSize=20
      - Mcp__MaxResults=100
      - Mcp__MaxAttachmentBytes=10000000
      - Mcp__AllowMessageDownloads=true
      - Mcp__MaxMessageBytes=10000000
      - Mcp__RateLimitPerMinute=120

      # MailSync Settings
      - MailSync__IntervalMinutes=15
      - MailSync__TimeoutMinutes=0
      - MailSync__ConnectionTimeoutSeconds=180
      - MailSync__CommandTimeoutSeconds=300
      - MailSync__AlwaysForceFullSync=false
      - MailSync__IgnoreSelfSignedCert=false
      - MailSync__MaxConcurrentSyncs=1
      - MailSync__InterAccountDelaySeconds=0
      - MailSync__MaxIssuesPerKind=20
      - MailSync__FullSyncIntervalHours=24
      - MailSync__ExcludeSubfolders=false
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Contacts

      # Original Message Storage (complete .eml per email, disabled by default)
      - RawMessages__Enabled=false
      - RawMessages__Storage=postgres
      - RawMessages__MaxMessageSizeMb=50
      - RawMessages__IncludeGraph=true
      - RawMessages__FileSystem__BasePath=RawMessages
      - RawMessages__FileSystem__ShardDepth=2
      - RawMessages__FileSystem__VerifyChecksumOnRead=true

      # BatchRestore Settings
      - BatchRestore__AsyncThreshold=50
      - BatchRestore__MaxSyncEmails=150
      - BatchRestore__MaxAsyncEmails=50000
      - BatchRestore__SessionTimeoutMinutes=30
      - BatchRestore__DefaultBatchSize=50

      # Tenant Management Settings
      - TenantManagement__MaxSelectedMailboxes=1000

      # Mailcow Settings
      - Mailcow__MaxSelectedMailboxes=1000

      # BatchOperation Settings
      - BatchOperation__BatchSize=50
      - BatchOperation__PauseBetweenEmailsMs=50
      - BatchOperation__PauseBetweenBatchesMs=250

      # Bandwidth Tracking Settings (for IMAP rate limit handling)
      - BandwidthTracking__Enabled=false
      - BandwidthTracking__DailyLimitMb=25000
      - BandwidthTracking__WarningThresholdPercent=80
      - BandwidthTracking__PauseHoursOnLimit=24
      - BandwidthTracking__TrackUploadBytes=false

      # Selection Settings
      - Selection__MaxSelectableEmails=250

      # View Settings (Privacy & Display)
      - View__DefaultToPlainText=true
      - View__BlockExternalResources=false

      # Dashboard Settings (statistics cache, counter parts, chart periods)
      - Dashboard__CacheSeconds=60
      - Dashboard__RefreshIntervalMinutes=15
      - Dashboard__ShowDirectionSplits=true
      - Dashboard__SelectablePeriods=true

      # Jobs Settings (background jobs page)
      - Jobs__RefreshSeconds=30

      # Npgsql Settings
      - Npgsql__CommandTimeout=900

      # Upload Settings for MBox and EML files
      - Upload__MaxFileSizeGB=10
      - Upload__KeepAliveTimeoutHours=4
      - Upload__RequestHeadersTimeoutHours=2

      # Local Import Settings (for CLI imports from mounted volumes)
      - LocalImport__AllowedPaths__0=/data/import

      # CSV Import Settings (for bulk IMAP account import)
      - CsvImport__MaxRows=5000
      - CsvImport__MaxFileSizeBytes=10000000

      # Deletion Policy Settings (Optional - controls whether email deletion is allowed)
      - DeletionPolicy__DeletionAllowed=true

      # TimeZone Settings
      - TimeZone__DisplayTimeZoneId=Etc/UCT

      # Database Maintenance Settings (Optional)
      - DatabaseMaintenance__Enabled=false
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30

      # Attachment Deduplication Settings (Optional - feature is always on)
      - AttachmentDeduplication__BatchSize=200
      - AttachmentDeduplication__DelayBetweenBatchesMs=0
      - AttachmentDeduplication__StartupDelaySeconds=20
      - AttachmentDeduplication__OrphanCleanupIntervalHours=12
      - AttachmentDeduplication__CommandTimeoutSeconds=300

      # Account Storage Settings (Optional - per-account storage display)
      - AccountStorage__Enabled=true
      - AccountStorage__DailyExecutionTime=02:30
      - AccountStorage__BackfillDelayMs=5000
      - AccountStorage__RefreshBatchDelayMs=1000
      - AccountStorage__CommandTimeoutSeconds=300

      # ReleaseNotes Settings (Version Update Splash Screen)
      - ReleaseNotes__Enabled=true
      - ReleaseNotes__UpdateHintEnabled=true
      - ReleaseNotes__UpdateCheckIntervalHours=6

      # Logging Settings (Optional - defaults to Information level)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning

      # Security Settings
      - AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com

      # OIDC Configuration (see OIDC_Implementation.md for detailed setup)
      - OAuth__Enabled=true
      - OAuth__Authority=https://example.com
      - OAuth__ClientId=YOUR-CLIENT-ID
      - OAuth__ClientSecret=YOUR-CLIENT-SECRET
      - OAuth__DisplayName=PocketID SSO
      - OAuth__ClientScopes__0=openid
      - OAuth__ClientScopes__1=profile
      - OAuth__ClientScopes__2=email
      - OAuth__DisablePasswordLogin=false
      - OAuth__AutoRedirect=false
      - OAuth__AutoApproveUsers=false
      - OAuth__AdminEmails__0=admin@example.com
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      # Uncomment the following line to mount a directory for local file imports
      # (also configure LocalImport__AllowedPaths__0=/data/import in environment variables)
      # - /path/to/your/mbox/files:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
      # Only with RawMessages__Storage=filesystem: keep the original messages outside the
      # container, otherwise they are lost when the container is recreated.
      # - ./raw-messages:/app/RawMessages
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. Edite a configuração do banco de dados no `docker-compose.yml` e defina uma senha segura na variável `POSTGRES_PASSWORD` e no `ConnectionString`.

4. Se você quiser usar autenticação (o que é fortemente recomendado), defina um `Authentication__Username` e `Authentication__Password` que é usado para o usuário admin.

5. Ajuste a variável de ambiente `TimeZone__DisplayTimeZoneId` para corresponder ao seu fuso horário preferido (o padrão é "Etc/UCT"). Você pode usar qualquer identificador de fuso horário IANA (ex: "Europe/Berlin", "Asia/Tokyo").

6. Opcionalmente, configure as variáveis de ambiente `Logging__LogLevel` para controlar a verbosidade dos logs da aplicação. Consulte a seção Logging Settings abaixo para opções disponíveis.

7. Configure um reverse proxy de sua escolha com https e autenticação para proteger o acesso à aplicação. 

> ⚠️ **Atenção**: A aplicação em si não fornece acesso criptografado via https! Ele deve ser configurado via um reverse proxy!

8. Início inicial dos containers:
```bash
docker compose up -d
```

9. Reiniciar containers:
```bash
docker compose restart
```

10. Acessar a aplicação

11. Faça login com suas credenciais definidas e adicione sua primeira Conta de e-mail:
   - Navegue até a seção "Email Accounts"
   - Clique em "New Account"
   - Insira os detalhes do seu servidor e credenciais
   - Salve e comece o arquivamento!
   - Se desejar, crie outros usuários e atribua contas.

## 📚 Explicações de Variáveis de Ambiente {#environment-variable-explanations}

### 🗄️ Conexão com o Banco de Dados {#database-connection}
- `ConnectionStrings__DefaultConnection`: A string de conexão para o banco de dados PostgreSQL. Modifique os valores `Host`, `Database`, `Username` e `Password` conforme necessário.

### 🔐 Configurações de Autenticação {#authentication-settings}
- `Authentication__Username`: O nome de usuário para a Conta de admin.
- `Authentication__Password`: A senha para a Conta de admin.
- `Authentication__SessionTimeoutMinutes`: O tempo limite da sessão em minutos. Isso se aplica ao cookie de login não persistente; usuários que marcam **Remember Me** no formulário de login recebem um cookie persistente válido por um período fixo de 7 dias. Veja [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: O nome do cookie de autenticação.
- `Authentication__CookieSameSite`: Configura o atributo SameSite para cookies de autenticação, sessão e proteção CSRF. Os valores válidos são:
  - `Strict` (padrão): Segurança máxima. Os cookies são enviados apenas com solicitações do mesmo site. Isso pode causar problemas ao navegar para a aplicação a partir de links externos (por exemplo, clicar em um link de outro site), pois a sessão existente não será reconhecida.
  - `Lax`: Recomendado ao usar um reverse proxy. Os cookies são enviados com navegações de nível superior e solicitações do mesmo site, permitindo que os usuários sigam links externos para a aplicação enquanto mantêm a proteção CSRF para solicitações POST.
  - `None`: Os cookies são enviados com todas as solicitações. Requer HTTPS e o atributo `Secure`. Use isso apenas se você tiver requisitos específicos de cross-site e entender as implicações de segurança.

### 🔌 Configurações da REST API {#rest-api-settings}
A REST API de leitura opcional está **desativada por padrão**. Consulte o [guia da REST API](API.md) para a referência completa.
- `Api__Enabled`: Interruptor mestre para a REST API de leitura (padrão `false`). Quando `false`, todas as rotas `/api/*` retornam `404`.
- `Api__AllowAttachmentDownloads`: Permite o download de bytes de Anexo através da API (padrão `true`). Quando `false`, o endpoint de Anexo retorna `403`.
- `Api__AllowMessageDownloads`: Permite o download de mensagens completas através da API, tanto o `/eml` reconstruído quanto o `/raw` original (padrão `true`). Quando `false`, ambos os endpoints retornam `403`. Uma mensagem completa carrega seus Anexos, portanto, **tanto** este interruptor quanto `Api__AllowAttachmentDownloads` devem estar em `true`; desativar qualquer um deles desativa ambos os endpoints.
- `Api__EnableSwaggerUi`: Expõe o Swagger UI em `/apidocs` e o documento OpenAPI em `/apidocs/spec/v1.json` (padrão `true`). Ambos requerem uma sessão de navegador logada.
- `Api__DefaultPageSize`: Tamanho de página padrão para endpoints de listagem quando não especificado (padrão `20`).
- `Api__MaxPageSize`: Tamanho de página máximo permitido; solicitações maiores são limitadas (padrão `100`).
- `Api__RateLimitPerMinute`: Orçamento de solicitação de janela fixa por chave de API por minuto (padrão `120`).

### 🤖 Configurações do Servidor MCP {#mcp-server-settings}
O servidor MCP (Model Context Protocol) opcional expõe o mesmo acesso de leitura de e-mail que a REST API para agentes de IA através do transporte Streamable HTTP. Ele vem **desativado por padrão**. Consulte o [guia MCP](MCP.md) para a referência completa. O MCP reutiliza exatamente as mesmas chaves de API da REST API (tokens de portador `ma_...`) e o mesmo escopo de acesso à Conta por usuário.
- `Mcp__Enabled`: Interruptor mestre para o servidor MCP (padrão `false`). Quando `false`, o endpoint `/mcp` retorna `404`.
- `Mcp__AllowAttachmentDownloads`: Permite que a ferramenta `get_attachment` retorne bytes de Anexo (padrão `true`). Quando `false`, a ferramenta retorna um erro.
- `Mcp__DefaultPageSize`: Tamanho de página padrão usado por `search_emails` quando o chamador omite `pageSize` (padrão `20`).
- `Mcp__MaxResults`: Limite superior para o tamanho da página de `search_emails`; valores maiores são limitados (padrão `100`).
- `Mcp__MaxAttachmentBytes`: Tamanho máximo de Anexo (em bytes) que a ferramenta `get_attachment` retornará inline como base64; Anexos maiores são recusados (padrão `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Permite que as ferramentas `get_email_eml` e `get_email_raw` retornem mensagens completas (padrão `true`). Quando `false`, as ferramentas retornam um erro. Uma mensagem completa carrega seus anexos, portanto, **tanto** este interruptor quanto `Mcp__AllowAttachmentDownloads` devem estar em `true`; desativar qualquer um deles desativa ambas as ferramentas.
- `Mcp__MaxMessageBytes`: Tamanho máximo de mensagem (em bytes) que as ferramentas `get_email_eml` e `get_email_raw` retornarão inline; mensagens maiores são recusadas (padrão `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Orçamento de requisição de janela fixa por chave de API por minuto (padrão `120`).

### 📨 Configurações do MailSync {#mailsync-settings}
- `MailSync__IntervalMinutes`: O intervalo em minutos entre a Sincronização de e-mails. Este é o padrão global; cada Conta pode substituí-lo individualmente na página Criar/Editar (deixe vazio para usar este padrão).
- `MailSync__FullSyncIntervalHours`: Padrão global opcional para resincronizações completas automáticas, em horas. Quando não definido (o padrão), nenhuma sincronização completa automática é executada, a menos que um valor de `FullSyncIntervalHours` por Conta seja definido na página Criar/Editar. Valores por Conta substituem este padrão global.
- `MailSync__TimeoutMinutes`: Tempo limite de sincronização por Conta em minutos. Uma sincronização que dure mais tempo para na próxima fronteira de mensagem, mantém seus checkpoints e retoma na próxima execução; o trabalho é relatado como `Timed Out` e `LastSync` não é avançado. Aplica-se apenas a sincronizações agendadas; uma sincronização iniciada pela UI (sincronização manual ou resincronização completa) é executada sem tempo limite. `0` (o padrão) ou qualquer valor não positivo significa que não há tempo limite. Revise o valor antes de atualizar se você sincroniza Caixas de correio grandes. Veja [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: O tempo limite de conexão para conexões IMAP em segundos.
- `MailSync__CommandTimeoutSeconds`: O tempo limite de comando para comandos IMAP em segundos.
- `MailSync__AlwaysForceFullSync`: Se deve sempre forçar uma sincronização completa (true/false).
- `MailSync__IgnoreSelfSignedCert`: Se deve ignorar certificados autoassinados (true/false).
- `MailSync__MaxConcurrentSyncs`: Quantas sincronizações de Conta podem ser executadas ao mesmo tempo. Padrão `1` (sequencial, retrocompatível). O agendador preenche as vagas conforme elas ficam livres em vez de esperar por um lote inteiro, portanto, uma Caixa de correio lenta não retém mais as outras. Aumente para sincronizar várias Contas simultaneamente — tenha em mente os limites de taxa do provedor e o uso de recursos locais. Veja [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Atraso de escalonamento opcional em segundos aplicado ao final de cada tarefa de sincronização de Conta. Padrão `0` (sem atraso). Útil para evitar inícios em rajada quando `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Quantos problemas de cada tipo um trabalho de sincronização lembra para a página da Conta. Pastas com falha, pastas ausentes e mensagens com falha são orçados separadamente, para que um fluxo de um tipo não desloque os outros da visualização; qualquer coisa além do orçamento é contada em vez de mantida. Padrão `20`. `0` desativa o detalhamento e deixa apenas os contadores. Veja [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Pastas excluídas da Sincronização para **todas** as Contas, além da própria lista de pastas excluídas de cada Conta. Vazio por padrão, portanto, as configurações existentes não são afetadas. As duas listas são **aditivas** — uma Pasta é ignorada quando corresponde a qualquer uma delas — e ambas usam as mesmas regras de correspondência: correspondência exata no caminho completo, correspondência exata no nome da Pasta, uma correspondência de sufixo de caminho (então `Drafts` também corresponde a `INBOX/Drafts` e `INBOX.Drafts`), e tudo abaixo da Pasta que uma entrada nomeia quando `MailSync__ExcludeSubfolders` está ativado. A correspondência não diferencia maiúsculas de minúsculas. Útil ao importar muitas Caixas de correio do mesmo servidor, onde a alternativa é manter uma lista de exclusão idêntica em cada Conta. Nenhuma Pasta é excluída por padrão; quais nomes valem a pena listar depende do servidor e de seu idioma. Exemplo para uma árvore de Caixa de correio que também contém pastas de calendário e contatos:
  ```yaml
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Calendars
      - MailSync__GlobalExcludedFolders__2=Contacts
      - MailSync__GlobalExcludedFolders__3=AddressBook
      - MailSync__GlobalExcludedFolders__4=Tasks
      - MailSync__GlobalExcludedFolders__5=TaskList
      - MailSync__GlobalExcludedFolders__6=Notes
      - MailSync__GlobalExcludedFolders__7=NoteList
      - MailSync__GlobalExcludedFolders__8=Journal
  ```
- `MailSync__ExcludeSubfolders`: Se uma entrada de exclusão também cobre as pastas abaixo daquela que ela nomeia. Aplica-se tanto à lista por Conta quanto à lista de toda a instalação, e a ambos os provedores. Padrão `false`, correspondendo ao comportamento antes da opção existir: uma entrada `Deleted Items` corresponde apenas àquela Pasta, e uma árvore é mantida fora do Arquivo nomeando sua raiz e cada Pasta abaixo dela. Defina como `true` para que uma entrada inclua a árvore abaixo dela também — uma entrada `Deleted Items` então também mantém `Deleted Items/2024` e o resto daquele ramo fora do Arquivo. A regra ancora no separador de caminho, portanto, ela pega Pastas inteiras e nunca parte de um nome: `Archive` cobre `Archive` e `Archive/Team`, e deixa uma pasta de e-mail chamada `Team Archive Review` de fora. Um `.` também conta como um separador, da mesma forma que a regra de sufixo de caminho o trata.

### 📧 Configurações de Armazenamento de Mensagens Originais {#original-message-storage-settings}

Por padrão, o Arquivo mantém apenas as partes analisadas de um e-mail, portanto, um `.eml` exportado é uma reconstrução: as codificações de transferência, os limites MIME e o dobramento de cabeçalho diferem da mensagem recebida, e as assinaturas DKIM não são mais verificadas. Com `RawMessages:Enabled`, o original completo também é armazenado.

| Parâmetro | Padrão | Propósito |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Armazena o original de cada e-mail recém-arquivado. |
| `RawMessages__Storage` | `postgres` | Onde os bytes residem: `postgres` ou `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Mensagens maiores são arquivadas sem o seu original. `0` = sem limite. |
| `RawMessages__IncludeGraph` | `true` | Também armazena originais para Contas Microsoft 365 (Graph). |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Diretório para os arquivos de mensagem. Deve apontar para um volume montado no Docker. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Níveis de diretório nos quais o id do e-mail é distribuído. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Verifica o SHA-256 em cada download. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Remove arquivos de mensagem aos quais nenhum e-mail se refere. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Com que frequência essa varredura é executada. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Arquivos mais novos que isso nunca são removidos. |

!!! warning "O backend do sistema de arquivos precisa de um volume montado"
    Com `RawMessages__Storage=filesystem`, os arquivos de mensagem residem fora do banco de dados.
    Sem um bind mount, eles ficam na camada de escrita do contêiner e são perdidos na
    próxima recriação do contêiner, enquanto as linhas do banco de dados sobrevivem. Faça o Backup do diretório
    junto com o dump do banco de dados.

Veja **[Original Message Storage](RawMessages.md)** para a descrição completa: onde o
original é servido, os backends de armazenamento, como mover mensagens existentes entre eles com
`--migrate-raw-storage`, e o que fazer o Backup.

### 📤 Configurações de BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: O número de e-mails que aciona o processamento assíncrono.
- `BatchRestore__MaxSyncEmails`: O número máximo de e-mails para processamento síncrono.
- `BatchRestore__MaxAsyncEmails`: O número máximo de e-mails para processamento assíncrono.
- `BatchRestore__SessionTimeoutMinutes`: O tempo limite da sessão para a restauração em lote em minutos.
- `BatchRestore__DefaultBatchSize`: O tamanho de lote padrão para operações de e-mail.

### 🏢 Configurações de Gerenciamento de Tenant {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Número máximo de caixas de correio que podem ser adicionadas em uma única operação de Gerenciamento de Tenant. O padrão é `1000`. Aumente este valor para tenants muito grandes ou diminua-o para evitar importações em massa acidentais. Quando o limite é excedido, a operação é rejeitada com um erro de validação e nenhuma conta é criada. Consulte o [M365 Tenant Import Guide](M365TenantImport.md) para detalhes.

### 🐄 Configurações do Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Número máximo de Caixas de correio que podem ser provisionadas em uma única execução de ativação do Mailcow. O padrão é `1000`. Diminua para evitar provisionamento em massa acidental, ou aumente para instâncias grandes. Quando o limite é excedido, nenhuma Conta é criada. Veja [Mailcow Instances](Mailcow.md) para detalhes.
- A integração com Mailcow também requer `Security__CredentialEncryptionKey` (veja [Security Settings](#-security-settings)). Conectar uma instância e ativar Caixas de correio são ambos recusados enquanto a criptografia de credenciais estiver desativada, portanto, a chave de API e as senhas de aplicativo geradas nunca podem ser armazenadas como texto simples.
- O acesso de saída é necessário para a API HTTPS do Mailcow e para o host IMAP via TLS (normalmente porta `993`).

### 📦 Configurações de BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: O tamanho do lote para operações de e-mail.
- `BatchOperation__PauseBetweenEmailsMs`: A pausa entre e-mails individuais em milissegundos.
- `BatchOperation__PauseBetweenBatchesMs`: A pausa entre lotes em milissegundos.

### 📤 Configurações de Offload {#offload-settings}
Configurações para o [date-windowed offload](Offload.md). Todos os padrões reproduzem o comportamento que o aplicativo tinha antes de o recurso existir, portanto, uma instalação que não configurar esta seção não será afetada.
- `Offload__MaxConcurrentJobs`: Quantos jobs de restauração ou offload podem ser executados ao mesmo tempo. O padrão é `1`, o que mantém o processamento estritamente serial que a fila de jobs sempre utilizou. No máximo um job por Caixa de correio de destino é executado, independentemente deste valor.
- `Offload__PrefetchMaxMessages`: Limite superior de quantas mensagens são indexadas de uma Caixa de correio de destino para detecção de duplicatas. O padrão é `500000`. Acima disso, a verificação é restringida a uma única Pasta e registra que o escopo foi reduzido.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Pastas de origem que nunca são enviadas via offload, correspondidas antes da renomeação e cobrindo subpastas. Vazio por padrão.
- `Offload__FolderRenameMap__<SourceFolder>`: Reescreve os segmentos iniciais de um caminho de Pasta de origem, por exemplo `Offload__FolderRenameMap__Sent Items=Sent`. Vazio por padrão.
- `Offload__MarkAsSeen`: Se o e-mail anexado é marcado como lido. O padrão é `true`, correspondendo ao comportamento de restauração existente.

Ambas as configurações de Pasta são enviadas vazias propositalmente: reescrever ou descartar Pastas sem ser solicitado surpreenderia qualquer pessoa que já esteja usando o caminho de restauração. Uma execução sem configuração, portanto, migra tudo, incluindo pastas de spam, e cria um segundo conjunto de pastas especiais ao lado das próprias do destino.

### 📊 Configurações de Rastreamento de Largura de Banda {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Ativa ou desativa o rastreamento de largura de banda para o tratamento de limite de taxa IMAP (true/false). O padrão é `false`. Quando ativado, o sistema rastreia o uso de largura de banda por Conta e pode pausar a Sincronização quando os limites do provedor são atingidos. Consulte [Rate Limit Handling](RateLimitHandling.md) para informações detalhadas.
- `BandwidthTracking__DailyLimitMb`: Limite diário de download em megabytes por Conta. O padrão é `25000` (25 GB). Para provedores com limites de largura de banda, defina isso para corresponder ao limite de taxa deles (ex: `2500` para provedores com limites diários de ~2500 MB). O sistema pausará a sincronização quando este limite for atingido.
- `BandwidthTracking__WarningThresholdPercent`: Porcentagem do limite diário na qual as mensagens de aviso são registradas. O padrão é `80`. Quando o uso de largura de banda atinge esta porcentagem, avisos são registrados para ajudar a monitorar a aproximação dos limites.
- `BandwidthTracking__PauseHoursOnLimit`: Número de horas para pausar a Sincronização quando o limite diário é atingido. O padrão é `24`. Após este período, a flag de limite é limpa automaticamente e a sincronização é retomada.
- `BandwidthTracking__TrackUploadBytes`: Se deve também rastrear a largura de banda de upload (true/false). O padrão é `false`. A maioria dos provedores IMAP limita apenas downloads, portanto, isso normalmente não é necessário.

### 🎯 Configurações de Seleção {#selection-settings}
- `Selection__MaxSelectableEmails`: O número máximo de e-mails que podem ser selecionados de uma só vez.

### 👁️ Configurações de Visualização (Privacidade e Exibição) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Controla o modo de visualização de e-mail padrão para privacidade e prevenção de rastreamento (true/false). O padrão é `false`.
  - Quando definido como `true`: Os e-mails abrem na visualização de texto simples por padrão, impedindo o carregamento automático de pixels de rastreamento, imagens externas e web beacons. Isso é recomendado para usuários preocupados com o rastreamento de e-mail e privacidade.
  - Quando definido como `false`: Os e-mails abrem na visualização HTML por padrão, exibindo o e-mail formatado com imagens e estilização.
  - Os usuários podem alternar entre as visualizações HTML e texto simples usando um botão na página de detalhes do e-mail (visível apenas quando ambos os formatos estão disponíveis).
  - O link "Full View" respeita o modo de visualização selecionado atualmente.

- `View__BlockExternalResources`: Bloqueia recursos externos (imagens remotas, CSS externo, scripts externos, fontes web, etc.) em visualizações de e-mail HTML para evitar rastreamento e melhorar a privacidade (true/false). O padrão é `false`.
  - Quando definido como `true`: Recursos externos são filtrados ao exibir e-mails HTML. Apenas conteúdo inline e URIs de dados (incluindo Anexos inline via referências `cid:`) são exibidos.
  - Quando definido como `false`: Os e-mails HTML são exibidos com todos os seus recursos externos originais.
  - **Importante**: Esta configuração afeta apenas a **exibição** do e-mail. E-mails arquivados são armazenados completamente inalterados no banco de dados, com todo o conteúdo original preservado.
  - Recursos bloqueados incluem:
    - Imagens remotas (pixels de rastreamento, imagens externas hospedadas em servidores)
    - Folhas de estilo CSS externas
    - Fontes externas via @font-face
    - Importações de CSS externas via @import
    - Imagens de fundo externas
  - Recursos permitidos:
    - Imagens inline incorporadas como data: URIs
    - Anexos inline referenciados via cid: URIs
    - Estilos CSS inline e tags de estilo
  - Esta configuração funciona independentemente de `DefaultToPlainText` e fornece uma camada adicional de proteção de privacidade ao visualizar e-mails HTML.

### 📊 Configurações do Dashboard {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Se os cartões de contagem exibem suas partes de entrada e saída, e se o cartão da Conta exibe o número de domínios distintos. O padrão é `true`. Defina como `false` e esses números não serão computados em vez de computados e ocultados: os contadores farão uma contagem simples, e a contagem de Anexo deixará de realizar o join com o e-mail ao qual está vinculado, que é a única consulta que essas partes adicionam.
- `Dashboard__SelectablePeriods`: Se os gráficos do dashboard oferecem uma resolução, um período e setas para movê-lo. O padrão é `true`. Defina como `false` e os gráficos mostrarão os últimos doze meses por mês e os remetentes de todo o Arquivo, o endpoint do gráfico responderá como se não existisse, e a data de envio mais antiga não será consultada.
- `Dashboard__CacheSeconds`: Por quanto tempo as estatísticas computadas do dashboard (totais com suas divisões de entrada e saída, contagens por Conta, a série do gráfico, principais remetentes, e-mails recentes, tamanho do banco de dados) são mantidas no cache em memória do servidor. O padrão é `60` segundos. Defina como `0` para desativar o cache e sempre recomputar as estatísticas. Cada seleção de resolução e período do gráfico é armazenada em cache individualmente, portanto, um dashboard que alterna entre seleções mantém uma entrada por seleção. Valores mais altos reduzem a carga do banco de dados em ambientes grandes ao custo de números mais defasados. Os selos de status de Sincronização, valores de armazenamento e a lista de e-mails recentes são sempre buscados ao vivo e não são afetados por este cache.
- `Dashboard__RefreshIntervalMinutes`: Com que frequência o serviço de segundo plano `DashboardStatsRefreshService` recomputa os agregados caros do dashboard (contagens totais com suas divisões de entrada e saída, contagens por Conta, o número de domínios da Conta, a série de gráfico padrão com seus principais remetentes, tamanho do banco de dados) na tabela de banco de dados `DashboardStatsCache`. Em instalações com vários milhões de e-mails, o dashboard lê esses valores preparados em vez de agregar todo o Arquivo no caminho da requisição, o que transforma um primeiro carregamento de ~10 segundos em milissegundos. O endpoint do gráfico responde à seleção padrão (meses ao longo de um ano, remetentes de entrada, o período atual) a partir da mesma linha, portanto, retornar a ela mostrará os números do primeiro carregamento; todas as outras seleções são computadas sob demanda e mantidas no cache em memória mencionado acima. O padrão é `15` minutos. Defina como `0` para desativar a atualização em segundo plano; o dashboard então computará as estatísticas ao vivo novamente (com o cache em memória acima ainda sendo aplicado). Os valores exibidos podem ter um atraso de até o intervalo configurado em relação ao estado real do Arquivo. Uma linha computada com valores `ShowDirectionSplits` ou `SelectablePeriods` diferentes dos atuais não é utilizada; o dashboard computa ao vivo até a próxima atualização.

### 🔄 Configurações de Jobs {#jobs-settings}
- `Jobs__RefreshSeconds`: Com que frequência a página de jobs em segundo plano recarrega a si mesma enquanto uma aba do navegador a mantém aberta. O padrão é `30` segundos. Defina como `0` para desativar o recarregamento automático e atualizar manualmente. Cada recarregamento reconstrói a página a partir de todas as oito fontes de job, portanto, em instalações com muitas Contas, um intervalo maior mantém a carga baixa, multiplicado por cada aba que estiver aberta na página.

### 🗃️ Configurações do Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: O tempo limite para comandos de banco de dados em segundos.

### 📥 Configurações de Upload {#upload-settings}
- `Upload__MaxFileSizeGB`: O tamanho máximo do arquivo para uploads em GB.
- `Upload__KeepAliveTimeoutHours`: O tempo de espera de keep alive para uploads em horas.
- `Upload__RequestHeadersTimeoutHours`: O tempo de espera para cabeçalhos de requisição em horas.

### 📂 Configurações de Importação Local {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, etc.: Lista de permissões de diretórios locais dos quais os comandos de importação da CLI (`--import-mbox`, `--import-eml`) têm permissão para ler arquivos. Cada entrada é um caminho dentro do contêiner. Você deve montar seus arquivos de importação em um desses diretórios usando volumes Docker. Padrão: `/data/import` (definido automaticamente como `/app/uploads` como fallback).
  - Ao usar `docker exec` para executar o comando de importação, o caminho do arquivo fornecido via `--file` deve estar dentro de um desses caminhos permitidos.
  - Esta medida de segurança evita o acesso arbitrário ao sistema de arquivos a partir de comandos CLI.
  - Múltiplos caminhos podem ser configurados para diferentes fontes de importação.
  - Consulte [CLI commands](CLI.md) para instruções detalhadas de uso.

### 📄 Configurações de Importação de CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Número máximo de linhas de CSV (Caixas de correio) processadas em uma única importação em massa. O padrão é `5000`. Aumente este valor para implantações de grande escala; diminua-o para limitar o impacto de uma única execução de importação na carga do banco de dados.
- `CsvImport__MaxFileSizeBytes`: Tamanho máximo permitido (em bytes) do arquivo CSV enviado. O padrão é `10000000` (10 MB). Ajuste este valor para corresponder aos seus limites de upload, se necessário.
- Consulte o [Account Import Guide](Account-Import.md) para instruções detalhadas de uso sobre a importação em massa de Conta IMAP via CSV.

### 📤 Configurações de Exportação de Auditoria {#audit-export-settings}
Configurações para a página de exportação de dados de auditoria (apenas administradores, acessível a partir da página de Logs). A exportação gera um pacote ZIP com dados massivos tabulares (INDEX.XML + tabelas CSV + DTD) do Arquivo existente para ferramentas de auditoria externas. Consulte o [Audit Data Export Guide](AuditExport.md) para detalhes de uso.
- `AuditExport__DataSupplierName`: Valor padrão para o campo de formulário "Data supplier name" que identifica sua organização no arquivo de índice exportado. Padrão: vazio.
- `AuditExport__DataSupplierLocation`: Valor padrão para o campo de formulário "Data supplier location" (ex: sede da empresa). Padrão: vazio.
- `AuditExport__Comment`: Valor padrão para o campo de formulário "Comment", incluído como texto livre no arquivo de índice exportado. Padrão: vazio.
- `AuditExport__OutputDirectory`: Diretório onde os arquivos ZIP gerados são armazenados. Caminhos relativos são resolvidos em relação à raiz de conteúdo do app. Padrão: `exports/audit`.
- `AuditExport__RetentionDays`: Número de dias após os quais os arquivos de exportação concluídos são excluídos pela limpeza diária. Padrão: `30`.
- `AuditExport__MaxRangeYears`: Intervalo máximo permitido entre a data de início e a data de término do período de exportação. Padrão: `10`.
- Os campos do formulário são preenchidos automaticamente com esses valores padrão, mas podem ser editados por exportação.
- Cada exportação escreve uma entrada de início e uma entrada de resultado no log de acesso (tipo "Audit Data Export"), portanto, o histórico é seguro contra revisões sem qualquer alteração no esquema do DB.

### 🔒 Configurações de Política de Exclusão {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Controla se a exclusão manual de e-mails arquivados é permitida (true/false). O padrão é `true`. Quando definido como `false`:
  - Todos os e-mails arquivados são bloqueados (`IsLocked = true`) na inicialização por meio do gatilho de conformidade do banco de dados, impedindo qualquer modificação ou exclusão no nível do banco de dados.
  - A exclusão manual (individual e em massa) é bloqueada no nível da aplicação com uma mensagem de erro.
  - O padrão da coluna é ajustado para que e-mails recém-importados também sejam bloqueados.
  - O estado atual da política é registrado na tabela AccessLogs em cada inicialização (visível na página Logs como entradas "Deletion Policy") para fins de auditabilidade.
  - A exclusão por Retenção local é isenta: e-mails que se enquadram em um período de Retenção configurado ainda são excluídos (eles são desbloqueados imediatamente antes da exclusão dentro do processo de Retenção).
- `DeletionPolicy__MaxBulkDeleteEmails`: Limite superior para uma exclusão em massa baseada em filtro (*Delete all matches*). O padrão é `100000`. Um filtro que corresponda a mais e-mails é rejeitado com uma mensagem solicitando que ele seja restringido; o trabalho nunca trunca silenciosamente. E-mails bloqueados pela política de Retenção são ignorados e relatados no status do trabalho, não são desbloqueados. O trabalho verifica novamente este limite e `DeletionPolicy__DeletionAllowed` quando inicia, portanto, um trabalho enfileirado antes de uma alteração de política é interrompido em vez de ser executado com a configuração antiga.
- **Proteção de Imutabilidade:** Quando `IsLocked = true`, o gatilho de conformidade do banco de dados (`prevent_locked_email_changes`) bloqueia QUALQUER modificação na linha de e-mail — todas as colunas são protegidas, não apenas uma lista fixa de campos. As únicas colunas isentas são a própria `IsLocked` (para que o desbloqueio para exclusão por Retenção e a aplicação da política de inicialização permaneçam possíveis) e `FolderName` (para que a Sincronização IMAP possa atualizar a Pasta quando um e-mail é movido no lado do servidor). A proteção é agnóstica à coluna (comparação baseada em JSONB) e cobre automaticamente futuras adições ao esquema.

### 🕐 Configurações de TimeZone {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: O fuso horário usado para exibir os registros de data e hora de e-mails na UI. Utiliza identificadores de fuso horário IANA (ex: "Europe/Berlin", "Asia/Tokyo"). O padrão é "Etc/UCT" para compatibilidade com versões anteriores. Ao importar e-mails, os registros de data e hora serão convertidos para este fuso horário para fins de exibição.

### 🎉 Configurações ReleaseNotes (Tela de Splash de Atualização de Versão) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: A string de versão que esta instância reporta. Ela é comparada com as tags de release do GitHub e decide se existe uma versão mais recente. Quando vazia, o changelog utiliza a versão do assembly compilado como fallback e o aviso de atualização permanece desligado.
- `ReleaseNotes__Enabled`: Ativa ou desativa a tela de splash de atualização de versão (true/false). O padrão é `true`. Quando ativado, os administradores verão um modal de changelog único após uma atualização do aplicativo, mostrando as notas de lançamento buscadas do GitHub Releases para a versão atual. Cada administrador pode fechar o modal, e ele só reaparecerá para uma nova versão. Defina como `false` para desativar completamente este recurso, incluindo o aviso de atualização.
- `ReleaseNotes__UpdateHintEnabled`: Mostra aos administradores um pequeno toast no canto inferior direito quando o GitHub publica uma versão estável mais recente do que a versão instalada (true/false). O padrão é `true`. O toast contém um link para essa release e é descartado por versão no navegador. Rascunhos e prereleases são ignorados. Requer `ReleaseNotes__AppVersion`; sem isso, nenhum aviso é exibido.
- `ReleaseNotes__UpdateCheckIntervalHours`: Com que frequência a lista de releases do GitHub é atualizada, em horas. O padrão é `6`. Um intervalo mais curto torna uma nova release visível mais rapidamente em uma instância de longa execução; um intervalo mais longo reduz as chamadas de API do GitHub. O mínimo é `1`.

### 🔧 Configurações de Manutenção de Banco de Dados {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Ativa ou desativa a manutenção diária automática do banco de dados (true/false). O padrão é `false`. Quando ativado, o sistema executará automaticamente operações VACUUM ANALYZE para otimizar o desempenho do banco de dados e evitar o inchaço (bloat). Consulte o [Database Maintenance Guide](DatabaseMaintenance.md) para mais detalhes.
- `DatabaseMaintenance__DailyExecutionTime`: O horário do dia em que a manutenção do banco de dados deve ser executada, no formato de 24 horas (HH:mm). O padrão é `02:00`. Escolha um horário durante o período de baixa atividade do sistema.
- `DatabaseMaintenance__TimeoutMinutes`: Tempo máximo permitido para operações de manutenção em minutos. O padrão é `30`. Aumente este valor para bancos de dados maiores.

### 🧬 Configurações de Deduplicação de Anexo {#attachment-deduplication-settings}
A Deduplicação de Anexo armazena cada payload de Anexo único apenas uma vez (endereçado por conteúdo via SHA-256) e é um **recurso principal que está sempre habilitado** – não há intencionalmente um interruptor de liga/desliga. Apenas os parâmetros de lote/agendamento abaixo podem ser ajustados. Consulte o [Attachment Deduplication Guide](AttachmentDeduplication.md) para detalhes completos.

- `AttachmentDeduplication__BatchSize`: Número de anexos existentes migrados por transação durante a migração em segundo plano de dados pré-existentes realizada uma única vez. O padrão é `200`. Valores maiores migram mais rápido, mas utilizam mais memória/carga de DB por lote.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Pausa opcional (em milissegundos) entre lotes de migração para controlar a carga do banco de dados em sistemas ocupados. O padrão é `0` (sem pausa).
- `AttachmentDeduplication__StartupDelaySeconds`: Atraso (em segundos) após o início da aplicação antes que a migração em segundo plano comece, dando tempo para que a migração de esquema seja concluída. O padrão é `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Intervalo (em horas) da coleta de lixo (garbage collection) que está sempre ativa e remove payloads de anexos que não são mais referenciados por nenhum e-mail. O padrão é `12`. Isso é executado independentemente de `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Tempo limite (timeout) do comando de banco de dados (em segundos) para as operações de lote de migração (INSERT com hashing SHA-256 e UPDATE). O padrão é `300` (5 minutos). Aumente este valor para bancos de dados ou anexos muito grandes, ou diminua-o se desejar uma detecção de falha mais rápida. Se um lote ainda sofrer timeout, o serviço tenta novamente automaticamente com metade do tamanho do lote.

### 💾 Configurações de Armazenamento de Conta {#account-storage-settings}
A exibição de armazenamento por Conta mostra o uso de armazenamento do banco de dados (todos os campos de e-mail + Anexos) para cada Conta na tabela "Account Overview" do Dashboard e na tabela "Show All" do MailAccounts. Um serviço de segundo plano autárquico (`AccountStorageRefreshService`) calcula os valores somando os PostgreSQL `pg_column_size` por coluna (cobrindo todos os campos de um e-mail sem ler os corpos dos e-mails) e os armazena em cache na tabela `AccountStorageCache`. Este serviço é executado independentemente de `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Ativa ou desativa o serviço de atualização de armazenamento (true/false). O padrão é `true`. Quando ativado, o serviço realiza um backfill retomável de Contas pendentes na inicialização (seguro contra falhas via `AccountStorageBackfillState`) e uma atualização completa diária posteriormente. Quando desativado, os valores de armazenamento são atualizados apenas quando os e-mails são sincronizados, importados ou excluídos, mas os valores exibidos podem se tornar obsoletos com o tempo.
- `AccountStorage__DailyExecutionTime`: Hora do dia (formato de 24 horas `HH:mm`) para a atualização completa diária de todas as Contas. O padrão é `02:30`. Escolha um horário durante períodos de baixa atividade do sistema.
- `AccountStorage__BackfillDelayMs`: Atraso (em milissegundos) entre Contas durante o backfill inicial na inicialização. O padrão é `5000`. Valores mais baixos aceleram o backfill, mas aumentam a carga do banco de dados; aumente este valor para Arquivos muito grandes para evitar sobrecarregar o banco de dados.
- `AccountStorage__RefreshBatchDelayMs`: Atraso (em milissegundos) entre Contas durante a atualização completa diária. O padrão é `1000`. Valores mais baixos aceleram a atualização, mas aumentam a carga do banco de dados; aumente este valor para Arquivos muito grandes.
- `AccountStorage__CommandTimeoutSeconds`: Tempo limite do comando do banco de dados (em segundos) para o cálculo de armazenamento por Conta (`pg_column_size` em todas as linhas de uma Conta). O padrão é `300` (5 minutos). O cálculo lê cada linha de e-mail, incluindo dados TOAST, o que pode levar bem mais de um minuto em Arquivos muito grandes (mais de 250k e-mails); o padrão anterior de 30 segundos do Npgsql fazia com que a atualização falhasse silenciosamente e deixava o dashboard mostrando valores obsoletos. Aumente este valor se o cálculo ainda apresentar timeout no seu Arquivo.

> 💡 **Nota**: Os valores de armazenamento são atualizados imediatamente após cada sincronização, importação ou exclusão de Retenção de e-mail, portanto, os valores exibidos permanecem atuais mesmo sem a atualização diária. A atualização diária é uma rede de segurança que captura casos excepcionais (ex: alterações diretas no banco de dados).

### 📝 Configurações de Logging {#logging-settings}
- `Logging__LogLevel__Default`: O nível de log padrão para o aplicativo. Os níveis disponíveis são: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. O padrão é `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Nível de log para mensagens do framework ASP.NET Core. O padrão é `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Nível de log para comandos de banco de dados do Entity Framework. O padrão é `Warning`.

### 🛡️ Configurações de Segurança {#security-settings}
- `AllowedHosts`: Uma lista separada por ponto e vírgula de nomes de host que a aplicação tem permissão para servir. Isso ajuda a prevenir ataques de HTTP Host header. Exemplo: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Importante**: Não use `*` em ambientes de produção, pois isso desativa a validação do host header.

#### 🔐 Criptografia de Credenciais em Repouso {#credential-encryption-at-rest}
As credenciais da Conta de e-mail (senha IMAP, segredo de cliente M365 e tokens OAuth da MSA) podem ser criptografadas no banco de dados. A chave de criptografia é mantida **fora** do banco de dados (variável de ambiente / Docker secret), portanto, um dump do banco de dados sozinho não é suficiente para ler as credenciais.

- `Security__CredentialEncryptionKey`: Chave de criptografia de 256 bits (32 bytes) codificada em Base64. Gere uma com:
  ```bash
  openssl rand -base64 32
  ```
  Defina o valor como uma variável de ambiente (recomendado: `Security__CredentialEncryptionKey` no seu arquivo compose ou ambiente do container). A aplicação não suporta Docker secrets no estilo `_FILE` — se você usar um Docker secret, monte-o e carregue seu conteúdo na variável de ambiente no seu entrypoint. Nunca envie a chave para o `appsettings.json`. Quando uma chave é configurada, as credenciais são criptografadas na gravação, e um serviço em segundo plano criptografa as credenciais existentes em texto simples uma única vez.
- `Security__EncryptAccountCredentials`: Ativa ou desativa a criptografia de novos/atualizados valores de credenciais (true/false). O padrão é `true`. Quando nenhuma chave é configurada, as credenciais são armazenadas como texto simples e um aviso é registrado na inicialização — instalações existentes não são afetadas até que uma chave seja fornecida.
- `Security__CredentialEncryptionBackfill`: Se o serviço em segundo plano deve criptografar as credenciais existentes em texto simples na inicialização (true/false). O padrão é `true`. Sem efeito sem uma chave configurada.
- `Security__CredentialEncryptionKeyPrevious`: Chave anterior opcional, usada apenas para descriptografar valores criptografados com ela (para rotação de chave). Novos valores são sempre criptografados com `Security__CredentialEncryptionKey`.

  > ⚠️ **Rotação de chave**: após a rotação, mantenha `Security__CredentialEncryptionKeyPrevious` configurado **até que cada credencial tenha sido reescrita** (por exemplo, editando a Conta ou por uma atualização automática de token OAuth) — valores existentes criptografados com a chave antiga *não* são re-criptografados automaticamente com a nova chave. Assim que você remover a chave anterior, qualquer credencial ainda criptografada com ela torna-se ilegível e deve ser inserida novamente. Enquanto estiver configurada, ela deve ser mantida tão segura quanto a chave atual.

> ⚠️ **Mantenha a chave segura**: Sem a chave, as credenciais criptografadas não podem ser recuperadas e devem ser inseridas novamente. Faça o Backup dela junto com (mas separadamente de) seus backups de banco de dados.
>
> 💡 **Instalações existentes**: Atualize com segurança — desde que nenhum `Security__CredentialEncryptionKey` esteja definido, nada muda. Defina a chave quando estiver pronto; as credenciais existentes em texto simples permanecem legíveis e são criptografadas em segundo plano.

### 🔐 Configuração OIDC {#oidc-configuration}

Para instruções detalhadas de configuração para a autenticação OpenID Connect, consulte o [OIDC Implementation Guide](OIDC_Implementation.md).

#### Configurações Básicas de OIDC {#basic-oidc-settings}
- `OAuth__Enabled`: Ativa ou desativa a autenticação OIDC (true/false)
- `OAuth__Authority`: A URL de autoridade do OpenID Connect (ex: https://sts.windows.net/{TENANT-ID}/ para Azure AD)
- `OAuth__ClientId`: O client ID atribuído pelo seu provedor de identidade
- `OAuth__ClientSecret`: O client secret atribuído pelo seu provedor de identidade
- `OAuth__DisplayName`: Nome de exibição opcional mostrado no botão de login OIDC e na página de redirecionamento automático (ex: `PocketID SSO`). Se omitido, o rótulo genérico "Login with OAuth" é usado.
- `OAuth__ClientScopes__0`: Primeiro escopo solicitado ao provedor de identidade (openid)
- `OAuth__ClientScopes__1`: Segundo escopo solicitado ao provedor de identidade (profile)
- `OAuth__ClientScopes__2`: Terceiro escopo solicitado ao provedor de identidade (email)
- `OAuth__ResponseMode`: Modo de resposta opcional da solicitação de autorização (`query`, `fragment` ou `form_post`). O padrão é `form_post`, o padrão do ASP.NET Core. Defina como `query` para provedores que não suportam `form_post`, como o Synology SSO Server. Consulte [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Aprova automaticamente novos usuários OIDC sem exigir aprovação manual do administrador (true/false). O padrão é `false`.

#### Configurações de Provisionamento de Usuário {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Aprova automaticamente novos usuários OIDC sem exigir aprovação manual do administrador (true/false). O padrão é `false`. Quando habilitado, os usuários que se autenticam via provedor OIDC são imediatamente ativados e podem acessar o aplicativo. Quando desabilitado (padrão), novos usuários OIDC são criados como inativos e requerem ativação manual por um administrador. Consulte [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) para informações detalhadas.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, etc.: Endereços de e-mail que devem ser provisionados automaticamente como administradores. Usuários com esses endereços de e-mail serão criados como administradores ativos no primeiro login OAuth, ignorando o processo normal de aprovação. A correspondência de e-mail não diferencia maiúsculas de minúsculas.

#### Configurações de Login sem Senha {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Desativa o login de nome de usuário/senha (true/false). O padrão é `false`. Quando habilitado, o formulário de senha é ocultado **e** o servidor rejeita cada POST de senha, portanto, apenas o login OAuth está disponível. Requer `OAuth__Enabled=true`. Se o OAuth estiver desabilitado, esta configuração não terá efeito.
- `OAuth__AutoRedirect`: Redireciona automaticamente os usuários para o provedor OAuth (true/false). O padrão é `false`. Requer que `OAuth__DisablePasswordLogin` seja `true`. Os usuários verão uma breve tela de carregamento antes de serem redirecionados.

#### Exemplo: Configuração Completa OIDC-First {#example-full-oidc-first-configuration}
```yaml
environment:
  - OAuth__Enabled=true
  - OAuth__Authority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - OAuth__ClientId=your-client-id
  - OAuth__ClientSecret=your-client-secret
  - OAuth__DisplayName=PocketID SSO
  - OAuth__ClientScopes__0=openid
  - OAuth__ClientScopes__1=profile
  - OAuth__ClientScopes__2=email
  - OAuth__DisablePasswordLogin=true
  - OAuth__AutoRedirect=true
  - OAuth__AutoApproveUsers=true
  - OAuth__AdminEmails__0=admin@example.com
  - OAuth__AdminEmails__1=manager@example.com
```

## 🔐 Configuração HTTPS do Kestrel (Opcional) {#kestrel-https-configuration-optional}

Embora o aplicativo deva ser acessado por meio de um reverse proxy com HTTPS, você também pode configurar o servidor web Kestrel para usar certificados SSL/TLS. Isso fornece criptografia de ponta a ponta entre o reverse proxy e o container da aplicação.

### Etapas de Configuração {#configuration-steps}

1. **Gerar ou obter um certificado SSL** no formato PFX (ex: `localhost.pfx`)

2. **Adicionar as seguintes variáveis de ambiente** ao seu `docker-compose.yml` para o serviço `mailarchive-app`:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Atualizar o mapeamento de portas** no serviço `mailarchive-app`:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Adicionar um mapeamento de volume** para o certificado:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Colocar seu arquivo de certificado** (ex: `localhost.pfx`) no diretório `./certs` em seu sistema host.

### Explicações das Variáveis de Ambiente {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL do endpoint HTTP (padrão: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL do endpoint HTTPS (padrão: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Caminho para o arquivo de certificado PFX dentro do container
- `Kestrel__Endpoints__Https__Certificate__Password`: Senha para o arquivo de certificado PFX

> 💡 **Nota**: Esta configuração é opcional. Se você estiver usando um reverse proxy com HTTPS (recomendado), a comunicação entre o reverse proxy e a aplicação pode permanecer HTTP. No entanto, para máxima segurança em ambientes sensíveis, você pode querer habilitar o HTTPS no Kestrel também para criptografar todo o caminho de comunicação.

## 🔑 Gerenciamento de Segredos para Produção {#secrets-management-for-production}

Codificar dados sensíveis como senhas de banco de dados, credenciais de administrador e segredos de cliente OAuth diretamente em `docker-compose.yml` é um risco de segurança, especialmente quando o arquivo é enviado para o controle de versão. A abordagem recomendada é externalizar esses valores em um arquivo `.env` que o Docker Compose carrega automaticamente.

### 📄 O Arquivo `.env` {#the-env-file}

Crie um arquivo `.env` no mesmo diretório que o seu `docker-compose.yml` com o seguinte conteúdo:

```env
# Database
POSTGRES_PASSWORD=YourSecureDBPassword123!

# Admin Account
AUTH_USERNAME=admin
AUTH_PASSWORD=YourSecureAdminPassword456!

# OIDC / OAuth Secrets
OAUTH_CLIENT_SECRET=YourOAuthClientSecret

# Credential Encryption at Rest (generate with: openssl rand -base64 32)
CREDENTIAL_ENCRYPTION_KEY=YourBase64Encoded32ByteKey

# Kestrel HTTPS (optional)
KESTREL_CERT_PASSWORD=YourCertPassword
```

**Importante**: O Docker Compose lê automaticamente o arquivo `.env` do mesmo diretório — você **não** precisa referenciá-lo manualmente. Todas as variáveis definidas em `.env` estão disponíveis em `docker-compose.yml` via a sintaxe `${VARIABLE_NAME}`.

### 🐳 `docker-compose.yml` Adaptado {#adapted-docker-composeyml}

O `docker-compose.yml` dos passos de instalação deve ser adaptado para usar placeholders em vez de segredos codificados:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=${POSTGRES_PASSWORD}

      # Authentication Settings
      - Authentication__Username=${AUTH_USERNAME}
      - Authentication__Password=${AUTH_PASSWORD}
      # ... other settings remain unchanged ...

      # OIDC Configuration
      - OAuth__ClientSecret=${OAUTH_CLIENT_SECRET}

      # Credential Encryption at Rest
      - Security__CredentialEncryptionKey=${CREDENTIAL_ENCRYPTION_KEY}
    # ... ports, volumes, networks ...

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    # ... volumes, networks ...
```

### 📋 Template de `.env.example` para Administradores {#envexample-template-for-administrators}

Crie um arquivo `.env.example` em seu repositório que sirva como um template para outros administradores. Ele contém valores de placeholder e documentação, mas **nenhum segredo real**:

```env
# ============================================
# Mail Archiver - Environment Configuration
# ============================================
# Copy this file to .env and fill in your values.
# Never commit the actual .env file to version control!

# --- PostgreSQL ---
POSTGRES_PASSWORD=change_me_db_password

# --- Admin Account ---
AUTH_USERNAME=admin
AUTH_PASSWORD=change_me_admin_password

# --- OIDC / OAuth ---
OAUTH_CLIENT_SECRET=change_me_client_secret

# --- Credential Encryption at Rest ---
# Generate with: openssl rand -base64 32
CREDENTIAL_ENCRYPTION_KEY=change_me_base64_32_byte_key

#...and so on
```

### ✅ Melhores Práticas {#best-practices}

- **Use `.env.example` como um template**
- **Defina Permissões de arquivo estritas** — Restrinja o acesso ao arquivo `.env`:
  ```bash
  chmod 600 .env
  ```
- **Use senhas fortes** — Gere senhas longas e aleatórias (pelo menos 16 caracteres) com uma mistura de letras, números e caracteres especiais.
- **Limite o acesso ao arquivo `.env`** — Apenas o usuário que executa o Docker Compose deve ter permissões de leitura para o arquivo `.env`.

## 🔒 Notas de Segurança {#security-notes}

- Use senhas fortes e altere as credenciais padrão. As senhas devem ter pelo menos 12 caracteres e incluir uma mistura de letras maiúsculas, letras minúsculas, números e caracteres especiais. Evite usar palavras comuns ou informações facilmente adivinháveis.
- Considere implementar HTTPS com um reverse proxy em produção
- Backups regulares do banco de dados PostgreSQL são recomendados. Para procedimentos detalhados de backup e restauração, consulte o [Backup and Restore Guide](BackupRestore.md).
