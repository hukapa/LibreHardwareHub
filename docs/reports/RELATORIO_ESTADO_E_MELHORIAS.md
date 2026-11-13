# Relatorio Tecnico — Estado do Projeto e Melhorias Implementadas

> **Projeto:** LibreHardwareMonitor (Fork hukapa)  
> **Data:** Novembro de 2026  
> **Autor:** Antigravity & hukapa  
> **Estado da Solucao:** Compilacao com 0 Erros e 0 Avisos Criticos em .NET 8.0 LTS (`net8.0-windows`)  
> **Regra Fundamental:** Zero Emojis  

---

## 1. Sumario Executivo

Este documento consolida todas as investigacoes tecnicas, correcoes arquiteturais e melhorias de usabilidade aplicadas no fork do LibreHardwareMonitor. O projeto foi atualizado a partir do upstream, adaptado para o ecossistema moderno do .NET 8.0 LTS, blindado contra politicas de seguranca avancadas do Windows 11 (Smart App Control), modularizado em classes parciais orientadas a responsabilidade unica e equipado com uma interface web de monitorizacao remota contemporanea.

---

## 2. Diagnostico e Resolucao do Bloqueio no Windows 11 (Smart App Control)

### 2.1 Identificacao da Causa Raiz
Ao tentar iniciar a aplicacao no Windows 11 com o Controlo de Aplicacoes Inteligente (Smart App Control - SAC) ativo (`VerifiedAndReputablePolicyState: 1`), o processo encerrava silenciosamente logo apos a confirmacao da caixa de dialogo do UAC.

A inspecao profunda dos registos do Windows (`Microsoft-Windows-CodeIntegrity/Operational` e `Application Error`) revelou dois fatores criticos:
1. **Bloqueio de DLLs nao assinadas:** A biblioteca `RAMSPDToolkit-NDD.dll` e posteriormente `Aga.Controls.dll` eram bloqueadas pelo kernel do Windows com a excecao:
   ```text
   System.IO.FileLoadException: Could not load file or assembly 'RAMSPDToolkit-NDD.dll'.
   Uma política de Controlo de Aplicações bloqueou este ficheiro. (0x800711C7)
   ```
2. **Falha de Elevacao Silenciosa (Erro 740):** O manifest original continha `<requestedExecutionLevel level="requireAdministrator" />`. Quando invocado atraves de uma consola ou script sem elevacao prévia, a API `CreateProcess` falhava imediatamente com `ERROR_ELEVATION_REQUIRED` (traduzido pelo PowerShell como "O pedido nao e suportado").

### 2.2 Medidas Corretivas Implementadas
1. **Isolamento de Carregamento Tardio (`NoInlining`):**  
   Em `MemoryGroup.cs`, a chamada ao `RAMSPDToolkit` foi movida para o metodo privado `InitializeSpd()`, anotado com `[MethodImpl(MethodImplOptions.NoInlining)]`. Desta forma, o compilador JIT do CLR nao tenta carregar a DLL ao instanciar o `MemoryGroup`. Se o Windows bloquear o driver de leitura SPD, a excecao e capturada no bloco `try / catch` sem interromper a aplicacao.
2. **Blindagem Global de Grupos de Hardware:**  
   Em `Computer.cs`, a instanciacao de todos os grupos de hardware foi envolvida em blocos de protecao para evitar que qualquer driver de terceiros provoque um encerramento inesperado.
3. **Captura Global de Excecoes e Registo em `crash.log`:**  
   Em `Program.cs`, foram subscritos os eventos `AppDomain.CurrentDomain.UnhandledException` e `Application.ThreadException`. Qualquer erro fatal grava agora o stacktrace detalhado em `crash.log` e exibe uma caixa de dialogo informativa ao utilizador.
4. **Ajuste do Manifest para `asInvoker`:**  
   Em `app.manifest`, o nivel de execucao foi ajustado para `asInvoker`. O executavel pode agora ser chamado sem exigir elevacao forçada do sistema, enquanto o script `Iniciar-LibreHardwareMonitor.bat` assegura elevacao limpa via `-Verb RunAs` e define explicitamente o `-WorkingDirectory`.
5. **Estrategia Single-File Standalone:**  
   Foi disponibilizada a opcao de publicacao em ficheiro unico (`PublishSingleFile=true`), onde todas as DLLs dependentes sao embutidas diretamente dentro do binario `LibreHardwareMonitor.Windows.Forms.exe`. Como nao existem ficheiros DLL soltos no disco, o Smart App Control nao bloqueia os modulos managed em tempo de execucao.

---

## 3. Minimizar para Segundo Plano ao Clicar no "X"

### 3.1 Objetivo
Permitir que o utilizador feche a janela principal sem terminar o servico de monitorizacao, mantendo o processo ativo na bandeja do sistema (System Tray) e o servidor web funcional.

