# 🔐 Autenticação de Dois Fatores {#two-factor-authentication}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

A autenticação de dois fatores (2FA) adiciona uma segunda etapa ao processo de login: após a sua senha, o Mail Archiver solicita um **código de 6 dígitos** gerado por um aplicativo autenticador no seu telefone. Uma senha roubada sozinha não é mais suficiente para acessar o Arquivo.

O Mail Archiver implementa 2FA usando **TOTP** (Time-based One-Time Password, RFC 6238) — o mesmo padrão usado pelo Google Authenticator, Microsoft Authenticator, Authy, 1Password, Bitwarden e outros. Nenhum serviço externo está envolvido; o segredo nunca sai da sua instância.

**O 2FA é opt-in por usuário.** Ele é configurado por cada usuário para sua própria Conta, não globalmente por um administrador.

## 🛠️ Pré-requisitos {#prerequisites}

- Um aplicativo autenticador compatível com TOTP no seu telefone ou desktop
- Acesso à sua própria Conta (você deve estar logado)
- **Uma Conta (senha) local** — veja a nota sobre OIDC abaixo

> **⚠️ Não disponível para usuários OIDC.** Se você fizer login por meio de um provedor de identidade externo (Entra ID, Authelia, Keycloak, …), o 2FA é gerenciado por esse provedor e a página de configuração é bloqueada com a mensagem *"OIDC users cannot enable 2FA. Authentication is managed by your OIDC provider."* Ative a autenticação de múltiplos fatores em seu provedor de identidade em vez disso — veja [OIDC / SSO](OIDC_Implementation.md).

## 🚀 Habilitando a Autenticação de Dois Fatores {#enabling-two-factor-authentication}

### Passo 1 — Abra a página de configuração {#step-1-open-the-setup-page}

1. Faça login no Mail Archiver
2. Abra o menu da sua Conta e escolha **Two-Factor Authentication**
3. Você chegará em **Two-Factor Setup**

### Passo 2 — Escaneie o código QR {#step-2-scan-the-qr-code}

A página mostra um código QR. Abra seu aplicativo autenticador, escolha *Add account* → *Scan QR code* e aponte a câmera para ele.

**Não consegue escanear o código?** Use a **Entrada Manual** em vez disso: a página exibe o segredo como texto (`If you can't scan the QR code, enter this secret manually in your authenticator app`). Digite-o em seu aplicativo como uma conta baseada em tempo/TOTP.

> **ℹ️ O segredo ainda não foi salvo.** Ele é mantido apenas durante a duração da configuração em sua sessão do navegador. O 2FA torna-se ativo apenas após o sucesso do passo 3 — se você abandonar a configuração, nada muda em sua Conta.

### Passo 3 — Confirme com um código {#step-3-confirm-with-a-code}

Insira o código de 6 dígitos que seu aplicativo mostra agora e clique em **Enable Two-Factor Authentication**.

Se o código for rejeitado, a página de configuração é exibida novamente com um novo código QR e a mensagem *"Invalid authentication code. Please try again."* Verifique se o relógio do seu telefone está configurado automaticamente — o TOTP depende de um tempo preciso.

### Passo 4 — Salve seus códigos de backup {#step-4-save-your-backup-codes}

Com sucesso, você será levado à página **Backup Codes**. Ela mostra **10 códigos**, cada um com 16 caracteres.

> **⚠️ Esta é a única vez que os códigos são exibidos.** Eles são armazenados como hash — nem você nem um administrador podem exibi-los novamente mais tarde. Use **Download Backup Codes** para salvá-los em um arquivo ou copie-os para seu gerenciador de senhas antes de sair da página.

O download contém este lembrete:

> *Mail Archiver - Two-Factor Authentication Backup Codes. Store these backup codes in a safe place. You can use them to log in if you lose access to your authenticator app. Each backup code can only be used once. After using a code, it will be invalidated.*
> *Importante: Mantenha esses códigos seguros e não os compartilhe com ninguém.*

