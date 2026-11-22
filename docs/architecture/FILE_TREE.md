# LibreHardwareHub — Arvore de Ficheiros e Registo Arquitetural

> **Finalidade:** Registo central de consulta rapida de toda a codebase do LibreHardwareHub.  
> **Regra de Limites de Linhas:** Ficheiros devem respeitar o intervalo de **250 a 450 linhas** sempre que possivel.  
> **Regra Fundamental:** Zero Emojis e responsabilidade unica (SRP).  

---

## 1. Mapa Global da Solucao

```text
LibreHardwareMonitor/
+-- Iniciar-LibreHardwareMonitor.bat             # Launcher rapido com deteccao e arranque elevado
+-- CHANGELOG.md                                 # Registo de alteracoes; embutido na aplicacao como alternativa offline das notas de versao
+-- tools/                                      # Ferramentas e instaladores externos desacoplados
|   \-- PawnIO_setup.exe                        # Instalador de Ring-0 PawnIO (3.22 MB desacoplado)
+-- docs/                                      # Hub Central de Documentacao
|   +-- INDEX.md                               # Guia Master de navegacao e sitemap
|   +-- architecture/
|   |   \-- FILE_TREE.md                       # Este ficheiro: registo rapido de todos os ficheiros e funcoes
|   +-- reports/
|   |   \-- RELATORIO_ESTADO_E_MELHORIAS.md    # Relatorio tecnico detalhado (SAC, .NET 8, Tray, Web, Fan Control)
|   +-- history/
|   |   \-- DOCUMENTATION.md                   # Historico cronologico de engenharia
|   \-- agents/
|       \-- AGENTS.md                          # Regras e padroes para programadores e agentes
|
+-- LibreHardwareMonitor.Windows.Forms/            # Projeto Desktop WinForms Principal
|   +-- Program.cs                             # Ponto de entrada, verificacao de integridade, CLI (--exit/--elevate) e crash handler
|   +-- Resources/
|   |   +-- app.manifest                       # Manifest de compatibilidade OS e nivel de execucao (asInvoker)
|   |   \-- Web/
|   |       \-- index.html                     # Dashboard web moderno embutido (HTML5/CSS3/ES6 dark mode)
|   +-- UI/
|   |   +-- MainForm.cs                        # Classe parcial: Construtor, ciclo de vida e estado
|   |   +-- MainForm.Designer.cs               # Controlos graficos gerados pelo designer WinForms
|   |   +-- MainForm.Layout.cs                 # Classe parcial: Layout Fluent moderno, barra lateral e cartao de arvore
|   |   +-- MainForm.Tray.cs                   # Classe parcial: Bandeja do sistema e minimizar ao fechar 'X'
|   |   +-- MainForm.WebServer.cs              # Classe parcial: Controlo do servidor web e dialogos de porta/auth
|   |   +-- MainForm.Plot.cs                   # Classe parcial: Painel de graficos, selecao e sincronizacao
|   |   +-- MainForm.Tree.cs                   # Classe parcial: Gestao de TreeViewAdv, colunas e contexto
|   |   +-- MainForm.Overlay.cs                # Classe parcial: configuracao, telemetria, atalhos e refresh do overlay
|   |   +-- MainForm.Updates.cs                # Classe parcial: pagina Definicoes, servico de atualizacoes e verificacao 6 s apos o arranque
|   |   +-- SensorLookup.cs                    # Escolha de hardware/sensores principais e formatacao de valores (sem LINQ)
|   |   +-- UpdateVisitor.cs                   # Motor de polling adaptativo por tiers (Critico, Normal, Lento)
|   |   +-- SystemTray.cs                      # Gestor de icones na area de notificacao do Windows e menu de perfis
|   |   +-- PlotPanel.cs                       # Painel de desenho com biblioteca OxyPlot
|   |   +-- ScaledPlotModel.cs                 # Modelo escalonado para visualizacao temporal de sensores
|   |   +-- Node.cs                            # No base da arvore de exibicao
|   |   +-- HardwareNode.cs                    # No que encapsula um componente de hardware
|   |   +-- SensorNode.cs                      # No que encapsula um sensor individual
|   |   +-- TypeNode.cs                        # No agrupador por tipo de sensor (Temperatura, Tensao, etc.)
|   |   +-- UserOption.cs                      # Mapeamento bidirecional de definicoes booleanas em menus
|   |   +-- UserRadioGroup.cs                  # Mapeamento de grupos de selecao mutualmente exclusivos
|   |   +-- UnitManager.cs                     # Gestor de unidades de medida (Celsius vs Fahrenheit)
|   |   +-- AboutBox.cs                        # Dialogo "Sobre" com informacoes de versao e creditos
|   |   +-- AuthForm.cs                        # Dialogo para definicao de credenciais do servidor web
|   |   +-- InterfacePortForm.cs               # Dialogo para definicao da porta e IP de escuta HTTP
|   |   +-- ParameterForm.cs                   # Dialogo para ajuste fino de parametros de calibracao
|   |   +-- Dashboard/                         # Painel de Cartoes Bento Inteligentes (Substituicao de Tabelas Brutas)
|   |   |   +-- BentoCardBase.cs               # Base dos cartoes: cabecalho, expansao em linha inteira e lista de sensores agrupada
|   |   |   +-- BentoCardBase.Drawing.cs       # Classe parcial: tiles KPI, barras de uso, legendas e badge
|   |   |   +-- CpuBentoCard.cs                # Cartao de CPU com KPIs de Package e Matriz de Nucleos (Core Matrix)
|   |   |   +-- GpuBentoCard.cs                # Cartao de GPU com Core/Hotspot, carga, consumo e barra visual de VRAM
|   |   |   +-- MemoryBentoCard.cs             # Cartao de RAM com barra segmentada Usada/Livre, swap e modulos
|   |   |   +-- StorageBentoCard.cs            # Cartao de Armazenamento com saude SMART, espaco e taxa de leitura/escrita
|   |   |   +-- SystemBentoCard.cs             # Cartao de Motherboard com tensoes principais (+12V, +5V, etc.), ventoinhas e rede
|   |   |   \-- HardwareBentoPanel.cs          # Contentor responsivo de cartoes Bento com suporte a filtros e escopos
|   |   +-- Overlay/                           # In-Game Overlay (HUD) e Keybind Global
|   |   |   +-- GlobalHotkeyManager.cs         # Gestor nativo de atalhos globais via user32.dll RegisterHotKey (Zero Input Lag)
|   |   |   +-- OverlayConfig.cs               # Opcoes do HUD e persistencia (a ordem da lista de leituras e a ordem de exibicao)
|   |   |   +-- OverlayMetricItem.cs           # Enumeracoes do overlay e modelo de uma leitura
|   |   |   +-- OverlayTelemetry.cs            # Leituras do HUD com sensores em cache; indica se algum texto mudou
|   |   |   +-- OverlayRenderer.cs             # Medicao, disposicao e pintura do HUD (usado pela janela e pela pre-visualizacao)
|   |   |   +-- OverlayForm.cs                 # Janela HUD em camadas sobre DIB persistente, click-through e arrasto
|   |   |   +-- Fps/                           # Medicao de FPS, 1% Low e frametime por ETW (sem injecao, so com o HUD visivel)
|   |   |   |   +-- EtwNative.cs               # P/Invoke de advapi32/user32 e estruturas x64 escritas em offsets documentados
|   |   |   |   +-- FrameTimeBuffer.cs         # Buffer circular sem alocacoes, FpsSnapshot e calculo do 1% Low
|   |   |   |   \-- FpsMonitorService.cs       # Sessao ETW "LibreHardwareHub_FPS_ETW", thread de leitura e filtro do jogo em primeiro plano
|   |   |   +-- OverlayPreviewCanvas.cs        # Pre-visualizacao ao vivo com arrasto de leituras (disposicao livre)
|   |   |   +-- OverlayEditorPanel.cs          # Estudio do HUD: toolbar, pre-visualizacao, aspeto, severidade e posicao
|   |   |   \-- OverlayEditorPanel.Metrics.cs  # Classe parcial: lista de leituras e menu "Adicionar sensor"
|   |   +-- Settings/                          # Pagina Definicoes (Ctrl+7), criada no primeiro clique
|   |   |   +-- SettingsContext.cs             # Opcoes e acoes do MainForm que a pagina usa (mesmos objetos do menu)
|   |   |   +-- SettingsPanel.cs               # Sistema e arranque, atualizacoes (interruptores, procurar agora)
|   |   |   \-- SettingsPanel.Notes.cs         # Classe parcial: notas de versao (GitHub Releases com alternativa embutida)
|   |   +-- Updates/                           # Atualizador nativo (UI)
|   |   |   +-- UpdateService.cs               # Verificar, descarregar em segundo plano e perguntar se reinicia
|   |   |   +-- UpdateDialog.cs                # Dialogo Fluent com notas, progresso e botoes
|   |   |   \-- ReleaseNotesView.cs            # Notas de versao como texto simples com altura automatica
|   |   +-- Controls/                          # Controlos Fluent personalizados
|   |   |   +-- CardPanel.cs                   # Contentor de superficie arredondada com contorno suave
|   |   |   +-- FluentBadge.cs                 # Indicador visual moderno em formato pilula (status, seguranca e alertas)
|   |   |   +-- FluentComboBox.cs              # Caixa de selecao suspensa Fluent com cantos arredondados e chevron nativo
|   |   |   +-- FluentColorPicker.cs           # Botao de cor com amostra e flyout HSV (quadrado, matiz, paleta, hex)
|   |   |   +-- FluentMetricCard.cs            # Cartao KPI Fluent de alta performance (rotulo, valor grande, unidade e severidade)
|   |   |   +-- FluentSectionCard.cs           # Painel de seccao arredondado com cabecalho estilizado e borda suave
|   |   |   +-- SearchBox.cs                   # Caixa de pesquisa estilizada
|   |   |   +-- SidebarItem.cs                 # Entrada de navegacao (texto, icone, tag e seccao)
|   |   |   +-- SidebarPanel.cs                # Navegacao por seccoes recolhiveis, modo compacto, teclado e scroll
|   |   |   +-- SidebarPanel.Paint.cs          # Classe parcial: pintura da barra lateral
|   |   |   \-- SummaryPanel.cs                # Barra superior com cartoes de resumo de telemetria
|   |   +-- FanControl/                        # Subsistema de Controlo Ativo de Ventilacao
|   |   |   +-- FanControlTypes.cs             # Enumeracoes de modos de canal e perfis globais
|   |   |   +-- FanCurvePoint.cs               # Ponto imutavel de curva (Temperatura, PWM %)
|   |   |   +-- FanCurve.cs                    # Interpolacao linear, serializacao e curvas de fabrica
|   |   |   +-- FanChannelConfig.cs            # Definicoes de configuracao e persistencia por canal
|   |   |   +-- FanChannelRuntime.cs           # Motor de canal: media movel, histerese assimetrica e Hard Thermal Override
|   |   |   +-- FanControlEngine.cs            # Orquestrador central, descoberta de IControl e Dead-Man's Switch
|   |   |   +-- FanChannelCard.cs              # Cartao de canal com modo, slider fixo e selecao de curva
|   |   |   \-- FanControlPanel.cs             # Painel integrado de controlo de ventiladores Fluent
|   |   +-- Tuning/                            # Subsistema de Hardware Tuning (GPU e CPU Tuning Seguro)
|   |   |   +-- GpuTuningTypes.cs              # Tipos, capacidades, estado e telemetria de afinacao de GPU
|   |   |   +-- IGpuTuningDevice.cs            # Interface de dispositivo de afincao multi-fabricante
|   |   |   +-- GenericGpuTuningDevice.cs      # Dispositivo generico e deteccao de GPUs integradas (leitura segura)
|   |   |   +-- NvidiaGpuTuningDevice.cs       # Implementacao para adaptadores NVIDIA com clamping e NVML nativo
|   |   |   +-- AmdGpuTuningDevice.cs          # Implementacao para adaptadores AMD Overdrive com limites rigorosos
|   |   |   +-- GpuTuningEngine.cs             # Motor de GPU tuning: Dirty-Boot Watchdog (120s) e Teste Watchdog (15s)
|   |   |   +-- GpuTuningPanel.cs              # Interface Fluent com sliders, telemetria em tempo real e banner de teste
|   |   |   +-- WindowsPowerPolicy.cs          # Gestao de politicas de energia nativas do Windows via powrprof.dll
|   |   |   +-- IntelRaplControl.cs            # Leitura e escrita de limites RAPL (PL1/PL2) via MSR 0x610 (PawnIO)
|   |   |   +-- CpuTuningTypes.cs              # Tipos, capacidades, estado e telemetria de afinacao de CPU
|   |   |   +-- CpuTuningEngine.cs             # Motor de CPU tuning: Camadas Universal + RAPL com Dirty-Boot Watchdog
|   |   |   \-- CpuTuningPanel.cs              # Interface Fluent de controlo de CPU (Boost Mode, Estado Maximo e PL1/PL2)
|   |   +-- Icons/
|   |   |   +-- FluentIcons.cs                 # Gerador vetorial GDI+ de iconografia dinamica por sensor e hardware
|   |   |   \-- FluentIconShapes.cs            # Primitivas geometricas dos icones
|   |   \-- Themes/
|   |       +-- Theme.cs                       # Classe abstrata base e gestao do tema ativo (Auto/Dark/Light)
|   |       +-- DarkTheme.cs                   # Paleta de cores para tema escuro
|   |       +-- LightTheme.cs                  # Paleta de cores para tema claro
|   |       +-- BlackTheme.cs                  # Paleta de cores para tema de contraste preto profundo
|   |       +-- ThemedToolStripRenderer.cs     # Renderizador visual customizado para menus
|   |       +-- ThemedTreeRenderer.cs          # Renderizador de nos da arvore TreeViewAdv
|   |       +-- ThemedScrollIndicators.cs      # Barras de deslocamento estilizadas
|   |       +-- ThemedHScrollIndicator.cs      # Indicador de barra horizontal
|   |       \-- ThemedVScrollIndicator.cs      # Indicador de barra vertical
|   \-- Utilities/
|       +-- HttpServer.cs                      # Servidor HTTP embutido que entrega JSON e o dashboard web
|       +-- PersistentSettings.cs              # Leitura e escrita de definicoes em ficheiro XML/config
|       +-- AppUpdater.cs                      # GitHub Releases (HttpClient + System.Text.Json), descarga e validacao do pacote
|       +-- UpdateApplier.cs                   # Troca do executavel: --apply-update e --cleanup-update
|       +-- ReleaseNotes.cs                    # Markdown para linhas simples e CHANGELOG.md embutido
|       +-- Logger.cs                          # Registo historico de leituras em ficheiro CSV
|       +-- StartupManager.cs                  # Configuracao de arranque automatico com o Windows
|       \-- EmbeddedResources.cs               # Extracao de imagens e recursos binarios embutidos
|
+-- LibreHardwareMonitorLib/                   # Biblioteca Nuclear de Leitura de Hardware
|   +-- Hardware/
|   |   +-- Computer.cs                        # Fachada central: gestao de grupos e traversal de visitantes
|   |   +-- IComputer.cs                       # Interface publica de acesso aos grupos de hardware
|   |   +-- IHardware.cs                       # Contrato para qualquer componente de hardware detetado
|   |   +-- ISensor.cs                         # Contrato de um sensor (Value, Min, Max, SensorType)
|   |   +-- Sensor.cs                          # Implementacao base de sensor com historico temporal
|   |   +-- Hardware.cs                        # Implementacao base com suporte a arvore hierarquica
|   |   +-- SensorType.cs                      # Enumeracao (Voltage, Clock, Temperature, Load, Fan, Flow, Power, etc.)
|   |   +-- Control.cs                         # Implementacao de IControl para manipulacao de portas PWM
|   |   +-- IControl.cs                        # Contrato de controlo de hardware (SetDefault, SetSoftware)
|   |   +-- ThreadAffinity.cs                  # Gestao otimizada de afinidade de thread com cache local [ThreadStatic]
|   |   +-- Cpu/                               # Leitura de CPU (Intel, AMD Zen/Ryzen) via MSR e PawnIO
|   |   +-- Gpu/                               # Leitura de placas graficas (Nvidia NVAPI, AMD ADL, Intel)
|   |   +-- Memory/                            # Memoria virtual, memoria fisica total e deteccao SPD
|   |   +-- Motherboard/                       # Chips Super I/O (ITE, Nuvoton, Winbond, Fintek, SMSC)
|   |   +-- Storage/                           # SMART de discos HDD, SSD SATA e NVMe
|   |   +-- Controller/                        # Controladores externos (AquaComputer, NZXT, Corsair, Razer, Arctic)
|   |   +-- Network/                           # Placas de rede (taxas de download e upload via Performance Counters)
|   |   +-- Psu/                               # Fontes de alimentacao inteligentes (Corsair, MSI)
|   |   \-- Battery/                           # Baterias de portateis (capacidade restante, taxa de descarga)
|   +-- Interop/
|   |   +-- Kernel32.cs                        # Chamadas Win32 nativas para memoria e processos
|   |   \-- SetupApi.cs                        # Enumeracao de dispositivos de hardware do Windows
|   \-- PawnIO/                                # Suporte ao driver moderno PawnIO para acesso Ring-0
|
\-- Aga.Controls/                              # Componente WinForms TreeViewAdv Multicoluna
    +-- Tree/
    |   +-- TreeViewAdv.cs                     # Controlo de arvore com renderizacao virtualizada de alto desempenho
    |   +-- TreeColumn.cs                      # Definicao de coluna (largura, cabecalho, visibilidade)
    |   +-- TreeModel.cs                       # Modelo de dados hierarquico que alimenta a arvore
    |   \-- NodeControls/                      # Controlos embutidos nas celulas (CheckBox, TextBox, Icon)
    \-- Threading/
        \-- AbortableThreadPool.cs             # Pool de threads para tarefas assincronas de interface
```

