# 🧪 Ambiente de Teste Local {#local-test-environment}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia descreve como executar a suíte de testes, a aplicação e um alvo IMAP em uma máquina de desenvolvimento. Ele é destinado a contribuidores: a pilha que ele levanta é deliberadamente insegura e não é um guia de implantação. Para implantação, consulte o [Guia de Configuração](Setup.md).

Três partes estão envolvidas:

| Parte | Fornecido por | Propósito |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Dois bancos de dados, um para `dotnet test` e um para a aplicação |
| Dovecot | `docker-compose.test.yml` | Um alvo IMAP representando uma Caixa de correio do mailcow |
| A aplicação | `dotnet run` no host | Mais rápido para iterar do que reconstruir a imagem do container |

## 🚀 Iniciando a stack {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

Isso publica tudo apenas no loopback:

| Serviço | Endereço do Host | Notas |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, não 5432, para não conflitar com um servidor local |
| Dovecot, plain | `127.0.0.1:1143` | Autenticação em texto simples está habilitada |
| Dovecot, TLS | `127.0.0.1:1993` | O certificado autoassinado da imagem |

Para pará-lo e descartar todos os bancos de dados e e-mails armazenados:

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ Os dois bancos de dados {#the-two-databases}

A suíte de testes deixa linhas de fixture para trás: após uma execução completa, o banco de dados de teste contém várias dezenas de contas de e-mail e usuários. Se a aplicação compartilhasse esse banco de dados, sua sincronização em segundo plano detectaria essas fixtures e tentaria repetidamente alcançar servidores que não existem. Portanto, o container postgres cria dois bancos de dados na primeira inicialização
(`tests/postgres/10-create-dev-db.sql`):

| Database | Used by | Configured in |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Ambos os arquivos de configuração estão no gitignore e devem ser criados localmente.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` na raiz do repositório:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` importa fora do container: ele assume por padrão o caminho absoluto
`/app/DataProtection-Keys`, que não existe em uma máquina de desenvolvimento, e sem ele cada
requisição falha ao ler o key ring. `IgnoreSelfSignedCert` é o que permite que a aplicação
aceite o certificado Dovecot na porta 1993.

## ✅ Executando os testes {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

As Migrations são aplicadas automaticamente pelo `TestDbFixture` na primeira execução. A maioria dos testes não precisa de nenhum banco de dados; o restante utiliza PostgreSQL, portanto, eles falham com `Name or service not known` se a stack não estiver em execução.

Uma string de conexão também pode ser fornecida através do ambiente, o que substitui cada arquivo de configuração:

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Executando a aplicação {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` já define `ASPNETCORE_ENVIRONMENT=Development` para
`dotnet run`, então um `dotnet run --project MailArchiver.csproj` simples também funciona e serve
http://localhost:5179 (o perfil `http`). O `ASPNETCORE_URLS` acima apenas seleciona a porta do container
5000 em vez disso.

Migrações pendentes são aplicadas ao `MailArchiverDev` na inicialização. Faça login com as credenciais de `appsettings.json`
(`Authentication:Username` e `Authentication:Password`).

## 📬 Usando o alvo Dovecot {#using-the-dovecot-target}

O Dovecot aceita **qualquer nome de usuário** com a senha `pass`, portanto, uma nova Caixa de correio de alvo não precisa de provisionamento: escolha um nome e faça o login. Adicione-a em Mail Accounts com:

| Field | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | anything, e.g. `offload-target@example.com` | same |
| Password | `pass` | same |

A configuração em `tests/dovecot/dovecot.conf` é modelada no padrão da própria imagem, com três diferenças deliberadas que a fazem se comportar como um alvo mailcow:

- **Armazenamento Maildir** em vez do sdbox da imagem, que é o que o mailcow utiliza.
- **`separator = /`**, correspondendo ao mailcow. Os caminhos de Pasta são divididos em `/` e `\` quando uma Restauração recria uma hierarquia de pastas, portanto, um servidor usando `.` se comportaria de forma diferente.
- **As pastas special-use `Drafts`, `Junk`, `Sent` e `Trash` são pré-criadas**, assim como o mailcow as pré-cria. Isso é importante ao testar uma migração do Exchange: uma pasta de origem chamada `Sent Items` cai ao lado da `Sent` existente em vez de se mesclar com ela, que é a situação que a renomeação de pastas deve lidar.

A autenticação em texto simples está habilitada na porta 1143 porque o aplicativo se conecta sem qualquer segurança de transporte quando uma Conta tem o SSL desabilitado.

## 🌱 Semeando um Arquivo para Testes {#seeding-an-archive-to-test-against}

Um Arquivo vazio não é de muita utilidade para testar uma Restauração ou uma migração, então
`tests/seed/seed.sh` constrói um:

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

Ele cria duas Contas, importa os fixtures e imprime o que foi processado. Ambas as Contas são
criadas com um intervalo de Sincronização por Conta de um ano, e a origem é adicionalmente desativada,
para que o serviço de Sincronização em segundo plano a ignore: seu servidor não existe, e o alvo
Dovecot só precisa receber novos itens. Uma ressalva: o estado de agendamento reside na memória, portanto, cada
reinicialização do aplicativo agenda o alvo uma vez, imediatamente. O alvo, portanto, também
exclui as Pastas de uso especial do Dovecot (`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`), para que
uma Sincronização de reinicialização não tenha nada para arquivar. Executar o script duas vezes é inofensivo;
a segunda execução reporta cada mensagem como já presente.

Os fixtures estão armazenados em `tests/seed/fixtures`, um mbox por Pasta de origem, com
`manifest.tsv` mapeando cada arquivo para sua Pasta de destino. Eles são importados através do
próprio CLI `--import-mbox` da aplicação, e esse detalhe é deliberado: o CLI roteia através de
`MailImporter.ImportEmailToDatabase`, o mesmo importador que a Sincronização IMAP utiliza, para que as linhas cheguem
exatamente no formato que um Arquivo real possui -- endereços unidos com `", "`, assuntos passados por
`CleanText`, `SentDate` convertidos para o fuso horário de exibição, `RawHeaders` extraído e truncado.
Inserir linhas diretamente no banco de dados pularia tudo isso e produziria fixtures que
silenciosamente diferem dos dados de produção.

Cada mensagem de fixture carrega um cabeçalho `X-Fixture-Purpose` dizendo por que ela existe, para que os arquivos
se documentem:

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

As datas são fixas em vez de relativas ao dia atual. Um limite de migração é resolvido para uma
data absoluta antes de um job ser executado, portanto, um conjunto de fixtures fixo e um limite fixo testam a mesma coisa
de forma mais previsível do que um limite móvel. As mensagens abrangem de 2012 a 2026.

O layout de Pastas espelha uma Caixa de correio do Exchange, e cada Pasta existe para exercitar algo:

| Pasta de origem | Por que está lá |
|---|---|
| `INBOX` | datas abrangendo quinze anos, além das mensagens de caso de borda abaixo |
| `Sent Items` | colide com o `Sent` de uso especial existente no alvo Dovecot |
| `Sent Items/2019` | uma renomeação deve reescrever apenas o segmento de caminho inicial |
| `Sent Items Archive` | compartilha um prefixo como uma substring, mas não em um limite de segmento, portanto não deve ser reescrita |
| `Deleted Items` | exclusão de Pasta |
| `Deleted Items/Old` | uma exclusão em um pai deve cobrir os descendentes |
| `Junk E-Mail` | o nome do Exchange para uma Pasta que o Dovecot chama de `Junk` |
| `Archive/Projects/2020` | três níveis de aninhamento |

As mensagens individuais cobrem uma mensagem sem cabeçalho `Message-ID` (que recebe o fallback
determinístico), uma cujo `Message-ID` não possui `@` (que não sobrevive intacta a uma Restauração), um assunto
contendo um caractere de controle, uma mensagem com uma cadeia de `Received` de três saltos e uma sem nenhum,
várias com dois destinatários `To`, e dois pares de quase duplicatas com um segundo de diferença.

### Uma nota sobre o código de saída {#a-note-on-the-exit-code}

`tests/seed/seed.sh` verifica as contagens de falhas e malformações da saída de importação em vez do
código de saída do processo. `MBoxImportService` reporta `CompletedWithErrors` quando pulou
duplicatas, e o CLI mapeia cada status que não seja `Completed` para exit 1, portanto, reexecutar uma
importação resulta em um exit não-zero mesmo quando nada deu errado. Reexecutar o script de semente é, portanto, seguro
e reporta cada mensagem como já presente.

## ⚠️ Não para produção {#not-for-production}

- Qualquer nome de usuário autentica contra o Dovecot com uma única senha fixa.
- A autenticação em texto simples está habilitada.
- As credenciais do banco de dados são os padrões documentados.
- O estado do e-mail e do banco de dados reside em volumes Docker que o `down -v` deleta.

## 🔧 Solução de problemas {#troubleshooting}

**Testes falham com `Name or service not known`** — a stack não está rodando, ou
`appsettings.Test.json` está faltando, então a string de conexão volta para o padrão do container
`Host=postgres`.

**`NETSDK1226` no build** — dados de carga de trabalho incompletos no .NET SDK local, não relacionados a este
repositório. Execute `dotnet workload repair` ou faça o build com
`-p:AllowMissingPrunePackageData=true`.

**Dovecot reinicia em um loop** — um erro de configuração. `docker compose -f
docker-compose.test.yml logs dovecot` reporta a linha causadora. Note que o Dovecot não
aceita várias configurações em uma única linha dentro de um bloco.

**A aplicação retorna HTTP 500 em cada requisição** — geralmente o data protection key ring.
Verifique se `DataProtection:KeyPath` aponta para um diretório gravável.
