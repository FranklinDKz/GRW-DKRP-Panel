# DK RP · Ghost Recon Wildlands

<p align="center">
  <img src="Assets/wildlands-last-rites.jpg" alt="Imagem oficial de Ghost Recon Wildlands: Last Rites" width="760">
</p>

<p align="center"><strong>Painel Windows em português · campanha solo/offline</strong><br>By: FranklinDKz (DK RP)</p>

Painel WPF em preto e branco com acabamento glassmorphism, detecção de `GRW.exe`, seleção do executável, integração local com Cheat Engine x64, controles para os recursos presentes na tabela e Console LOG.

## Estado e compatibilidade

- A Ubisoft anunciou **Last Rites** em agosto de 2026 como a atualização mais recente consultada para Wildlands. Ela adiciona ajustes nativos de dificuldade, percepção inimiga, resistência de veículos, composição da equipe e opções de reviver.
- O `GRW.exe` detectado no computador do autor não expõe uma versão de produto no cabeçalho PE. O painel mostra o metadado disponível e o SHA-256 local, mas a Ubisoft não publica um hash oficial para comparação. **Não é possível confirmar apenas por esse arquivo se a instalação está na versão mais recente.**
- A tabela incluída foi revisada antes do lançamento Last Rites e suas assinaturas de memória não foram certificadas para essa atualização. Quando a inicialização falha ou a assinatura não confere, não habilite os recursos: o erro fica no Console LOG do Cheat Engine e os controles do painel permanecem indisponíveis. O projeto não promete funcionamento “100%”.
- É necessário ter Cheat Engine x64 instalado. Ao clicar em **INJETAR**, o painel cria uma cópia temporária da tabela em `%LOCALAPPDATA%\DKRP\Wildlands`, abre o Cheat Engine, conecta o processo selecionado e tenta inicializar a entrada `INICIALIZAR`. Se o Cheat Engine pedir autorização para o Lua da tabela, confirme essa ação na janela dele.
- Use apenas na campanha solo/offline. O painel não altera Ubisoft Connect, sincronização de contatos, serviços online, anticheat ou partidas com amigos.

> A faixa vermelha “Não foi possível sincronizar os contatos online” é um aviso do jogo/Ubisoft Connect. Ela não é gerada pelo painel nem pelo Cheat Engine. O painel não consegue corrigir nem suprimir uma falha de rede do jogo; seu próprio Console LOG registra separadamente problemas de conexão e de compatibilidade.

## Recursos do painel

- Detecção do processo e seleção de `GRW.exe` quando não estiver aberto.
- Ação **INJETAR** com Cheat Engine x64 e cópia temporária da tabela.
- Ponte Lua local para ativar/desativar entradas pelo painel após validar a inicialização.
- Console LOG com horário e mensagens do Cheat Engine.
- Cartões para invencibilidade, invisibilidade, teleporte, velocidade, salto, NoClip experimental, munição, recuo, precisão e drone.
- Diagnóstico do executável: metadados disponíveis e SHA-256 local.
- QR Pix fornecido pelo autor e chave `dkpixscr@gmail.com`.

## Opções dentro do próprio jogo

Last Rites já oferece opções oficiais que atendem parte do “PATCH DK RP”. Abra **Pausa → Opções → Dificuldade** para ajustar poder de fogo, resistência dos inimigos e dos veículos, percepção inimiga, atraso de regeneração e preset personalizado. A composição da equipe controla a quantidade de colegas IA; Ghost Parameters inclui opções de reviver. World and Session Settings oferece densidade de patrulhas, alerta máximo da Unidad e outros ajustes de sessão.

Essas opções são preferíveis a escrever valores internos sem endereço verificado. Aimbot, ESP, puxar/spawnar veículos, “nunca procurado”, desbloqueios globais e alterações de IA não estão implementados na tabela e não aparecem como botões falsos.

O teleporte existente percorre o destino em trechos e inclui pausas para dar tempo ao carregamento; não garante terreno válido em qualquer ponto. O NoClip permanece experimental e a travessia de colisões não foi validada. Se a validação da build falhar, a tabela deve ser desativada.

## Compilar

Requisitos: Windows 10/11 e .NET SDK 10.

```powershell
dotnet publish .\GRW_DKRP_Panel.csproj -c Release -r win-x64 --self-contained true
```

O executável ficará em `bin\Release\net10.0-windows\win-x64\publish\DKRP-GRW-Panel.exe`. O Cheat Engine não é redistribuído; instale-o a partir do site oficial. A tabela e os recursos visuais precisam permanecer ao lado do painel na pasta publicada.

## Arquivos e logs

- Tabela preservada no projeto: `Cheats/GRW_Atualizado_PTBR.CT`.
- QR Pix: `Assets/pix-dk-rp.png` (imagem recebida do autor, incluída sem recodificar).
- Imagem do jogo: `Assets/wildlands-last-rites.jpg`, hospedada pela Ubisoft e creditada em `ASSETS.md`.
- Log da ponte: `%LOCALAPPDATA%\DKRP\Wildlands\cheat-engine.log`.

## Licença

Código e tabela sob GNU GPL v3.0. A licença não cobre Ghost Recon Wildlands, marcas/imagens da Ubisoft nem o QR de pagamento enviado pelo autor. Consulte `ASSETS.md`.

## Créditos

- FranklinDKz (DK RP).
- Ubisoft, pela imagem oficial e pelas notas de atualização de Last Rites.
- Cheat Engine, pela ponte Lua local documentada no próprio projeto.
- `grw-scripthook` (GPL-3.0), referenciado na tabela para conferência de alguns layouts.

