# 📤 Exportando uma Conta de E-mail (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

A exportação de Conta escreve **cada mensagem de uma conta** de volta em um arquivo ZIP para download. É a contraparte da
[importação na web](Import-EML-MBox.md): você obtém um Arquivo portátil da conta em um
formato que qualquer cliente de e-mail ou ferramenta de migração pode ler.

Dois formatos são oferecidos:

| Formato | O que você recebe | Uso típico |
|--------|--------------|-------------|
| **EML** | Arquivos `.eml` individuais, organizados em pastas | Reimportação em um cliente de e-mail, processamento por mensagem, legibilidade a longo prazo |
| **MBox** | Arquivos de Caixa de correio tradicionais — **um arquivo por pasta** | Ferramentas e clientes que leem MBox, processamento de texto em massa |

Em ambos os casos, as mensagens são escritas como mensagens RFC-822 brutas, portanto, os **Anexos
estão incluídos** na exportação.

> 💡 **Dica**: Para exportar apenas uma seleção de mensagens em vez de uma conta inteira,
> use as ações em massa na [Mail search](Search.md#-acting-on-many-results-at-once).

## 🧭 Acesso {#access}

1. Abra **Mail Accounts** no menu principal.
2. Clique no botão **⋮** (mais ações) na linha da conta e escolha
   **Export**. A mesma entrada está na página de **Details** da conta.

A exportação requer a função **admin** ou **self manager** — usuários padrão veem
a entrada, mas não podem usá-la. A exportação sempre abrange a conta inteira; não há
filtro de data ou pasta.

## 🖥️ Usando a Página de Exportação {#using-the-export-page}

A página mostra as contagens **totais**, **de entrada** e **de saída** de mensagens da conta,
para que você possa ver o tamanho do trabalho antes de iniciá-lo. Então:

1. Escolha o formato — **EML** ou **MBox**.
2. Pressione **Start Export**.

A exportação é executada **em segundo plano**. Você será levado a uma página de status mostrando o
ID do trabalho, o formato escolhido, os registros de data/hora e as contagens de mensagens processadas/totais.

### Status do trabalho {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Quando o trabalho atinge **Completed**, um **botão de download** aparece na página de status.
O mesmo trabalho também é listado na página [Jobs](BackgroundJobs.md).

> ⚠️ **Os arquivos de exportação são limpos automaticamente.** O ZIP é excluído **após um
> download bem-sucedido** e, em qualquer caso, **após 7 dias**. Baixe o arquivo enquanto
> ele ainda estiver disponível — o Arquivo em si não é afetado, você pode simplesmente iniciar uma
> nova exportação mais tarde.

## ⚠️ Coisas para prestar atenção {#things-to-watch-out-for}

- **Contas grandes levam um tempo.** Uma Caixa de correio grande pode precisar de vários minutos e o
  ZIP pode se tornar muito grande, dependendo do volume de mensagens e Anexos.
  Como o trabalho é executado em segundo plano, você pode fechar o navegador e voltar
  à página de status mais tarde.
- **Espaço em disco do servidor.** O ZIP é montado no servidor antes de você baixá-lo,
  então certifique-se de que haja espaço livre suficiente para o Arquivo.
- **"Export file not found"** — o trabalho ainda não terminou, ou o arquivo já
  foi baixado ou removido pela limpeza de 7 dias. Inicie uma nova exportação.
- **Sem opções de filtro** — a exportação é tudo ou nada por conta. Use a
  exportação da página de busca para um subconjunto.
- **Não é uma exportação de conformidade.** Para uma entrega de mídia de dados pronta para auditoria com um
  arquivo de índice, use o [Audit Data Export](AuditExport.md) em vez disso — ele produz um
  pacote no formato de dados massivos tabulares que as ferramentas de auditoria esperam.

## 🔗 Documentação Relacionada {#related-documentation}

- [Importing Mail in the Web UI](Import-EML-MBox.md) — a direção oposta
- [Mail search](Search.md) — exportando uma seleção de mensagens
- [Background Jobs](BackgroundJobs.md) — lista de trabalhos, progresso e cancelamento
- [Audit Data Export](AuditExport.md) — pacotes de exportação prontos para auditoria
- [CLI commands](CLI.md) — importando de um diretório montado
