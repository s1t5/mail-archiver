# Arquivamento de instâncias Mailcow {#mailcow-instance-archiving}

[← Voltar para o Índice da Documentação](index.md)

## Visão Geral {#overview}

O Mail Archiver pode arquivar as Caixas de correio de uma instância [Mailcow](https://mailcow.email)
centralmente, sem coletar as senhas pessoais dos proprietários das Caixas de correio.

Um administrador conecta a instância uma vez com sua chave de API. O Mail Archiver então lista
as Caixas de correio e, para cada Caixa de correio selecionada para arquivamento, cria uma **senha de app dedicada** restrita a `imap_access`. Essa senha de app é usada pelo provedor IMAP
existente para arquivar a Caixa de correio e pode ser revogada novamente a qualquer momento.

Os administradores acessam o gerenciador de instâncias através de **Add Account → Mailcow** ou do
menu de ações de uma Conta que já é gerenciada pelo Mailcow. O gerenciador é exclusivo para
administradores, pois a chave de API armazenada pode gerenciar credenciais para cada Caixa de correio
na instância.

## Criando contas a partir do Mailcow {#creating-accounts-from-mailcow}

Em **Mail Accounts → Add Account**, os administradores podem selecionar **Mailcow** como o provedor.
O formulário então oferece as instâncias conectadas e uma ação **Manage mailboxes**. Selecionar
Caixas de correio ali cria contas de arquivo IMAP comuns com senhas de app dedicadas; nenhuma
Conta Mailcow separada é armazenada. Se nenhuma instância existir ainda, **Manage Mail-cow instances**
abre o gerenciador de instâncias onde uma pode ser conectada.

## Vinculando uma conta existente {#linking-an-existing-account}

Na visualização de edição de uma conta IMAP, os administradores podem alterar o provedor para **Mailcow**
e escolher uma instância. A conta mantém o provedor IMAP, mas é adotada pelo gerenciamento do
Mailcow: o Mail Archiver cria uma senha de app dedicada, a verifica via IMAP e
substitui a senha armazenada da conta. A conta então aparece com uma ação Mailcow na
lista de contas.

Para deixar o gerenciamento do Mailcow, selecione outro provedor e salve. A senha de app é
revogada e a senha local é limpa. Ao alternar de volta para IMAP, insira uma nova senha IMAP;
alternar para Microsoft 365 ou uma conta apenas de importação fornece suas próprias credenciais.
Não-administradores ainda podem editar as configurações de não-credenciais de uma
Conta gerenciada pelo Mailcow, mas não podem vinculá-la ou desvinculá-la.

## Pré-requisitos {#prerequisites}

### A criptografia de credenciais deve estar habilitada {#credential-encryption-must-be-enabled}

O Mail Archiver armazena a chave de API do Mailcow e cada senha de app gerada criptografada em
repouso usando o recurso de criptografia de credenciais existente. Defina

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

antes de conectar uma instância. A criação de uma instância e a ativação de Caixas de correio são
recusadas enquanto a criptografia estiver desabilitada, para que as credenciais nunca sejam escritas como texto simples através deste recurso. Mantenha a chave fora do banco de dados e inclua-a em seu Backup seguro — sem ela, as senhas de app armazenadas não podem ser descriptografadas e as Caixas de correio não podem ser provisionadas. Veja [Setup](Setup.md#-security-settings) para detalhes.

### Acesso à API do Mailcow {#mailcow-api-access}

Crie uma chave de API no Mailcow em **Configuration → Access → API**. A chave deve ter
permissão para

- listar Caixas de correio (`get/mailbox/all`),
- listar, criar e deletar senhas de app (`get/app-passwd/all`, `add/app-passwd`,
  `delete/app-passwd`).

Restrinja a API ao endereço IP do host do Mail Archiver nas configurações de API do Mailcow.
O endpoint de app-password do Mailcow requer que o recurso **App Passwords** esteja habilitado para
a instância.

### Acesso de rede {#network-access}

- **HTTPS** para a origem do Mailcow (por exemplo `https://mail.example.com`).
- **IMAP over TLS** para o host IMAP, normalmente porta `993`. IMAP em texto simples não é suportado
  para provisionamento.

## Uso passo a passo {#step-by-step-usage}

### Conectando uma instância {#connecting-an-instance}

1. Faça login no Mail Archiver como administrador.
2. Abra **Mail Accounts** e clique em **Mailcow instances**.
3. Clique em **Connect Mailcow instance**.
4. Insira os detalhes de conexão:
   - **Account name** — um nome de exibição para a instância no Mail Archiver.
   - **Mailcow URL** — apenas a origem HTTPS, por exemplo `https://mail.example.com`.
     Caminhos, query strings e credenciais incorporadas são rejeitados.
   - **API key** — a chave criada no Mailcow.
   - **IMAP host** — o hostname usado para arquivamento, por exemplo `mail.example.com`.
     Insira apenas o hostname, sem esquema ou porta.
   - **IMAP TLS port** — padrão é `993`.
5. Clique em **Connect**. O Mail Archiver verifica a chave de API listando as Caixas de correio antes de
   salvar a instância. Em caso de sucesso, ele abre a seleção de Caixas de correio.

A chave de API é armazenada criptografada e nunca é exibida de volta no formulário ou em uma mensagem
de erro. Redirecionamentos HTTP estão desabilitados para chamadas de API para que a chave não possa ser
encaminhada para outra origem.

### Ativando Caixas de correio {#activating-mailboxes}

1. Na página da Caixa de correio, opcionalmente filtre a lista com o campo de busca. O filtro
   corresponde ao endereço e ao nome de exibição e não diferencia maiúsculas de minúsculas.
2. Selecione as Caixas de correio para arquivar. **Select all** e **Clear selection** afetam apenas
   as Caixas de correio atualmente visíveis (filtradas), portanto, filtrar é uma maneira segura de escolher um
   subconjunto.
3. Clique em **Activate archiving**.

Para cada Caixa de correio selecionada, o Mail Archiver:

1. re-valida o endereço contra a lista atual de Caixas de correio do Mailcow e ignora Caixas de correio
   desabilitadas,
2. cria uma conta de arquivo chamada `{instance name} – {mailbox display name}`,
3. gera uma senha de app aleatória chamada `MailArchiver-<unique id>` restrita a
   `imap_access`,
4. verifica a credencial com um login IMAP via TLS,
5. e só então habilita a Sincronização.

Se a verificação IMAP falhar, a conta permanece desabilitada e pode ser tentada novamente.

### Badges de status da Caixa de correio {#mailbox-status-badges}

A página de seleção rotula cada Caixa de correio para que fique claro por que uma caixa de seleção está disponível ou não:

| Badge | Significado | Selecionável |
| --- | --- | --- |
| **Available** | Listada no Mailcow, ainda não provisionada | sim |
| **Active** | Provisionada e sincronizando | não |
| **Pending** | Provisionamento iniciado mas não finalizado (repetível) | sim |
| **Revoked** | A senha de app foi revogada; pode ser ativada novamente | sim |
| **Existing** | Uma conta IMAP independente com este endereço já existe | não |
| **Disabled** | A Caixa de correio está desabilitada no Mailcow | não |

### Tentando novamente uma Caixa de correio pendente {#retrying-a-pending-mailbox}

A credencial gerada e o nome único da senha de app são persistidos **antes** de a
API do Mailcow ser chamada. Se uma requisição falhar de forma ambígua — por exemplo, um timeout após a
senha de app ter sido criada — a Caixa de correio permanece visível como **Pending**. Recarregue a página e
selecione-a novamente: o Mail Archiver procura o nome da app primeiro e só cria uma nova senha de
app se nenhuma existir. Tentar novamente não acumula credenciais duplicadas.

O provisionamento e a revogação para uma instância são serializados usando locks de aconselhamento do PostgreSQL,
para que duas operações simultâneas não possam provisionar a mesma Caixa de correio duas vezes.

### Revogando acesso {#revoking-access}

Clique em **Revoke app password** ao lado de uma Caixa de correio. Isso

- desabilita a Sincronização futura,
- deleta a senha de app gerenciada no Mailcow,
- e limpa a senha local.

**O e-mail arquivado é mantido.** Apenas a senha de app gerenciada é removida — outras senhas de
app pertencentes à Caixa de correio nunca são tocadas. Operações de Sincronização já em execução podem terminar com sua conexão existente.

Se a requisição remota falhar, a conta permanece desabilitada e a revogação pode ser tentada novamente.

Deletar uma conta de arquivo em **Mail Accounts** não remove silenciosamente a credencial remota.
O vínculo permanece listado em **Remaining app passwords** na página da instância para que possa ser revogado explicitamente.

### Editando uma instância {#editing-an-instance}

Abra **Mail de Mailcow instances** e clique em **Edit**. O campo da chave de API está sempre vazio — a
chave armazenada nunca é exibida. Deixe vazio para manter a chave atual, ou insira uma nova para
substituí-la. Os detalhes de conexão alterados são verificados contra o Mailcow antes de salvar.

### Deletando uma instância {#deleting-an-instance}

Abra **Mail de Mailcow instances** e clique em **Delete**.

Deletar uma instância remove sua chave de API criptografada e seus vínculos de Caixa de correio. **Contas de
arquivo e seus e-mails arquivados são mantidos**, mas as contas são desabilitadas.

A exclusão é **recusada enquanto qualquer senha de app gerenciada ainda estiver ativa** no Mailcow:
remover a instância deletaria a chave de API necessária para revogá-la. Revogue todas as senhas de app
gerenciadas na página da Caixa de correio primeiro.

## Registro de auditoria {#audit-logging}

Cada operação administrativa é escrita no log de acesso com o usuário que agiu:

- conectando uma instância,
- atualizando uma instância,
- deletando uma instância,
- ativando Caixas de correio (com o número provisionado e o número que falhou),
- revogando uma senha de app.

## Limite de Caixa de correio {#mailbox-limit}

Uma única execução de ativação pode provisionar no máximo `Mailcow__MaxSelectedMailboxes` Caixas de correio
(padrão `1000`). Isso protege contra provisionamento em massa acidental e carga excessiva de
API do Mailcow e IMAP. Para instâncias maiores, execute a operação várias vezes.

Cada Caixa de correio requer várias idas e voltas de API mais uma verificação IMAP, e elas são
processadas sequencialmente. Provisionar centenas de Caixas de correio em uma única requisição pode exceder
os timeouts de proxy reverso ou do navegador; divida a seleção em lotes.

## Notas de segurança {#security-notes}

- A chave de API permanece no lado do servidor. Ela é enviada como um cabeçalho `X-API-Key`, ocultada dos
  logs e nunca retornada ao navegador.
- As respostas de app-password do Mailcow podem ecoar senhas em texto simples em seus campos `log`.
  O Mail Archiver deliberadamente não registra corpos de resposta e não expõe erros de API ao navegador;
  falhas são reportadas como mensagens localizadas.
- Uma resposta HTTP 200 não é tratada como sucesso: mutações exigem o `type: success` por resultado do Mailcow.
- A Permissão IMAP não é intrinsecamente apenas de leitura. Contas provisionadas não têm
  política de deleção no lado do servidor configurada, e os administradores mantêm o controle das
  configurações da conta. Este recurso arquiva mensagens, não a configuração do Mailcow ou Backups.

### Hostnames e redes privadas {#hostnames-and-private-networks}

A URL do Mailcow e o host IMAP são inseridos por um administrador e podem apontar para
espaços de endereçamento privados. Isso é intencional e suportado: o Mail Archiver é comumente
implantado ao lado do servidor de e-mail, então `mail.example.lan`, `192.168.10.20` ou `10.0.0.5`
são valores normais. **Nenhum endereço privado, de loopback ou local-link é bloqueado.**

O limite de confiança é a função de administrador, não o endereço de rede. `Mail Accounts`
está disponível para auto-gerenciadores, que já podem configurar um servidor IMAP arbitrário para
suas próprias contas; a seção Mailcow é restrita a administradores, e um
administrador já tem acesso irrestrito a cada conta de arquivo na
instalação. Restringir o host IMAP, portanto, não fecharia um limite de privilégio,
enquanto quebraria a implantação on-premise pretendida.

Duas restrições ainda se aplicam:

- A URL do Mailcow deve ser uma origem HTTPS pura. Caminhos, query strings, informações de usuário e
  redirecionamentos são rejeitados, portanto a chave de API não pode ser encaminhada para outra origem.
- O host IMAP deve ser um hostname sem esquema ou porta, e o provisionamento sempre
  conecta com TLS (`SslOnConnect`).

Se você precisar restringir quais hosts o Mail Archiver pode alcançar, aplique isso na camada de rede — regras de firewall de saída ou uma política de DNS para o container — em vez de confiar na validação de entrada na aplicação.

## Solução de problemas {#troubleshooting}

| Sintoma | Causa e remédio |
| --- | --- |
| *Encryption required* ao conectar ou ativar | `Security__CredentialEncryptionKey` não está definido ou é inválido. Configure uma chave de 32 bytes codificada em base64 e reinicie. |
| *HTTPS origin required* | A URL tem um caminho, query string ou credenciais incorporadas. Insira a origem pura, ex: `https://mail.example.com`. |
| *IMAP host invalid* | O host IMAP deve ser um hostname sem esquema ou porta. |
| *Instance already connected* | Outra instância com a mesma URL já existe. Edite essa instância em vez disso. |
| *API request failed (401/403)* | A chave de API carece das permissões necessárias, ou a restrição de IP não inclui o host do Mail Archiver. |
| *API request rejected* | O Mailcow retornou uma linha `error`/`danger`/`warning`. Verifique os logs do Mailcow; o Mail Archiver não encaminha corpos de resposta. |
| *IMAP verification failed* | Host ou porta IMAP incorretos, um certificado não confiável, ou o recurso de app-password está desabilitado no Mailcow. |
| *IMAP account exists* | Uma conta IMAP independente já arquiva este endereço. Use essa conta em vez de criar um segundo arquivo. |
| *Revoke previous password* | A conta de arquivo para esta Caixa de correio foi deletada. Revogue a senha de app restante primeiro, depois ative novamente. |
| Caixa de correio permanece **Pending** | A API do Mailcow sofreu timeout. Recarregue a página e ative a Caixa de correio novamente; nenhuma senha de app duplicada é criada. |
| Deletar é recusado | Senhas de app gerenciadas ainda estão ativas. Revogue-as na página da Caixa de correio primeiro. |

## Endpoints de API usados {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Mudanças no banco de dados {#database-changes}

A migração adiciona apenas as tabelas `MailcowInstances` e `MailcowMailboxes` junto
com seus índices e chaves estrangeiras. As tabelas de Caixa de correio existentes e os dados arquivados são
mantidos. Remover uma conta de arquivo define o `MailAccountId` do vínculo para `NULL` para que a
credencial remota ainda possa ser revogada depois.
