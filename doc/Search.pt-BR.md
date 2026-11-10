# 🔍 Mail Search {#mail-search}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

A busca é o coração de um Arquivo de e-mail. O Mail Archiver pesquisa o **texto completo**
do seu e-mail arquivado — assunto, corpo, remetente e destinatários — em todas as
Contas às quais você tem acesso, e retorna resultados em milissegundos, mesmo em
Arquivos com milhões de mensagens.

Este guia cobre a interface de busca, a sintaxe de consulta, filtros, ordenação,
paginação e o que fazer quando você precisa agir sobre um conjunto de resultados.

> **ℹ️ A busca só vê o que você pode ver.** Os resultados são filtrados pelas
> Permissões da sua Conta. Um não-administrador só encontrará e-mails das
> Caixas de correio atribuídas a ele — a restrição é aplicada na própria consulta,
> não na interface.

## 🚀 Acessando a Busca {#accessing-search}

1. Faça login e escolha **Archive** no menu principal
2. A barra de busca fica acima da lista de resultados

Abrir o Arquivo sem um termo de busca lista seus e-mails mais recentes, do mais novo para o mais antigo. Você pode navegar a partir daí ou refinar com os filtros.

## 🎯 A Interface de Busca {#the-search-interface}

| Campo | O que faz |
|---|---|
| **Search Term** | Consulta de texto completo — veja [Query Syntax](#-query-syntax) abaixo |
| **From** | Apenas e-mails enviados em ou após esta data |
| **To** | Apenas e-mails enviados em ou antes desta data |
| **Direction** | `All`, `Incoming` ou `Outgoing` |
| **Account** | Restringir a uma Conta de e-mail |
| **Folders** | Restringir a uma Pasta via árvore de pastas |
| **Search** | Executar a consulta |
| **Reset** | Limpar todos os filtros |

O termo de busca corresponde ao **assunto, corpo, remetente e destinatários**
(`From`, `To`, `Cc`, `Bcc`).

### A árvore de pastas {#the-folder-tree}

As Pastas são exibidas como uma **árvore hierárquica** à esquerda, espelhando a estrutura de pastas da Caixa de correio de origem — incluindo pastas aninhadas. Clique em uma pasta para restringir a busca a ela e use o controle de recolher/expandir para ocultar a árvore quando precisar de espaço na tela.

Selecionar uma pasta não altera seu termo de busca; os dois se combinam.

## 🧩 Query Syntax {#query-syntax}

A sintaxe é deliberadamente pequena. Tudo abaixo é o que a aplicação
realmente entende.

### Palavras simples — e por que prefixos funcionam {#plain-words-and-why-prefixes-work}

Digite uma ou mais palavras; todas elas devem estar presentes (uma busca **AND**):

```
invoice january
```

Encontra e-mails contendo tanto `invoice` quanto `january` nos campos pesquisados.

> **💡 Palavras parciais são correspondidas automaticamente.** Cada palavra é tratada como um
> **prefixo**, então uma busca por `isenb` também encontra `isenboeck` e `isenböck`, e
> `rech` encontra `Rechnung` assim como `Rechnungen`. Você não precisa de um caractere
> curinga — não há nenhum para digitar. Isso também significa que um termo muito curto pode
> retornar muito; veja [Performance](#-performance-and-large-archives).

A busca **não diferencia maiúsculas de minúsculas**. `Invoice`, `invoice` e `INVOICE` são a
mesma consulta.

### Frases exatas — aspas {#exact-phrases-quotes}

Envolva o texto em aspas duplas para exigir que as palavras estejam **lado a lado** nessa
ordem:

```
"important meeting"
```

Sem aspas, `important meeting` também corresponderia a um e-mail onde as duas
palavras aparecem em frases não relacionadas. Com aspas, apenas a frase adjacente
corresponde.

### Busca específica por campo {#field-specific-search}

Prefixe um termo com o nome de um campo e dois pontos para pesquisar **apenas** naquele campo:

| Sintaxe | Pesquisa em |
|---|---|
| `subject:invoice` | Apenas Subject |
| `from:john` | Apenas Sender |
| `to:example.com` | Apenas Recipients |
| `body:important` | Apenas Body |

Os nomes dos campos não diferenciam maiúsculas de minúsculas (`Subject:` também funciona), e cada um pode ser
combinado com aspas para uma frase:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Quatro campos são suportados:** `subject`, `body`, `from`, `to`. Escrever
> `cc:someone` ou `bcc:someone` **não** é uma busca de campo — o termo é tratado
> como texto comum e pesquisado em todos os campos. Para encontrar um destinatário que estava
> apenas em Cc, pesquise pelo endereço ou nome sem um prefixo.

### Combinando tudo {#combining-everything}

Filtros e termos se somam. Esta consulta encontra e-mails de qualquer remetente de seguros,
com "car insurance" como uma frase exata no corpo, e "invoice" no
assunto:

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Combinações práticas:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Caracteres que são ignorados {#characters-that-are-ignored}

O parser de consulta remove os caracteres `& | ! ( ) : *` de termos individuais,
porque eles possuem significado no mecanismo de texto completo subjacente. Se sua busca
não retornar nada inesperado, verifique se um caractere especial é o motivo.

## 📊 Sorting e Paging {#sorting-and-paging}

### Sorting {#sorting}

Clique no cabeçalho de uma coluna para ordenar. Colunas ordenáveis:

| Coluna | Ordena por |
|---|---|
| **Subject** | Linha de assunto, alfabeticamente |
| **From** | Remetente |
| **To** | Destinatários |
| **Date** | Data de envio |

Clicar no mesmo cabeçalho novamente inverte a direção. O padrão é **mais novos
primeiro**.

### Tamanho da página {#page-size}

Escolha quantos resultados mostrar por página:

**20** (padrão) · **50** · **75** · **100** · **150**

Uma página maior mostra mais contexto, mas leva mais tempo para renderizar. Use os
controles **Previous** / **Next** para navegar pelas páginas de resultados; a contagem de
resultados informa quantos e-mails corresponderam no total.

> **ℹ️ Seu fuso horário se aplica às datas exibidas.** Os horários exibidos são
> convertidos de UTC para o seu fuso horário configurado, para que a data que você vê corresponda
> ao seu calendário local — útil quando você está pesquisando por volta da meia-noite.

## 👁️ Trabalhando com Resultados {#working-with-results}

Cada linha mostra a Conta, remetente, assunto, data e um **📎 ícone de clipe de papel**
quando o e-mail possui Anexos. O clipe de papel é apenas um indicador — não é
um filtro.

Clique em **Details** para abrir um e-mail:

- Corpo completo (HTML ou texto simples)
- **Anexos**, listados com a opção de baixar cada um
- **Download as EML** — a mensagem completa, para uso em outro lugar
- **Restauração** — copia o e-mail de volta para uma Caixa de correio ativa

Veja [Mailbox Migration](MailboxMigration.md) para restaurar e-mails para uma Caixa de correio.

## ✅ Agindo em Muitos Resultados de uma Vez {#acting-on-many-results-at-once}

Os resultados podem ser selecionados e processados em massa. Ative o **Selection Mode** para obter
caixas de seleção e, em seguida:

- **Select All** / **Deselect All** para a página atual
- **Select all {n} matching** — todos os resultados da busca atual em todas as
  páginas (exibido quando os resultados abrangem mais de uma página)
- **Copy Selected** — restauração para uma Caixa de correio
- **Export Selected** — como MBOX ou EML
- **Delete Selected** — do Arquivo

O contador mostra *"{selected} de {total} selecionados"* enquanto você trabalha.

### O limite de seleção {#the-selection-limit}

> **⚠️ Um máximo de 250 e-mails pode ser selecionado de uma vez** (configurável via
> `Selection:MaxSelectableEmails`). Além disso, a interface mostra *"Maximum
> selections reached (250)"* e **Too Many — Split Required**.

Esse limite existe porque cada operação precisa manter as mensagens afetadas. O
aviso *"Split larger operations into multiple batches"* é a solução pretendida: trabalhe
nos resultados em grupos de 250 ou menos.

**Select all {n} matching** respeita o mesmo limite. Quando a busca corresponde a mais
e-mails do que o limite permite, ela seleciona os primeiros na ordem de classificação atual
e mostra o aviso *"Maximum selections reached"*. Refine a busca (intervalo de data,
Pasta, Conta) para atingir o restante.

Tentar exceder o limite mostra: *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Excluindo cada correspondência de um filtro {#deleting-every-match-of-a-filter}

O botão **Delete all matches** ao lado de *Reset* exclui cada e-mail que
corresponde à busca atual, não apenas a página ou seleção atual. Ele está disponível para administradores e autogerentes e é executado como um trabalho em segundo plano, portanto,
o limite de seleção de 250 e-mails não se aplica.

A caixa de diálogo de confirmação mostra quantos e-mails podem ser excluídos e quantos estão bloqueados pela política de Retenção. Você deve digitar esse número para habilitar a
confirmação. O servidor conta novamente e aborta se o conjunto de resultados tiver mudado nesse
meio tempo, por exemplo, porque uma sincronização adicionou e-mails.

E-mails bloqueados nunca são excluídos por esta ação; eles são relatados como pulados no
status do trabalho. `DeletionPolicy:MaxBulkDeleteEmails` (padrão 100.000) limita um
trabalho. Um filtro que corresponda a mais e-mails é rejeitado com um pedido para refiná-lo.

O trabalho aparece na página [Background Jobs](BackgroundJobs.md) e pode ser
cancelado lá.

### Processamento direto vs. trabalho em segundo plano {#direct-processing-vs-background-job}

Para **Copy** e **Export**, você escolhe como o trabalho é executado:

| Opção | Comportamento |
|---|---|
| **Process on this page (wait for completion)** | O navegador espera; bom para um punhado de e-mails |
| **Process in background (monitor separately)** | Executa como um trabalho; você pode fechar o navegador |

Exports sempre rodam em segundo plano:

> *"The export will run in the background and you will be able to download the
> result from the Jobs page once completed."*

Encontre trabalhos em execução e concluídos na página [Background Jobs](BackgroundJobs.md).

> **⚠️ A exclusão é permanente.** A confirmação é explícita: *"Are you sure you
> want to permanently delete {n} selected email(s)? This action cannot be undone
> and each deletion will be logged for audit purposes."* As exclusões são registradas
> no log de acesso.

## ⚡ Performance e Grandes Arquivos {#performance-and-large-archives}

A busca é suportada por um índice de texto completo PostgreSQL, portanto, a diferença entre uma
busca rápida e uma lenta é geralmente o quão **seletiva** a consulta é.

**Faça:**

- Adicione um **intervalo de data** quando você souber aproximadamente quando o e-mail chegou — este é
  o refinamento individual mais eficaz
- Restrinja a uma **Conta** ou **Pasta** quando você souber onde o e-mail está
- Use **várias palavras** em vez de uma muito curta
- Use buscas de campo (`subject:`, `from:`) para eliminar correspondências não relacionadas

**Evite:**

- Um único prefixo curto como `re` — como uma busca de prefixo, ele corresponde a uma enorme
  parte do seu Arquivo
- Intervalos de data muito amplos combinados com uma palavra comum
- Navegar por muitas páginas quando você poderia refinar a busca em vez disso

> **💡 Se uma busca parecer lenta, o intervalo de data geralmente é a resposta.** Restringir
> a um mês geralmente transforma uma busca de vários segundos em uma instantânea, porque o
> índice pode pular a maior parte do Arquivo.

## ❓ Solução de Problemas {#troubleshooting}

| Sintoma | Causa | O que fazer |
|---|---|---|
| Nenhum resultado, mas você espera alguns | Termo de busca muito específico ou erro de digitação | Tente menos palavras; verifique a ortografia |
| `cc:name` não encontra nada útil | `cc` não é um campo suportado | Pesquise o nome sem um prefixo |
| Um caractere especial quebra a consulta | `& \| ! ( ) : *` são removidos | Remova ou substitua o caractere |
| Muitos resultados | Palavra comum ou prefixo curto | Adicione um intervalo de data ou busca de campo |
| *"Too Many — Split Required"* | Mais de 250 e-mails selecionados | Reduza a seleção ou divida a operação |
| Um export está faltando | Ele roda como um trabalho em segundo plano | Verifique [Background Jobs](BackgroundJobs.md) |
| Resultados parecem estar faltando | Permissões de Conta | Confirme se a Caixa de correio está atribuída ao seu usuário |
| Datas aparecem deslocadas | Conversão de fuso horário | Verifique sua configuração de fuso horário |

## 📚 Documentação Relacionada {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — restaurando resultados para uma Caixa de correio
- [Background Jobs](BackgroundJobs.md) — monitorando exports e restaurações
- [Access Logging](Logs.md) — o rastro de auditoria de buscas e exclusões
- [REST API](API.md) — o mesmo Arquivo via API
- [MCP Server](MCP.md) — busca a partir de um agente de IA
- [Retention Policies](RetentionPolicies.md) — quando os e-mails saem do Arquivo
