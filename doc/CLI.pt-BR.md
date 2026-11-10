# 🖥️ CLI Commands {#cli-commands}

[← Voltar para o Índice da Documentação](index.md)

## 📋 Visão Geral {#overview}

Alguns trabalhos não pertencem à interface web: importar um Arquivo existente do sistema de arquivos local ou mover mensagens originais armazenadas entre backends de armazenamento. O Mail Archiver aceita ambos como comandos na linha de comando.

| Comando | Propósito |
|----------|---------|
| `--import-mbox` | Importar um arquivo MBox de um diretório montado |
| `--import-eml` | Importar um Arquivo ZIP de arquivos EML de um diretório montado |
| `--migrate-raw-storage` | Mover mensagens originais armazenadas entre o banco de dados e o sistema de arquivos |

Todos os comandos são processados antes do início do pipeline web e saem quando terminam, portanto, é seguro executá-los com `docker compose exec` enquanto o Arquivo está em execução.

## ⚠️ Modelo de Segurança {#security-model}

Os comandos de importação são **apenas CLI** – não há um endpoint web para navegar ou importar arquivos locais. Você deve ter acesso ao host Docker (`docker exec`) para executá-los.

Uma **whitelist de caminhos** (`LocalImport:AllowedPaths`) controla de quais diretórios o importador pode ler. Arquivos fora desses caminhos são rejeitados.

`--migrate-raw-storage` não está sujeito a essa whitelist: ele lê apenas os arquivos de mensagem que o próprio Arquivo escreveu sob `RawMessages:FileSystem:BasePath`.

## 🛠️ Configuração {#setup}

### 1. Configurar Caminhos Permitidos {#1-configure-allowed-paths}

