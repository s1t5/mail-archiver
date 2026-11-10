# 💾 Guia de Backup e Restauração {#backup-and-restore-guide}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia fornece instruções detalhadas para realizar o backup e a restauração do seu ambiente Mail Archiver Docker. Backups regulares são essenciais para proteger seus e-mails arquivados e dados de configuração.

## ⚠️ Aviso Importante {#important-disclaimer}

**Todos os scripts e comandos fornecidos neste guia são apenas exemplos.** Nenhuma garantia ou responsabilidade é assumida por sua funcionalidade ou adequação ao seu ambiente específico. Sempre:
- Teste os procedimentos de backup e restauração em um ambiente que não seja de produção primeiro
- Verifique se os backups estão completos e são restauráveis
- Adapte os scripts e comandos à sua configuração e requisitos específicos
- Mantenha múltiplas cópias de backup em locais diferentes
- Teste regularmente seus procedimentos de recuperação de desastres

**Use por sua conta e risco.**

## 🎯 Recomendações de Backup {#backup-recommendations}

### Backups Offline (Altamente Recomendado) {#offline-backups-highly-recommended}
Para máxima consistência e integridade dos dados, **sempre realize os backups com a aplicação e o banco de dados parados**. Isso garante:
- Sem transações de banco de dados ativas durante o backup
- Consistência de dados completa em todos os arquivos
- Sem risco de corrupção de arquivos por gravações simultâneas
- Capacidade de restauração confiável

Embora os backups a quente (Métodos 2 e 3) sejam convenientes para minimizar o tempo de inatividade, eles carregam um risco maior de inconsistências. Para ambientes de produção críticos, agende backups offline regulares durante janelas de manutenção.

### Backups em Nível de Sistema {#system-level-backups}
Além dos backups específicos da aplicação, é **fortemente recomendado** criar backups de toda a infraestrutura do sistema:
- **Backups de Container LXC**: Se estiver executando em um container LXC, crie snapshots offline de todo o container
- **Backups de Máquina Virtual**: Para implantações em VM, crie snapshots completos da VM
- **Backups do Sistema Host**: Inclua volumes Docker, configurações de container e definições do sistema
- **Arquivos de Configuração**: Faça o backup de todos os arquivos de configuração do Docker e do sistema separadamente

Backups em nível de sistema fornecem uma camada adicional de segurança e permitem a recuperação completa de desastres, incluindo a capacidade de restaurar todo o ambiente para um host diferente, se necessário.

## 🛠️ Pré-requisitos {#prerequisites}

- Configuração Mail Archiver Docker em execução (veja o [Guia de Configuração](Setup.md))
- Acesso ao sistema host Docker
- Espaço de armazenamento suficiente para backups

## 📦 O que fazer Backup {#what-to-backup}

O ambiente Mail Archiver Docker consiste em vários volumes de dados importantes que precisam de backup:

### 1. Banco de Dados PostgreSQL {#1-postgresql-database}
- **Volume**: `./postgres-data`
- **Contém**: Todos os e-mails arquivados, contas de usuário, contas de correio e dados da aplicação
- **Importância**: ⭐⭐⭐ Crítica - Este é o seu principal repositório de dados

### 2. Chaves de Proteção de Dados {#2-data-protection-keys}
- **Volume**: `./data-protection-keys`
- **Contém**: Chaves de Data Protection do ASP.NET Core para criptografia de cookies e autenticação
- **Importância**: ⭐⭐ Importante - Necessário para sessões de usuário existentes e autenticação segura