## 🔑 Fazendo Login com 2FA {#logging-in-with-2fa}

Após inserir sua senha, você será levado para a **Two-Factor Verification** e solicitado um código:

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character backup code*

Ambos funcionam no mesmo campo — o Mail Archiver os distingue pelo comprimento:

| Entrada | Comprimento | Comportamento |
|---|---|---|
| Código do seu aplicativo autenticador | 6 dígitos | Verificado contra a janela de tempo atual |
| Código de backup | 16 caracteres | Verificado contra seus códigos armazenados, então é **invalidado** |

Para aceitar pequenas diferenças de relógio, códigos do passo de tempo imediatamente anterior e seguinte também são aceitos.

**"Remember me"** é transferido do formulário de login: se você o marcou, você permanece logado entre reinicializações do navegador após concluir a etapa de 2FA. A seleção é mantida na sessão do lado do servidor enquanto a etapa de 2FA está pendente, portanto, ela só sobrevive se você inserir o código dentro do tempo de inatividade de **60 minutos** da sessão. Detalhes e limites (tempo de vida absoluto de 7 dias, sem efeito para usuários OIDC) estão em [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).

### Usando um código de backup {#using-a-backup-code}

1. Insira um de seus códigos salvos de 16 caracteres no mesmo campo
2. O código é aceito e **imediatamente invalidado** — ele não pode ser reutilizado
3. Logins bem-sucedidos com um código de backup são registrados no log de acesso com a nota `(2FA Backup Code)`, para que você possa detectar usos incomuns mais tarde

### Limite de taxa (Rate limiting) {#rate-limiting}

Para retardar tentativas de força bruta, a verificação de 2FA é limitada a **5 tentativas por 15 minutos**, contadas por endereço IP e nome de usuário. Depois disso, você verá:

> **Too Many Attempts** — *You have made too many 2FA verification attempts. Please wait and try again later.*

Aguarde o tempo passar e tente novamente. O limite se aplica a tentativas falhas *e* bem-sucedidas dentro da janela.

## 🚫 Desabilitando a Autenticação de Dois Fatores {#disabling-two-factor-authentication}

1. Abra **Two-Factor Authentication** no menu da sua Conta
2. A página solicita confirmação: *"Are you sure you want to disable two-factor authentication? This will reduce the security of your account."*
3. Insira sua **senha** — esta etapa requer sua senha atual, não um código 2FA
4. Clique em **Disable Two-Factor Authentication**

Desabilitar remove o segredo e invalida todos os códigos de backup restantes. Seu próximo login precisará apenas da senha.

## 🔄 Perdeu seu Autenticador? Duas formas de voltar {#lost-your-authenticator-two-ways-back-in}

### Se você ainda tem códigos de backup {#if-you-still-have-backup-codes}

Faça login com um código de backup como descrito acima, então desabilite e reabilite o 2FA com seu novo dispositivo. Lembre-se de salvar os novos códigos de backup.

### Se você não tem mais códigos de backup {#if-you-have-no-backup-codes-left}

Um **administrador** deve resetar o 2FA para sua Conta:

1. O administrador abre **Users**
2. Edita o usuário afetado
3. Escolhe **Reset Two-Factor Authentication** e confirma:
   *"Are you sure you want to reset two-factor authentication for this user? They will need to set it up again."*
4. O usuário pode então fazer login apenas com sua senha e configurar o 2FA novamente

O reset limpa o segredo e os códigos de backup. A mensagem de confirmação *"Two-factor authentication has been successfully reset for user …"* aparece na lista de Users.

> **ℹ️ Escopo do reset.** O reset cobre apenas o 2FA. Não é um reset de senha — veja [Emergency Account Recovery](EmergencyAccountRecovery.md) se a senha for o problema, ou se a Conta estiver totalmente bloqueada.

## 👁️ O que os Administradores podem ver {#what-administrators-can-see}