Em seu `docker-compose.yml`, defina os caminhos de importação permitidos:

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Montar Seus Arquivos {#2-mount-your-files}

Monte o diretório contendo seus arquivos MBox/EML no contêiner:

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

Substitua `/home/user/mbox-archives` pelo caminho real em seu host Docker.

### 3. Reiniciar o Contêiner {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 Uso {#usage}

### Encontre o ID da sua Conta de destino {#find-your-target-account-id}

Antes de importar, você precisa do ID do banco de dados da Conta de e-mail de destino. Você pode encontrá-lo via Web UI:

Navegue até "Email Accounts", abra os detalhes da conta de destino; o ID da conta é exibido na lista de contas ou na URL ao visualizar uma conta.

### Importar um Arquivo MBox {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Parâmetros:**
| Parâmetro | Obrigatório | Descrição |
|-----------|----------|-------------|
| `--import-mbox` | Sim | Sinaliza uma operação de importação MBox |
| `--file <path>` | Sim | Caminho para o arquivo MBox dentro do contêiner |
| `--account-id <id>` | Sim | ID do banco de dados da Conta de e-mail de destino |
| `--folder <name>` | Não | Nome da Pasta de destino (padrão: `INBOX`) |

### Importar Arquivos EML (Arquivo ZIP) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Parâmetros:**
| Parâmetro | Obrigatório | Descrição |
|-----------|----------|-------------|
| `--import-eml` | Sim | Sinaliza uma operação de importação EML |
| `--file <path>` | Sim | Caminho para um **Arquivo ZIP** contendo arquivos `.eml` dentro do contêiner |
| `--account-id <id>` | Sim | ID do banco de dados da Conta de e-mail de destino |

> **Importante:** `--import-eml` espera um **Arquivo ZIP** contendo arquivos `.eml` — ele **não** aceita arquivos `.eml` individuais. Para importar arquivos EML únicos, compacte-os em um Arquivo ZIP primeiro:
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Nota para `dotnet run`:** Ao usar `dotnet run` (em vez da DLL publicada), separe os argumentos do dotnet dos argumentos da aplicação com `--`:
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> O `--` informa ao `dotnet run` que todos os argumentos seguintes pertencem à sua aplicação, não ao compilador .NET.

## 📊 Exemplo de Saída {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Monitoramento {#monitoring}

Durante a importação, o progresso detalhado é gravado nos logs do container:

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Referência de Configuração {#configuration-reference}

| Variável de Ambiente | Padrão | Descrição |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | Primeiro caminho permitido para importações locais |
| `LocalImport__AllowedPaths__1` | (vazio) | Segundo caminho permitido (opcional) |

Adicione mais caminhos incrementando o índice (`__2`, `__3`, etc.).

## 🚫 Tratamento de Erros {#error-handling}

| Erro | Causa | Solução |
|-------|-------|----------|
| `File not found` | O caminho `--file` não existe no contêiner | Verifique o caminho do arquivo e a montagem do volume |
| `File path is not in an allowed import directory` | O arquivo está fora dos caminhos permitidos | Mova o arquivo para um caminho permitido ou adicione seu diretório ao `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | ID da Conta incorreto | Verifique o ID da Conta via Web UI ou consulta ao banco de dados |
| `Invalid account-id` | Valor não numérico para `--account-id` | Use um ID numérico do banco de dados |

## 🔒 Melhores Práticas de Segurança {#security-best-practices}

1. **Use montagens de apenas leitura** sempre que possível:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Restrinja AllowedPaths** apenas aos diretórios que você realmente utiliza.

3. **Remova as montagens de volume** após a conclusão da importação para manter uma superfície de segurança limpa.

4. **Nunca exponha** o diretório de importação via servidor web – não existe um endpoint web para importações locais.

## 📦 Formatos de Importação de Relance {#import-formats-at-a-glance}

| CLI Flag | Formato de Entrada | Extensão de Arquivo Típica | E-mail Único | Lote |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | Arquivo MBox | `.mbox`, `.mbx` | ❌ (sempre lote) | ✅ |
| `--import-eml` | Arquivo ZIP de EMLs | `.zip` | ❌ (ZIP necessário) | ✅ |
| `--migrate-raw-storage` | Mensagens originais armazenadas | — | — | ✅ |

*Arquivos `.eml` individuais não são suportados diretamente. Use `zip` para envolvê-los ou importe via [web upload](Import-EML-MBox.md).*

## 🔧 Comandos de Manutenção {#maintenance-commands}

### Movendo Mensagens Originais Armazenadas {#moving-stored-original-messages}

Quando `RawMessages:Enabled` está ativado, a mensagem original de cada e-mail é mantida no banco de dados ou em arquivos, dependendo de `RawMessages:Storage`. Este comando move os originais já armazenados de um backend para outro; consulte [Original Message Storage](RawMessages.md#storage-backend) para a configuração.

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argumento | Padrão | Descrição |
|----------|---------|-------------|
| `--from` | `RawMessages:Storage` configurado | Backend de onde as mensagens são movidas. |
| `--to` | `RawMessages:Storage` configurado | Backend para onde as mensagens são movidas. |
| `--batch` | `200` | Mensagens por transação de banco de dados. |

Ao contrário dos comandos de importação, este não é restrito por `LocalImport:AllowedPaths`: ele lê apenas os arquivos de mensagem que o próprio Arquivo escreveu sob `RawMessages:FileSystem:BasePath`.

**O que ele garante:**

- Cada mensagem é verificada contra o checksum registrado no momento do Arquivo antes de ser copiada. Uma mensagem que falhar é deixada exatamente onde está e reportada — um original corrompido nunca é propagado e sua única cópia restante nunca é excluída.
- Um arquivo é excluído apenas após a transação do banco de dados que o moveu ter sido confirmada, portanto, uma execução interrompida não pode perder a única cópia.
- A execução é retomável: o trabalho é selecionado pelo armazenamento registrado em cada mensagem, portanto, iniciar o comando novamente continua com o que ainda está pendente.
- Uma mensagem ruim não aborta a execução. O código de saída é `1` se qualquer mensagem falhar, `0` caso contrário.

**Exemplo de saída:**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

As leituras sempre seguem o backend registrado em cada mensagem, não a configuração configurada, portanto, um download continua funcionando durante e após uma migração. Alterar `RawMessages:Storage` afeta apenas e-mails recém-arquivados — execute este comando para mover o que já está armazenado.

> **Backup:** com o backend de sistema de arquivos, os arquivos de mensagem residem fora do banco de dados. Faça o Backup de `RawMessages:FileSystem:BasePath` junto com o dump do banco de dados; uma Restauração sem os arquivos deixa originais que não podem ser servidos.

## ❓ FAQ {#faq}

**P: Por que o `--import-eml` requer um arquivo ZIP?**
R: O serviço de importação EML subjacente (`EmlImportService`) processa Arquivos ZIP internamente usando a API `ZipFile` do .NET. Este design suporta importações em massa de forma eficiente. Para arquivos `.eml` individuais, envolva-os em um Arquivo ZIP (ex: `zip emails.zip *.eml`) ou use o [web upload](Import-EML-MBox.md).

**P: Por que não consigo navegar por arquivos através da interface web?**
R: Este é um design de segurança deliberado. Esta função é implementada intencionalmente via CLI para interações em nível de sistema, pois usuários normais não têm acesso direto ao nível do SO; eles interagem apenas com a interface Web.

Para evitar riscos de segurança, como ataques de path traversal e exposição não autorizada do sistema de arquivos, os usuários regulares são restritos ao uso da interface Web para uploads e importações de arquivos. Isso garante que eles não possam contornar a interface pretendida para acessar o sistema de arquivos do container subjacente.