### 2a. Arquivos de Mensagens Originais (apenas com `RawMessages.Storage=filesystem`) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volume**: `./raw-messages` (o lado do host de `/app/RawMessages`)
- **Contém**: O `.eml` original de cada e-mail, compactado em gzip, gravado quando `RawMessages:Enabled` está configurado e o backend de armazenamento é `filesystem`
- **Importância**: ⭐⭐⭐ Crítica **quando em uso** - o banco de dados mantém apenas o checksum, o tamanho e o caminho relativo, portanto, uma restauração sem este diretório deixa os originais que não podem ser servidos
- Faça o backup deste diretório e do dump do banco de dados **juntos**. Restaurar o banco de dados sem os arquivos não é um erro que o Arquivo pode reparar: os downloads respondem `404` e os checksums não podem ser re-derivados de mais nada. Restaurar os arquivos sem o banco de dados é igualmente inútil, pois nada faz referência a eles.
- Se você usar `RawMessages.Storage=postgres` (o padrão), este diretório não existe e os originais estão dentro de `./postgres-data` como antes.
- Veja [Armazenamento de Mensagens Originais](RawMessages.md) para a funcionalidade em si e como alternar backends.

### 3. Configuração do Docker Compose {#3-docker-compose-configuration}
- **Arquivo**: `docker-compose.yml`
- **Contém**: Configuração do container, variáveis de ambiente e definições
- **Importância**: ⭐⭐ Importante - Contém a configuração da sua aplicação

## 🔄 Procedimentos de Backup {#backup-procedures}

### Método 1: Backup Completo (Recomendado) {#method-1-complete-backup-recommended}

Este método faz o backup de todos os dados enquanto os containers estão parados para garantir a consistência dos dados.

```bash
# 1. Stop the containers
docker compose down

# 2. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 3. Copy all data volumes
cp -r postgres-data "$BACKUP_DIR/"
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Copy docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

# 7. Start the containers again
docker compose up -d

echo "Backup completed: $BACKUP_DIR.tar.gz"
```

### Método 2: Backup a Quente (Tempo de Inatividade Mínimo) {#method-2-hot-backup-minimal-downtime}

Este método cria um backup completo enquanto o sistema está em execução com tempo de inatividade mínimo.

**Nota:** Substitua `mailuser` e `MailArchiver` pelas suas credenciais de banco de dados reais de `docker-compose.yml` (verifique as variáveis de ambiente `POSTGRES_USER` e `POSTGRES_DB`).

```bash
# 1. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 2. Backup the database
# Replace 'mailuser' and 'MailArchiver' with your actual POSTGRES_USER and POSTGRES_DB
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > "$BACKUP_DIR/database.sql"

# 3. Backup data protection keys (these change rarely, can be done while running)
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Backup docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

echo "Hot backup completed: $BACKUP_DIR.tar.gz"
```

### Método 3: Script de Backup Automatizado {#method-3-automated-backup-script}

Para backups automatizados regulares, fornecemos um script de backup pronto para uso. O script está disponível em [backup.sh](attachments/backup.sh).

**Recursos:**
- ✅ Detecção automática de credenciais do banco de dados a partir do docker-compose.yml
- ✅ Backup a quente (tempo de inatividade mínimo)
- ✅ Mecanismo de arquivo de trava para evitar backups simultâneos
- ✅ Verificação de espaço em disco antes do backup
- ✅ Limpeza automática de backups antigos

**Configuração:**

Antes de usar o script, edite a seção de configuração no topo do arquivo:

```bash
# Required settings:
MAILARCHIVER_DIR="/path/to/your/mailarchiver"   # Mail Archiver installation directory
BACKUP_BASE_DIR="/path/to/your/backups"         # Where to store backups

# Optional settings (with defaults):
RETENTION_DAYS=30                               # How many days to keep backups
LOG_FILE="/var/log/mailarchiver-backup.log"     # Log file location
MIN_DISK_SPACE_MB=1000                          # Minimum free disk space required
LOCK_FILE="/var/lock/mailarchiver-backup.lock"  # Lock file to prevent concurrent runs
```

**Configuração:**

