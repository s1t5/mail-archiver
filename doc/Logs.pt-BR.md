# 📋 Registro de acesso {#access-log}

[← Voltar para o Índice da Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia explica a funcionalidade de registro de acesso no aplicativo Mail Archiver, que permite o registro e a visualização das atividades dos usuários dentro do sistema. Esta funcionalidade é crucial para fins de monitoramento, segurança e depuração.

## 📊 O que é registrado {#what-is-logged}

O aplicativo registra vários tipos de atividades de usuário, incluindo:

1. **Login e Logout** - Registro de operações de login e logout
2. **Operações de Busca** - Registro de solicitações de busca de e-mail dentro do sistema
3. **Abertura de E-mail** - Registro de quando os usuários abrem o conteúdo do e-mail
4. **Download de E-mail** - Registro de quando os usuários baixam e-mails
5. **Restauração de E-mail** - Registro de quando os usuários realizam a Restauração de e-mails arquivados
6. **Gerenciamento de Conta** - Registro de ações relacionadas à Conta
7. **Operações de Exclusão** - Registro de ações de exclusão
8. **Exportação de Dados de Auditoria** - Registro de execuções de exportação de dados de auditoria (início e resultado, veja [Audit Data Export](AuditExport.md))

## 📝 Tipos de Ação e Descrições {#action-types-and-descriptions}

As ações registradas são categorizadas nos seguintes tipos:

### Login {#login}
- **Descrição**: Registra quando um usuário faz login no sistema
- **Detalhes**: Username, timestamp

### Logout {#logout}
- **Descrição**: Registra quando um usuário faz logout do sistema
- **Detalhes**: Username, timestamp

### Search {#search}
- **Descrição**: Registra solicitações de busca dentro do sistema de e-mail
- **Detalhes**: Username, timestamp, search parameters

### Open {#open}
- **Descrição**: Registra quando um usuário abre um e-mail
- **Detalhes**: Username, timestamp, email ID, sender, subject

### Download {#download}
- **Descrição**: Registra quando os usuários baixam e-mails
- **Detalhes**: Username, timestamp, email ID, sender, subject

### Restore {#restore}
- **Descrição**: Registra quando os usuários realizam a Restauração de e-mails arquivados
- **Detalhes**: Username, timestamp, email ID, sender, subject

### Account {#account}
- **Descrição**: Registra ações relacionadas à Conta (criação, modificação, etc.)
- **Detalhes**: Username, timestamp, account ID

### Deletion {#deletion}
- **Descrição**: Registra operações de exclusão em e-mails ou outros recursos
- **Detalhes**: Username, timestamp, affected resource
- **Nota de Conformidade**: Operações de exclusão são registradas para fins de auditoria de conformidade

## 🔍 Opções de Filtro {#filter-options}

A página de log oferece as seguintes opções de filtro:

1. **Date** - Filtrar por intervalo de datas (De-Para)
2. **Username** - Filtrar por usuário específico (Apenas Admin)
3. **Action Type** - Filtrar por tipo de ação específico

## 🖥️ Exibição de Log {#log-display}

Os logs são exibidos em formato de tabela com as seguintes colunas:

- **Timestamp**: Data e hora da ação
- **Username**: Nome do usuário envolvido
- **Type**: Tipo de ação com codificação por cores
- **Information**: Detalhes adicionais sobre a ação

Para dispositivos móveis, os logs são exibidos em formato de cartão.

## 🔐 Permissões de Acesso {#access-permissions}

- **Usuários não-admin**: Podem apenas visualizar seus próprios logs
- **Usuários Admin**: Podem visualizar logs de todos os usuários e filtrar por usuário

## 📤 Audit Data Export {#audit-data-export}

Admins podem abrir a página dedicada [Audit Data Export](AuditExport.md) diretamente da página de Logs. Ela cria pacotes de dados massivos tabulares (INDEX.XML + CSV) a partir do Arquivo para ferramentas de auditoria externas. Cada execução de exportação escreve duas entradas de registro de acesso do tipo **Audit Data Export** (início e resultado), que são listadas e filtráveis na página de Logs como todas as outras entradas.

Para detalhes, veja o [guia de Audit Data Export](AuditExport.md).
