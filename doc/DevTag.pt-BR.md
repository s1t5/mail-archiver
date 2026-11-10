# 🧪 Usando Versões de Desenvolvimento (Dev Tag) {#using-development-versions-dev-tag}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia explica como alternar da versão estável `latest` para a versão de desenvolvimento `dev` do Mail Archiver para testar correções de bugs e novos recursos. Isso é particularmente útil para usuários que desejam testar correções antes que elas sejam lançadas oficialmente.

## ⚠️ Considerações Importantes {#important-considerations}

Antes de alternar para a versão de desenvolvimento, considere o seguinte:

- **Versões de desenvolvimento podem conter bugs ou recursos instáveis** que não estão presentes no lançamento estável
- **Sempre faça o Backup dos seus dados** antes de alternar para uma versão de desenvolvimento
- **Use versões de desenvolvimento apenas para testes** e não em ambientes de produção
- **Relate quaisquer problemas** que você encontrar para ajudar a melhorar o software

## 🛑 Parando o Container (Recomendado) {#stopping-the-container-recommended}

Antes de alternar para a versão de desenvolvimento, recomenda-se parar o container:

```bash
docker compose down
```

## 🔄 Alternando para a Dev Tag {#switching-to-the-dev-tag}

1. Abra seu arquivo `docker-compose.yml` em um editor de texto

2. Localize a seção do serviço `mailarchive-app` e altere a tag da imagem de `latest` para `dev`:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Salve as alterações no seu arquivo `docker-compose.yml`

## 📥 Baixando a Imagem de Desenvolvimento {#pulling-the-development-image}

Após atualizar o arquivo docker-compose, você precisa baixar a imagem de desenvolvimento:

```bash
docker compose pull
```

Este comando baixará a versão de desenvolvimento mais recente da imagem do Mail Archiver.

## ▶️ Iniciando o Container com a Nova Versão {#starting-the-container-with-the-new-version}

Após baixar a nova imagem, inicie o container para usar a versão de desenvolvimento:

```bash
docker compose up -d
```

## 🔁 Voltando para o Lançamento Estável {#switching-back-to-stable-release}

Se você quiser retornar à versão estável, siga estas etapas:

1. Pare o container:
```bash
docker compose down
```

2. Edite `docker-compose.yml` e altere a imagem de volta para:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Baixe a imagem mais recente:
```bash
docker compose pull
```

4. Inicie o container:
```bash
docker compose up -d
```

## 📝 Notas Adicionais {#additional-notes}

- Versões de desenvolvimento são atualizadas com mais frequência do que os lançamentos estáveis
- Se você encontrar problemas, verifique os logs em busca de mensagens de erro:
  ```bash
  docker compose logs mailarchive-app
  ```
- Você pode verificar qual versão está sendo executada no momento consultando as informações da imagem do container:
  ```bash
  docker compose images
  ```
