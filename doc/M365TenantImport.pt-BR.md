# Importação de Caixa de correio do Locatário Microsoft 365 {#microsoft-365-tenant-mailbox-import}

[← Voltar para o Índice de Documentação](index.md)

## Visão Geral {#overview}

O Mail Archiver pode criar contas de e-mail do Microsoft 365 para mais de uma Caixa de correio no mesmo locatário a partir de um único formulário de criação. Isso é útil ao integrar um cliente ou organização onde todas as Caixas de correio do Microsoft 365, ou um subconjunto escolhido, devem ser arquivadas.

A importação de locatário utiliza as credenciais de aplicativo do Microsoft Graph configuradas no formulário e cria uma conta de e-mail do Mail Archiver por Caixa de correio importada.

## Pré-requisitos {#prerequisites}

Antes de usar a importação de locatário, conclua a configuração de registro de aplicativo do Microsoft 365 descrita no [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md).

Você precisa destes valores do Microsoft Entra ID:

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

O registro do aplicativo deve ter **permissões de aplicativo** do Microsoft Graph que permitam ao Mail Archiver ler as Caixas de correio do locatário. Além das permissões necessárias para o arquivamento regular de Conta M365, a seguinte permissão é necessária para a importação de locatário:

- **User.Read.All** – necessária para listar os usuários do locatário e seus endereços de Caixa de correio.

A importação de locatário é selecionada como o provedor **Microsoft 365 (tenant)** no formulário de criação.

### Filtragem {#filtering}

A importação de locatário e o Tenant Management excluem automaticamente:
- **Guest accounts** (`userType == "Guest"`) — usuários externos convidados sem suas próprias Caixas de correio no locatário.
- **Usuários sem uma licença do Exchange Online** — apenas usuários com um plano de serviço Exchange ativo em seu `assignedPlans` são listados. Isso evita a importação de contas que não podem ser arquivadas.

## Modos de Importação {#import-modes}

A importação de locatário suporta dois modos:

### Importar todas as Caixas de correio listadas {#import-all-listed-mailboxes}

Use este modo quando cada Caixa de correio retornada pelo Microsoft Graph deve ser criada como uma conta do Mail Archiver.

- O Mail Archiver carrega a lista de Caixas de correio do locatário do Microsoft Graph.
- Contas de e-mail M365 já existentes são ignoradas.
- Se **Skip disabled mailboxes** estiver ativado, usuários desativados não são incluídos na lista de Caixas de correio.

### Importar apenas Caixas de correio selecionadas {#import-selected-mailboxes-only}

Use este modo quando apenas Caixas de correio específicas no locatário devem ser arquivadas.

- Carregue a lista de Caixas de correio do locatário primeiro.
- Desative **Import all listed mailboxes**.
- Selecione as Caixas de correio que devem ser importadas.
- Envie o formulário para criar contas apenas para as Caixas de correio selecionadas.

## Uso Passo a Passo {#step-by-step-usage}

1. Faça login no Mail Archiver como um usuário que pode criar contas de e-mail.
2. Abra **Mail Accounts** > **Create**.
3. Selecione **Microsoft 365 (tenant)** como o provedor.
4. Insira as credenciais do Microsoft 365:
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Insira o prefixo **Account name** que deve ser usado para as contas geradas.
6. Escolha se as Caixas de correio desativadas devem ser ignoradas.
7. Clique em **Load mailboxes** para listar as Caixas de correio do locatário.
8. Escolha o comportamento de importação desejado:
   - Mantenha **Import all listed mailboxes** ativado para importar todas as Caixas de correio listadas.
   - Desative-o e selecione Caixas de correio individuais para importar apenas Caixas de correio específicas.
9. Clique em **Save**.

Após a criação, o Mail Archiver redireciona de volta para a lista de contas de e-mail e mostra quantas contas de locatário foram importadas e quantas contas existentes foram ignoradas.

## Nomeação de Conta {#account-naming}

Para importações de locatário, o Mail Archiver usa o valor inserido em **Account name** como um prefixo para cada conta de Caixa de correio criada.

Exemplo:

- Nome de conta inserido: `Test GmbH`
- Caixa de correio do locatário: `mailbox@example.com`
- Nome de conta criado: `Test GmbH - <mailbox@example.com>`

Isso facilita a identificação de contas importadas que pertencem ao mesmo locatário ou cliente.

## Contas Existentes {#existing-accounts}

Se uma Caixa de correio já existir como uma conta do Microsoft 365 no Mail Archiver, a importação de locatário a ignora em vez de criar uma duplicata. A detecção de conta existente é baseada no endereço de e-mail da Caixa de correio e no provedor M365.

A lista de Caixas de correio também pode mostrar se uma Caixa de correio já existe antes de enviar a importação.

## Caixas de correio desativadas {#disabled-mailboxes}

A caixa de seleção **Skip disabled mailboxes** controla se os usuários desativados do locatário devem ser excluídos da lista de Caixas de correio.

