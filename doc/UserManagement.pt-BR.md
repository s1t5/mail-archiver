# 👥 Gerenciamento de Usuários e Permissões de Caixa de correio {#user-management-and-mailbox-permissions}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia fornece instruções detalhadas para criar novas Contas de usuário e atribuir permissões de Caixa de correio no aplicativo Mail Archiver. Também aborda a alteração de sua própria senha e a alteração forçada de senha em uma instância recém-instalada.


## 🛠️ Pré-requisitos {#prerequisites}

- Acesso administrativo ao aplicativo Mail Archiver
- Contas de e-mail existentes configuradas no sistema

## 🧑‍💻 Criando uma Nova Conta de Usuário {#creating-a-new-user-account}

1. Faça login no aplicativo Mail Archiver com uma Conta que tenha privilégios administrativos
2. Navegue até a seção "Users" no menu principal
3. Clique no botão "Create New User"
4. Preencha as informações de usuário necessárias:
   - **Username**: Insira um nome de usuário exclusivo para o novo usuário
   - **Password**: Insira uma senha segura para o usuário
   - **Email**: Endereço de e-mail do usuário
   - **Admin**: Marque esta caixa se o usuário deve ter privilégios administrativos
   - **Self Manager**: Marque esta caixa se o usuário deve ser capaz de gerenciar suas próprias contas de e-mail (editar as existentes e adicionar novas, bem como excluir contas)
   - **active**: Deve estar marcado para permitir logins para o usuário
5. Clique em "Create User" para salvar a nova Conta de usuário

## 🔐 Atribuindo Permissões de Caixa de correio aos Usuários {#assigning-mailbox-permissions-to-users}

1. Após criar o usuário, ou ao editar um usuário existente, navegue até a seção "Users" no menu principal
2. Encontre o usuário na lista onde você deseja atribuir contas de e-mail e clique no botão "Assign" para esse usuário
3. Na página "Assign Mail Accounts", você verá todas as contas de e-mail disponíveis e caixas de seleção que indicam se elas estão atribuídas ao usuário
5. Para atribuir uma nova conta de e-mail, marque a caixa correspondente à conta específica
6. Para remover uma conta de e-mail do usuário, desmarque a caixa
7. Clique em "Save Assignments" para aplicar as alterações

## 👤 Permissões de Conta de Usuário {#user-account-permissions}

### Usuários Admin {#admin-users}
Usuários Admin têm acesso total ao aplicativo e podem:
- Gerenciar todas as Contas de usuário e suas permissões
- Acessar todas as contas de e-mail no sistema
- Visualizar todos os logs de acesso
- Realizar todas as tarefas administrativas

### Usuários Self Manager {#self-manager-users}
Usuários Self manager podem:
- Adicionar novas contas de e-mail à sua própria Conta
- Editar contas de e-mail existentes atribuídas a eles
- Excluir contas de e-mail atribuídas a eles
- Acessar e gerenciar e-mails de suas contas atribuídas (incluindo exclusão)
- Visualizar seus próprios logs de acesso

### Usuários Standard {#standard-users}
Usuários Standard têm acesso limitado e podem:
- Visualizar e-mails arquivados de contas de e-mail atribuídas
- Pesquisar dentro de suas contas de e-mail atribuídas
- Exportar e-mails de suas contas atribuídas
- Realizar a Restauração de e-mails de suas contas atribuídas
- Acessar Anexos de e-mail de contas atribuídas

## 🔑 Alterando Sua Própria Senha {#changing-your-own-password}

Todo usuário com uma senha local pode alterá-la sem um administrador:

1. Clique no seu **username** no canto superior direito para abrir o menu de usuário.
2. Escolha **Change Password**.
3. Insira sua **current password** e a **new password** duas vezes e, em seguida, envie.

A nova senha deve atender aos requisitos abaixo e deve ser **diferente da atual**.

> ℹ️ **Usuários OIDC não podem alterar sua senha aqui.** A página responde com
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Altere a senha no seu provedor de identidade em vez disso —
> veja [OIDC / SSO](OIDC_Implementation.md).

### Requisitos de senha {#password-requirements}

- pelo menos **10 caracteres**
- pelo menos uma letra **maiúscula**
- pelo menos uma letra **minúscula**
- pelo menos um **número**
- pelo menos um **caractere especial**

O formulário mostra isso como uma lista de verificação ao vivo enquanto você digita. As mesmas regras se aplicam quando um administrador cria um usuário ou redefine uma senha.

## ⚠️ Alteração Forçada de Senha (Configuração Inicial) {#forced-password-change-initial-setup}

Uma instância recém-instalada começa com as credenciais do administrador da configuração (`Authentication__Username` / `Authentication__Password`). Enquanto essa senha padrão ainda estiver em uso **e nenhuma conta de e-mail existir ainda**, o primeiro login força uma alteração:

