# ☁️ Configuração de Conta Microsoft Pessoal (Outlook.com / M365 Family) {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia explica como fazer o Arquivo de e-mails de uma **conta Microsoft pessoal** (Outlook.com, Hotmail, live.com ou Microsoft 365 Family) no Mail Archiver.

Ao contrário das contas organizacionais do Microsoft 365 (que usam o fluxo client-credentials — veja [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)), as contas pessoais usam **OAuth2 com o Device Code Flow (RFC 8628)**. A Microsoft descontinuou a Autenticação Básica e senhas de aplicativo para contas pessoais, portanto, o OAuth2 é obrigatório.

O Mail Archiver vem com um **Client ID compartilhado pré-registrado** para que a maioria dos usuários **não** precise criar seu próprio Azure App Registration. Usuários avançados ainda podem registrar seu próprio aplicativo (veja a Opção B abaixo).

## 📚 Sumário {#table-of-contents}

1. [Opção A: Configuração Padrão (Recomendado)](#option-a-standard-setup-recommended)
2. [Opção B: Registro de Aplicativo Personalizado (Usuários Avançados)](#option-b-custom-app-registration-power-users)
3. [Atualização e Revogação de Token](#token-refresh--revocation)
4. [Notas Importantes](#important-notes)
5. [Solução de Problemas](#troubleshooting)

---

## 🚀 Opção A: Configuração Padrão (Recomendado) {#option-a-standard-setup-recommended}

Use esta opção se a sua instância do Mail Archiver tiver um Client ID padrão configurado (o padrão para imagens Docker oficiais). Não é necessário acesso ao Azure Portal.

### 🛠️ Pré-requisitos {#prerequisites}

- Uma conta Microsoft pessoal (Outlook.com, live.com ou M365 Family)
- Um dispositivo com navegador (smartphone, laptop ou tablet) para concluir a autorização

### 📝 Passos {#steps}

1. Faça login no seu aplicativo Mail Archiver.
2. Navegue até **Mail Accounts** > **Create**.
3. Preencha os seguintes campos:
   - **Name**: Nome descritivo para a Conta (ex: "Outlook Pessoal")
   - **Email Address**: Seu endereço de e-mail Microsoft pessoal (ex: `you@outlook.com`)
   - **Provider**: Selecione **Microsoft Personal**
4. Nenhum campo de Client ID ou Client Secret é exibido — o Mail Archiver usa o registro padrão integrado automaticamente.
5. Clique em **Create**.
6. A página de autorização por código de dispositivo abre automaticamente. Um código curto (ex: `ABCD-EFGH`) é exibido junto com um link para `https://microsoft.com/devicelogin` (ou a URL de verificação exibida).
7. Em **qualquer dispositivo** com um navegador, abra essa URL e faça login com sua conta Microsoft pessoal.
8. Insira o código do passo 6.
9. Uma tela de consentimento aparece, mostrando **"Mail-Archiver"** solicitando acesso IMAP. Revise e aprove.
10. A página do Mail Archiver detecta o sucesso automaticamente e redireciona para a visualização de edição da Conta.
11. Pronto — a Sincronização começa automaticamente de acordo com as configurações da Conta.

> ⚠️ **Aviso de Editor Não Verificado**: A tela de consentimento pode mostrar um aviso de que o "Mail-Archiver" é um editor não verificado. Isso é esperado — a verificação de editor requer uma identidade comercial (conta Microsoft Partner Network) e não é necessária para que o fluxo funcione. O aplicativo funciona normalmente sem ela.

---

## 🔧 Opção B: Registro de Aplicativo Personalizado (Usuários Avançados) {#option-b-custom-app-registration-power-users}

Use esta opção se você não quiser usar o Client ID padrão compartilhado (por exemplo, por motivos de conformidade ou se quiser controle total sobre o registro do aplicativo).

### 🛠️ Pré-requisitos {#prerequisites}

- Uma conta Microsoft (pessoal ou de trabalho/escola) que possa acessar o Azure Portal
- Familiaridade básica com o portal Azure/Entra

### 🚀 Criar Registro de Aplicativo {#create-app-registration}

1. Navegue até o [Microsoft Entra Admin Center](https://entra.microsoft.com) ou o [Azure Portal](https://portal.azure.com).
2. Faça login com sua conta Microsoft.
3. No painel de navegação à esquerda, selecione **App registrations** (em Identity > Applications).
4. Clique em **+ New registration**.
5. Preencha os seguintes detalhes:
   - **Name**: Insira um nome descritivo (ex: "Mail Archiver MSA")
   - **Supported account types**: Selecione **Accounts in any organizational directory and personal Microsoft accounts**
     - Esta é a opção multi-tenant e suporta tanto contas pessoais quanto organizacionais.
   - **Redirect URI**: Deixe em branco (não é necessário para o Device Code Flow).
6. Clique em **Register**.
7. Anote o **Application (client) ID** na página **Overview**.

### 🔐 Habilitar Fluxos de Cliente Público {#enable-public-client-flows}

1. No seu registro de aplicativo, navegue até **Authentication** no menu à esquerda.
2. Na parte inferior, encontre **Allow public client flows** e defina como **Yes**.
3. Clique em **Save**.

> ℹ️ Nenhum client secret, redirect URI ou Permissão de API precisa ser configurado manualmente — o Device Code Flow usa permissões delegadas que são solicitadas em tempo de execução via o escopo `IMAP.AccessAsUser.All`.

### 📧 Configurar Mail Archiver {#configure-mail-archiver}

Sobrescrever o padrão global (afeta todas as contas MSA)

Defina o Client ID em sua variável de ambiente:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Atualização e Revogação de Token {#token-refresh-revocation}

### Atualização Automática de Token {#automatic-token-refresh}

Após a autorização inicial, o Mail Archiver recebe:
- Um **access token** (curta duração, ~1 hora)
- Um **refresh token** (longa duração, usado para obter novos access tokens automaticamente)

O Mail Archiver atualiza automaticamente o access token antes de cada execução de sincronização. Você não precisa reautorizar, a menos que o refresh token expire ou seja revogado.

### Revogando o Acesso {#revoking-access}

Para revogar o acesso do Mail Archiver à sua conta Microsoft pessoal:

1. Visite [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Faça login com sua conta Microsoft pessoal.
3. Encontre **"Mail-Archiver"** (ou o nome do seu aplicativo personalizado) na lista.
4. Clique em **Remove** para revogar o acesso.

Após a revogação, o Mail Archiver não poderá mais acessar a Conta até que você a reautorize.

---

## ⚠️ Notas Importantes {#important-notes}
- **Client ID Compartilhado**: Ao usar o Client ID padrão, todos os usuários do Mail Archiver em todas as instâncias compartilham o mesmo Azure App Registration. O mantenedor do Mail Archiver é responsável por manter o registro ativo. Se a Microsoft desativar o registro compartilhado, todos os usuários precisarão mudar para um registro personalizado (Opção B) até que uma nova versão seja lançada com um Client ID atualizado.


- **Contas Organizacionais**: O provedor Microsoft Personal usa o endpoint de autoridade `/common` e suporta tanto contas pessoais quanto organizacionais (trabalho/escola). Para contas organizacionais que exigem acesso client-credentials (apenas aplicativo, sem login de usuário), use o provedor **M365** em vez disso.

---

## 🔍 Solução de Problemas {#troubleshooting}

### Pastas visíveis no outlook.com, mas ausentes no Mail Archiver {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Sintoma**: Algumas pastas existem na Caixa de correio (visíveis no outlook.com ou no Outlook), mas nunca aparecem no Mail Archiver — nem na lista de pastas na página de configurações da Conta, nem durante a sincronização. Nenhum erro é registrado.

**Causa (lado do servidor)**: Pastas que foram criadas por **clientes de terceiros através de EWS** — que configura contas do outlook.com como contas Exchange em vez de IMAP — podem acabar com uma propriedade `PR_CONTAINER_CLASS` incorreta (ex: `IPF.Imap` em vez de `IPF.Note`). O Outlook.com então **filtra essas pastas da resposta IMAP `LIST` inteiramente**, mesmo que estejam totalmente visíveis na interface web. Este é um comportamento do lado da Microsoft, não um bug do Mail Archiver — o mesmo fenômeno é documentado para a Microsoft Graph API, onde pastas com uma classe de contêiner não-`IPF.Note` ficam ausentes das listagens `mailFolders` até que a propriedade seja corrigida:

O Mail Archiver consulta a lista de pastas de três fontes (`LIST` recursivo, travessia por nível de cada filho de pasta e `LSUB`) e mescla os resultados. Quando o servidor não reporta uma pasta através de nenhum deles, não há como qualquer cliente IMAP vê-la.

**Solução de Contorno**: Toque nas pastas afetadas uma vez através da interface web do outlook.com para que a Microsoft corrija a classe do contêiner:

1. Abra <https://outlook.com> e localize uma pasta ausente.
2. Renomeie-a (qualquer nome temporário) e renomeie-a de volta — ou crie uma nova pasta e mova os e-mails para ela.
3. Repita para cada pasta afetada.
4. Acione uma **Full Resync** da Conta na página de Detalhes da Conta no Mail Archiver. Uma resincronização completa é necessária porque as sincronizações incrementais buscam apenas e-mails mais novos que a última data de sincronização da Conta — e-mails em pastas que nunca foram descobertas de outra forma permaneceriam sem Backup.


*Este guia está atualizado até 2026. A Microsoft atualiza regularmente seus serviços e interface, portanto, alguns passos podem diferir. Consulte a [documentação da plataforma de identidade Microsoft](https://learn.microsoft.com/en-us/entra/identity-platform/) para os detalhes mais recentes.*