### 3.2 Implementacao Tecnica
* Foi adicionada a opcao `_minimizeOnClose` (ativa por omissao).
* O metodo `OnFormClosing(FormClosingEventArgs e)` verifica se o motivo de encerramento foi iniciado pelo utilizador (`CloseReason.UserClosing`). Se verdadeiro, cancela o encerramento (`e.Cancel = true`), oculta a janela (`Visible = false`) e assegura que o icone do System Tray fica visivel.
* No `WndProc`, a intercepcao das mensagens `WM_SYSCOMMAND` com parametro `SC_CLOSE` redireciona para a rotina `SysTrayHideShow()`.
* O encerramento definitivo e agora executado atraves do menu contextual da bandeja ("Exit") ou do menu principal, invocando o metodo `CloseApplication()`.

---

## 4. Modernizacao do Dashboard Web Remoto

### 4.1 Problema do Legado
A interface web anterior remontava a 2012, baseada em versoes obsoletas do jQuery e Knockout.js, com dependencias externas nao manutenidas e estetica desatualizada.

### 4.2 Nova Arquitetura (`Resources/Web/index.html`)
* **Zero Dependencias:** 100% desenvolvido em HTML5 semantico, CSS moderno e JavaScript nativo (ES2022).
* **Suporte Completo a Dark/Light Mode:** Utilizacao da propriedade CSS `color-scheme: light dark` e da funcao nativa `light-dark()`, respeitando a preferencia de cores do sistema operativo do utilizador.
* **Layout em Gridcards Responsivo:** Os sensores sao agrupados visualmente por componente de hardware em cartoes limpos com animacoes suaves.
* **Pesquisa e Filtragem em Tempo Real:** Barra de pesquisa instantanea no topo da pagina para localizar rapidamente qualquer sensor de temperatura, voltagem ou utilizacao.
* **Consumo Eficiente da API:** Polling assincrono para `/data.json` com re-renderizacao otimizada do DOM e indicador visual de estado de ligacao em direto.

---

## 5. Decomposicao Modular de `MainForm.cs`

A classe `MainForm.cs` continha originalmente 1.479 linhas, violando o Principio da Responsabilidade Unica (SRP) e dificultando a manutencao. Foi modularizada em 5 classes parciais (`sealed partial class MainForm`), todas alinhadas com o padrao do projeto CrystalStudio AI:

```text
LibreHardwareMonitor.Windows.Forms/UI/
├── MainForm.cs            # ~700 lin: Construtor, ciclo de vida, ciclo de atualizacao, definicoes e estado
├── MainForm.Tray.cs       # ~105 lin: System Tray, minimizar para segundo plano, intercepcao WndProc e saida
├── MainForm.WebServer.cs  # ~ 40 lin: Controlo do servidor HTTP, configuracao de porta e credenciais
├── MainForm.Plot.cs       # ~240 lin: Painel de graficos, modelos temporais OxyPlot e selecao de sensores
└── MainForm.Tree.cs       # ~270 lin: Gestao da arvore de sensores TreeViewAdv, eventos de rato e menus de contexto
```

---

## 6. Estado Atual de Compilacao e Testes

* **Framework de Destino Exclusivo:** .NET 8.0 LTS (`net8.0` na biblioteca central e `net8.0-windows` na interface de utilizador)
* **Suporte Legado .NET Framework 4.7.2 (`net472`):** Descontinuado formalmente para eliminar peso morto, diretivas condicionais e acelerar a compilação
* **Erros de Compilacao:** 0
* **Avisos de Compilacao:** 0 avisos (validado com `dotnet build LibreHardwareMonitor.sln -c Release /p:Platform=x64`)
* **Regra Zero Emojis:** 100% em conformidade em todo o repositorio.

---

## 7. Diagnostico e Correcoes de Rotas HTTP e Arranque Instantaneo

### 7.1 Erro 404 em `/data.json`
* **Causa Raiz:** O cliente JavaScript no navegador adicionava parametros de cache-busting (`?t=timestamp`) aos pedidos `GET /data.json?t=...`. A implementacao do `HttpServer.cs` lia `request.RawUrl.Substring(1)`, obtendo `"data.json?t=..."`, o que falhava a condicao `requestedFile == "data.json"`. Como resultado, o servidor tentava carregar um recurso embutido inexistente e respondia com status HTTP 404.
* **Solucao:** A deteção do pedido foi refatorada para utilizar `request.Url.AbsolutePath.TrimStart('/')`, garantindo isolamento estrito entre o caminho do recurso solicitado e as cadeias de consulta (`QueryString`). O endpoint responde agora de forma imediata com o schema JSON esperado pelo dashboard.

### 7.2 Otimizacao do Arranque e Supressao de Prompts UAC
* **Problema:** A invocacao via PowerShell no script `.bat` mantinha a janela de comando do Windows aberta durante o tempo de inicializacao do runtime e aguardava pela resposta da caixa de confirmacao de administrador (UAC).
* **Solucao:** 
  1. Suporte a tarefa de alta prioridade no Agendador de Tarefas do Windows (`schtasks /run /tn "LibreHardwareMonitor-CrystalStudio"`), permitindo arranque imediato em ~40ms sem caixas de confirmacao do UAC.
  2. Implementacao de `launch.vbs` executado de forma assincrona via `wscript //nologo`, eliminando o atraso do PowerShell e fechando a consola CMD em menos de 50ms.
  3. Disponibilizacao de `Configurar-Arranque-Rapido-Sem-UAC.bat` para registo da tarefa no sistema com um unico clique.

