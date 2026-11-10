# 🐳 Docker Compose Logs Guide {#docker-compose-logs-guide}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia explica como visualizar e configurar logs ao executar o aplicativo Mail Archiver usando Docker Compose. O gerenciamento adequado de logs é essencial para monitorar a integridade do aplicativo, solucionar problemas e entender o comportamento do sistema.

## 📖 Visualizando Logs do Docker Compose {#viewing-docker-compose-logs}

### Comandos Básicos de Log {#basic-log-commands}

Para visualizar logs de todos os serviços em sua configuração do Docker Compose:

```bash
docker compose logs
```

Para visualizar logs de um serviço específico (ex: o aplicativo Mail Archiver):

```bash
docker compose logs mailarchive-app
```

### Streaming de Logs em Tempo Real {#real-time-log-streaming}

Para acompanhar os logs em tempo real (semelhante ao `tail -f`):

```bash
docker compose logs -f
```

Para acompanhar os logs de um serviço específico:

```bash
docker compose logs -f mailarchive-app
```

### Filtragem de Logs e Opções {#log-filtering-and-options}

Mostrar as últimas N linhas de logs:
```bash
docker compose logs --tail=100
```

Mostrar logs desde um horário específico:
```bash
docker compose logs --since=1h
```

Mostrar logs até um horário específico:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Mostrar carimbos de data/hora (timestamps) nos logs:
```bash
docker compose logs -t
```

## 📝 Configuração de Nível de Log {#log-level-configuration}

O aplicativo Mail Archiver suporta vários níveis de log que controlam a verbosidade da saída. Você pode configurar esses níveis usando variáveis de ambiente em seu arquivo `docker-compose.yml`.

### Níveis de Log Disponíveis {#available-log-levels}

Os seguintes níveis de log estão disponíveis, ordenados do mais verboso para o menos verboso:

- **`Trace`** - Registro mais detalhado, incluindo mensagens internas do framework
- **`Debug`** - Informações detalhadas de depuração para desenvolvimento e solução de problemas
- **`Information`** - Informações gerais sobre o fluxo do aplicativo (nível padrão)
- **`Warning`** - Mensagens de aviso sobre problemas potenciais
- **`Error`** - Mensagens de erro sobre operações que falharam
- **`Critical`** - Mensagens de erro críticas que requerem atenção imediata
- **`None`** - Sem saída de log

### Configuração de Log Padrão {#default-log-configuration}

Por padrão, o aplicativo usa a seguinte configuração de nível de log:
- **Padrão**: `Information`
- **Microsoft.AspNetCore**: `Warning` (reduz o ruído do framework)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (reduz o ruído de comandos de banco de dados)

### Configurando Níveis de Log no Docker Compose {#configuring-log-levels-in-docker-compose}

Para personalizar os níveis de log, adicione as seguintes variáveis de ambiente ao seu arquivo `docker-compose.yml` sob o serviço `mailarchive-app`:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Logging Settings (Optional)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning
```

### Exemplo: Configuração de Debug {#example-debug-configuration}

Para fins de solução de problemas, você pode querer habilitar um registro mais verboso:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Verbose Logging for Debugging
      - Logging__LogLevel__Default=Debug
      - Logging__LogLevel__Microsoft_AspNetCore=Information
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Debug
```

### Exemplo: Configuração de Produção {#example-production-configuration}

Para ambientes de produção, você pode querer reduzir a verbosidade do log para melhorar o desempenho e reduzir o volume de logs:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Production Logging (Reduced Verbosity)
      - Logging__LogLevel__Default=Warning
      - Logging__LogLevel__Microsoft_AspNetCore=Error
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Error
```

## 🔍 Entendendo a Saída de Log {#understanding-log-output}

### Formato de Log {#log-format}

Os logs seguem este formato geral:
```
timestamp [log-level] category - message
```

Exemplo:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Categorias Comuns de Log {#common-log-categories}

- **`MailArchiver.Services.*`** - Logs de serviço do aplicativo
- **`MailArchiver.Controllers.*`** - Logs de controlador da Web API
- **`Microsoft.AspNetCore`** - Logs do framework ASP.NET Core
- **`Microsoft.EntityFrameworkCore`** - Logs do Entity Framework
- **`Microsoft.Extensions.Hosting`** - Logs relacionados à hospedagem (Hosting)

## 🛠️ Dicas de Solução de Problemas {#troubleshooting-tips}

### 1. Encontrando Mensagens de Erro {#1-finding-error-messages}

Para encontrar rapidamente mensagens de erro nos logs:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Monitorando Operações Específicas {#2-monitoring-specific-operations}

Para monitorar a Sincronização de e-mails:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Verificando Avisos {#3-checking-for-warnings}

Para verificar mensagens de aviso:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Melhores Práticas de Gerenciamento de Logs {#log-management-best-practices}

### Para Desenvolvimento {#for-development}
- Use o nível `Debug` ou `Information` para solução de problemas detalhada
- Habilite o streaming de logs em tempo real durante o desenvolvimento
- Monitore os logs continuamente enquanto testa novos recursos

### Para Produção {#for-production}
- Use o nível `Warning` ou `Error` para reduzir o volume de logs
- Implemente a rotação de logs para evitar problemas de espaço em disco
- Considere soluções de registro centralizado para configurações de múltiplos contêineres
- Revise regularmente os logs em busca de eventos de segurança e problemas de desempenho

## 📚 Documentação Relacionada {#related-documentation}

- [Guia de Instalação e Configuração](Setup.md) - Instruções completas de configuração
- [Registro de Acesso](Logs.md) - Registro de acesso em nível de aplicativo
- [Guia de Backup e Restauração](BackupRestore.md) - Procedimentos de proteção de dados
