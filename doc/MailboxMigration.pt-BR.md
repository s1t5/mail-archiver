# 📥 Guia de Migração de Caixa de correio {#mailbox-migration-guide}

[← Voltar para o Índice de Documentação](index.md)

Este guia explica como migrar uma Caixa de correio de um provedor de e-mail para outro usando o aplicativo Mail Archiver.

## 📋 Visão Geral {#overview}

O aplicativo Mail Archiver fornece funcionalidade para migrar e-mails de uma Caixa de correio de origem para uma Caixa de correio de destino. Isso é particularmente útil ao mudar de provedores de e-mail enquanto preserva seu histórico de e-mails. A estrutura de Pasta da Caixa de correio de origem também é preservada durante a migração.

> **Migrando apenas e-mails recentes?** O fluxo descrito abaixo copia a Caixa de correio arquivada *inteira*, e executá-lo duas vezes anexa tudo uma segunda vez. Se a nova Caixa de correio deve conter apenas e-mails mais recentes que um limite, enquanto o histórico completo permanece no Arquivo, use o [date-windowed offload](Offload.md) em vez disso. Ele aceita uma janela de data, pode excluir e renomear pastas, oferece um dry run e é seguro para repetir porque pula o que o destino já possui. Ele também pode ser controlado via linha de comando, o que torna uma frota de caixas de correio programável.

## 🚀 Processo de Migração {#migration-process}

Siga estas etapas para migrar sua Caixa de correio:

1. **Adicionar a Conta de origem**
   - Navegue até a seção "Email Accounts"
   - Clique em "Add Account"
   - Insira os detalhes e credenciais da sua Conta de origem
   - Salve a Conta

2. **Sincronizar a Conta de origem**
   - Permita que o aplicativo realize a Sincronização de todos os e-mails da Conta de origem
   - Aguarde a conclusão do processo de Sincronização

3. **Adicionar a Conta de destino**
   - Na seção "Email Accounts", clique em "Add Account"
   - Insira os detalhes e credenciais da sua Conta de destino
   - Salve a Conta

4. **Sincronizar a Conta de destino**
   - Se a Conta de destino for nova, realize a Sincronização para garantir que ela esteja configurada corretamente
   - Aguarde a conclusão do processo de Sincronização

5. **Iniciar a migração**
   - Na seção "Email Accounts", identifique a Conta de origem
   - Vá para os detalhes da Conta
   - Selecione a opção "Copy All Emails to Another Mailbox"
   - Escolha a Conta de destino e uma Pasta de destino
   - Ative a opção **"Preserve Folder Structure"** – a Pasta de destino selecionada servirá então como a raiz sob a qual toda a estrutura de Pasta de origem é recriada recursivamente
   - Se esta opção não estiver ativada, todos os e-mails serão colocados diretamente na Pasta de destino selecionada (plana, sem subpastas)
   - Inicie o processo de migração

6. **Monitorar a migração**
   - Se houver um grande número de e-mails, a migração será realizada como uma tarefa em segundo plano
   - O status e o progresso da migração podem ser visualizados na aba "Jobs"
   - Aguarde a conclusão bem-sucedida da migração

7. **Desativar a Conta de origem**
   - Após a transferência bem-sucedida, volte para a Conta de origem
   - Defina a Conta de origem como "Disabled" nas configurações da Conta
   - Isso garante que ela não será mais arquivada no futuro

## ⚠️ Notas Importantes {#important-notes}

- Este fluxo não é repetível: executá-lo novamente anexa cada mensagem uma segunda vez. O [date-windowed offload](Offload.md) é a alternativa repetível.
- Certifique-se de que a Conta de origem tenha Permissão suficiente para ler todos os e-mails
- Certifique-se de que a Conta de destino tenha espaço de armazenamento suficiente para os e-mails migrados
- O processo de migração pode levar algum tempo, dependendo do número de e-mails e da velocidade da conexão
- Recomenda-se realizar a migração durante horários de pouco movimento para minimizar o impacto no serviço de e-mail
- Verifique regularmente o status da migração na aba "Jobs" para garantir que o processo esteja progredindo
- Após a migração, verifique se todos os e-mails foram transferidos com sucesso para a Conta de destino

## ✅ Melhores Práticas {#best-practices}

- Sempre teste o processo de migração com um pequeno subconjunto de e-mails antes de migrar a Caixa de correio inteira
- Mantenha um Backup de e-mails importantes antes de iniciar a migração
- Agende a migração para um horário em que o uso de e-mail seja mínimo
- Monitore de perto o progresso da migração e esteja preparado para resolver quaisquer problemas que possam surgir
- Após a conclusão da migração, verifique se todos os e-mails foram transferidos corretamente e estão acessíveis