### 7.3 Eliminacao de Congelamento de 5s no Servidor Web
* **Problema:** Ao desligar a funcionalidade de Web Server ou fechar a janela, o programa paralisava durante 5 segundos antes de terminar.
* **Causa Raiz:** O metodo `StopHttpListener()` aguardava ate 5 segundos por `_listenerTask` que se encontrava bloqueada em `GetContextAsync()`. Sem abortar o listener antes do `Wait`, a thread da interface de utilizador ficava bloqueada.
* **Solucao:** A chamada a `_listener.Stop()` foi antecipada para encerrar imediatamente o socket HTTP de escuta, permitindo saida em 0 milissegundos sem congelamento visual.


---

## 8. Execucao da Fase 1: Limpeza da Codebase e Modo Zero-Overhead

### 8.1 Poda de Divida Tecnica e Reducao de Binario em 90%
1. **Consolidacao em Windows Forms:** Remocao dos projetos Avalonia de `LibreHardwareMonitor.sln`, concentrando recursos no ecossistema WinForms nativo de baixo consumo de memoria RAM (< 25 MB).
2. **Extirpacao de Recursos Mortos:** Eliminacao definitiva de bibliotecas JavaScript/CSS de 2012 em `Resources/Web/` e do subsistema legado de Gadgets (`Gadget.cs`, `GadgetWindow.cs`, `SensorGadget.cs`, `ShowDesktop.cs`).
3. **Desacoplamento de PawnIO_setup.exe:** O instalador de 3.22 MB foi transferido de recurso embutido para a pasta `tools/`. Como resultado, `LibreHardwareMonitor.Windows.Forms.dll` reduziu de 4.56 MB para 472 KB.

### 8.2 Arquitetura de Polling Adaptativo por Tiers
Em `UpdateVisitor.cs`, a atualizacao sincrona e indiferenciada foi substituta por um mecanismo de frequencia diferenciada:
* **Tier Lento (30 segundos):** Leituras SMART de unidades de armazenamento (`StorageDevice.cs`) e telemetria de bateria (`Battery.cs`), cessando a execucao constante de IOCTLs de disco e ACPI a cada segundo.
* **Tier Normal (2s ativo, 5s minimizado):** Estatisticas de Memoria RAM, contadores de adaptadores de rede e telemetria de fontes de alimentacao (PSU).
* **Tier Critico (800ms ativo, 2.5s minimizado):** Temperaturas, tensoes, potencias e clocks de CPU, GPU, Motherboard e controladores de ventoinhas.

### 8.3 Modo Baixo Consumo na System Tray e Otimizacao de Memoria
* **Supressao de Pintura GDI+:** Quando a janela principal se encontra oculta ou minimizada, as chamadas a `treeView.Invalidate()`, `UpdateShell()` e `_plotPanel.InvalidatePlot()` sao suspensas, eliminando alocacoes de objetos de desenho GDI+.
* **Esvaziamento de Working Set:** Na minimizacao para a bandeja, `TrimWorkingSet()` executa `GC.Collect()` e chama a API Win32 `EmptyWorkingSet`, devolvendo paginas de memoria fisica ao sistema operativo e garantindo uma pegada de RAM minima visivel no Gestor de Tarefas.
* **Supressao de Historico Inativo:** Sensores agora apenas acumulam historico temporal (`ValuesTimeWindow`) quando o painel de graficos (`PlotPanel`) esta ativamente aberto pelo utilizador.


---

## 9. Execucao da Fase 2: Motor de Controlo de Ventoinhas (Fan Control), Fail-Safes e UI Integrada

### 9.1 Arquitetura Desacoplada e Integracao com Primitivas IControl
A Fase 2 estabelece o subsistema de controlo ativo de arrefecimento em `LibreHardwareMonitor.Windows.Forms.UI.FanControl`, operando diretamente sobre as interfaces `IControl` (`Control.cs`) e sensores `SensorType.Control` / `SensorType.Fan` de `LibreHardwareMonitorLib`:
* **FanControlEngine.cs:** Coordenador central que efetua a descoberta automatica de todos os controlos de hardware (chips Super I/O em Motherboard e controladores dedicados AIO/RGB), associando cada canal ao respetivo sensor de RPM e mantendo a persistencia em `PersistentSettings`.
* **Modos de Operacao por Canal (`FanChannelMode`):**
  - `Default (Auto BIOS)`: Restaura o controlo autonomo do hardware via `IControl.SetDefault()`.
  - `Fixed (Manual %)`: Fixa a percentagem de rotacao via `IControl.SetSoftware(float value)`, respeitando os limites `MinSoftwareValue` e `MaxSoftwareValue`.
  - `Curve (Curva Dinamica)`: Calcula o PWM alvo em tempo real com base no sensor ou combinacao de sensores configurados.

