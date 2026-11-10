# 🌍 Idioma e Localização {#language-and-localization}

[← Voltar para o Índice da Documentação](index.md)

## 📋 Visão Geral {#overview}

A interface do Mail Archiver está disponível em **14 idiomas**. O idioma é uma
**preferência por navegador** armazenada em um cookie — não está vinculado a uma Conta de usuário
e nunca afeta o e-mail arquivado em si: as mensagens são sempre armazenadas exatamente
como foram recebidas.

## 🌐 Idiomas suportados {#supported-languages}

| Idioma | Código |
|----------|------|
| English (default) | `en` |
| English (GB) | `en-GB` |
| Slovenščina | `sl` |
| Deutsch | `de` |
| Italiano | `it` |
| Français | `fr` |
| Español | `es` |
| Nederlands | `nl` |
| Русский | `ru` |
| Magyar | `hu` |
| Polski | `pl` |
| Português (Brasil) | `pt-BR` |
| 日本語 | `ja` |
| 简体中文 | `zh` |

**O inglês é o padrão e o fallback.** Qualquer coisa que uma tradução não cubra
é exibida em inglês, portanto, um idioma parcialmente traduzido nunca deixará você com um
rótulo vazio.

## 🔄 Alterando o idioma {#switching-the-language}

1. Clique no seu **username** no canto superior direito para abrir o menu de usuário.
2. Em **Language**, escolha um idioma da lista — a página recarrega
   imediatamente naquele idioma.

A escolha é gravada em um cookie que é válido por **um ano** e se aplica
apenas a este navegador. Outros usuários, e o mesmo usuário em outro navegador ou em
outro dispositivo, mantêm sua própria configuração. Como o cookie sobrevive à sessão,
o idioma ainda estará selecionado após você sair e entrar novamente.

## 🧩 O que a configuração de idioma afeta — e o que não afeta {#what-the-language-setting-does-and-does-not-affect}

- **Traduzido:** todo o texto da interface — menus, botões, rótulos de formulário, validação e
  mensagens de erro, páginas de status de tarefas e as descrições de ação do registro de auditoria.
- **Não traduzido:** e-mail arquivado. Corpos, assuntos e Anexos são armazenados
  e exibidos byte a byte conforme recebidos.
- **Busca:** a sintaxe de consulta é a mesma em todos os idiomas; veja
  [Mail search](Search.md). Alterar o idioma não altera como o e-mail é
  armazenado ou indexado.
- **Timestamps:** as datas são exibidas no fuso horário local do navegador e
  formatadas para a cultura selecionada. Para fixar o fuso horário de exibição em toda a instância,
  defina `TimeZone__DisplayTimeZoneId` (identificador IANA, ex:
  `Europe/Berlin`) — veja [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ Para contribuidores: adicionando ou alterando traduções {#for-contributors-adding-or-changing-translations}

As strings traduzidas residem em `Resources/`:

| Arquivo | Propósito |
|------|---------|
| `SharedResource.resx` | Arquivo de recurso neutro — **English**, 1148 chaves |
| `SharedResource.<culture>.resx` | Um arquivo por idioma, ex: `SharedResource.de.resx` |

Regras que derivam de como o .NET resolve os recursos aqui:

1. **Adicione novas chaves ao arquivo neutro primeiro**, depois aos arquivos de idioma. Uma chave
   que esteja faltando em uma tradução utiliza o texto em inglês como fallback — nada
   quebra, a página apenas permanece parcialmente em inglês.
2. **Uma chave que existe apenas em um arquivo de tradução é peso morto.** Ela nunca
   é consultada, porque a busca começa no arquivo neutro. (No momento da redação,
   `DeleteAccountAllEmailsButton` e `Sign Out` existem apenas em algumas traduções,
   e o arquivo polonês também contém chaves de designer remanescentes: `Bitmap1`, `Icon1`,
   `Name1`.)
3. **`en-GB` é uma sobreposição parcial, por design.** Ela define apenas 245 das 1148
   chaves e herda o restante do arquivo neutro em inglês — razão pela qual
   parece inglês, exceto onde a terminologia britânica ou os formatos de data diferem.
   Chinês (Simplificado), Alemão, Espanhol, Francês, Húngaro, Italiano,
   Japonês, Holandês, Polonês, Português (Brasil), Russo e Esloveno são
   atualmente completos.
4. **Adicionar um idioma inteiro** requer três coisas: um novo
   `SharedResource.<culture>.resx`, uma entrada na lista de culturas em `Program.cs`
   (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`),
   e um `<option>` no menu suspenso de idiomas em
   `Views/Shared/_UserInfo.cshtml`. Sem os dois últimos, o arquivo nunca é usado.

Veja [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md)
para o processo de contribuição, padrões de codificação e como enviar um pull request.

## 🔗 Documentação Relacionada {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — sintaxe de consulta
- [User Management](UserManagement.md) — funções e Permissão
