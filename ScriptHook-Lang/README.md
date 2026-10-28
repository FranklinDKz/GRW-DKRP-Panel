# Tradução PT-BR do GRW ScriptHook Reforged

Tradução da interface principal e dos plugins que acompanham o pacote beta6. Não altera DLLs nem executáveis.

## Instalar

No PowerShell, execute a partir desta pasta:

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
