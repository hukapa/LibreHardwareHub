# Registo de alterações

Cada versão começa com um título `## vX.Y.Z - AAAA-MM-DD`. Este ficheiro é embutido na aplicação e serve de
alternativa às notas das Releases do GitHub quando não há ligação à Internet.

## v0.11.0 - 2026-11-21

### Novidades
- Nova pagina de Definicoes (`Ctrl+7`) com arranque automatico sem UAC, tema da interface, unidade de temperatura, opcoes de atualizacao e historico de notas de versao (online com fallback offline embutido).
- Atualizador nativo atraves das Releases do GitHub com verificacao de integridade SHA-256, dialogo Fluent de progresso e substituicao limpa do executavel preservando configuracoes e modo portatil.
- Pipeline automatizado de publicacao de releases via GitHub Actions e script `Lancar-Release.ps1`.

### Melhorias e Correcoes
- Blindagem do foco de teclado no In-Game Overlay (`ShowWithoutActivation`, `MA_NOACTIVATE` e `ShowNoActivate`) para nunca interromper teclas em jogos como Roblox e reforco de Z-order `HWND_TOPMOST`.
- Refactor visual minimalista nos temas Escuro, Preto OLED e Claro, com barras de 3 px, badges suaves, barra lateral refinada e respiro lateral alinhado.
- Corrigido o hash do Git na barra de titulo e o corte na linha de diagnostico SMART do cartao de armazenamento.
- Corrigidas fugas de recursos no desregisto de atalhos globais (`GlobalHotkeyManager`) e no evento `PowerModeChanged`.

## v0.10.1 - 2026-11-21

- Overlay HUD: deixa de roubar o foco do teclado ao jogo ao ser ligado pelo atalho global ou ao ser arrastado.
- Overlay HUD: o HUD é mantido por cima de jogos em modo Sem Bordas sem provocar cintilação. Em ecrã inteiro exclusivo continua a ser necessário o modo Sem Bordas.
- Interface minimalista: nova paleta escura, preta e clara, mini-blocos planos, barras de 3 px e selos de estado suaves.
- Barra lateral com indicador do item ativo e cartões com respiro adequado em relação à barra lateral.
- Cartão de armazenamento: a linha de diagnóstico SMART já não é cortada na base.
- A barra de título mostra apenas o número da versão.
- Nova página Definições (Ctrl+7) com arranque com o Windows, bandeja, tema, unidades, atualizações e notas de versão.
- Atualizações automáticas nativas a partir das Releases do GitHub, sem serviços em segundo plano.

## v0.10.0 - 2026-11-20

- Nova página Saúde dos discos (Ctrl+6) com avaliação S.M.A.R.T. para discos ATA e NVMe, sem acessos adicionais ao disco.
- Distinção entre vida reportada pelo disco (SSD e NVMe) e saúde estimada (discos mecânicos).
- Tabela de valores S.M.A.R.T. expansível e copiável por disco.
- Os cartões de armazenamento do painel principal usam o mesmo veredicto de saúde.

## v0.9.6 - 2026-11-05

- Identidade LibreHardwareHub, executável autónomo e dados em %AppData%\LibreHardwareHub.
- Modo portátil com o ficheiro portable.txt ao lado do executável.
- Instalação mais robusta do driver PawnIO em sistemas novos.