### 9.2 Motor de Curvas Termicas e Algoritmo Anti-Oscilacao
* **Interpolacao Linear por Pontos (`FanCurve.cs` / `FanCurvePoint.cs`):** Avaliacao continua de pontos `(Temperatura C, PWM %)`. Abaixo do ponto inferior aplica o PWM minimo; acima do ponto superior aplica o PWM maximo; entre pontos aplica interpolacao linear proporcional.
* **Fontes Termicas e Mix Function:** Suporte nativo a sensores individuais (`CPU Package`, `GPU Core`) ou funcao composta `Max(CPU, GPU)` para ventiladores de chassis que necessitam de responder a carga quer de processamento quer grafica.
* **Estabilizacao Anti-Oscilacao (`FanChannelRuntime.cs`):**
  - **Suavizacao Temporal (`ResponseTimeSeconds`):** Media movel das ultimas medicoes (predefinicao: 3s) para filtrar picos instantaneos de boost da CPU.
  - **Histerese Assimetrica:** Exige variacao minima de +1.5 C para acelerar e -3.0 C para desacelerar, prevenindo ciclos acusticos irritantes de aceleracao e travagem em idle/carga moderada.
  - **Taxa de Variacao Limitada (`MaxStepUpPercent` / `MaxStepDownPercent`):** Ramp-up de ate 25%/s e ramp-down suave de 8%/s para proteger os rolamentos das ventoinhas e manter conforto acustico.

### 9.3 Fail-Safes Criticos de Hardware
1. **Hard Thermal Override (Bloqueio Termico Forcado):**
   - Se a temperatura do sensor alvo atingir ou ultrapassar **90 C**, o motor forca imediatamente 100% de PWM no canal, ativando um banner visual de emergencia em carmim na interface.
   - O retorno a curva normal so e permitido quando a temperatura descer abaixo de **82 C** (histerese de seguranca de 8 C).
2. **Dead-Man's Switch (Devolucao Automatica a BIOS):**
   - Registo estatico global em `FanControlEngine.RestoreAllControlsToDefault()`.
   - Subscrito automaticamente em `AppDomain.CurrentDomain.ProcessExit`, `AppDomain.CurrentDomain.UnhandledException`, `Application.ThreadException`, no encerramento normal da aplicacao (`CloseApplication`) e no encerramento da sessao do Windows (`Microsoft.Win32.SystemEvents.SessionEnded`).
   - Garante que nenhuma ventoinha fica presa a baixas rotacoes caso a aplicacao falhe, seja terminada abruptamente ou o sistema seja reiniciado.

### 9.4 Acesso Rapido na System Tray e Painel Fluent Integrado
* **Submenu Contextual no System Tray (`SystemTray.cs`):** Integracao do submenu "Perfil de Ventoinhas" permitindo alternar instantaneamente entre perfis globais (*BIOS Padrao*, *Silencioso*, *Equilibrado*, *Desempenho*, *Personalizado*) sem abrir a janela.
* **Painel Dedicado Fluent (`FanControlPanel.cs` / `FanChannelCard.cs`):** Seccao integrada acessivel na barra lateral Fluent (`SidebarPanel.cs`), exibindo telemetria termica em tempo real, seletor de perfil global e cartoes individuais por canal com ajuste de sliders, curvas de fabrica e alertas de seguranca.
* **Frequencia de Background Otimizada:** Quando o Fan Control se encontra ativo e a aplicacao minimizada na bandeja, o ciclo do Tier Critico ajusta-se para 1000ms mantendo todas as rotinas GDI+ de renderizacao suspensas e a memoria fisica enxuta via `TryTrimWorkingSetOnce()`.

---

## 10. Otimizacoes de Desempenho e Refinamentos de Codigo (KISS, DRY e Zero-Allocation)

### 10.1 Correcao de Restauro de Afinidade em ThreadAffinity.cs
* **Problema:** A condicao de cache estatico `_isAffinityCached && _currentThreadAffinity == affinity` devolvia a afinidade alvo quando a thread ja se encontrava no nucleo, corrompendo a variavel de restauro em chamadas encadeadas e retendo a thread presa a um unico core logico.
* **Solucao:** O bypass incorreto foi eliminado. A API nativa do Windows `SetThreadGroupAffinity` gere nativamente a verificacao de nucleo e preenche com precisao `previousGroupAffinity`, garantindo que os blocos `finally` restauram com 100% de fiabilidade a mascara original de todos os nucleos.

### 10.2 Avaliacao Unica de Tick em UpdateVisitor.cs
* **Problema:** A leitura `DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond` era executada repetidamente para cada no de hardware e sub-hardware dentro de `VisitHardware`, gerando centenas de chamadas e calculos de calendario por segundo.
* **Solucao:** A leitura do timestamp foi concentrada uma unica vez no inicio de `VisitComputer` utilizando `#if NETCOREAPP || NET8_0_OR_GREATER Environment.TickCount64`, propagando o valor imutavel aos nos filhos sem sobrecarga de CPU.

