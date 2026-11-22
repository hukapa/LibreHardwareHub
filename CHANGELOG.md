# Registo de alterações

Cada versão começa com um título `## vX.Y.Z - AAAA-MM-DD`. Este ficheiro é embutido na aplicação e serve de
alternativa às notas das Releases do GitHub quando não há ligação à Internet.

## v0.12.0 - 2026-11-22

### Novidades
- Medicao em tempo real de FPS, 1% Low e Frametime (ms) no In-Game Overlay via ETW nativo (DXGI e DxgKrnl), sem injecao de DLLs, sem dependencias externas e com ativacao 100% on-demand (0% CPU quando o HUD esta oculto).
- Secao "Leituras" do Estudio de Overlay redesenhada: sensores ativos sempre agrupados no topo com reordenacao por drag-and-drop (com guia visual, auto-scroll e cancelamento por Esc) e sensores disponiveis em chips compactos de 1 clique.
- Barra lateral com item "Definicoes" fixo no fundo (sticky bottom) e accordions refinados com hover, chevron vetorial e contador de itens nas seccoes recolhidas.
- Metrica "VRAM Usada" no Overlay passa a suportar os tres formatos compactos (`GB + %`, `So GB`, `Usado / Total`), barra de progresso e cor automatica.

### Melhorias e Correcoes
- Auditoria global de sensores: centralizacao em `SensorLookup` garantindo distincao rigorosa entre GPU Core e GPU Hot Spot nos cartoes, ventoinhas, ajustes e resumo, alem de limites termicos proprios para GPU e legendas dinamicas reais.
- Endurecimento de seguranca no atualizador elevado e no instalador PawnIO: revalidacao SHA-256 com bloqueio de leitura contra substituicao em `%TEMP%`, validacao estrita de argumentos e reparse points em `--apply-update` / `--cleanup-update`, e comparacao em tempo constante na autenticacao do servidor web.
- Corrigida fuga de handles GDI no `ToolTip` do Estudio de Overlay ao reconstruir linhas e chips, e reforcado o ciclo de vida e pinning da sessao ETW em caso de arranque parcial falhado.

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
