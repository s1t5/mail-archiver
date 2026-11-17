# Saúde e Métricas {#health-and-metrics}

O Mail-Archiver expõe endpoints de saúde e um endpoint de métricas compatível com Prometheus para monitoramento externo. Ambos fazem parte da REST API: eles residem sob `/api/v1`, exigem uma chave de API e exigem que essa chave pertença a um administrador. Não há um caminho de saúde anônimo e não há um token de monitoramento separado.

## Requisitos {#requirements}

| Requisito | Por que |
|---|---|
| `Api:Enabled=true` | Os endpoints residem sob `/api/`. Com a API desativada, o middleware de autenticação responde 404. |
| Uma chave de API de administrador | Health e metrics expõem o estado de toda a instância, portanto, uma chave que não seja de administrador recebe 403. |
| `Metrics:Enabled=true` | O endpoint de metrics é opt-in. Health funciona sem ele. |

Crie a chave em **API Keys** enquanto estiver conectado como um administrador. Use uma chave dedicada e, se possível, um usuário administrador dedicado para monitoramento, para que a rotação da chave ou a desativação do usuário não afete o acesso de uma pessoa.

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Process is alive | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL reachable and schema present | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Aggregate status including sync freshness | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus text format | 200 | 404 when disabled, 401, 403 |

Todos os quatro utilizam a mesma autenticação do restante da API:

- chave ausente ou inválida: `401` com `WWW-Authenticate: Bearer`
- chave válida sem a role admin: `403` com `application/problem+json`
- API desativada: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

O Liveness não toca no PostgreSQL. Ele prova apenas que o processo aceita requisições, portanto, uma interrupção no banco de dados não faz com que o Docker ou o Kubernetes reiniciem um processo saudável.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

A verificação abre sua própria conexão PostgreSQL, solicita o schema do Arquivo e desiste após dois segundos. Ela retorna `503` quando o banco de dados está inacessível ou o schema `mail_archiver` está ausente. Use este endpoint para decidir se uma instância deve receber tráfego.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Isso executa a verificação do banco de dados e a verificação de frescor da Sincronização. Uma Sincronização obsoleta torna o relatório `Degraded`, que permanece HTTP 200 porque o processo ainda pode atender requisições. Apenas `Unhealthy` retorna `503`.

A verificação de Sincronização é desativada por padrão. Defina `Health:SyncStaleMinutes` para o número de minutos após os quais uma Conta habilitada é considerada obsoleta:

```yaml
Health:
  SyncStaleMinutes: 90
```

Uma Conta que nunca realizou Sincronização tem `LastSync` no valor padrão, portanto, é contada como obsoleta assim que a verificação for habilitada.

## Métricas {#metrics}