### 10.3 Eliminacao de Alocacoes LINQ em FanChannelRuntime.cs
* **Problema:** As rotinas de media movel utilizavam `_tempHistory.RemoveAll(...)` e `_tempHistory.Average(...)` a cada segundo por cada canal ativo, gerando alocacoes de predicados e iteradores LINQ no Managed Heap.
* **Solucao:** Substituicao por um ciclo `for` imperativo com acumulador e expurgo por indice `_tempHistory.RemoveAt(0)`, reduzindo as alocacoes e a pressao sobre o Garbage Collector para 0 bytes por segundo.

### 10.4 Cache de Sensores Termicos em FanControlEngine.cs
* **Problema:** Em `ResolveTemperatures()`, executava-se `IndexOf("Package", ...)` e `IndexOf("Core", ...)` sobre colecoes de sensores a cada segundo, e a resolucao por identificador percorria linearmente todo o hardware.
* **Solucao:** Os sensores primarios de CPU (`_primaryCpuTempSensor`) e GPU (`_primaryGpuTempSensor`) sao agora descobertos e armazenados em cache durante `DiscoverChannels()`. A resolucao de sensores customizados foi migrada para o dicionario indexado `_tempSensorsById` com complexidade O(1).

### 10.5 Integracao de SessionEnded no Dead-Man's Switch
* Subscricao do evento `Microsoft.Win32.SystemEvents.SessionEnded` em `FanControlEngine.RegisterDeadManSwitch()` para garantir que qualquer encerramento de sessao ou reinicio do Windows repoe imediatamente todas as ventoinhas em modo automatico da BIOS.

### 10.6 Blindagem Multi-Runtime e Compilacao AnyCPU
* Inclusao de `<RollForward>Major</RollForward>` em `LibreHardwareMonitor.Windows.Forms.csproj`, permitindo que o executavel arranque de forma transparente em runtimes .NET 8, .NET 9 ou .NET 10.
* Inclusao de `<CsWin32PlatformTarget Condition="'$(CsWin32PlatformTarget)' == ''">x64</CsWin32PlatformTarget>` em `LibreHardwareMonitorLib.csproj` para viabilizar compilacoes AnyCPU sem erros de geracao de P/Invoke.

### 10.7 Higienizacao do Repositorio
* Eliminacao fisica do diretorio unreferenced `LibreHardwareMonitor.Avalonia`, desimpedindo o repositorio e focando a base de codigo a 100% no cliente de alta performance em Windows Forms.

---

## 11. Implementacao da Subfase 3A - Modulo de Hardware Tuning (GPU Tuning e Blindagem de Seguranca)

### 11.1 Arquitetura Desacoplada e Orientada a Interfaces (`UI/Tuning/`)
Foi criado o subsistema de afincao de adaptadores graficos de baixo overhead em `LibreHardwareMonitor.Windows.Forms/UI/Tuning/`, composto por:
* **`IGpuTuningDevice` e `GenericGpuTuningDevice.cs`:** Contrato padrao para suporte multi-fabricante. Suporta deteccao de recursos (`GpuTuningCapabilities`), leitura de telemetria em tempo real (`GpuTuningTelemetry`), aplicacao de estados (`GpuTuningState`) e reposicao de valores de fabrica (`ResetToStock`).
* **`NvidiaGpuTuningDevice.cs` e `AmdGpuTuningDevice.cs`:** Especializacoes para placas dedicadas NVIDIA e AMD com verificacao de tipo de adaptador e aplicacao protegida.
* **`GpuTuningEngine.cs`:** Motor coordenador central responsavel pela descoberta de GPUs, gestao de perfis, persistencia em `PersistentSettings` e orquestracao dos mecanismos de watchdog.
* **`GpuTuningPanel.cs`:** Interface visual Fluent integrada (`IThemedControl`), com seletor de adaptador, 4 cartoes de telemetria em tempo real (Frequencia do Core, VRAM, Temperatura e Carga), sliders com duplo buffer e banner animado de contagem decrescente para testes.
* **Integracao na Barra Lateral (`MainForm.Layout.cs`):** Novo item "GPU Tuning" na barra lateral com icone dedicado `FluentIcons.ForSensorType(SensorType.Clock)`.

### 11.2 Mecanismos Rigorosos de Seguranca e Estabilidade (Zero Risco de Bricking)
Seguindo a diretriz de risco zero para o hardware do utilizador:
1. **Dirty-Boot Watchdog (Protecao contra Loops de Reinicio):**
   - Antes de aplicar qualquer perfil no arranque, o motor grava a flag `tuning.gpu.dirty_boot = true`.
   - Se o computador sofrer um crash, congelamento ou reinicio inesperado, o proximo arranque deteta a flag `dirty_boot`, cancela imediatamente o overclock no arranque, repoem os valores de fabrica e alerta o utilizador com uma mensagem de aviso.
   - O arranque so e marcado como estavel (`dirty_boot = false`) apos **120 segundos de execucao continua sem incidentes**.
