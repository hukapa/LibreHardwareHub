# LibreHardwareMonitor — Arvore de Ficheiros e Registo Arquitetural

> **Finalidade:** Registo central de consulta rapida de toda a codebase do LibreHardwareMonitor.  
> **Regra de Limites de Linhas:** Ficheiros devem respeitar o intervalo de **250 a 450 linhas** sempre que possivel.  
> **Regra Fundamental:** Zero Emojis e responsabilidade unica (SRP).  

---

## 1. Mapa Global da Solucao

```text
LibreHardwareMonitor/
+-- Iniciar-LibreHardwareMonitor.bat             # Launcher rapido com deteccao e arranque elevado
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
|   |   |   +-- BentoCardBase.cs               # Classe base para cartoes Bento com renderizacao GDI+ e temas
|   |   |   +-- CpuBentoCard.cs                # Cartao de CPU com KPIs de Package e Matriz de Nucleos (Core Matrix)
|   |   |   +-- GpuBentoCard.cs                # Cartao de GPU com Core/Hotspot, carga, consumo e barra visual de VRAM
|   |   |   +-- MemoryBentoCard.cs             # Cartao de RAM com barra segmentada Usada/Livre, swap e modulos
|   |   |   +-- StorageBentoCard.cs            # Cartao de Armazenamento com saude SMART, espaco e taxa de leitura/escrita
|   |   |   +-- SystemBentoCard.cs             # Cartao de Motherboard com tensoes principais (+12V, +5V, etc.), ventoinhas e rede
|   |   |   \-- HardwareBentoPanel.cs          # Contentor responsivo de cartoes Bento com suporte a filtros e escopos
|   |   +-- Overlay/                           # In-Game Overlay (HUD) e Keybind Global
|   |   |   +-- GlobalHotkeyManager.cs         # Gestor nativo de atalhos globais via user32.dll RegisterHotKey (Zero Input Lag)
|   |   |   +-- OverlayConfig.cs               # Modelo de dados e persistencia completa de personalizacao do Overlay
|   |   |   +-- OverlayForm.cs                 # Janela HUD fantasma WS_EX_NOACTIVATE e WS_EX_TRANSPARENT com click-through
|   |   |   \-- OverlayEditorPanel.cs          # Estudio de personalizacao visual do Overlay com Live Preview em tempo real
|   |   +-- Controls/                          # Controlos Fluent personalizados
|   |   |   +-- CardPanel.cs                   # Contentor de superficie arredondada com contorno suave
|   |   |   +-- FluentBadge.cs                 # Indicador visual moderno em formato pilula (status, seguranca e alertas)
|   |   |   +-- FluentComboBox.cs              # Caixa de selecao suspensa Fluent com cantos arredondados e chevron nativo
|   |   |   +-- FluentMetricCard.cs            # Cartao KPI Fluent de alta performance (rotulo, valor grande, unidade e severidade)
|   |   |   +-- FluentSectionCard.cs           # Painel de seccao arredondado com cabecalho estilizado e borda suave
|   |   |   +-- SearchBox.cs                   # Caixa de pesquisa estilizada
|   |   |   +-- SidebarPanel.cs                # Barra lateral colapsavel de navegacao
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

---

## 3. Padroes de Seguranca de Hardware e Fail-Safes

1. **Hard Thermal Override:**  
   Se qualquer temperatura monitorizada por uma curva atingir ou exceder 90 C, o motor forca imediatamente 100% de PWM no canal, ignorando restricoes de taxa de variacao e ativando o alerta visual. A saida do modo de emergencia so ocorre com histerese aos 82 C.

2. **Dead-Man's Switch:**  
   Garante a devolucao de todos os canais de controlo para a gestao autonoma da BIOS (`IControl.SetDefault()`) perante qualquer encerramento inesperado (`UnhandledException`, `ThreadException`), fecho regular da aplicacao ou termino de sessao do utilizador.

3. **Prevencao de Regressao de Memoria (TrimWorkingSet Unico):**  
   Invocado estritamente uma unica vez por transicao para o estado minimizado/oculto atraves de `TryTrimWorkingSetOnce()`. Nunca e executado dentro do ciclo continuo do timer, prevenindo hard page faults continuos no kernel.