---

## 2. Registo Detalhado de Responsabilidades por Ficheiro

### 2.1 Modulo de Interface Desktop (`LibreHardwareMonitor.Windows.Forms`)

| Ficheiro | Responsabilidade Principal | Limite de Linhas | Dependencias Criticas |
|---|---|---|---|
| `Program.cs` | Ponto de entrada, verificacao de integridade de ficheiros, CLI (--exit/--elevate), mutex unico e crash handler. | <= 250 lin | `System.Windows.Forms`, `MainForm`, `FanControlEngine` |
| `UI/MainForm.cs` | Orquestracao principal do formulario, ciclo de vida, construtor, polling adaptativo e despacho de atualizacoes. | <= 800 lin | `Computer`, `PersistentSettings`, `FanControlEngine` |
| `UI/MainForm.Layout.cs` | Disposicao Fluent do ecra: CardPanel, Toolbar de pesquisa, KPI summary strip e alternancia de paineis. | <= 250 lin | `SidebarPanel`, `SummaryPanel`, `FanControlPanel` |
| `UI/MainForm.Overlay.cs` | Cria a janela do HUD e os atalhos globais, aplica e persiste a configuracao e atualiza HUD e pre-visualizacao apos cada ciclo de sensores. | <= 150 lin | `OverlayForm`, `OverlayTelemetry`, `GlobalHotkeyManager` |
| `UI/SensorLookup.cs` | Escolha do hardware principal (GPU dedicada, memoria fisica), pesquisa de sensores por palavras-chave e formatacao por tipo de sensor. | <= 250 lin | `IHardware`, `ISensor` |
| `UI/Overlay/OverlayTelemetry.cs` | Resolve e guarda em cache os sensores de cada leitura (reavaliados a cada 10 ciclos) e devolve se algum texto mudou. | <= 300 lin | `SensorLookup`, `OverlayConfig` |
| `UI/Overlay/OverlayRenderer.cs` | Unico codigo de layout e pintura do HUD, com fontes em pixeis (independente do DPI). | <= 350 lin | `OverlayConfig`, `OverlayTelemetry` |
| `UI/Overlay/OverlayForm.cs` | Janela em camadas: pinta diretamente numa seccao DIB selecionada num DC de memoria e chama `UpdateLayeredWindow`. | <= 350 lin | `OverlayRenderer`, `user32.dll`, `gdi32.dll` |
| `UI/Overlay/OverlayEditorPanel.cs` | Pagina do estudio; cada controlo e criado por um helper que o liga a um valor da configuracao. | <= 450 lin | `OverlayPreviewCanvas`, `FluentColorButton` |
| `UI/Overlay/Fps/EtwNative.cs` | Chamadas Win32 de ETW (`StartTraceW`, `EnableTraceEx2` com filtro por id de evento, `OpenTraceW`, `ProcessTrace`, `CloseTrace`) e leitura de `EVENT_RECORD` sem alocar. Tudo x64. | <= 250 lin | `advapi32.dll`, `user32.dll` |
| `UI/Overlay/Fps/FrameTimeBuffer.cs` | Buffer circular de 4096 frames e array de ordenacao reutilizavel; FPS e frametime (1 s) e 1% Low (5 s, media dos `ceil(n/100)` frames mais lentos). Sem alocacoes. | <= 200 lin | `Stopwatch` |
| `UI/Overlay/Fps/FpsMonitorService.cs` | Ciclo de vida da sessao ETW (Start/Stop idempotentes, sessao orfa, `ProcessExit`), DXGI e DxgKrnl sem dupla contagem e processo em primeiro plano. | <= 350 lin | `EtwNative`, `FrameTimeBuffer` |
| `UI/MainForm.Updates.cs` | Liga a pagina Definicoes e o servico de atualizacoes ao formulario; verificacao unica 6 s apos o arranque por um temporizador de interface. | <= 150 lin | `SettingsPanel`, `UpdateService` |
| `UI/Settings/SettingsPanel*.cs` | Pagina Definicoes (Ctrl+7): arranque, bandeja, tema, unidade, atualizacoes e notas de versao; criada no primeiro clique. | <= 300 lin | `SettingsContext`, `UserOption`, `UpdateService` |
| `UI/Updates/UpdateService.cs` | Estado da verificacao, descarga opcional em segundo plano e dialogo de reinicio; nada corre entre verificacoes. | <= 200 lin | `AppUpdater`, `UpdateApplier`, `UpdateDialog` |
| `Utilities/AppUpdater.cs` | Consulta as Releases do GitHub (um `HttpClient`, 15 s, `User-Agent`), compara versoes, descarrega e valida o ZIP portatil. | <= 250 lin | `System.Net.Http`, `System.Text.Json` |
| `Utilities/UpdateApplier.cs` | Troca o executavel com `--apply-update PID DESTINO` e limpa `%TEMP%` com `--cleanup-update`, sem tocar em `portable.txt`, definicoes nem registos. | <= 250 lin | `AppInfo`, `AppUpdater` |
| `UI/Dashboard/BentoCardBase.cs` | Geometria dos cartoes (`IWrapGridItem`), recolha de sensores detalhados apenas quando expandido e layout unico para medir e pintar. | <= 400 lin | `WrapGrid`, `SensorLookup` |
| `UI/Controls/SidebarPanel.cs` | Linhas de navegacao por seccao, estado recolhido persistente, selecao por rato e teclado. | <= 400 lin | `SidebarItem`, `Theme` |
| `UI/MainForm.Tray.cs` | Controlo da barra de notificacoes (System Tray), intercepcao do comando fechar no botao 'X' e esvaziamento unico de WorkingSet. | <= 150 lin | `SystemTray`, `FormClosingEventArgs`, `psapi.dll` |
| `UI/MainForm.WebServer.cs` | Ativacao, paragem e configuracao da porta de escuta e credenciais de seguranca do servidor HTTP. | <= 100 lin | `HttpServer`, `InterfacePortForm`, `AuthForm` |
| `UI/MainForm.Plot.cs` | Sincronizacao do formulario ou painel embutido de graficos, selecao de cores de sensores e rotacao de logs. | <= 300 lin | `PlotPanel`, `OxyPlot`, `Logger` |
| `UI/MainForm.Tree.cs` | Gestao de eventos da arvore `TreeViewAdv`, redimensionamento de colunas, cliques, atalhos de teclado e menu de contexto. | <= 400 lin | `TreeViewAdv`, `SensorNode`, `HardwareNode` |
| `UI/UpdateVisitor.cs` | Polling adaptativo por tiers (Tier Lento 30s, Tier Normal 2-5s, Tier Critico 800ms-1000ms com Fan Control). | <= 150 lin | `IVisitor`, `IHardware` |
| `UI/FanControl/FanControlEngine.cs` | Orquestrador de canais `IControl`, aplicacao de perfis globais e Dead-Man's Switch para seguranca de hardware. | <= 300 lin | `IControl`, `ISensor`, `PersistentSettings` |
| `UI/FanControl/FanChannelRuntime.cs` | Avaliacao de PWM por canal com media movel, histerese assimetrica (+1.5C/-3.0C) e Hard Thermal Override (>=90C -> 100%). | <= 200 lin | `IControl`, `FanChannelConfig` |
| `UI/FanControl/FanControlPanel.cs` | Painel Fluent para monitorizacao e configuracao de todas as ventoinhas com seletor de perfil e telemetria. | <= 200 lin | `FanControlEngine`, `FanChannelCard` |
| `UI/FanControl/FanChannelCard.cs` | Cartao interativo com modos BIOS, Fixo (slider) e Curva (botoes de perfis), e banner de alerta termico. | <= 250 lin | `FanChannelRuntime`, `Theme` |
| `UI/Tuning/GpuTuningEngine.cs` | Orquestrador de afincao de GPU, Dirty-Boot Watchdog (120s) e Temporizador de Teste de Seguranca (15s). | <= 300 lin | `IGpuTuningDevice`, `PersistentSettings` |
| `UI/Tuning/GpuTuningPanel.cs` | Painel Fluent para overclock/undervolt seguro de GPU com cartoes de telemetria, sliders e protecao de teste. | <= 350 lin | `GpuTuningEngine`, `IThemedControl` |
| `UI/Tuning/NvidiaGpuTuningDevice.cs` | Especializacao de afincao para NVIDIA com limites rigorosos de seguranca (clamping). | <= 150 lin | `GenericGpuTuningDevice`, `GpuTuningState` |
| `UI/Tuning/AmdGpuTuningDevice.cs` | Especializacao de afincao para AMD Overdrive com limites de protecao termica e energetica. | <= 150 lin | `GenericGpuTuningDevice`, `GpuTuningState` |
| `UI/Tuning/GenericGpuTuningDevice.cs` | Implementacao base segura com deteccao passiva de GPUs integradas e telemetria pura sem escritas arriscadas. | <= 150 lin | `IGpuTuningDevice`, `IHardware` |
| `Utilities/HttpServer.cs` | Servidor HTTP multithreaded que escuta requisicoes REST (`/data.json`) e serve o dashboard web embutido. | <= 450 lin | `HttpListener`, `IElement`, `Node` |
| `Utilities/PersistentSettings.cs` | Mecanismo de persistencia de configuracoes em XML com fallback automatico para ficheiro `.backup`. | <= 250 lin | `System.Xml`, `ISettings` |
| `Utilities/AppInfo.cs` | Nome e identificador da aplicacao, pasta de dados (AppData ou portatil), caminhos de configuracao e crash log, migracao do `.config` antigo e nome do evento de saida. | <= 150 lin | `System.IO` |
| `installer/LibreHardware.iss` | Instalador Inno Setup: ficheiros, atalhos, tarefas opcionais (arranque, PawnIO) e fecho limpo da instancia em execucao antes de atualizar. | <= 150 lin | Inno Setup 6 |
| `scripts/Publicar-Release.ps1` | Publica o executavel autonomo e gera instalador e ZIP portatil em `dist/`. | <= 120 lin | `dotnet publish`, `ISCC.exe` |
| `scripts/Lancar-Release.ps1` | Lanca uma versao: valida, exige notas no `CHANGELOG.md`, atualiza `Directory.Build.props`, compila, faz commit, tag anotada e push (`-Simular` so valida). | <= 200 lin | `git`, `dotnet`, `Notas-Versao.ps1` |
| `scripts/Notas-Versao.ps1` | Funcao `Get-ReleaseNotesSection`: extrai do `CHANGELOG.md` as notas de uma versao (aceita `## vX.Y.Z`, `## [X.Y.Z]` e `## X.Y.Z`). | <= 80 lin | `CHANGELOG.md` |
| `.github/workflows/release.yml` | Em cada tag `v*`: confere a versao, compila, gera o ZIP portatil e o instalador (se possivel) e publica a Release com `gh release create`. | <= 150 lin | `Publicar-Release.ps1`, `Notas-Versao.ps1`, `gh` |

---

## 3. Padroes de Seguranca de Hardware e Fail-Safes

1. **Hard Thermal Override:**  
   Se qualquer temperatura monitorizada por uma curva atingir ou exceder 90 C, o motor forca imediatamente 100% de PWM no canal, ignorando restricoes de taxa de variacao e ativando o alerta visual. A saida do modo de emergencia so ocorre com histerese aos 82 C.

2. **Dead-Man's Switch:**  
   Garante a devolucao de todos os canais de controlo para a gestao autonoma da BIOS (`IControl.SetDefault()`) perante qualquer encerramento inesperado (`UnhandledException`, `ThreadException`), fecho regular da aplicacao ou termino de sessao do utilizador.

3. **Prevencao de Regressao de Memoria (TrimWorkingSet Unico):**  
   Invocado estritamente uma unica vez por transicao para o estado minimizado/oculto atraves de `TryTrimWorkingSetOnce()`. Nunca e executado dentro do ciclo continuo do timer, prevenindo hard page faults continuos no kernel.