O endpoint retorna o formato de exposição de texto do Prometheus (versão 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` deve ser `true`. `Metrics:RefreshMinutes` controla a frequência com que o snapshot
é reconstruído (padrão 5, valores abaixo de 1 são elevados para 1). O scrape em si apenas lê o
snapshot, portanto, uma consulta do Prometheus não pode se transformar em um scan de tabela.

| Métrica | Tipo | Labels | Significado |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1 quando o endpoint de métricas responde. |
| `mailarchiver_metrics_ready` | gauge | | 1 após o primeiro snapshot bem-sucedido. |
| `mailarchiver_db_up` | gauge | | 1 quando a última atualização alcançou o PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix time da última atualização bem-sucedida. |
| `mailarchiver_emails` | gauge | | Número de e-mails arquivados. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Número de contas de e-mail. |
| `mailarchiver_raw_messages` | gauge | | Número de mensagens originais armazenadas. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1 quando `RawMessages:Enabled` está configurado. |
| `mailarchiver_raw_messages_missing` | gauge | | E-mails arquivados sem um original armazenado. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1 quando a Conta nunca completou uma Sincronização. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix time da última Sincronização bem-sucedida. Ausente para Contas que nunca sincronizaram. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Segundos desde a última Sincronização bem-sucedida. Ausente para Contas que nunca sincronizaram. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Mensagens originais que não puderam ser armazenadas. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Execuções de Sincronização que terminaram em falha, limite de taxa ou timeout. |

Notas:

- Os contadores são locais ao processo e começam em 0 após um reinício. `rate()` e `increase()`
  no Prometheus lidam com isso corretamente.
- `mailarchiver_raw_messages_missing` conta cada e-mail arquivado sem uma mensagem bruta,
  incluindo e-mails arquivados antes de `RawMessages:Enabled` ser ativado. Em uma instalação
  que possui originais armazenados desde o primeiro dia, este é o gap relevante.
- `mailarchiver_account_sync_age_seconds` não é um backlog real. Ele indica quando a última Sincronização
  terminou, não quantas mensagens estão aguardando no servidor.
- `mailarchiver_account_never_synced` existe porque uma Conta que nunca sincronizou não possui uma série
  `sync_age`. Um alerta apenas em `sync_age` a perderia.
- O endpoint contém nomes e ids de Conta, mas nenhum conteúdo de e-mail, assuntos ou
  credenciais.

## Prometheus {#prometheus}

```yaml
scrape_configs:
  - job_name: mail-archiver
    scrape_interval: 30s
    authorization:
      credentials_file: /etc/prometheus/mailarchiver-api-key
    static_configs:
      - targets: ["mail-archiver:5000"]
```

O arquivo de credenciais contém a chave bruta, sem o prefixo `Bearer`. Restrinja suas
permissões ao usuário Prometheus.

Exemplos de alertas:

```yaml
groups:
  - name: mail-archiver
    rules:
      - alert: MailArchiverSyncStale
        expr: mailarchiver_account_sync_age_seconds{enabled="true"} > 3600
        for: 15m

      - alert: MailArchiverNeverSynced
        expr: mailarchiver_account_never_synced{enabled="true"} == 1
        for: 30m

      - alert: MailArchiverSyncFailures
        expr: increase(mailarchiver_sync_failures_total[1h]) > 0

      - alert: MailArchiverMissingOriginals
        expr: mailarchiver_raw_messages_enabled == 1 and mailarchiver_raw_messages_missing > 0

      - alert: MailArchiverDatabaseDown
        expr: mailarchiver_db_up == 0
        for: 5m

      - alert: MailArchiverMetricsStale
        expr: time() - mailarchiver_metrics_refresh_timestamp_seconds > 900
```

## Docker health check {#docker-health-check}

A imagem e o `docker-compose.yml` embutido verificam `/api/v1/health/live` com uma chave de administrador. A chave não pode existir antes da primeira inicialização, portanto, a primeira inicialização ocorre sem ela e o contêiner reporta `unhealthy` até que a chave seja configurada.

1. Inicie a stack uma vez com `MAILARCHIVER_HEALTH_API_KEY` vazio.
2. Faça login como administrador e crie uma chave chamada `monitoring-health` em **API Keys**.
3. Coloque a chave em `.env`:

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. Recrie o contêiner com `docker compose up -d`.
5. Verifique o estado:

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

`Api:Enabled=true` é necessário. Com a API desativada, o health check recebe 404 e o contêiner permanece unhealthy.

Se você não quiser uma chave de administrador no ambiente do contêiner, use uma verificação TCP no contêiner e chame `/api/v1/health/ready` de um host de monitoramento externo com a chave.
A verificação TCP não pode informar se o PostgreSQL está acessível.

## Segurança {#security}

- A chave é uma chave de administrador. Ela pode ler todo o Arquivo através da API. Trate-a como uma
  senha de administrador.
- Use um usuário administrador dedicado para monitoramento. Desativar esse usuário desativa o health check
  e as métricas.
- Mantenha a chave em um arquivo ou em um Docker secret, nunca em um parâmetro de consulta. Parâmetros de consulta
  acabam em logs de proxy e de acesso.
- Restrinja o acesso ao host de monitoramento ou à rede do Prometheus. A chave da API é a
  autenticação, não um substituto para limites de rede.
- Os endpoints de health retornam apenas strings de status. Detalhes de exceção permanecem no
  log da aplicação.

## Solução de problemas {#troubleshooting}

| Sintoma | Causa | Correção |
|---|---|---|
| 404 em todos os quatro caminhos | `Api:Enabled=false` | Defina `Api:Enabled=true` e reinicie. |
| 404 apenas em `/metrics` | `Metrics:Enabled=false` | Defina `Metrics:Enabled=true` e reinicie. |
| 401 | Chave ausente, incorreta, revogada ou expirada | Crie uma nova chave e verifique o cabeçalho `Authorization: Bearer`. |
| 403 | A chave pertence a um usuário que não é administrador | Use uma chave de administrador. |
| `mailarchiver_metrics_ready 0` por mais de um minuto | O serviço de atualização não concluiu sua primeira execução ou não consegue alcançar o banco de dados | Verifique o log da aplicação em busca de `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | A última atualização falhou | Verifique o PostgreSQL e a string de conexão. Contagens antigas permanecem até o próximo sucesso. |
| O container permanece instável | Chave vazia ou `Api:Enabled=false` | Defina ambos e recrie o container. |
| `health/ready` retorna 503 | PostgreSQL inacessível ou esquema ausente | Verifique o banco de dados e se as migrações foram executadas. |

## O que as métricas não cobrem {#what-the-metrics-do-not-cover}

- Acessibilidade IMAP ou Graph por Conta. Uma idade de sincronização apenas mostra que a última execução é antiga.
- O número de mensagens aguardando no servidor, o backlog real.
- Verificação de checksum de cada original armazenado. Use a verificação de mensagem bruta existente
  e a limpeza de órfãos para isso.
- Contagens por Pasta ou tamanhos de Caixa de correio.

Para estes, use a página da Conta, a página de jobs em segundo plano e o log da aplicação.
