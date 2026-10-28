# Instalação do ScriptHook Reforged em português

Este pacote reúne o arquivo oficial **GRW ScriptHook Reforged 1.0-beta6**, os arquivos de idioma PT-BR e um instalador para Windows. O ScriptHook é um projeto de terceiros; os créditos, a licença GPL-3.0 e o código-fonte correspondente estão incluídos.

## Instalação automática

1. Extraia todo o conteúdo de `DKRP-ScriptHook-PTBR-Instalador.zip` para uma pasta.
2. Feche Ghost Recon Wildlands.
3. Clique com o botão direito em `Install-ScriptHook-PTBR.ps1` e escolha **Executar com PowerShell**. Se a política do Windows bloquear o script, abra PowerShell nessa pasta e execute:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\Install-ScriptHook-PTBR.ps1
   ```

4. Escolha a pasta principal do jogo, a mesma que contém `GRW.exe`.
5. Aguarde a confirmação e inicie o jogo novamente. O idioma fica definido como **Português (Brasil)**.

O instalador valida o SHA-256 do pacote oficial e faz uma cópia dos arquivos que serão substituídos em uma pasta `.dkrp-scripthook-backup-*` dentro da pasta do jogo. Ele não inicia o jogo nem altera saves.

## Instalação manual

Extraia `GRW-ScriptHook-Reforged-1.0-Beta6.zip` ao lado de `GRW.exe`, preservando as pastas `plugins` e `mods`. Depois execute `Install-ptBR.ps1 -GameRoot "CAMINHO\PARA\Wildlands"` para adicionar a tradução e reinicie o jogo.

## Compatibilidade e avisos

- Esta distribuição usa exatamente o pacote oficial beta6, sem modificar seus executáveis ou plugins. O SHA-256 oficial é `e712721902b8d1e403d08d69c2164c0ad3109c0bec130bc3c21f9162bff05b1e`.
- O autor do ScriptHook verifica as builds do jogo. Se a sua build não for aceita, não force o carregamento; consulte o log do ScriptHook e a página oficial.
- Recomendado somente para campanha solo/offline. Não há promessa de compatibilidade com sessões online.
- O ScriptHook pode conflitar com outro `dinput8.dll` ou loader. O instalador guarda uma cópia dos arquivos substituídos para recuperação.

Código-fonte e licença: consulte `grw-scripthook-beedf74-source.zip` e `GRW-ScriptHook-LICENSE-GPL-3.0.txt` incluídos neste pacote.
