# v1.1.0-standalone-beta.12

- Inclui no GitHub o ScriptHook Reforged oficial beta6, seu código-fonte correspondente e a licença GPL-3.0.
- Adiciona um instalador PT-BR que valida o pacote por SHA-256, permite selecionar a pasta do GRW e guarda cópias dos arquivos que substituir.
- Separa claramente a tradução simples do instalador completo do ScriptHook.

# v1.1.0-standalone-beta.11

- Disponibiliza o ScriptHook Reforged oficial beta6 junto de um instalador PT-BR, com seletor da pasta do jogo, validação SHA-256 e backup de arquivos substituídos.
- Inclui os arquivos de tradução, a licença GPL-3.0, avisos de terceiros e o código-fonte correspondente ao commit upstream `beedf74`.
- Mantém separada a distribuição do painel e do ScriptHook; recomenda-se usar o framework apenas na campanha solo/offline.

# v1.1.0-standalone-beta.10

- Inclui o pacote oficial GRW ScriptHook Reforged beta6 no GitHub sem modificar seus binários.
- Adiciona instalador PT-BR com seletor da pasta do jogo, validação SHA-256 e cópia de segurança de arquivos que serão substituídos.
- Inclui tradução, licença GPL-3.0, avisos de terceiros e arquivo de código-fonte do commit upstream correspondente.

# v1.1.0-standalone-beta.9

- Traduz o menu principal do ScriptHook e os plugins instalados para português do Brasil.
- Adiciona instalador de idioma que preserva o inglês/chinês, faz backup dos INIs e seleciona pt-BR.
- Inclui no ZIP portable somente os arquivos de tradução (não inclui DLLs de terceiros).
- Corrige a validação do botão F4: consulta `logs\\scripthook.log` e verifica se Spawner/SkipIntro carregaram no PID atual.

# v1.1.0-standalone-beta.8

- Adiciona ao painel o botão para abrir o menu F4 do ScriptHook, após verificar o carregador, os plugins, o processo e o log da sessão.
- Expõe pelo complemento oficial as páginas de catálogo de veículos e SkipIntro; ambas dependem de reiniciar o jogo após a instalação.
- Mantém a captura reutilizável do marcador e a validação dos estados 1 e 5.

# v1.1.0-standalone-beta.7

- Corrige a validação do marcador na tabela: aceita os estados 1 e 5, incluindo o estado 5 que a rotina identificada grava ao definir um destino.
- Reutiliza o último marcador capturado com NUMPAD 0; colocar outro marcador atualiza o destino.
- Atualiza mensagens de ativação/erro para explicar que basta colocar o marcador uma vez por destino.
- O instalador agora gera arquivos beta.7 e mantém hashes SHA-256 dos binários e da tabela.

# v1.1.0-standalone-beta.6

- Porta para o painel munição ilimitada, pente sem consumo, bateria ilimitada do drone e proteção contra explosões, com validação das assinaturas/bytes antes de alterar memória.
- Reorganiza o teleporte para subir a uma altitude de trânsito, avançar horizontalmente e descer em etapas; aplica a mesma rota à tabela `.CT`.
- Remove do teleporte da tabela o limite fixo de 5 km, mantendo validação de coordenadas e interrupção segura.
- Detecta o hook de marcador antigo que estava ativo na execução aberta e orienta reiniciar o jogo em vez de instalar captura sobre captura.
- Continua sem aimbot, ESP, wallhack, hit kill, alterações de IA ou modificações online: não há rotinas validadas para esses recursos.

# v1.1.0-standalone-beta.4

- Adiciona à tabela PT-BR uma entrada independente para abrir o painel instalado.
- Inclui a tabela `.CT` no ZIP portátil e como arquivo separado no release; o painel continua executável sem Cheat Engine.
- Atualiza a interface e a documentação para registrar que o limite de explosivos implantados não foi validado nem alterado.
- Mantém o uso restrito à campanha solo/offline; não há recurso online ou anti-banimento.

# v1.1.0-standalone-beta.3

Este beta substitui a prévia que chamava o Cheat Engine. O painel agora é um aplicativo Windows independente e não precisa de Cheat Engine instalado.

- Inclui executável portátil, instalador por usuário e arquivo ZIP portátil.
- Ícone DK RP incluído no instalador e no executável principal do painel.
- Reescreve o README em português e documenta o diagnóstico, limites conhecidos e uso offline.
- Adiciona orientação para o Aim Assist nativo do controle. O painel não altera diretamente essa configuração nem implementa assistência para mouse e teclado.
- Remove a tabela `.CT` antiga do pacote independente.
- Os recursos de memória continuam experimentais e não foram validados em sessão limpa da versão atual do jogo.

Use somente em campanha solo/offline. A opção de confirmação no painel não detecta o estado da sessão.