- Ativado: apenas usuários do locatário ativados são listados e importados.
- Desativado: usuários desativados do locatário podem aparecer na lista e podem ser importados se selecionados ou se todas as Caixas de correio listadas forem importadas.

## Tenant Management {#tenant-management}

Além da importação de locatário no formulário de criação, o Mail Archiver fornece uma página dedicada de **Tenant Management** para administradores. Ela é acessível a partir do índice de Mail Accounts através do botão **Tenant Management** (ícone de edifício) ao lado de qualquer conta do Microsoft 365.

O Tenant Management é útil para adicionar Caixas de correio que não foram importadas inicialmente — por exemplo, quando novas Caixas de correio são criadas no locatário após a integração inicial, ou quando contas foram ignoradas originalmente.

### Como ele difere da importação de locatário no formulário de criação {#how-it-differs-from-the-create-form-tenant-import}

- As credenciais do Microsoft 365 (Client ID, Client Secret, Tenant ID) **não são inseridas novamente**. Elas são obtidas da conta de origem na qual o botão foi clicado.
- A lista de Caixas de correio do locatário é carregada automaticamente quando a página abre (sem botão "Load mailboxes").
- Caixas de correio já importadas são exibidas com um selo "already exists" e não podem ser selecionadas novamente.
- Apenas administradores podem acessar o Tenant Management.

### Uso Passo a Passo {#step-by-step-usage}

1. Faça login no Mail Archiver como um administrador.
2. Abra **Mail Accounts**.
3. Localize a conta do Microsoft 365 cujo locatário você deseja gerenciar e clique no botão **Tenant Management** ao lado dela.
4. A página carrega e lista todas as Caixas de correio retornadas pelo Microsoft Graph para o locatário da conta de origem.
   - Caixas de correio que já existem como contas M365 no Mail Archiver são marcadas com um selo "already exists" e não podem ser selecionadas.
   - Usuários desativados do locatário não são listados; o Tenant Management sempre ignora Caixas de correio desativadas.
5. Selecione as Caixas de correio que você deseja adicionar.
6. Insira o prefixo **Account name** que deve ser usado para as novas contas (o esquema de nomeação é `{prefix} - <{email}>`, idêntico à importação de locatário no formulário de criação).
7. Opcionalmente, configure **Delete After Days** e **Local Retention Days** para as novas contas.
8. Clique em **Add selected mailboxes**.

Após a criação, o Mail Archiver redireciona para a lista de contas de e-mail e mostra quantas Caixas de correio foram adicionadas.

### Pesquisando Caixas de correio {#searching-mailboxes}

Para grandes locatários com vários milhares de Caixas de correio, um campo de pesquisa acima da lista de Caixas de correio permite filtrar por nome de exibição ou endereço de e-mail. O filtro não diferencia maiúsculas de minúsculas e é aplicado instantaneamente enquanto você digita. **Select all** e **Clear selection** operam apenas nas Caixas de correio atualmente visíveis (filtradas), portanto, a filtragem é uma maneira segura de selecionar um subconjunto específico sem afetar as entradas ocultas.

### Renomeando contas existentes {#renaming-existing-accounts}

A caixa de seleção **Rename existing accounts** aplica o esquema de nomeação `{prefix} - <{email}>` a todas as contas M365 que compartilham o mesmo registro de aplicativo (Client ID e Tenant ID) que a conta de origem. Isso é útil para normalizar nomes de conta após uma importação inicial onde os nomes estavam inconsistentes.

Quando marcada, salvar o formulário renomeia cada conta correspondente — incluindo a conta de origem — independentemente de novas Caixas de correio serem selecionadas para importação. Você pode usar esta opção sozinha (sem selecionar nenhuma nova Caixa de correio) para normalizar nomes sem adicionar contas.

### Limite de Caixa de correio {#mailbox-limit}

Uma única operação de Tenant Management pode adicionar no máximo `TenantManagement__MaxSelectedMailboxes` Caixas de correio de uma vez (padrão `1000`). Isso protege contra importações em massa acidentais e carga excessiva no Microsoft Graph / banco de dados. Se precisar importar mais Caixas de correio, execute a operação várias vezes. Consulte o [Setup Guide](Setup.md#-tenant-management-settings) para saber como ajustar este limite.

### Permissões necessárias {#permissions-required}

O Tenant Management usa as mesmas permissões de aplicativo do Microsoft Graph que a importação de locatário no formulário de criação. O registro de aplicativo da conta de origem deve ter:

- As permissões necessárias para o arquivamento regular de conta M365, **e**
- **User.Read.All** — necessária para listar os usuários do locatário e seus endereços de Caixa de correio.

Consulte o [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) para a configuração inicial.

### Registro de auditoria {#audit-logging}

Cada visualização de página do Tenant Management e cada operação de adição é registrada no log de acesso, incluindo o número de Caixas de correio listadas ou adicionadas e a conta de origem utilizada.