1. **Copie o script para o local desejado:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Edite a configuração:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Ajuste as configurações
   ```

3. **Torne o script executável:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Teste o script manualmente:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Verifique o backup:**
   - Verifique o diretório de backup para o arquivo `.tar.gz` criado
   - Revise o arquivo de log para quaisquer erros ou avisos

**Agendamento de Backups Automatizados:**

Para agendar backups automáticos usando cron:

```bash
# Edit crontab
crontab -e

# Add a line to run backup daily at 2 AM
0 2 * * * /usr/local/bin/mailarchiver-backup.sh

# Or weekly on Sunday at 3 AM
0 3 * * 0 /usr/local/bin/mailarchiver-backup.sh

# Or multiple times (e.g., daily at 2 AM and 2 PM)
0 2,14 * * * /usr/local/bin/mailarchiver-backup.sh
```

## 🔙 Procedimentos de Restauração {#restore-procedures}

Os procedimentos de restauração são projetados para corresponder aos métodos de backup. Escolha o método de restauração apropriado com base no método de backup que você usou:

- **Método 1 (Backup Completo)**: Use o procedimento "Restauração Total a partir de Backup Completo". Este método é usado quando você tem um backup completo que inclui o diretório `postgres-data`.
- **Método 2 (Backup a Quente)** ou **Método 3 (Script de Backup Automatizado)**: Use o procedimento "Restauração a partir de Dump SQL" (lida com as chaves de proteção de dados e o banco de dados). Este método é usado quando você tem um backup a quente que inclui o dump SQL do banco de dados e as chaves de proteção de dados.

Para conveniência, também fornecemos um script de restauração automatizado que pode lidar com todos os procedimentos de restauração automaticamente. Você pode usar o script em vez de seguir manualmente as etapas abaixo.

### Script de Restauração Automatizado {#automated-restore-script}

O script de restauração está disponível no arquivo [restore.sh](attachments/restore.sh). Ele fornecerá uma restauração guiada e solicitará o método de backup, o caminho para o arquivo tar de backup e o diretório de instalação, e então executará o processo de restauração automaticamente.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

O script guiará você através do processo de restauração e lidará automaticamente com todas as etapas com base no método de backup que você usou.

### Restauração Total a partir de Backup Completo (Método Manual) {#full-restore-from-complete-backup-manual-method}

Isso restaura todos os dados de um Arquivo de backup completo. Este método deve ser usado quando você tem um backup completo que inclui o diretório `postgres-data` (criado pelo método um).

```bash
# 1. Stop the containers
docker compose down

# 2. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 3. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 4. Remove current data (make sure to backup first if needed!)
rm -rf postgres-data
rm -rf data-protection-keys

# 5. Restore data volumes
cp -r "$BACKUP_PATH/postgres-data" ./
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Start the containers
docker compose up -d

echo "Restore completed from: $BACKUP_FILE"
```

### Restauração a partir de Dump SQL (Método Manual) {#restore-from-sql-dump-manual-method}

Isso restaura o banco de dados e as chaves de proteção de dados a partir de um backup a quente (Métodos dois e três).

**Nota:** Os comandos abaixo usam credenciais de exemplo (`mailuser` e `MailArchiver`). Você deve substituí-las pelos valores reais do seu arquivo `docker-compose.yml`. Verifique as variáveis de ambiente `POSTGRES_USER` e `POSTGRES_DB` sob o serviço `postgres`.

```bash
# 1. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 2. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 3. Stop the containers
docker compose down

# 4. Backup current data protection keys (optional, for safety)
# cp -r data-protection-keys data-protection-keys.backup

# 5. Restore data protection keys
rm -rf data-protection-keys
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Extract database credentials from docker-compose.yml
# Check your docker-compose.yml for the actual values and replace in the commands below:
# - POSTGRES_USER (default: mailuser)
# - POSTGRES_DB (default: MailArchiver)
DB_USER="mailuser"  # Replace with your POSTGRES_USER
DB_NAME="MailArchiver"  # Replace with your POSTGRES_DB

# 8. Start the database container
docker compose up -d postgres

# 9. Wait for database to be ready
echo "Waiting for database to be ready..."
sleep 10

