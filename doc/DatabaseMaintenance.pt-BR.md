# 🔧 Guia de Manutenção de Banco de Dados {#database-maintenance-guide}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia explica como configurar e usar o recurso de manutenção automática de banco de dados no Mail Archiver. O aplicativo inclui um serviço de segundo plano integrado que executa automaticamente operações de manutenção de banco de dados PostgreSQL para manter o desempenho ideal e evitar o inchaço do banco de dados.

## ⚠️ Aviso Importante {#important-notice}

**Bancos de dados PostgreSQL não diminuem de tamanho automaticamente quando os dados são excluídos.** Quando você exclui e-mails ou Caixas de correio, o banco de dados marca o espaço como disponível para reutilização, mas o tamanho real do arquivo do banco de dados permanece o mesmo. Esse fenômeno é chamado de "database bloat" (inchaço do banco de dados).

O Mail Archiver inclui um serviço de manutenção automática que executa operações VACUUM ANALYZE diariamente para:
- Reivindicar armazenamento para reutilização dentro do banco de dados
- Atualizar estatísticas de consulta para desempenho ideal
- Prevenir a degradação do desempenho ao longo do tempo
- Manter a integridade do banco de dados

## 🚀 Manutenção Automática (Recomendado) {#automatic-maintenance-recommended}

### Configuração {#configuration}

Ative a manutenção automática do banco de dados em seu `appsettings.json` ou via variáveis de ambiente em `docker-compose.yml`:

#### Opção 1: Via appsettings.json {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### Opção 2: Via docker-compose.yml (Recomendado) {#option-2-via-docker-composeyml-recommended}

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Enable automatic database maintenance
      - DatabaseMaintenance__Enabled=true
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30
```

### Opções de Configuração {#configuration-options}

| Parâmetro | Tipo | Padrão | Descrição |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Ativa ou desativa a manutenção automática |
| `DailyExecutionTime` | string | `"02:00"` | Hora do dia para executar a manutenção (formato HH:mm, 24 horas) |
| `TimeoutMinutes` | integer | `30` | Tempo máximo permitido para operações de manutenção em minutos |

### Como Funciona {#how-it-works}

Quando ativado, o serviço de manutenção:

1. **Inicia automaticamente** com o aplicativo
2. **Aguarda** até o horário de execução configurado
3. **Executa VACUUM ANALYZE** em todo o banco de dados
4. **Registra a operação** no Registro de auditoria do aplicativo com a duração
5. **Reagenda** para o dia seguinte

A operação de manutenção:
- ✅ Executa sem tempo de inatividade (downtime)
- ✅ Não interfere nas operações normais do aplicativo
- ✅ Fornece registro e monitoramento completos
- ✅ Reivindica espaço para reutilização dentro do banco de dados
- ✅ Atualiza estatísticas de consulta para melhor desempenho

### Escolhendo o Horário de Execução {#choosing-the-execution-time}

Selecione um horário em que seu sistema tenha a menor atividade:
- **Recomendado**: Entre 02:00 e 04:00 (madrugada)
- **Considere**: A configuração de seu fuso horário
- **Evite**: Horários de pico de uso ou janelas de Backup

Exemplos de configurações:

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Monitorando a Manutenção Automática {#monitoring-automatic-maintenance}

#### 1. Logs do Aplicativo {#1-application-logs}

Verifique os logs do aplicativo para atividades de manutenção:

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Registro de auditoria (UI) {#2-access-log-ui}

Todas as operações de manutenção são registradas no Registro de auditoria do aplicativo:

1. Navegue até **Logs** → **Access Log** na UI
2. Filtre por tipo: **Database Maintenance**
3. Visualize os horários de execução, duração e quaisquer erros

## 🔧 Manutenção Manual (Avançado) {#manual-maintenance-advanced}

### VACUUM FULL para Recuperação de Espaço em Disco {#vacuum-full-for-disk-space-recovery}

**⚠️ Requer Tempo de Inatividade do Aplicativo**

O VACUUM FULL compacta o banco de dados e devolve o espaço em disco ao sistema operacional, mas requer bloqueios exclusivos.

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**Quando usar:**
- Após excluir grandes Caixas de correio
- Para reivindicar espaço significativo em disco
- Durante janelas de manutenção agendadas

**Importante:**
- Requer espaço livre em disco igual ao tamanho da tabela
- Pode levar horas em bancos de dados grandes
- Bloqueia todas as operações do banco de dados
- Deve ser agendado durante horários de baixa atividade

Para procedimentos manuais detalhados, consulte a [Documentação PostgreSQL VACUUM](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Documentação Relacionada {#related-documentation}

- [Guia de Configuração](Setup.md) - Configuração inicial e variáveis de ambiente
- [Guia de Backup e Restauração](BackupRestore.md) - Procedimentos de Backup do banco de dados
- [Documentação PostgreSQL VACUUM](https://www.postgresql.org/docs/current/sql-vacuum.html) - Documentação oficial do PostgreSQL

## 📝 Resumo {#summary}

✅ **Ative a manutenção automática** para otimização de banco de dados sem intervenção manual
✅ **Monitore via registros de acesso** para verificar operações bem-sucedidas
✅ **Use VACUUM FULL manual** apenas quando a recuperação de espaço em disco for necessária
✅ **Agende o VACUUM FULL** durante janelas de manutenção para grandes exclusões
✅ **Mantenha os Backups atualizados** antes de qualquer operação de manutenção manual

O serviço de manutenção automática lida com a otimização diária do banco de dados, eliminando a necessidade de intervenção manual na maioria dos casos.