- Um banner de aviso explica o porquê: *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- Você não pode navegar para fora da página: *"You must change your password before
  you can continue using the system."*
- A nova senha deve ser diferente da atual.
- Após salvar, a flag é limpa e você é levado ao dashboard.

Depois que você alterar a senha padrão, a alteração forçada nunca mais aparecerá.

## 🕒 Permanecer Conectado — a Caixa de seleção "Remember Me" {#staying-signed-in-the-remember-me-checkbox}

O formulário de login possui uma caixa de seleção rotulada como **Remember Me** (*angemeldet bleiben* na interface em alemão). Ela faz exatamente uma coisa: decide se o cookie de autenticação é um cookie de sessão ou um persistente.

| | Caixa de seleção desmarcada | Caixa de seleção marcada |
|---|---|---|
| Tempo de vida do Cookie | `Authentication__SessionTimeoutMinutes` (padrão **60 minutos**) | **7 dias** |
| Tipo de Cookie | Cookie de sessão — descartado quando o navegador fecha | Cookie persistente — armazenado no disco |
| Reinicialização do navegador | Você deve fazer login novamente | Você permanece conectado |
| Tempo limite de inatividade | Estendido a cada requisição (`SlidingExpiration`) | Estendido a cada requisição, até o limite absoluto de 7 dias |
| Sair | Encerra a sessão imediatamente | Encerra a sessão imediatamente |

Portanto, a caixa de seleção transforma "conectado para esta sessão de navegador, no máximo 60 minutos de inatividade" em "conectado por até 7 dias, incluindo reinicializações de navegador e de máquina".

### Coisas que a caixa de seleção não faz {#things-the-checkbox-does-not-do}

- **Ela não lembra sua senha ou nome de usuário.** O formulário de login não é
  preenchido automaticamente na próxima visita; apenas o cookie de sessão é mantido.
- **Os 7 dias são um limite rígido e não podem ser configurados.** O valor é fixo
  no código (`Auth/Services/CookieAuthenticationService.cs`). Apenas o tempo de vida não persistente segue `Authentication__SessionTimeoutMinutes`.
- **Não é um temporizador de inatividade.** 7 dias são contados a partir do login; uma vez
  que terminem, um novo login é necessário mesmo que você tenha usado o Arquivo um minuto atrás.
- **Não sobrevive a um logout.** Clicar em *Logout* deleta o cookie
  independentemente da caixa de seleção.

### Interação com 2FA {#interaction-with-2fa}

Com a autenticação de dois fatores habilitada, a caixa de seleção é lembrada durante a
etapa de 2FA e aplicada apenas após o sucesso do segundo fator. A transferência do 2FA
reside na sessão do lado do servidor, que possui um **tempo limite de inatividade de 60 minutos** próprio: se o código não for inserido dentro dessa janela, o login é abortado e a seleção da caixa de seleção é perdida junto.

### Interação com a sessão do aplicativo {#interaction-with-the-application-session}

O Mail Archiver mantém uma segunda sessão separada no lado do servidor para estados de curta duração — a transferência do 2FA, a seleção de restauração em lote e o último estado de pesquisa. Essa sessão tem um **tempo limite de inatividade de 60 minutos** fixo e *não* é persistente. Após uma reinicialização do navegador com um cookie "Remember Me" válido, você estará conectado, mas qualquer operação pendente mantida nessa sessão começará do zero.

### Interação com OIDC / SSO {#interaction-with-oidc-sso}

Usuários que fazem login por meio de um provedor de identidade externo nunca recebem o
cookie persistente: o callback do OIDC sempre inicia a sessão com
`persistAuthentication: false`. Para essas Contas, a caixa de seleção **não tem efeito** — quanto tempo uma sessão dura é decidido pelas configurações de sessão do provedor de identidade.

> ⚠️ **Nota de segurança.** Em uma máquina compartilhada ou desassistida, deixe a caixa de seleção
> desmarcada — um cookie persistente mantém o Arquivo acessível por até 7 dias
> sem uma senha. Em ambientes onde isso é inaceitável, encurte
> `Authentication__SessionTimeoutMinutes` e exija que os usuários não usem a
> caixa de seleção, ou use OIDC onde o provedor impõe sua própria política de sessão.

## 🔒 Considerações de Segurança {#security-considerations}

1. Use senhas fortes para todas as Contas de usuário
2. Altere a senha padrão do administrador imediatamente após a instalação
3. Atribua a Permissão Admin apenas a usuários que precisam dela
4. Atribua a Permissão Self Manager apenas a usuários que precisam gerenciar suas próprias Contas
5. Revise regularmente as permissões de usuário para garantir que o acesso seja apropriado
6. Remova o acesso do usuário quando não for mais necessário
7. Use o princípio do privilégio mínimo - atribua acesso apenas às contas de e-mail que os usuários precisam
