# 🚨 Recuperação de Emergência de Conta {#emergency-account-recovery}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia fornece instruções detalhadas para recuperar o acesso ao aplicativo Mail Archiver quando você esquecer a senha do administrador. Este procedimento de emergência permite que você recupere o acesso ao seu sistema sem perder nenhum dado.

> 📝 **Requisito de Versão**: Este método de recuperação está disponível na versão 2601.1 do Mail Archiver e posteriores.

## ⚠️ Aviso de Segurança Importante {#important-security-notice}

Este método de recuperação deve ser usado apenas em situações de emergência quando você não conseguir acessar sua Conta de administrador. Ele requer a modificação temporária da configuração do seu aplicativo e deve ser revertido imediatamente após a recuperação da senha.

## 🛠️ Etapas de Recuperação {#recovery-steps}

### 1. Criar Conta de Administrador de Emergência {#1-create-emergency-administrator-account}

Modifique suas variáveis de ambiente `appsettings.json` ou Docker para criar uma Conta de administrador de emergência temporária:

**Para Docker Compose (Recomendado):**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Emergency admin account
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempPass123!
      # ... other settings remain unchanged
```

**Para modificação direta no appsettings.json:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Reiniciar o Aplicativo/Container {#2-restart-the-applicationcontainer}

Após modificar a configuração, reinicie seu aplicativo:

**Para Docker Compose:**
```bash
docker compose down
docker compose up -d
```

**Para aplicativo direto:**
```bash
# Restart your application service
```

### 3. Login com Conta de Emergência {#3-login-with-emergency-account}

1. Acesse a interface web do Mail Archiver
2. Faça login usando as credenciais de emergência:
   - **Username**: `emergency_admin` (ou o que você definiu)
   - **Password**: `TempPass123!` (ou o que você definiu)

### 4. Redefinir Senha Original do Administrador {#4-reset-original-administrator-password}

1. Navegue até a seção "Users" no menu principal
2. Encontre sua Conta de administrador original na lista de usuários
3. Clique em "Edit" para esse usuário
4. Altere a senha para uma nova senha segura
5. Clique em "Save Changes"

### 5. Restaurar Configuração Original {#5-restore-original-configuration}

Reverta suas alterações de configuração para o nome de usuário do administrador original:

**Para Docker Compose:**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Original admin account
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword
      # ... other settings
```

**Para appsettings.json direto:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Reiniciar o Aplicativo Novamente {#6-restart-application-again}

Reinicie o aplicativo com a configuração restaurada:

**Para Docker Compose:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Exemplo: Processo Completo de Recuperação via Docker Compose {#example-complete-docker-compose-recovery-process}

Aqui está um exemplo completo do processo de recuperação de emergência usando Docker Compose:

### Passo 1: Configuração de Emergência {#step-1-emergency-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # EMERGENCY ADMIN ACCOUNT
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempEmergencyPass2026!
      
      # Other settings remain unchanged...
      - MailSync__IntervalMinutes=15
      - TimeZone__DisplayTimeZoneId=Europe/Berlin
      # ... rest of configuration
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Passo 2: Reiniciar e Login {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Passo 3: Redefinir Senha Original {#step-3-reset-original-password}
1. Faça login na interface web com as credenciais de emergência
2. Vá para a seção Users
3. Edite o usuário admin original e defina uma nova senha
4. Salve as alterações

### Passo 4: Restaurar Configuração Normal {#step-4-restore-normal-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # RESTORED ORIGINAL ADMIN ACCOUNT
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword2026!
      
      # ... rest of configuration unchanged
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Passo 5: Reinicialização Final {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Chave de Criptografia de Credencial Perdida {#lost-credential-encryption-key}

Se `Security__CredentialEncryptionKey` (criptografia de credenciais em repouso) for perdida, as credenciais da Conta de e-mail armazenadas não poderão mais ser descriptografadas. O aplicativo **não** trava: os valores afetados são tratados como "sem credencial" e um aviso é registrado no Registro de auditoria. Para recuperar:

1. Restaure a chave a partir do seu Backup / gerenciador de segredos e reinicie o aplicativo. As credenciais funcionarão novamente de imediato, sem necessidade de alterações de dados.
2. Se a chave for perdida permanentemente, as credenciais criptografadas não poderão ser recuperadas. As Contas afetadas são fáceis de identificar: sua Sincronização falha e um aviso de descriptografia é escrito no log do aplicativo (e aparece como um problema de sincronização na interface do usuário).
3. Abra cada Conta afetada em **Mail Accounts → Edit** e reinsira suas credenciais: a senha IMAP, o client secret do M365, ou reautorize a Conta (MSA). Salvar o formulário criptografa novamente a credencial com a chave agora configurada — nenhuma alteração manual no banco de dados é necessária.

> 💡 A chave nunca é armazenada no banco de dados, portanto, Backups de banco de dados sozinhos não são suficientes — sempre faça o Backup da chave separadamente.

## 🔒 Melhores Práticas de Segurança {#security-best-practices}

1. **Use Senhas Temporárias Fortes**: Torne sua senha de emergência complexa e única
2. **Altere-a Imediatamente**: Redefina a senha de emergência assim que recuperar o acesso