A visão geral de **Users** mostra uma coluna de 2FA:

- *"Two-factor authentication is currently enabled for this user."* — O 2FA está ativo
- Caso contrário, o 2FA está desligado para aquele usuário

Administradores podem resetar o 2FA (acima), mas **não podem** ler o segredo ou os códigos de backup — ambos são armazenados como hash. Um usuário que perde seu dispositivo sem códigos de backup não tem outra forma de voltar além do reset do admin.

## 🔍 Trilha de Auditoria {#audit-trail}

A atividade de 2FA é escrita no log e, para logins bem-sucedidos, no log de acesso:

| Evento | Onde | Entrada |
|---|---|---|
| Login via aplicativo autenticador | Access log | `Login` com nota `IP: <address> (2FA)` |
| Login via código de backup | Access log | `Login` com nota `IP: <address> (2FA Backup Code)` |
| Tentativa de 2FA falha | Application log | Warning com username, IP e token mascarado |
| Código de backup inválido | Application log | Warning com username e IP |

A entrada no log de acesso torna o uso de códigos de backup visível após o fato — útil para detectar um código sendo usado por alguém que não deveria tê-lo. Veja [Access Logging](Logs.md) para saber como ler essas entradas.

## ⚙️ Como funciona por baixo dos panos {#how-it-works-under-the-hood}

Para administradores e revisores:

- **Padrão**: TOTP (RFC 6238), segredo aleatório de 20 bytes, codificado em base32
- **Janela de verificação**: ±1 passo de tempo, para tolerar desvios de relógio
- **Códigos de backup**: 10 códigos, 16 caracteres cada, gerados a partir de um RNG criptográfico, armazenados como **hash**, invalidados individualmente ao uso
- **Armazenamento do segredo**: no registro do usuário; nunca retornado ao navegador após a configuração
- **Manuseio de sessão**: a sessão é regenerada antes que a identidade 2FA seja armazenada, para que um cookie de sessão plantado antes do login não possa ser levado para a sessão autenticada
- **Contas OIDC**: o gerenciamento de 2FA é bloqueado; o provedor é a autoridade
- **API keys**: ignoram o 2FA por design, pois tokens não interativos não podem responder a um desafio — veja a justificativa de segurança no [REST API guide](API.md)

## ❓ Solução de Problemas {#troubleshooting}

| Sintoma | Causa | Correção |
|---|---|---|
| *"Invalid authentication code"* | Relógio do telefone incorreto, ou conta errada no aplicativo | Ative a Sincronização automática de tempo; verifique se o aplicativo mostra a entrada do Mail Archiver |
| *"Two-factor secret is missing. Please restart the setup process."* | A página de configuração ficou aberta por muito tempo, a sessão expirou | Inicie a configuração novamente a partir do menu da Conta |
| *"Your session has expired. Please log in again."* | Passou muito tempo na página de verificação | Faça login novamente |
| *"Two-factor authentication is not enabled for this account."* | O 2FA foi resetado ou nunca foi concluído | Configure-o novamente, ou peça a um administrador |
| Código de backup rejeitado | Código já usado, ou caractere lido incorretamente | Códigos de backup funcionam **uma vez**; use outro, ou peça o reset do 2FA |
| *"Too Many Attempts"* | Mais de 5 tentativas de verificação em 15 minutos | Aguarde o tempo da janela |
| Sem página de configuração no menu da Conta | Você faz login via OIDC | Ative o MFA em seu provedor de identidade |
| Sem coluna de 2FA na lista de Users | Você não é um administrador | Peça a um administrador |

## 📚 Documentação Relacionada {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — criando Contas, direitos de admin
- [OIDC / SSO Authentication](OIDC_Implementation.md) — login via provedores externos
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — Contas bloqueadas
- [Access Logging](Logs.md) — lendo o Registro de auditoria
- [REST API](API.md) — por que as API keys ignoram o 2FA
