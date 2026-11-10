# 📧 Original Message Storage {#original-message-storage}

[← Voltar para o Índice da Documentação](index.md)

## 📋 Visão Geral {#overview}

Por padrão, o Arquivo mantém as **partes analisadas** de um e-mail: cabeçalhos, corpos em texto e HTML, anexos. Um `.eml` exportado é reconstruído a partir desses campos.

Isso é suficiente para ler um e-mail, mas não é a mensagem **conforme recebida**. Codificações de transferência, limites MIME e o dobramento de cabeçalho diferem do original, portanto, uma mensagem reconstruída não verifica mais sua assinatura DKIM e os scanners de spam a pontuam de forma diferente.

Com o armazenamento da mensagem original habilitado, o Arquivo mantém a mensagem completa conforme ela chegou, juntamente com os campos analisados. O original armazenado é o que o Arquivo pode provar que foi entregue.

| | Apenas campos analisados | Com o original armazenado |
| --- | --- | --- |
| Cabeçalhos, corpo, anexos | ✅ | ✅ |
| Mensagem byte a byte exata | ❌ | ✅ |
| Assinatura DKIM verifica | ❌ | ✅ |
| Uso forense (tribunal, auditoria) | limitado | ✅ |

## 🎯 Onde o original é servido {#where-the-original-is-served}

Uma vez armazenado, o original é retornado onde quer que uma mensagem completa seja esperada:

