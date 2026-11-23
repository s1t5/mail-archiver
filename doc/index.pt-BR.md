# 📧 Documentação do Mail Archiver {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Arquivamento de e-mail auto-hospedado — arquive, pesquise e exporte e-mails de qualquer provedor.**
Configure uma vez, mantenha cada mensagem e encontre-a novamente anos depois.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Primeiros passos__

    ---

    Instale o Mail Archiver com Docker Compose e conecte sua primeira Caixa de correio.

    [:octicons-arrow-right-24: Configuração e parâmetros](Setup.md)

-   :material-magnify:{ .lg .middle } __Encontre qualquer coisa__

    ---

    Pesquisa de texto completo em cada Conta, com filtros, ordenação e ações em massa.

    [:octicons-arrow-right-24: Pesquisa de e-mail](Search.md)

-   :material-api:{ .lg .middle } __Automatize__

    ---

    Leia seu Arquivo a partir de scripts e agentes de IA — chaves de API de apenas leitura e com escopo limitado.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Proteja__

    ---

    Autenticação de dois fatores, SSO, registro de acesso e políticas de Retenção.

    [:octicons-arrow-right-24: Autenticação de dois fatores](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Instalação & Manutenção {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Configuração e parâmetros__

    ---

    Cada variável de ambiente, Kestrel HTTPS e gerenciamento de segredos.

    [:octicons-arrow-right-24: Ler](Setup.md)

-   :material-server-network:{ .lg .middle } __Proxy reverso__

    ---

    Coloque o Mail Archiver atrás do nginx ou Caddy com terminação TLS.

    [:octicons-arrow-right-24: Ler](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __Atualização do PostgreSQL__

    ---

    Mova para uma nova versão principal do PostgreSQL sem perder dados.

    [:octicons-arrow-right-24: Ler](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Manutenção do banco de dados__

    ---

    VACUUM, ANALYZE e manter o Arquivo responsivo ao longo do tempo.

    [:octicons-arrow-right-24: Ler](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Backup e Restauração__

    ---

    Faça o Backup do banco de dados e realize a Restauração — incluindo configurações de Proxmox.

    [:octicons-arrow-right-24: Ler](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Ambiente de teste local__

    ---

    Execute uma instância temporária localmente antes de mexer na produção.

    [:octicons-arrow-right-24: Ler](LocalTestEnvironment.md)

</div>

## 📥 Recebendo E-mails {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Importação de Conta__

    ---

    Adicione muitas Contas de uma vez a partir de um arquivo CSV.

    [:octicons-arrow-right-24: Ler](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Sincronização__

    ---

    Sincronização rápida vs. completa, checkpoints e o que acontece quando uma Sincronização falha.

    [:octicons-arrow-right-24: Ler](Synchronization.md)

-   :material-console:{ .lg .middle } __Comandos CLI__

    ---

    Importe Arquivos existentes via linha de comando e mova mensagens originais armazenadas entre o banco de dados e o sistema de arquivos.

    [:octicons-arrow-right-24: Ler](CLI.md)

-   :material-upload:{ .lg .middle } __Importação Web (EML / MBox)__

    ---

    Faça o upload de um arquivo MBox ou um ZIP de arquivos EML através do navegador.

    [:octicons-arrow-right-24: Ler](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Tratamento de limite de taxa__

    ---

    Rastreamento de largura de banda e como as Sincronizações pausadas retomam sozinhas.

    [:octicons-arrow-right-24: Ler](RateLimitHandling.md)

</div>

## 🔍 Trabalhando com o Arquivo {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Pesquisa de e-mail__

    ---

    Sintaxe de consulta, filtros, ordenação, paginação e ações em massa.

    [:octicons-arrow-right-24: Ler](Search.md)

-   :material-progress-clock:{ .lg .middle } __Tarefas em segundo plano__

    ---

    Acompanhe Sincronizações, importações, Restaurações e exportações — e cancele-as com segurança.

    [:octicons-arrow-right-24: Ler](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Migração de Caixa de correio__

    ---

    Copie e-mails entre Caixas de correio preservando a estrutura de Pasta.

    [:octicons-arrow-right-24: Ler](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Exportação de Conta (EML / MBox)__

    ---

    Escreva uma Conta inteira de volta como um Arquivo EML ou MBox portátil.

    [:octicons-arrow-right-24: Ler](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Armazenamento de mensagem original__

    ---

    Mantenha a mensagem como recebida, para que o DKIM ainda verifique e as exportações sejam byte a byte exatas.

    [:octicons-arrow-right-24: Ler](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Deduplicação de Anexo__

    ---

    Como Anexos idênticos são armazenados uma única vez em vez de várias vezes.

    [:octicons-arrow-right-24: Ler](AttachmentDeduplication.md)

-   :material-file-pdf-box:{ .lg .middle } __Visualização de fatura XRechnung__

    ---

    Leia Anexos XML XRechnung como um PDF diretamente no Arquivo.

    [:octicons-arrow-right-24: Ler](EInvoicing.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Descarregamento por janela de data__

    ---

    Mova e-mails mais antigos para outra Conta, janela por janela.

    [:octicons-arrow-right-24: Ler](Offload.md)

-   :material-harddisk:{ .lg .middle } __Armazenamento por Conta__

    ---

    Veja quanto espaço cada Caixa de correio ocupa.

    [:octicons-arrow-right-24: Ler](AccountStorage.md)

</div>

## 🔐 Administração & Segurança {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Gerenciamento de usuários__

    ---

    Crie usuários, conceda direitos de administrador, atribua Permissões de Caixa de correio e altere sua própria senha.

    [:octicons-arrow-right-24: Ler](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Autenticação de dois fatores__

    ---

    Configuração de TOTP, códigos de backup e o que fazer quando um dispositivo é perdido.

    [:octicons-arrow-right-24: Ler](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Faça login através do Entra ID, Authelia ou outro provedor de identidade.

    [:octicons-arrow-right-24: Ler](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Registro de acesso__

    ---

    Quem abriu, pesquisou, exportou ou deletou o quê.

    [:octicons-arrow-right-24: Ler](Logs.md)

-   :material-file-export:{ .lg .middle } __Exportação de dados de auditoria__

    ---

    Exporte o rastro de auditoria para revisões de conformidade.

    [:octicons-arrow-right-24: Ler](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Recuperação de conta de emergência__

    ---

    Recupere o acesso quando uma Conta estiver bloqueada.

    [:octicons-arrow-right-24: Ler](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Políticas de Retenção__

    ---

    Exclusão automática do servidor de e-mail ou do Arquivo local.

    [:octicons-arrow-right-24: Ler](RetentionPolicies.md)

</div>

## ☁️ Guias de Provedores {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Registro de app Azure (M365)__

    ---

    Registre um app e configure a Retenção para Microsoft 365.

    [:octicons-arrow-right-24: Ler](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __Importação de Caixa de correio de tenant M365__

    ---

    Importação em massa de cada Caixa de correio de um tenant a partir de um único formulário.

    [:octicons-arrow-right-24: Ler](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Instâncias Mailcow__

    ---

    Selecione Caixas de correio centralmente e provisione senhas de app IMAP dedicadas.

    [:octicons-arrow-right-24: Ler](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Contas Microsoft pessoais__

    ---

    Outlook.com, Hotmail e M365 Family via device code flow.

    [:octicons-arrow-right-24: Ler](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Melhores práticas do Gmail__

    ---

    Senhas de app, labels, expunge e limites de taxa do Gmail.

    [:octicons-arrow-right-24: Ler](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Limitações do Yahoo mail__

    ---

    O que os endpoints de IMAP e exportação do Yahoo não farão.

    [:octicons-arrow-right-24: Ler](YahooMailExportLimitations.md)

</div>

## 🔌 Acesso Programático {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Acesso de apenas leitura ao Arquivo com chaves de API por usuário com escopo limitado.

    [:octicons-arrow-right-24: Ler](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Exponha o Arquivo para agentes de IA como ferramentas de descoberta.

    [:octicons-arrow-right-24: Ler](MCP.md)

</div>

## 🧰 Operações & Desenvolvimento {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Idioma e localização__

    ---

    Alterne o idioma da interface e como adicionar uma tradução.

    [:octicons-arrow-right-24: Ler](Localization.md)

-   :material-docker:{ .lg .middle } __Logs do Docker Compose__

    ---

    Leia os logs dos containers e altere o nível de log.

    [:octicons-arrow-right-24: Ler](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg .middle } __Saúde e métricas__

    ---

    Monitore liveness, readiness e métricas do Arquivo do Prometheus ou outro sistema de monitoramento.

    [:octicons-arrow-right-24: Ler](Monitoring.md)

-   :material-tag:{ .lg .middle } __Versões de desenvolvimento__

    ---

    Alterne para uma tag de dev para testar mudanças futuras.

    [:octicons-arrow-right-24: Ler](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Esta documentação está sendo continuamente expandida.**
Se você notar um erro ou tiver uma sugestão, por favor
[abra uma issue](https://github.com/s1t5/mail-archiver/issues) ou envie um pull request.
</div>
