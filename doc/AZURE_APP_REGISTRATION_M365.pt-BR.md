# ☁️ Guia de Registro de Aplicativo Azure e Política de Retenção para Mail Archiver {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Voltar para o Índice da Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia fornece instruções abrangentes para configurar o Registro de Aplicativo Azure para recuperação de e-mails e configurar políticas de retenção no aplicativo Mail Archiver.

## 📚 Índice {#table-of-contents}

1. [Visão Geral](#overview)
2. [Registro de Aplicativo Azure para Recuperação de E-mail](#azure-app-registration-for-email-retrieval)
   - [Pré-requisitos](#prerequisites)
   - [Criar Registro de Aplicativo](#create-app-registration)
   - [Definir Permissões de API Necessárias](#set-required-api-permissions)
   - [Gerar Segredo do Cliente](#generate-client-secret)
3. [Configurar Conta M365 do Mail Archiver](#configure-mail-archiver-m365-account)
4. [Configuração de Política de Retenção](#retention-policy-setup)
   - [Configurar Política de Retenção](#configure-retention-policy)

## 🌐 Visão Geral {#overview}

O aplicativo Mail Archiver suporta Contas Microsoft 365 (M365) usando o fluxo de credenciais de cliente OAuth2 para recuperação de e-mails. Além disso, ele fornece funcionalidade de política de retenção para excluir e-mails do servidor de e-mail automaticamente após um período especificado, ajudando a gerenciar o espaço de armazenamento enquanto mantém um Arquivo completo.

## ☁️ Registro de Aplicativo Azure para Recuperação de E-mail {#azure-app-registration-for-email-retrieval}

### 🛠️ Pré-requisitos {#prerequisites}

- Acesso administrativo ao Microsoft Entra ID (Azure AD)
- Um locatário (tenant) Microsoft 365 com licenças do Exchange Online

### 🚀 Criar Registro de Aplicativo {#create-app-registration}

1. Navegue até o [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Faça login com sua Conta de administrador
3. No painel de navegação à esquerda, selecione **App registrations**
4. Clique em **+ New registration** no topo da página de App registrations
5. Preencha os seguintes detalhes:
   - **Name**: Insira um nome descritivo (ex: "Mail Archiver M365 Provider")
   - **Supported account types**: Selecione **Accounts in this organizational directory only** (Single tenant)
     - Isso é necessário para o fluxo de credenciais de cliente usado pelo Mail Archiver
   - **Redirect URI**: Deixe em branco (não é necessário para o fluxo de credenciais de cliente)
6. Clique em **Register**

**Importante**: Anote os seguintes valores da página **Overview**:
- **Application (client) ID** - Você precisará disso como `ClientId` no Mail Archiver
- **Directory (tenant) ID** - Você precisará disso como `TenantId` no Mail Archiver

### 🔐 Definir Permissões de API Necessárias {#set-required-api-permissions}

1. Navegue até **API permissions** no menu à esquerda
2. Clique em **+ Add a permission**
3. Selecione **Microsoft Graph**
4. Escolha **Application permissions** (necessário para o fluxo de credenciais de cliente do Mail Archiver)
5. Adicione as seguintes permissões:
   - **Mail.Read** - Ler e-mails em todas as Caixas de correio
   - **Mail.ReadWrite** - Ler e escrever e-mails em todas as Caixas de correio (para função de Restauração, bem como exclusão)
   - **User.Read.All** - Ler perfis completos de todos os usuários (necessário para importação de Caixa de correio de locatário Microsoft 365; também fornece `assignedPlans` para filtragem de licença do Exchange)

6. Clique em **Add permissions**
7. **CRÍTICO**: Clique em **Grant admin consent for [Sua Organização]**
8. Confirme clicando em **Yes**

**Nota**: Permissões de aplicativo são necessárias porque o Mail Archiver usa o fluxo de credenciais de cliente para acessar Caixas de correio sem interação do usuário.

### 🔑 Gerar Segredo do Cliente {#generate-client-secret}

1. Navegue até **Certificates & secrets** no menu à esquerda
2. Em **Client secrets**, clique em **+ New client secret**
3. Forneça uma descrição (ex: "Mail Archiver Secret")
4. Selecione um período de expiração
5. Clique em **Add**
6. **Importante**: Copie o **Value** imediatamente e armazene-o com segurança. Este segredo não será exibido novamente.

## 📧 Configurar Conta M365 do Mail Archiver {#configure-mail-archiver-m365-account}

Após concluir o registro do aplicativo, você deve configurar uma conta de e-mail M365 no Mail Archiver com os seguintes valores:

### 📋 Valores Necessários do Registro de Aplicativo: {#required-values-from-app-registration}

1. **Client ID**: Copie o **Application (client) ID** da página **Overview** do registro do aplicativo
2. **Client Secret**: Copie o **Value** que você salvou ao criar o segredo do cliente
3. **Tenant ID**: Copie o **Directory (tenant) ID** da página **Overview** do registro do aplicativo

### 🚀 Criando Conta M365 no Mail Archiver: {#creating-m365-account-in-mail-archiver}

1. Faça login no seu aplicativo Mail Archiver
2. Navegue até **Mail Accounts** > **Create**
3. Preencha os seguintes campos:
   - **Name**: Nome descritivo para a conta (ex: "Sales Team M365")
   - **Email Address**: O endereço de e-mail da Caixa de correio alvo para arquivar
   - **Provider**: Selecione **M365**
   - **Client ID**: Insira o Application (client) ID do seu registro de aplicativo
   - **Client Secret**: Insira o valor do segredo do cliente que você salvou
   - **Tenant ID**: Insira o Directory (tenant) ID do seu registro de aplicativo

4. Clique em **Create**

### ⚠️ Notas Importantes: {#important-notes}

- O campo **Email Address** deve conter a Caixa de correio real que você deseja arquivar ao criar uma única conta M365.
- O registro do aplicativo deve ter permissões para acessar a Caixa de correio especificada.
- Para criar contas para todas ou para Caixas de correio selecionadas no locatário de uma só vez, use o guia [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md).


## 🗑️ Configuração de Política de Retenção {#retention-policy-setup}

O aplicativo Mail Archiver fornece funcionalidade de política de retenção para excluir e-mails do servidor IMAP automaticamente após terem sido arquivados com sucesso, ajudando a gerenciar o espaço de armazenamento no servidor IMAP enquanto mantém um Arquivo completo.

### 🛠️ Configurar Política de Retenção {#configure-retention-policy}

1. Navegue até **Mail Accounts** no aplicativo Mail Archiver
2. Crie uma nova conta M365 ou edite uma existente
3. No formulário de configuração da conta, localize o campo **Delete After Days**
4. Insira o número de dias após os quais os e-mails arquivados devem ser excluídos do servidor:
   - Por exemplo, insira `30` para excluir e-mails após 30 dias
   - Deixe vazio para desativar a exclusão automática
5. Salve a configuração da conta


## ℹ️ Informações Sobre Caixas de Correio Migradas {#ℹ-information-regarding-migrated-mailboxes}

### 📁 Pastas Não Visíveis Após a Migração {#folders-not-visible-after-migration}

Se você migrou sua Caixa de correio de outro provedor de e-mail para o Microsoft 365, algumas pastas podem não aparecer na lista de pastas.

#### 🔍 Contexto {#background}

O Mail Archiver recupera informações de pastas de Caixas de correio M365 via Microsoft Graph API. O aplicativo solicita corretamente todas as pastas disponíveis (incluindo pastas ocultas) e percorre toda a hierarquia de pastas.

No entanto, a Microsoft Graph API retorna apenas pastas com `PR_CONTAINER_CLASS` definido como `IPF.Note`. Durante migrações de Caixa de correio de outros sistemas de e-mail, as pastas podem receber uma classe de contêiner diferente, como `IPF.Imap`. Essas pastas não são expostas através dos endpoints de listagem de pastas da Graph API.

Este comportamento afeta todos os aplicativos que utilizam esta API para recuperação de pastas.

#### 🔧 Opções de Contorno {#workaround-options}

**Opção 1: Usar a Ferramenta MFCMAPI (Recomendado)**

1. Baixe o [MFCMAPI](https://github.com/Microsoft/mfcmapi/) do repositório oficial da Microsoft
2. Inicie o MFCMAPI e conecte-se à sua Caixa de correio
3. Navegue até cada pasta ausente
4. Altere a propriedade `PR_CONTAINER_CLASS` de `IPF.Imap` para `IPF.Note`
5. Reinicie o Outlook e recarregue as pastas no Mail Archiver

**Opção 2: Recriar Pastas no Outlook**

1. Crie novas pastas diretamente no Outlook (estas terão a classe `IPF.Note` correta)
2. Mova todos os e-mails das pastas migradas para as novas pastas
3. Exclua as antigas pastas migradas, se desejar

**Opção 3: Script PowerShell (Para Alterações em Massa)**

Para Caixas de correio com muitas pastas migradas, você pode usar o PowerShell com o módulo Exchange Online para alterar a classe do contêiner para várias pastas de uma só vez. Entre em contato com seu administrador de Exchange para assistência com esta abordagem.


---

**Nota**: Este guia está atualizado até 2025. A Microsoft atualiza seus serviços regularmente, portanto, alguns elementos da interface do usuário podem diferir. Sempre consulte a documentação mais recente da Microsoft para obter as informações mais atualizadas.
