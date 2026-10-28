# GRW ScriptHook Reforged em português do Brasil

Esta pasta contém a tradução do menu principal e dos plugins do ScriptHook Reforged 1.0-beta6, além do instalador do framework oficial e do idioma. A distribuição do ScriptHook permanece inalterada; créditos, GPL-3.0 e fonte correspondente estão em `THIRD_PARTY_NOTICES.md` e no instalador completo publicado na release.

## Instalação completa

Baixe `DKRP-ScriptHook-PTBR-Instalador.zip` na página de Releases do painel e siga `README-Instalacao.md` (incluído como `LEIA-ME.md` no pacote). O instalador extrai o ScriptHook e configura o idioma PT-BR.

## Apenas aplicar a tradução

Se o ScriptHook já estiver instalado, execute no PowerShell:

```powershell
.\Install-ptBR.ps1 -GameRoot "D:\SteamLibrary\steamapps\common\Wildlands"
```

O instalador preserva os idiomas existentes, substitui somente as seções `[pt-BR]` desta tradução e cria cópias `.dkrp-ptbr.bak` antes de alterar cada INI. Depois, feche e abra o jogo para carregar os textos. No menu do ScriptHook, pressione F4 → **Configurações do ScriptHook** → **Idioma do menu** e escolha **Português (Brasil)**, caso não esteja selecionado.

O catálogo de veículos fica em F4 → **Veículos**. Para configurar vídeos de abertura, abra F4 → **Pular vídeos de abertura**; o plugin informa que as mudanças exigem reinicialização.

## Arquivos

- `pt-BR/lang.ini`: interface principal do ScriptHook.
- `pt-BR/plugins/*/lang.ini`: interface dos plugins incluídos.
- `Install-ptBR.ps1`: mescla as traduções sem substituir os textos em inglês ou chinês.

O ScriptHook e os plugins binários são distribuídos pelo [projeto original](https://github.com/GameXueRen/grw-scripthook/releases/tag/v1.0-beta6); esta pasta contém apenas a tradução.