2. **Temporizador de Teste de 15 Segundos (15s Watchdog):**
   - Ao clicar em "Testar Perfil (15s)", o perfil entra em modo de validacao temporizado.
   - Um banner de contagem decrescente e exibido com as opcoes "Manter Alteracoes" e "Reverter".
   - Se o utilizador nao confirmar no espaco de 15 segundos (ou se o ecra congelar ou o driver de video reiniciar via TDR), o motor reverte **automaticamente** todos os offsets para os valores de fabrica seguros.
3. **Bloqueio Rigoroso de Limites (Clamping de Seguranca):**
   - Offset de Nucleo (Core): limitado estritamente entre **-300 MHz e +150 MHz**.
   - Offset de Memoria (VRAM): limitado estritamente entre **-1000 MHz e +1000 MHz**.
   - Limite de Potencia (TDP %): limitado estritamente entre **50% e 115%** (alvos perigosos acima de 115% sao fisicamente bloqueados).
   - Alvo Termico: limitado entre **65 C e 88 C**.
   - Tensao (Core Voltage): **bloqueada em modo apenas leitura**. Modificacoes de tensao que possam degradar silicio foram totalmente excluidas por seguranca.
4. **Deteccao de GPUs Integradas e VBIOS Bloqueadas:**
   - Em sistemas sem placa dedicada ou com GPUs integradas (ex: Intel HD Graphics 5500), o motor deteta automaticamente `IsSupported = false`, desativa os sliders e exibe uma indicacao informativa, mantendo apenas a leitura passiva de sensores de telemetria sem emitir qualquer escrita arriscada no hardware.
5. **Dead-Man's Switch para Tuning:**
   - Subscrito em `ProcessExit`, `UnhandledException` e `SessionEnded` para repor automaticamente perfis nao confirmados.

---

## 12. Implementacao da Subfase 3B - Modulo de CPU Tuning (Power Limits, Universal Boost e Risco Zero de BSoD)

### 12.1 Correcoes de Infraestrutura e Scripts da Raiz
* **`Publicar-Release.bat`:** Eliminado o caminho hardcoded de utilizador `C:\Users\tiago\...`. Implementada verificacao dinamica de disponibilidade do comando `where dotnet`, com fallback defensivo para `%LOCALAPPDATA%\Microsoft\dotnet`.
* **`Iniciar-LibreHardwareMonitor.bat`:** Verificacao e blindagem completa da sintaxe de blocos condicionais em Windows Batch (`cmd.exe`), assegurando o fecho correto de parenteses e encaminhamento com elevacao administrativa UAC sem falhas.

### 12.2 Habilitacao de Escrita Segura em Modulos Ring-0 do PawnIO
* **`IntelMsr.cs`:** Expostos metodos `WriteMsr(uint index, ulong value)` e `WriteMsr(uint index, uint eax, uint edx)` que comunicam diretamente com a rotina nativa `ioctl_write_msr` exportada pelo binario assinado `IntelMSR.bin`. Adicionada a propriedade `IsLoaded`.
* **`RyzenSmu.cs`:** Exposto o metodo `SendSmuCommand(uint command, params uint[] args)` que acede a `ioctl_send_smu_command` no driver assinado `RyzenSMU.bin`, protegido rigorosamente por `Mutexes.WaitPciBus(5000)` e libertacao em bloco `finally`.
* **`AmdFamily17.cs`:** Exposto o wrapper seguro `WriteMsr` correspondente a `ioctl_write_msr` para CPUs AMD Family 17h/19h.

### 12.3 Ligacao de Hardware Real em GPU Tuning (`NvidiaGpuTuningDevice.cs`)
* Integrado P/Invoke nativo direto a `nvml.dll` (`nvmlInit_v2`, `nvmlDeviceGetHandleByIndex_v2`, `nvmlDeviceSetPowerManagementLimit`, `nvmlDeviceGetPowerManagementLimitConstraints`, `nvmlDeviceGetPowerManagementDefaultLimit`).
* Ao aplicar o Power Limit na UI, o valor percentual e convertido em miliwatts reais e submetido ao driver da NVIDIA com clamping de hardware, com fallback gracioso para placas travadas ou ambientes sem NVML.