| Local | O que acontece |
| --- | --- |
| Botão **Export EML** na página de detalhes do e-mail | Serve o original armazenado quando um existe, recorrendo à reconstrução para e-mails arquivados sem um. |
| **Restore to a mailbox** | Anexa o original armazenado quando um existe, recorrendo à reconstrução para e-mails arquivados sem um. |
| `GET /api/v1/emails/{id}/raw` | Download via REST API, veja o [REST API guide](API.md#download-original-message). |
| `get_email_raw` | Ferramenta MCP, veja o [MCP guide](MCP.md#get_email_raw). |

A resposta informa qual forma você recebeu, para que o chamador possa distinguir um original genuíno de uma reconstrução sem inspecionar os bytes:

| Sinal | Significado |
| --- | --- |
| `X-Raw-Message-Source: imap` | Cópia exata do servidor IMAP (`BODY[]` conforme entregue). |
| `X-Raw-Message-Source: graph` | Cópia exata, o conteúdo MIME retornado pelo Exchange. |
| `X-Raw-Message-Source: eml-file` | Cópia exata, o conteúdo do arquivo `.eml` importado. |
| `X-Raw-Message-Source: serialized` | Mensagem analisada e escrita novamente. Idêntica, exceto que as quebras de linha são CRLF (importação de MBox, busca de recuperação IMAP). |
| `X-Raw-Message-Sha256` | SHA-256 do original armazenado. |
| `X-Raw-Message-Reconstructed: true` | Nenhum original armazenado; a mensagem foi reconstruída. Sem cabeçalho de checksum. |
| `X-Raw-Message-Storage` | Backend de onde o original foi lido (`postgres` ou `filesystem`). |

O nome do arquivo também indica: `email-{id}-original-...eml` para um original armazenado, `email-{id}-...eml` para uma reconstrução.

Quando um original armazenado **falha em seu checksum**, a exportação e a restauração são recusadas com uma mensagem de erro. Retornar a reconstrução em vez disso ocultaria o fato de que a cópia arquivada foi alterada, o que é exatamente o que um Arquivo nunca deve fazer.

Uma restauração lê o original armazenado e o anexa através do cliente do provedor, para que a Caixa de correio receba os cabeçalhos e a estrutura MIME originais. O fluxo de bytes exato não é garantido no cabo porque o cliente serializa a mensagem novamente; a própria cópia arquivada permanece inalterada.

## ⚙️ Configurações {#settings}

Todas as configurações residem na seção `RawMessages`, ou como variáveis de ambiente com o prefixo `RawMessages__`.

| Configuração | Padrão | Descrição |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Armazena a mensagem original de cada e-mail recém-arquivado. E-mails arquivados antes de ativar esta opção não possuem o original. |
| `RawMessages:MaxMessageSizeMb` | `50` | Mensagens maiores que este valor são arquivadas sem o seu original. `0` = sem limite. |
| `RawMessages:IncludeGraph` | `true` | Também armazena o original para Contas Microsoft 365 (Graph). Custa uma requisição Graph adicional por mensagem, o que conta para o orçamento de throttling. |
| `RawMessages:Storage` | `postgres` | Onde os bytes da mensagem são mantidos: `postgres` (no banco de dados) ou `filesystem` (arquivo gzip ao lado do Arquivo). Veja [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Diretório onde os arquivos de mensagem são gravados, relativo à raiz de conteúdo da aplicação. Aponte isso para um volume montado no Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Número de níveis de diretório de dois caracteres sobre os quais o id do e-mail é distribuído. `2` gera 10.000 diretórios, `0` é plano. Mantém a contagem de arquivos por diretório baixa em Arquivos grandes. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Recomputa o SHA-256 do arquivo em cada download e o compara com o checksum registrado no momento do Arquivo. Uma divergência faz o download falhar com HTTP 500 em vez de servir bytes alterados. |

### Dimensionamento {#sizing}

O armazenamento cresce aproximadamente pelo tamanho de cada e-mail, incluindo seus Anexos, compactados em gzip. Anexos **não** passam por Deduplicação dentro do original armazenado, portanto, um e-mail com um Anexo grande custa esse Anexo duas vezes: uma vez no armazenamento de Anexos com Deduplicação e uma vez dentro do original. Planeje para aproximadamente o **dobro** do tamanho do banco de dados de um Arquivo sem originais.

Ativar a configuração não preenche retroativamente os e-mails existentes. Apenas e-mails recém-arquivados recebem um original.

## 💾 Backend de Armazenamento {#storage-backend}

A configuração `Storage` decide onde os bytes residem. A **linha de metadados sempre permanece no
banco de dados**, independentemente de qual backend esteja configurado, e contém o SHA-256, tamanho,
fonte de captura e (para arquivos) o caminho relativo. Essa linha é o que permite ao Arquivo
provar o que uma mensagem armazenada deve conter.

### postgres (default) {#postgres-default}

A mensagem compactada reside na coluna `Content` de `mail_archiver."RawMessages"`.
Este é o comportamento que o Arquivo sempre teve e não requer volume extra.

Escolha esta opção quando o banco de dados já for sua unidade de Backup e você quiser apenas
uma coisa para fazer o Backup e a Restauração.

### filesystem {#filesystem}

A mensagem é gravada em `{BasePath}/{xx}/{yy}/{id}.eml.gz`, onde `xx`/`yy` são derivados
do id do e-mail. A linha do banco de dados mantém apenas os metadados.

Escolha esta opção quando o Arquivo for grande o suficiente para que os originais dominem o
tamanho do banco de dados, ou quando você quiser colocar os arquivos de mensagem em um armazenamento
mais barato ou separado.

!!! danger "Monte um volume no Docker"
    O `BasePath` padrão (`RawMessages`) resolve para `/app/RawMessages` **dentro do
    container**. Sem um bind mount, esse diretório pertence à camada gravável do container,
    então cada container recriado - incluindo `docker compose down && docker compose
    up -d`, uma atualização de imagem ou um reboot do host com uma camada efêmera - leva cada
    original armazenado com ele, enquanto as linhas do banco de dados sobrevivem.

    Sempre monte o diretório no host ao usar o backend filesystem:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    O caminho do host (`./raw-messages` aqui) é o que você faz o Backup. O caminho do container deve
    corresponder a `RawMessages:FileSystem:BasePath`, ou defina `RawMessages:FileSystem:BasePath` para o
    ponto de montagem que você escolheu. Se você mover a montagem, mantenha `BasePath` apontando para ela: os
    valores de `StoragePath` armazenados são relativos, portanto, continuam sendo resolvidos.

### Alternando backends {#switching-backends}

As leituras seguem o armazenamento registrado em cada linha, não a configuração definida. Após
você alterar `Storage`, as mensagens arquivadas anteriormente permanecem baixáveis de onde quer que estejam.
A configuração afeta apenas e-mails **novamente** arquivados.

Para mover o que já está armazenado, execute a migração de armazenamento. Consulte os
[CLI commands](CLI.md#moving-stored-original-messages) para a referência completa:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

O que a migração garante:

- Cada mensagem é verificada contra seu checksum registrado antes de ser copiada. Uma mensagem
  que falha é deixada onde está e reportada: um original corrompido nunca é propagado e
  sua única cópia restante nunca é deletada.
- Um arquivo é deletado apenas após a transação do banco de dados que o moveu ter sido confirmada,
  então uma execução interrompida não pode perder a única cópia.
- A execução é retomável. O trabalho é selecionado pela coluna `StorageType`, portanto, iniciar o
  comando novamente continua com o que ainda está pendente.
- O código de saída é `1` se qualquer mensagem falhar, `0` caso contrário.

Uma migração não é necessária para o Arquivo funcionar. Ela é necessária apenas para mover os
bytes existentes para o outro backend.

## 🛟 Backup e restauração {#backup-and-restore}

=== "postgres backend"
    Nada extra. Os originais fazem parte do banco de dados e são cobertos pelo dump
    normal, veja [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Faça o Backup do diretório `BasePath` **junto com** o dump do banco de dados.

    Uma restauração de banco de dados sem os arquivos deixa o Arquivo com linhas cujo conteúdo
    não pode ser servido. O download então responde HTTP 404 em vez de retornar algo
    errado, e os próprios e-mails permanecem legíveis a partir de seus campos analisados.

## 🗑️ Excluindo um e-mail {#deleting-an-email}

Excluir um e-mail remove seu original junto com ele: as regras de Retenção, a exclusão manual individual e em massa, e a exclusão de uma Conta de e-mail inteira removem o arquivo como parte da mesma operação. Com o backend `postgres`, não há nada extra a fazer, porque os bytes viajam dentro da linha que o cascade já remove.

O arquivo é excluído **após** a linha do e-mail ser confirmada, nunca antes. As duas etapas não podem ser uma única transação, porque um arquivo não faz parte da transação do banco de dados, portanto, uma delas deve vir em segundo lugar. Esta ordem é a segura: uma exclusão que falha ou sofre rollback, por exemplo, em um registro travado, deixa o original intacto, e uma falha entre as duas etapas deixa um arquivo sem uma linha, que a varredura abaixo recupera. A outra ordem destruiria o original de um e-mail que ainda existe.

**Arquivos órfãos são coletados.** Qualquer coisa que tenha passado, incluindo arquivos deixados por exclusões de antes desta limpeza existir, é removida por uma varredura periódica de `BasePath` que compara cada arquivo com as linhas e exclui aqueles aos quais nenhum e-mail se refere:

| Setting | Default | Meaning |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Ativa ou desativa a varredura |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Com que frequência ela é executada |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Arquivos mais novos que isso nunca são tocados |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Atraso antes da primeira execução, para que uma migração de armazenamento pendente termine primeiro |

O período de carência é o que torna a varredura segura: uma importação escreve um arquivo antes de confirmar a linha que aponta para ele, e um arquivo escrito momentos atrás não deve ser confundido com um órfão. Arquivos `.tmp` parcialmente escritos de uma gravação interrompida são coletados sob a mesma regra. Com o backend `postgres`, a varredura não faz nada e nem sequer percorre o diretório.

## 🔍 Verificando um Arquivo {#verifying-an-archive}

O checksum registrado no momento do Arquivo é o que torna a verificação possível.

- **No download** (`VerifyChecksumOnRead=true`, o padrão) o arquivo é transformado em hash e
  comparado. Uma divergência recusa o download com HTTP 500.
- **Na migração de armazenamento** cada mensagem é verificada antes de ser copiada, em ambos
  os sentidos.

Como a linha de metadados está no banco de dados e os bytes podem estar no disco, uma comparação de checksum
é a única maneira de notar que um arquivo foi alterado fora do aplicativo.

## ⚠️ Coisas para saber {#things-to-know}

- **Ativar não realiza o preenchimento retroativo.** Apenas e-mails arquivados após a mudança recebem um original.
- **A reconstrução não é a mensagem.** Para e-mails sem um original armazenado, cada
  caminho de download retorna uma mensagem reconstruída e informa isso via
  `X-Raw-Message-Reconstructed: true`.
- **Filesystem significa duas unidades de backup.** O banco de dados e o diretório de mensagens devem ser restaurados
  juntos, caso contrário, as linhas sobrevivem sem o seu conteúdo.
- **`VerifyChecksumOnRead` consome CPU.** Cada download de uma mensagem grande é descompactado
  e processado via hash. Deixe ativado, a menos que você tenha medido um problema; é a única proteção
  contra alteração silenciosa no disco.
- **O original armazenado não passa por deduplicação.** Um anexo que aparece em muitos e-mails
  é armazenado uma vez no armazenamento de anexos, mas uma vez por e-mail dentro do original.
- **Excluir também remove o original.** Um e-mail excluído por retenção ou manualmente leva
  seu original armazenado junto, arquivo e linha. Nada é mantido que o Arquivo não
  liste mais.

## 🔗 Veja também {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - a migração de armazenamento completa
- [Setup and Parameters](Setup.md) - todos os parâmetros de configuração
- [REST API](API.md#download-original-message) - o endpoint `/raw`
- [MCP Server](MCP.md#get_email_raw) - a ferramenta `get_email_raw`
- [Backup and Restore](BackupRestore.md) - o que fazer o Backup
- [Attachment Deduplication](AttachmentDeduplication.md) - como os Anexos são armazenados
