# 📧 Mail Archiver Documentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Arquivamento de e-mail auto-hospedado — arquive, pesquise e exporte e-mails de qualquer provedor.**
Configure uma vez, mantenha cada mensagem e encontre-a novamente anos depois.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Primeiros passos__

    ---

    Instale o Mail Archiver com Docker Compose e conecte sua primeira Caixa de correio.

    [:octicons-arrow-right-24: Setup and parameters](Setup.md)

-   :material-magnify:{ .lg .middle } __Encontre qualquer coisa__

    ---

    Busca de texto completo em cada Conta, com filtros, ordenação e ações em massa.

    [:octicons-arrow-right-24: Mail search](Search.md)

-   :material-api:{ .lg .middle } __Automatize__

    ---

    Leia seu Arquivo a partir de scripts e agentes de IA — chaves de API de apenas leitura e com escopo definido.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Proteja__

    ---

    Autenticação de dois fatores, SSO, registro de acesso e políticas de Retenção.

    [:octicons-arrow-right-24: Two-factor auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Instalação & Manutenção {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup and parameters__

    ---

    Cada variável de ambiente, Kestrel HTTPS e gerenciamento de segredos.

    [:octicons-arrow-right-24: Read](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Coloque o Mail Archiver atrás de nginx ou Caddy com terminação TLS.

    [:octicons-arrow-right-24: Read](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL upgrade__

    ---

    Mude para uma nova versão principal do PostgreSQL sem perder dados.

    [:octicons-arrow-right-24: Read](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Database maintenance__

    ---

    VACUUM, ANALYZE e manter o Arquivo responsivo ao longo do tempo.

    [:octicons-arrow-right-24: Read](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Backup and restore__

    ---

    Faça o Backup do banco de dados e realize a Restauração — incluindo configurações Proxmox.

    [:octicons-arrow-right-24: Read](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Local test environment__

    ---

    Execute uma instância temporária localmente antes de tocar na produção.

    [:octicons-arrow-right-24: Read](LocalTestEnvironment.md)

</div>

## 📥 Recebendo E-mails {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Account import__

    ---

    Adicione muitas Contas de uma vez a partir de um arquivo CSV.

    [:octicons-arrow-right-24: Read](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Sincronização__

    ---

    Sincronização rápida vs. completa, checkpoints e o que acontece quando uma sincronização falha.

    [:octicons-arrow-right-24: Read](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI commands__

    ---

    Importe Arquivos existentes via linha de comando e mova mensagens originais armazenadas entre o banco de dados e o sistema de arquivos.

    [:octicons-arrow-right-24: Read](CLI.md)

-   :material-upload:{ .lg .middle } __Web import (EML / MBox)__

    ---

    Faça o upload de um arquivo MBox ou um ZIP de arquivos EML através do navegador.

    [:octicons-arrow-right-24: Read](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit handling__

    ---

    Rastreamento de largura de banda e como as sincronizações pausadas retomam por conta própria.

    [:octicons-arrow-right-24: Read](RateLimitHandling.md)

</div>

## 🔍 Trabalhando com o Arquivo {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail search__

    ---

    Sintaxe de consulta, filtros, ordenação, paginação e ações em massa.

    [:octicons-arrow-right-24: Read](Search.md)

-   :material-progress-clock:{ .lg .middle } __Background jobs__

    ---

    Monitore sincronizações, importações, restaurações e exportações — e cancele-as com segurança.

    [:octicons-arrow-right-24: Read](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Mailbox migration__

    ---

    Copie e-mails entre Caixas de correio preservando a estrutura de Pasta.

    [:octicons-arrow-right-24: Read](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Account export (EML / MBox)__

    ---

    Exporte uma Conta inteira como um Arquivo EML ou MBox portátil.

    [:octicons-arrow-right-24: Read](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Original message storage__

    ---

    Mantenha a mensagem como recebida, para que o DKIM ainda seja verificado e as exportações sejam byte a byte idênticas.

    [:octicons-arrow-right-24: Read](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Attachment deduplication__

    ---

    Como Anexos idênticos são armazenados apenas uma vez em vez de várias vezes.

    [:octicons-arrow-right-24: Read](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Date-windowed offload__

    ---

    Mova e-mails mais antigos para outra Conta, janela por janela.

    [:octicons-arrow-right-24: Read](Offload.md)

-   :material-harddisk:{ .lg .middle } __Per-account storage__

    ---

    Veja quanto espaço cada Caixa de correio ocupa.

    [:octicons-arrow-right-24: Read](AccountStorage.md)

</div>

## 🔐 Administração & Segurança {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __User management__

    ---

    Crie usuários, conceda direitos de administrador, atribua Permissões de Caixa de correio e altere sua própria senha.

    [:octicons-arrow-right-24: Read](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Two-factor authentication__

    ---

    Configuração de TOTP, códigos de backup e o que fazer quando um dispositivo é perdido.

    [:octicons-arrow-right-24: Read](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Faça login através do Entra ID, Authelia ou outro provedor de identidade.

    [:octicons-arrow-right-24: Read](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Access logging__

    ---

    Quem abriu, pesquisou, exportou ou deletou o quê.

    [:octicons-arrow-right-24: Read](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit data export__

    ---

    Exporte a trilha de auditoria para revisões de conformidade.

    [:octicons-arrow-right-24: Read](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Emergency account recovery__

    ---

    Recupere o acesso quando uma Conta estiver bloqueada.

    [:octicons-arrow-right-24: Read](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Retention policies__

    ---

    Exclusão automática do servidor de e-mail ou do Arquivo local.

    [:octicons-arrow-right-24: Read](RetentionPolicies.md)

</div>

## ☁️ Guias de Provedores {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app registration (M365)__

    ---

    Registre um app e configure a Retenção para Microsoft 365.

    [:octicons-arrow-right-24: Read](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant mailbox import__

    ---

    Importação em massa de cada Caixa de correio de um tenant a partir de um único formulário.

    [:octicons-arrow-right-24: Read](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instances__

    ---

    Selecione Caixas de correio centralmente e provisione senhas de app IMAP dedicadas.

    [:octicons-arrow-right-24: Read](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Personal Microsoft accounts__

    ---

    Outlook.com, Hotmail e M365 Family via device code flow.

    [:octicons-arrow-right-24: Read](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail best practices__

    ---

    Senhas de app, labels, expunge e os limites de taxa do Gmail.

    [:octicons-arrow-right-24: Read](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail limitations__

    ---

    O que os endpoints de IMAP e exportação do Yahoo não farão.

    [:octicons-arrow-right-24: Read](YahooMailExportLimitations.md)

</div>

## 🔌 Acesso Programático {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Acesso de apenas leitura ao Arquivo com chaves de API com escopo por usuário.

    [:octicons-arrow-right-24: Read](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Exponha o Arquivo para agentes de IA como ferramentas descobertas.

    [:octicons-arrow-right-24: Read](MCP.md)

</div>

## 🧰 Operações & Desenvolvimento {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Language and localization__

    ---

    Alterne o idioma da interface e como adicionar uma tradução.

    [:octicons-arrow-right-24: Read](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logs__

    ---

    Leia os logs do container e altere o nível de log.

    [:octicons-arrow-right-24: Read](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Development versions__

    ---

    Alterne para uma tag de dev para testar mudanças futuras.

    [:octicons-arrow-right-24: Read](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Esta documentação está sendo expandida continuamente.**
Se você encontrar um erro ou tiver uma sugestão, por favor
[open an issue](https://github.com/s1t5/mail-archiver/issues) ou envie um pull request.
</div>
