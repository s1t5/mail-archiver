# 🔄 Guia de Atualização de Versão Major do PostgreSQL {#postgresql-major-version-upgrade-guide}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia explica como atualizar o banco de dados PostgreSQL de uma versão major para outra em seu ambiente Mail Archiver Docker Compose. Atualizações de versão major são necessárias para aproveitar novos recursos do PostgreSQL, melhorias de desempenho e atualizações de segurança.

## ⚠️ Aviso Importante {#important-disclaimer}
> **Nota**: Este procedimento de atualização é um processo geral de migração de banco de dados PostgreSQL e não é específico para o Mail Archiver. Ele é fornecido como uma recomendação baseada nas melhores práticas do PostgreSQL. Sempre teste o procedimento em um ambiente que não seja de produção primeiro e certifique-se de ter Backups adequados antes de prosseguir.


**Todos os procedimentos descritos neste guia devem ser testados em um ambiente que não seja de produção primeiro.** Atualizações de versão major envolvem migração de dados e trazem riscos de perda ou corrupção de dados. Sempre:
- Crie um Backup completo antes de iniciar o processo de atualização
- Teste o procedimento de atualização em um ambiente de desenvolvimento
- Verifique toda a funcionalidade da aplicação após a atualização
- Tenha um plano de rollback em caso de problemas
- Agende atualizações durante janelas de manutenção

**Use por sua conta e risco.**

## 🎯 Quando Atualizar {#when-to-upgrade}

Considere atualizar o PostgreSQL quando:
- Sua versão atual estiver se aproximando do fim da vida útil (end-of-life)
- Atualizações de segurança exigirem uma versão mais recente
- Você estiver planejando uma janela de manutenção importante do sistema

## 🛠️ Pré-requisitos {#prerequisites}

- Configuração Mail Archiver Docker em execução (veja o [Guia de Configuração](Setup.md))
- Espaço em disco suficiente (pelo menos 1,5x o tamanho atual do banco de dados)
- Acesso ao sistema host Docker
- Backup completo do banco de dados atual (veja o [Guia de Backup e Restauração](BackupRestore.md))
- Janela de manutenção para tempo de inatividade da aplicação

## 📋 Configuração Atual {#current-configuration}

Sua configuração atual do Mail Archiver utiliza:
- **Nome do Banco de Dados**: MailArchiver
- **Usuário do Banco de Dados**: mailuser
- **Volume de Dados**: `./postgres-data` mapeado para `/var/lib/postgresql/data`

Se necessário, isso deve ser ajustado nos comandos e scripts de exemplo a seguir.

## 🔄 Procedimentos de Atualização {#upgrade-procedures}

Este método cria um backup lógico de seus dados e o restaura na nova versão do PostgreSQL. É a abordagem mais segura e funciona entre quaisquer versões do PostgreSQL.

> **Métodos Alternativos**: O PostgreSQL também fornece outros métodos de atualização, como `pg_upgrade` para atualizações mais rápidas. Para mais informações sobre abordas de atualização alternativas, consulte a [documentação oficial de atualização do PostgreSQL](https://www.postgresql.org/docs/current/upgrading.html).

#### Passo 1: Preparar o Ambiente {#step-1-prepare-the-environment}

```bash
# 1. Stop the application container (keep database running for backup)
docker compose stop mailarchive-app

# 2. Create a backup directory
mkdir -p postgres-backup

# 3. Create a database dump
echo "Export DB content (this can take some time)"
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > postgres-backup/database_dump.sql

# 4. Verify the dump was created successfully
ls -la postgres-backup/database_dump.sql

# 5. Stop the database container
docker compose down
```

#### Passo 2: Atualizar a Configuração do Docker Compose {#step-2-update-docker-compose-configuration}

Edite seu arquivo `docker-compose.yml` para usar a nova versão do PostgreSQL:

```yaml
  postgres:
    image: postgres:18  # Changed from old version
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data-new:/var/lib/postgresql  # New data directory
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
```

**Mudanças Importantes:**
- `image` atualizado de `postgres:17` para `postgres:18` (ou sua versão de destino)
- Mapeamento de volume alterado de `./postgres-data:/var/lib/postgresql/data` para `./postgres-data-new:/var/lib/postgresql` (a mudança do caminho interno é exigida pela versão 18 e subsequentes do postgres)

#### Passo 3: Iniciar Nova Instância do PostgreSQL {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Passo 4: Restaurar Dados para o Novo Banco de Dados {#step-4-restore-data-to-new-database}

Restaure o dump do banco de dados para a nova instância do PostgreSQL:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
Isso pode levar algum tempo dependendo do tamanho do banco de dados
Fique atento a quaisquer mensagens de erro durante a Restauração

#### Passo 5: Testar e Concluir a Migração {#step-5-test-and-complete-migration}
Inicie a aplicação para testar a conectividade:

```bash
docker compose up -d
```

Teste a funcionalidade da aplicação:
- Faça login na interface web
- Verifique se a busca de e-mails funciona
- Verifique se os dados existentes estão acessíveis
- Teste a funcionalidade de Backup/Restauração

Se tudo funcionar corretamente, limpe os dados antigos:

Pare os containers
```bash
docker compose down
```
Remova o diretório de dados antigo (somente após confirmar que a nova configuração funciona)
```bash
rm -rf postgres-data
```
Remova o diretório de backup
```bash
rm -rf postgres-backup
```

Renomeie o novo diretório de dados para o nome padrão
```bash
mv postgres-data-new postgres-data
```

Atualize o docker-compose.yml para usar o nome de volume padrão:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Inicie o sistema completo
```bash
docker compose up -d
```

## ⚠️ Considerações Importantes {#important-considerations}

### Planejamento de Tempo de Inatividade {#downtime-planning}

- Dependendo do seu sistema e do tamanho do banco de dados, este procedimento pode levar muito tempo.
- Agende durante janelas de manutenção

### Requisitos de Espaço em Disco {#disk-space-requirements}

Certifique-se de ter espaço em disco adequado (aproximadamente o dobro do tamanho do seu banco de dados)

Verifique o espaço disponível:
```bash
df -h
du -sh postgres-data/
```

### Plano de Rollback {#rollback-plan}

Sempre prepare-se para um rollback:
1. Mantenha o diretório de dados antigo até que a atualização seja verificada
2. Mantenha o antigo Backup de `docker-compose.yml`
3. Teste o procedimento de rollback em desenvolvimento primeiro

### Verificação Pós-Atualização {#post-upgrade-verification}

Após a atualização, verifique:
- Conectividade da aplicação com o banco de dados
- Funcionalidade de busca e recuperação de e-mails
- Autenticação de usuário e gerenciamento de sessão
- Funcionalidade de Backup e Restauração
- Métricas de desempenho do banco de dados

## 📚 Documentação Relacionada {#related-documentation}

- [Guia de Backup e Restauração](BackupRestore.md) - Essencial para Backups pré-atualização
- [Guia de Manutenção de Banco de Dados](DatabaseMaintenance.md) - Manutenção pós-atualização
- [Guia de Configuração](Setup.md) - Configuração do Docker Compose
- [Notas de Lançamento do PostgreSQL](https://www.postgresql.org/docs/release/) - Documentação oficial de lançamento