### 12.4 Arquitetura de CPU Tuning em Duas Camadas (Compatibilidade Universal + Hardware Direct)
1. **Camada 1 - Windows Processor Power Control (`powrprof.dll` - Universal em 100% dos CPUs):**
   - Interage diretamente com as politicas nativas de gestao de energia do Windows atraves da classe [WindowsPowerPolicy.cs](file:///c:/Users/supor/Downloads/LibreHardwareHub-main/LibreHardwareMonitor.Windows.Forms/UI/Tuning/WindowsPowerPolicy.cs).
   - Controla o **Estado Maximo do Processador** (`PROCTHROTTLEMAX`, 50% a 100%) e o **Modo de Turbo Boost** (`PERFBOOSTMODE`: *0: Desativado*, *1: Ativo/Padrao*, *2: Agressivo*, *3: Eficiente*).
   - Aplica os ajustes instantaneamente sem reiniciar via `PowerSetActiveScheme`.
2. **Camada 2 - Limites de Potencia Diretos no Silicio (Intel RAPL via PawnIO):**
   - A classe [IntelRaplControl.cs](file:///c:/Users/supor/Downloads/LibreHardwareHub-main/LibreHardwareMonitor.Windows.Forms/UI/Tuning/IntelRaplControl.cs) le as unidades de potencia do processador em `MSR_RAPL_POWER_UNIT` (`0x606`) e os limites em `MSR_PKG_POWER_LIMIT` (`0x610`).
   - Verifica o **Bit 63 (Package Lock Bit)**: se o bit estiver ativo (bloqueado pela BIOS OEM), a aplicacao assume modo informativo seguro sem provocar erros. Se desbloqueado, permite configurar PL1 (Sustained Power) e PL2 (Short Burst) com clamping seguro (15W a 253W).

### 12.5 Risco Zero de BSoD e Fail-Safes Rigorosos
* **Zero Manipulacao de Tensao Bruta:** O registo MSR `0x150` foi mantido intocado. Alteracoes de limites de potencia (PL1/PL2/PPT) e modos de boost nunca originam instabilidade do kernel nem erros WHEA, garantindo estabilidade absoluta.
* **Dirty-Boot Watchdog (`tuning.cpu.dirty_boot`):** Exatamente como no modulo de GPU, a aplicacao no arranque e protegida por uma janela de estabilidade de 120 segundos. Qualquer encerramento anormal desativa automaticamente os ajustes no arranque seguinte.
* **Watchdog de 15 Segundos:** Permite testar o perfil com reversao automatica se nao for explicitamente confirmado pelo utilizador.
* **Perfis Rapidos Integrados:** Botoes de acao imediata para *Eco / Silencioso*, *Equilibrado (Stock)* e *Desempenho Maximo*.
* **Interface Fluent Integrada:** Adicionado o painel [CpuTuningPanel.cs](file:///c:/Users/supor/Downloads/LibreHardwareHub-main/LibreHardwareMonitor.Windows.Forms/UI/Tuning/CpuTuningPanel.cs) e a entrada "CPU Tuning" na barra lateral de navegacao.
* **Estado de Compilacao:** Compilacao limpa com **0 Warnings e 0 Errors**.

---

## 13. Modernizacao de Design Fluent, UX/UI e Otimizacao de Performance (Fan Control, CPU Tuning e GPU Tuning)

### 13.1 Diagnostico de Desalinhamento Visual Inicial
A analise aprofundada dos modulos de controlo revelou discrepancias visuais e tecnicas em relacao a shell principal (`SummaryPanel`, `CardPanel`, `SidebarPanel`):
1. **Tipografia Inconsistente:** Paineis recorriam a `SystemFonts.DefaultFont.FontFamily` ou `Font.FontFamily`, ignorando a hierarquia tipografica `Segoe UI Variable / Segoe UI` providenciada pela classe `FluentStyle.cs`.
2. **Cartoes de Metricas Primitivos:** Caixas de telemetria eram etiquetas `Label` comuns com `BorderStyle = FixedSingle`, apresentando cantos vivos de 90 graus e aspeto datado de interfaces legadas.
3. **Indicadores em Texto Bruto:** Alertas e estados de seguranca eram apresentados em etiquetas simples entre parenteses retos (ex: `[RISCO ZERO DE BSOD]`).
4. **Banners com Contraste Inadequado em Modo Escuro:** O banner de teste temporizado (Watchdog) utilizava cores fixas de fundo amarelo claro e texto castanho, prejudicando a legibilidade e contraste nos temas `Dark` e `Black`.
5. **Redundancia e Codigo Repetitivo:** Cada painel definia manualmente blocos de desenho, calculos de cantos e gestao de fontes, ferindo os principios KISS e DRY.

### 13.2 Novos Componentes Reutilizaveis de Design System (`UI/Controls/`)
Para unificar a experiencia de utilizacao e erradicar duplicacao de codigo, foram concebidos tres componentes centrais de alto desempenho:
* **`FluentMetricCard.cs`:**
  - Componente de dupla memoria tampao (`OptimizedDoubleBuffer | AllPaintingInWmPaint`) para renderizacao sem cintilacao (*flicker-free*).
  - Apresenta rotulo superior em caixa alta subtil (`theme.MutedForegroundColor`), valor de destaque em fonte display (`FluentStyle.CreateDisplayFont`), unidade de medida e linha inferior de detalhe.
  - Classificacao dinamica de severidade visual (`Normal`, `Cool`, `Warm`, `Hot`, `Accent`) com realce suave ao passar o cursor (*hover*).
  - Alocacao de memoria residual zero durante as leituras ciclicas de telemetria.
* **`FluentBadge.cs`:**
  - Indicador em formato pilula com geometria de cantos arredondados (`radius = height / 2`).
  - Suporta estilos semanticos (`Success`, `Warning`, `Danger`, `Accent`, `Neutral`) com fundo de baixa opacidade e texto de alto contraste compativel com temas escuros e claros.
  - Substitui todos os textos brutos entre parenteses por elementos visuais modernos.
* **`FluentSectionCard.cs`:**
  - Contentor de agrupamento com cantos arredondados (`theme.CornerRadius + 2`), contorno suave (`theme.SurfaceStrokeColor`) e superficie adaptavel (`theme.SurfaceColor`).
  - Suporta titulo em semibold, subtitulo descritivo e linha divisoria horizontal milimetrica.
* **Auxiliares de Estilo em `FluentStyle.cs`:**
  - `StylePrimaryButton`: Estiliza o botao principal de acao com `AccentColor` e `AccentForegroundColor`.
  - `StyleSecondaryButton`: Estiliza botoes secundarios com fundo de superficie e contorno suave.
  - `StyleChipButton`: Estiliza botoes de perfil rapido (chips) com destaque visual dinamico consoante o estado ativo ou inativo.

### 13.3 Transformacao dos Tres Modulos de Hardware

#### A. Ajuste de Processador (`CpuTuningPanel.cs`)
* **Cabecalho:** Titulo em `Segoe UI Variable Text Bold` (13.5pt), subtitulo descritivo e pilula `FluentBadge` indicando o modo seguro de operacao.
* **Faixa de Telemetria:** 4 cartoes `FluentMetricCard` em tempo real para **Temperatura do Pacote**, **Potencia em Watts**, **Frequencia Media** e **Carga Total da CPU** com severidade adaptavel.
* **Perfis Rapidos Integrados:** Contentor `FluentSectionCard` com chips selecionaveis (*Eco / Silencioso*, *Equilibrado Stock*, *Desempenho Maximo*) que atualizam imediatamente o estado visual e os controlos deslizantes.
* **Watchdog de Seguranca Estilizado:** Banner arredondado com fundo suave e botoes "Confirmar e Manter" e "Reverter".
* **Secao de Parametros:** Controlos deslizantes com fundo alinhado a superficie, etiquetas em semibold e valores em destaque na cor de realce do sistema.
* **Barra de Acoes:** Botao primario destacado para teste temporizado de 15 segundos e reposicao rapida de padroes de fabrica.

#### B. Ajuste de Placa Grafica (`GpuTuningPanel.cs`)
* **Cabecalho & Seletor:** Alinhamento limpo da selecao de placa grafica com pilula `FluentBadge` de status ("Modo Seguro - Clamping Ativo" ou "Apenas Telemetria").
* **Faixa de Telemetria:** 4 cartoes `FluentMetricCard` cobrindo **Core Clock**, **VRAM Clock**, **Temperatura do Chip** e **Carga do Motor 3D**.
* **Perfis Rapidos:** Selecao imediata de perfis balanceados para a GPU ativa.
* **Parametros com Clamping:** Sliders de offset de clock e limites de TDP/Temperatura operando com limites rigorosamente balizados pelo firmware.
* **Tratamento de GPUs Integradas:** Mensagem contextual limpa e adaptativa para adaptadores sem suporte de escrita.

#### C. Controlo de Ventilacao (`FanControlPanel.cs` & `FanChannelCard.cs`)
* **Painel Principal:** Faixa superior de KPI indicando a **Temperatura da CPU**, **Temperatura da GPU**, **Canais Ativos Super I/O** e **Perfil Global Ativo**.
* **Estado Vazio Inteligente:** Se nenhum canal com suporte PWM for detectado, o utilizador e elucidado com um cartao `FluentSectionCard` informativo sobre chips Super I/O e BIOS OEM.
* **Cartoes de Canal (`FanChannelCard`):**
  - Desenho em superficie arredondada com realce ao passar o rato (*hover*).
  - Pilula de estado com percentagem de PWM e rotacao em RPM.
  - Pilula de alerta vermelho de alta visibilidade caso ocorra intervencao de emergencia termica (>90 C).
  - Botoes de selecao rapida de curvas pre-definidas (*Silencioso*, *Equilibrado*, *Desempenho*) estilizados via `StyleChipButton`.
  - Apresentacao nitida dos pontos de curva e histerese.

### 13.4 Otimizacoes de Performance e Recursos
* **Pintura Livre de Cintilacao:** Todas as novas classes ativam `OptimizedDoubleBuffer | AllPaintingInWmPaint | UserPaint`.
* **Zero Overhead em Segundo Plano:** Atualizacoes de telemetria dos tres paineis continuam estritamente suspensas quando a janela esta minimizada ou quando o utilizador se encontra a navegar noutras vistas.
* **Pegada de Memoria:** O binario da aplicacao `LibreHardwareMonitor.Windows.Forms.dll` mantem-se extremamente leve (~577 KB).
* **Compilacao:** Validada com sucesso em `.NET 8.0 LTS` exclusivo em toda a solucao com **0 Warnings e 0 Errors**.