# 10. Verify database is ready (adjust -U and -d parameters if needed)
docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME"

# 11. Drop and recreate the database (WARNING: This deletes all current data!)
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "DROP DATABASE IF EXISTS \"$DB_NAME\";"
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "CREATE DATABASE \"$DB_NAME\";"

# 12. Restore the database from SQL dump
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_PATH/database.sql"

# 13. Start all containers
docker compose up -d

# 14. Clean up extracted backup
rm -rf "$BACKUP_PATH"

echo "Restore completed from: $BACKUP_FILE"
```


## ⚠️ Notas Importantes {#important-notes}

### Configurações de Conexão do Banco de Dados {#database-connection-settings}
Certifique-se de que suas configurações de conexão de banco de dados em `docker-compose.yml` correspondam entre o backup e a restauração:
- `POSTGRES_USER` (padrão: mailuser)
- `POSTGRES_PASSWORD` (padrão: masterkey)
- `POSTGRES_DB` (padrão: MailArchiver)

### Chaves de Proteção de Dados {#data-protection-keys}
O diretório `data-protection-keys` é usado para:
- Autenticação de usuário e gerenciamento de sessão
- Criptografia de cookies
- Manuseio seguro de dados

Sem essas chaves, as sessões de usuário existentes serão invalidadas e os usuários deverão fazer login novamente.

### Variáveis de Ambiente {#environment-variables}
Todas as configurações da aplicação são armazenadas em variáveis de ambiente em `docker-compose.yml`. Certifique-se de:
- Fazer o Backup do seu arquivo `docker-compose.yml`
- Documentar quaisquer configurações personalizadas
- Usar a mesma configuração durante a restauração

## 🔒 Melhores Práticas de Segurança {#security-best-practices}

1. **Criptografar Backups**: Considere criptografar os arquivos de backup, especialmente se eles contiverem dados de e-mail sensíveis.

2. **Armazenamento Seguro**: Armazene os backups em um local seguro:
   - Use um servidor ou sistema de armazenamento separado
   - Implemente controles de acesso
   - Considere armazenamento off-site ou em nuvem

3. **Testes Regulares**: Teste regularmente seus procedimentos de backup e restauração para garantir que funcionem corretamente

4. **Monitorar o Tamanho do Backup**: Acompanhe os tamanhos dos backups para garantir que você tenha espaço de armazenamento adequado

5. **Retenção de Backup**: Defina e implemente uma política de Retenção de backup (ex: manter backups diários por 7 dias, backups semanais por 4 semanas, backups mensais por 12 meses)

6. **Alterar Senhas Padrão**: As senhas padrão na configuração são inseguras e devem ser alteradas para uso em produção.


## 🖥️ Backup Proxmox {#proxmox-backup}

Ao executar o ambiente Mail Archiver Docker em um container Proxmox LXC, recomenda-se usar o modo "stop" para fazer o backup de todo o container para garantir a consistência dos dados.

### Por que o Modo Stop é Recomendado {#why-stop-mode-is-recommended}

1. **Consistência de Dados**: Parar o container garante que todas as transações do banco de dados sejam concluídas e os dados sejam gravados no disco antes que o backup seja realizado.
2. **Prevenir Corrupção de Dados**: Executar backups enquanto o container está ativo pode levar à corrupção de dados, especialmente com operações de banco de dados.
3. **Integridade do Sistema de Arquivos**: Parar o container garante que todas as operações do sistema de arquivos sejam concluídas, evitando gravações parciais durante o backup.

### Notas de Backup Proxmox {#proxmox-backup-notes}

- Sempre certifique-se de ter espaço de armazenamento suficiente para backups.
- Teste seu procedimento de restauração regularmente para garantir que os backups sejam válidos.
- Considere configurar um agendamento de backup na interface web do Proxmox para backups automatizados.
- Monitore os logs de backup para quaisquer erros ou problemas.
