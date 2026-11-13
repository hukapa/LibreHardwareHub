# Historico de Engenharia — Cronologia de Evolucao Tecnica

> **Projeto:** LibreHardwareMonitor (Fork hukapa)  
> **Regra Fundamental:** Zero Emojis  

---

## 1. Fase 1: Criacao do Fork e Configuracao do Ambiente

* **Objetivo:** Estabelecer o repositorio fork em `hukapa/LibreHardwareMonitor` e configurar os remotos Git locais.
* **Acoes Efetuadas:**
  - Clonagem para o diretorio de desenvolvimento: `C:\Users\tiago\Documents\GitHub\CrystalStudio-AI\LibreHardwareMonitor`.
  - Configuracao dos remotos:
    - `origin`: `https://github.com/hukapa/LibreHardwareMonitor.git`
    - `upstream`: `https://github.com/LibreHardwareMonitor/LibreHardwareMonitor.git`
  - Definicao do fluxo de trabalho no GitHub Desktop para "For my own purposes", garantindo commits e publicacao direta no fork.

---

## 2. Fase 2: Modernizacao para .NET 8.0 LTS

* **Problema Identificado:**  
  O repositorio upstream havia saltado para versoes experimentais do .NET 10 (`net10.0`), que nao estao disponiveis no ambiente de producao do utilizador (equipado com .NET 8.0.427). Por outro lado, manter o projeto em .NET Framework 4.7.2 representava manter divida tecnica com bibliotecas e runtime obsoletos de 2018.
* **Acoes Efetuadas:**
  - Atualizacao de `LibreHardwareMonitorLib.csproj`, `Aga.Controls.csproj` e `LibreHardwareMonitor.Windows.Forms.csproj` para suportar `net8.0-windows` e `net8.0`.
  - Correcao do gerador de codigo nativo CsWin32 adicionando `<PlatformTarget>x64</PlatformTarget>` e `<CsWin32PlatformTarget>x64</CsWin32PlatformTarget>`.
  - Retrocesso de sintaxe C# 13 preview (`field` keyword em `Sensor.cs` e atribuicao null-condicional de l-value em `ArcticFanController.cs`) para padroes estaveis do C# 12 / .NET 8.
  - Alinhamento de versoes do pacote NuGet `System.Resources.Extensions` para eliminar avisos `NU1605`.

---

## 3. Fase 3: Funcionalidade Minimizar para a Bandeja no 'X'

* **Objetivo:** Fazer com que o encerramento da janela principal coloque a aplicacao em segundo plano no System Tray em vez de terminar o processo.
* **Acoes Efetuadas:**
  - Criacao da opcao persistente `_minimizeOnClose` com valor padrao `true`.
  - Sobrescrita de `OnFormClosing` para detetar `CloseReason.UserClosing`, aplicando `e.Cancel = true`, ocultando o formulario com `Visible = false` e mantendo o icone da bandeja ativo.
  - Intercepcao da mensagem de sistema `SC_CLOSE` em `WndProc` para direcionamento imediato para `SysTrayHideShow()`.
  - Restauracao automatica do estado da janela para `FormWindowState.Normal` ao clicar no icone da bandeja do sistema quando a aplicacao se encontra minimizada.

---

## 4. Fase 4: Modernizacao Completa do Servidor Web

* **Objetivo:** Substituir o dashboard web antigo de 2012 por uma solucao moderna e responsiva.
* **Acoes Efetuadas:**
  - Substituicao de jQuery 1.7 e Knockout.js por JavaScript ES2022 nativo sem dependencias.
  - Criacao de folha de estilos contemporanea com suporte automatico a tema claro e escuro (`light-dark()`, `color-scheme: light dark`).
  - Layout em cartoes responsivos por dispositivo de hardware com atualizacao automatica a cada 1.5s via endpoint REST `/data.json`.
  - Adicao de barra de pesquisa instantanea para filtragem de sensores em tempo real.

---

## 5. Fase 5: Diagnostico e Blindagem do Smart App Control (Windows 11)

* **Problema Identificado:**  
  O Controlo de Aplicacoes Inteligente do Windows 11 bloqueava `RAMSPDToolkit-NDD.dll` com erro `0x800711C7`, provocando encerramento silencioso no arranque. Adicionalmente, o manifest com `requireAdministrator` gerava erro 740 ("O pedido nao e suportado") ao ser chamado sem elevacao explicita.
* **Acoes Efetuadas:**
  - Isolamento com `[MethodImpl(MethodImplOptions.NoInlining)]` e `try/catch` no `MemoryGroup.cs`.
  - Protecao global em `Computer.cs` para adicao resiliente de grupos de hardware.
  - Adicao de captura global de falhas em `Program.cs` com registo em `crash.log`.
  - Alteracao do manifest para `asInvoker` e atualizacao do script `Iniciar-LibreHardwareMonitor.bat` com `-WorkingDirectory`.
  - Suporte a empacotamento standalone em ficheiro unico (`PublishSingleFile=true`).

---

## 6. Fase 6: Decomposicao Modular de `MainForm.cs` e Hub `docs/`

* **Objetivo:** Eliminar a classe monolitica de 1.479 linhas, modularizar por SRP e criar o registo de documentacao do projeto no padrao CrystalStudio AI.
* **Acoes Efetuadas:**
  - Criacao da pasta `docs/` com os modulos `INDEX.md`, `FILE_TREE.md`, `RELATORIO_ESTADO_E_MELHORIAS.md`, `DOCUMENTATION.md` e `AGENTS.md`.
  - Decomposicao de `MainForm.cs` em 5 classes parciais (`MainForm.cs`, `MainForm.Tray.cs`, `MainForm.WebServer.cs`, `MainForm.Plot.cs`, `MainForm.Tree.cs`), respeitando limites rigorosos de linhas e o principio da responsabilidade unica.
  - Validacao de compilacao: 0 erros e 0 avisos bloqueantes.

---

## 7. Fase 7: Resolucao de Assemblies Embutidos no Modo Single-File

* **Problema Identificado:**  
  Ao iniciar a versao compilada em ficheiro unico (`bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe`), surgia a caixa de dialogo `"Error: The following file could not be found: Aga.Controls.dll. Please extract all files from the archive."`. A verificacao legada em `Program.cs` utilizava `File.Exists(path + fileName)` buscando ficheiros `.dll` soltos em disco, ignorando que no modo Single-File todos os assemblies geridos estao embutidos em memoria no proprio binario executavel.
* **Acoes Efetuadas:**
  - Substituicao de `IsFileAvailable(string fileName)` por `IsAssemblyAvailable(string assemblyName)`.
  - Resolucao de assemblies atraves de `AppDomain.CurrentDomain.GetAssemblies()` e `Assembly.Load(new AssemblyName(assemblyName))` com fallback seguro para verificacao de ficheiro fisico em disco.
  - Re-publicacao do executavel standalone com sucesso e validacao de arranque sem erros em ambiente de producao.

---

## 8. Fase 8: Correcao de Roteamento HTTP `/data.json` e Otimizacao de Arranque

* **Problemas Identificados:**  
  1. O endpoint REST `http://localhost:8085/data.json?t=...` retornava erro HTTP 404. Em `HttpServer.cs`, a rota comparava `requestedFile == "data.json"` extraindo a string de `request.RawUrl.Substring(1)`, que continha os parametros de query de cache busting (`?t=...`), falhando a correspondencia exata e caindo na pesquisa de recursos estaticos inexistentes.
  2. O script de arranque `Iniciar-LibreHardwareMonitor.bat` mantinha a janela da consola aberta durante varios segundos enquanto aguardava a elevacao sincrona via PowerShell, e solicitava confirmacao de UAC a cada execucao.
* **Acoes Efetuadas:**
  - Em `HttpServer.cs`, refatoracao da deteção de rotas para `request.Url.AbsolutePath.TrimStart('/')`, isolando o caminho do recurso dos parametros de query (`?t=...`) e garantindo correspondencia para `data.json`, `images_icon/`, `metrics`, `Sensor` e `ResetAllMinMax`.
  - Otimizacao de `Iniciar-LibreHardwareMonitor.bat` para verificar inicialmente o Agendador de Tarefas do Windows (`schtasks /query /tn "LibreHardwareMonitor"`). Se a tarefa estiver presente, arranca de imediato em ~40ms sem caixa de confirmacao UAC.
  - Criacao de `launch.vbs` executado de forma assincrona via `wscript //nologo`, eliminando atrasos de inicializacao do PowerShell e permitindo fecho instantaneo da consola CMD (< 50ms).
  - Criacao do utilitario `Configurar-Arranque-Rapido-Sem-UAC.bat` para registar a tarefa de alta prioridade no Agendador de Tarefas do Windows com um unico clique.

---

## 9. Fase 9: Eliminacao de Congelamento de 5s no Servidor Web e Desambiguacao de Tarefas

* **Problemas Identificados:**  
  1. Ao desmarcar a opcao "Run Web Server" ou ao encerrar a aplicacao, a interface congelava durante exatamente 5 segundos antes de responder.
  2. O utilizador visualizou inesperadamente o design web legado de 2012 e sem atualizacao de dados, devido a colisao com uma tarefa pre-existente no Agendador de Tarefas do Windows apontando para uma instalacao antiga (`Desktop\stuff 231\LibreHardwareMonitor\LibreHardwareMonitor.exe`).
* **Causas Raiz e Solucoes:**
  - **Bloqueio de 5s:** Em `HttpServer.cs`, `StopHttpListener()` invocava `_listenerTask?.Wait(TimeSpan.FromSeconds(5))` antes de chamar `_listener?.Stop()`. Como `GetContextAsync()` bloqueia de forma sincrona ate receber um novo socket HTTP ou ser explicitamente abortado, a thread de interface aguardava 5 segundos inteiros ate ao timeout. A ordem foi invertida: `_listener?.Stop()` e chamado em primeiro lugar com tratamento dedicado de `HttpListenerException`, libertando o socket em 0ms.
  - **Desambiguacao de Tarefas:** O script `Iniciar-LibreHardwareMonitor.bat` agora pesquisa especificamente pela tarefa dedicada `LibreHardwareMonitor-CrystalStudio` e executa a limpeza prévia de processos antigos (`taskkill /F /IM LibreHardwareMonitor.Windows.Forms.exe` e `LibreHardwareMonitor.exe`), assegurando que apenas o binario do fork e executado.

---

## 10. Fase 10: Redesenho do Dashboard Web com Iconografia SVG, Zero Emojis e Maxima Performance

* **Objetivo:** Refazer a interface web completa em conformidade com as diretrizes `modern-web-guidance`, `debug-optimize-lcp` e `redesign-screen`, eliminando totalmente emojis e aplicando icones vetoriais SVG, seccao Hero de metricas vitais e isolamento de layout CSS.
* **Acoes Efetuadas:**
  - **Zero Emojis & Iconografia SVG:** Substituicao de qualquer representacao em emoji por icones SVG vetoriais leves integrados em `currentColor` para CPU, GPU, RAM, Motherboard, Discos, Ventiladores, Rede e controlo de temas.
  - **Hero Highlights:** Adicao de 4 cartoes de topo com metricas imediatas de CPU (temperatura de pacote e carga total com barra), GPU (temperatura de nucleo e carga 3D), RAM (utilizacao e percentagem) e Armazenamento (temperatura maxima e unidades ativas).
  - **Performance e LCP:** Implementacao de `content-visibility: auto` com `contain-intrinsic-size` em cada cartao para isolar o trabalho de renderizacao e garantir 60 FPS com consumo minimo de CPU.
  - **Modern Web Guidance:** Suporte nativo a `<meta name="color-scheme" content="light dark">`, prevencao de FOUC via inline script, alternador tri-state (Auto, Claro, Escuro) persistente em `localStorage`, e comutador fluido entre Vista em Cartões e Vista em Tabela Técnica.


---

## 11. Fase 11: Limpeza da Solucao, Polling Adaptativo por Tiers e Modo de Baixo Consumo na System Tray

* **Objetivo:** Executar a Fase 1 da transformacao em Master Hardware Hub, eliminando codigo legado obsoleto, estancando fugas de memoria e implementando polling adaptativo diferenciado com supressao de redesenho em segundo plano.
* **Acoes Efetuadas:**
  - **Poda de Projetos Avalonia:** Remocao dos 5 projetos Avalonia (`LibreHardwareMonitor.Avalonia.*`) e pasta de solucao de `LibreHardwareMonitor.sln`, concentrando o pipeline exclusivamente em `LibreHardwareMonitor.Windows.Forms`, `LibreHardwareMonitorLib` e `Aga.Controls`.
  - **Eliminacao de Ativos Web Obsoletos:** Remocao fisica de ficheiros jQuery 1.7, Knockout 2.1 e estilos de 2012 em `Resources/Web/` (`js/`, `css/`, `images/`), reduzindo centenas de kilobytes de ativos mortos.
  - **Extirpacao do Subsistema Gadget:** Remocao de `Gadget.cs`, `GadgetWindow.cs`, `SensorGadget.cs`, `ShowDesktop.cs`, `gadget.png` e `gadget.xcf`, eliminando hooks de janelas GDI+ e referencias de menus em `MainForm.cs`, `MainForm.Designer.cs` e `MainForm.Tree.cs`.
  - **Desacoplamento de `PawnIO_setup.exe`:** Realocacao do instalador binario de 3.22 MB de dentro dos recursos compilados para a pasta externa `tools/`, reduzindo o tamanho de `LibreHardwareMonitor.Windows.Forms.dll` de 4.56 MB para 472 KB (~90% de reducao).
  - **Consolidacao de Scripts:** Remocao de ficheiros redundantes na raiz (`Configurar-Arranque-Rapido-Sem-UAC.bat`, `Fechar-LibreHardwareMonitor.bat`, `Iniciar-Elevado.cmd`, `launch.vbs`) e integracao direta de gestao de instancia unica via `Mutex`, argumentos de encerramento (`--exit`) e elevacao (`--elevate`) em `Program.cs`.
  - **Polling Adaptativo por Tiers:** Refatoracao de `UpdateVisitor.cs` para segmentar o ciclo de leitura de hardware em 3 cadencias temporais (Tier Lento a 30s para Storage SMART e Bateria; Tier Normal a 2-5s para Memoria, Rede e PSU; Tier Critico a 800ms-2500ms para CPU, GPU e Ventoinhas).
  - **Modo Baixo Consumo na System Tray e Supressao de UI:**
    - Em `MainForm.cs` e `MainForm.Tray.cs`, suspensao total de `treeView.Invalidate()`, `UpdateShell()` e `_plotPanel.InvalidatePlot()` quando a janela esta oculta ou minimizada.
    - Comutacao automatica do timer de leitura para 3000ms-5000ms quando minimizado na bandeja.
    - Invocacao da API Win32 `EmptyWorkingSet` via `psapi.dll` associada a `GC.Collect()` na transicao para a bandeja do sistema, libertando memoria fisica nao utilizada.
  - **Otimizacao de Alocacao de Historico de Sensores:** Em `MainForm.Plot.cs` e `MainForm.cs`, desativacao automatica de `ValuesTimeWindow` (`TimeSpan.Zero`) para todos os sensores quando o painel de graficos se encontra inativo, suprimindo alocacoes contínuas de series temporais.
  - **Cache de Afinidade de Thread em MSRs:** Em `ThreadAffinity.cs`, introducao de cache local `[ThreadStatic]` para evitar chamadas de sistema `SetThreadGroupAffinity` redundantes para o mesmo grupo e mascara.


---

## 12. Fase 12: Motor de Controlo de Ventoinhas (Fan Control), Fail-Safes e UI Integrada

* **Objetivo:** Implementar a Fase 2 da transformacao em Master Hardware Hub, introduzindo controlo ativo de ventilacao seguro e de baixo overhead sobre as primitivas `IControl` (`Control.cs`) e sensores `SensorType.Control` / `SensorType.Fan` existentes em `LibreHardwareMonitorLib`.
* **Acoes Efetuadas:**
  - **Resolucao Cirurgica de Detalhes da Fase 1:**
    - Frequencia do timer em background com Fan Control ativo: `UpdateVisitor.ActiveFanControl` faz com que o Tier Critico e o PWM sejam avaliados a cada 1000ms mesmo quando minimizado, mantendo a pintura de UI GDI+ 100% suspensa.
    - Gatilho unico para `TrimWorkingSet()`: Introduzidos `TryTrimWorkingSetOnce()` e `ResetWorkingSetTrimmed()` para garantir que `GC.Collect()` e `EmptyWorkingSet` ocorrem exclusivamente na transicao para o estado minimizado/oculto, evitando hard page faults continuos no kernel.
    - Blindagem de `ThreadAffinity.cs`: Garantido que se `SetThreadGroupAffinity` falhar, a cache `_isAffinityCached` e invalidada; ao saltar syscalls redundantes por o nucleo ja corresponder a afinidade pretendida, o metodo devolve a afinidade valida para restauracao segura nos blocos finally.
  - **Subsistema Nuclear de Fan Control (`UI/FanControl/`):**
    - `FanControlTypes.cs`: Enumeracoes `FanChannelMode` (`DefaultBios`, `Fixed`, `Curve`) e `GlobalFanPreset` (`DefaultBios`, `Silent`, `Balanced`, `Performance`, `Custom`).
    - `FanCurvePoint.cs`: Estrutura imutavel com clamping de PWM (0-100%) compativel com .NET 8 e .NET Framework 4.7.2.
    - `FanCurve.cs`: Interpolacao linear entre pontos com clamping de extremos, serializacao compacta para texto e definicao de curvas de fabrica (Silent, Balanced, Performance).
    - `FanChannelConfig.cs`: Configuracao por canal persistida em `PersistentSettings` com selecao de fontes termicas (`CPU Package`, `GPU Core`, `Max(CPU, GPU)` ou sensor individual), histerese assimetrica e suavizacao temporal.
    - `FanChannelRuntime.cs`: Motor de runtime por canal com media movel (`ResponseTimeSeconds`), histerese assimetrica (+1.5 C para acelerar, -3.0 C para desacelerar), limitadores de taxa de variacao (`MaxStepUpPercent`, `MaxStepDownPercent`) e Hard Thermal Override (>= 90 C forca 100% PWM com histerese de retorno ate 82 C).
    - `FanControlEngine.cs`: Orquestrador central que descobre canais atraves de `IControl`, associa sensores RPM correspondentes, resolve temperaturas compostas, aplica perfis globais e gere a persistencia.
  - **Fail-Safes de Seguranca de Hardware:**
    - Hard Thermal Override: Alerta visual de emergencia e bloqueio automatico em 100% PWM perante temperatura do sensor alvo >= 90 C com histerese de desbloqueio aos 82 C.
    - Dead-Man's Switch: Registo estatico global que devolve todos os canais de controlo ao padrao do BIOS (`IControl.SetDefault()`) nos eventos `AppDomain.ProcessExit`, `AppDomain.UnhandledException`, `Application.ThreadException`, encerramento da aplicacao (`CloseApplication`) e termino de sessao do Windows (`SessionEnded`).
  - **Interface Fluent e Acesso Rapido:**
    - Cartao de canal `FanChannelCard.cs`: Controlo visual com selecao de modo, slider de percentagem fixa, atalhos de curvas e indicador de alerta de emergencia.
    - Painel integrado `FanControlPanel.cs`: Seletor de perfil global, telemetria termica em tempo real e lista dinamica de canais.
    - Integracao com a barra lateral Fluent (`SidebarPanel.cs`): Item dedicado "Fan Control" com iconografia vetorial `FluentIcons.ForSensorType(SensorType.Fan)`.
    - Menu de contexto na System Tray: Submenu "Perfil de Ventoinhas" para troca instantanea de perfis sem abrir a janela principal.


---

## 13. Fase 13: Subfases 3A e 3B — Hardware Tuning (GPU/CPU), Fail-Safes Rigorosos e Design System Fluent

* **Objetivo:** Implementar a Fase 3 da transformacao em Master Hardware Hub, introduzindo afinacao ativa de placas graficas (GPU Tuning) e processadores (CPU Tuning) com isolamento estrito contra BSoDs/bricking, seguidos da modernizacao visual dos componentes Fluent e eliminacao de alocacoes redundantes.
* **Acoes Efetuadas:**
  - **Otimizacoes de Performance e Refinamentos de Codigo (KISS, DRY e Zero-Allocation):**
    - `ThreadAffinity.cs`: Eliminado o bypass estatico com cache; `SetThreadGroupAffinity` gere nativamente a afinidade e preenche `previousGroupAffinity`, assegurando que os blocos `finally` restauram 100% dos nucleos sem reter a thread num unico core logico.
    - `UpdateVisitor.cs`: Leitura de timestamp concentrada uma unica vez no inicio de `VisitComputer` com `Environment.TickCount64` em .NET 8, evitando chamadas repetidas a `DateTime.UtcNow`.
    - `FanChannelRuntime.cs`: Eliminadas alocacoes LINQ (`RemoveAll`, `Average`) no calculo da media movel, substituindo-as por iteracao imperativa sem alocacoes no Heap.
    - `FanControlEngine.cs`: Criada indexacao O(1) de sensores de temperatura (`_tempSensorsById`) e cache direta dos sensores primarios de CPU e GPU.
    - `FanControlEngine.RegisterDeadManSwitch()`: Integrada subscricao automatica de `SystemEvents.SessionEnded` para restauro de emergencia da BIOS em encerramentos de sessao.
    - Suporte Multi-Runtime: Inclusao de `<RollForward>Major</RollForward>` em `Windows.Forms.csproj` e `<CsWin32PlatformTarget>x64</CsWin32PlatformTarget>` em `LibreHardwareMonitorLib.csproj`.
    - Higienizacao: Remocao fisica definitiva do diretorio `LibreHardwareMonitor.Avalonia/`.
  - **Subfase 3A — Modulo de GPU Tuning (`UI/Tuning/`):**
    - Contratos `IGpuTuningDevice` e implementacao generica `GenericGpuTuningDevice.cs` com deteccao passiva de GPUs integradas.
    - Especializacoes `NvidiaGpuTuningDevice.cs` (NVML nativo, clamping estrito) e `AmdGpuTuningDevice.cs` (AMD Overdrive).
    - `GpuTuningEngine.cs`: Orquestrador central com duplo watchdog:
      - *15s Test Watchdog:* Reverte automaticamente qualquer alteracao caso o utilizador nao confirme a estabilidade do ajuste no ecra.
      - *120s Dirty-Boot Watchdog:* Cria marca de arranque e restaura imediatamente os valores de fabrica se o sistema reiniciar ou falhar no espaco de 120 segundos.
    - `GpuTuningPanel.cs`: Interface Fluent com seletor de GPU, 4 cartoes KPI de telemetria em tempo real, sliders com duplo buffer e faixa animada de teste.
  - **Subfase 3B — Modulo de CPU Tuning (`UI/Tuning/`):**
    - `WindowsPowerPolicy.cs`: Camada universal compativel com todos os processadores (Intel, AMD Zen 1 a 5, Snapdragon ARM64) manipulando `ProcessorPerformanceBoostMode` e limites de estado de desempenho via `powrprof.dll`.
    - `IntelRaplControl.cs`: Controlo direto de limites de potencia PL1 (Long Duration) e PL2 (Short Duration) via MSR `0x610` utilizando o driver PawnIO.
    - `CpuTuningEngine.cs`: Motor central com deteccao automatica de suporte, aplicacao de perfis predefinidos (*Eco/Silencioso*, *Equilibrado Stock*, *Desempenho Maximo*) e integracao com Dirty-Boot Watchdog.
    - `CpuTuningPanel.cs`: Painel Fluent com seletor de politicas, limites de TDP e faixas de telemetria.
  - **Design System Fluent Unificado (`UI/Controls/`):**
    - `FluentMetricCard.cs`: Cartao KPI de alta performance desenhado com GDI+ e duplo buffer para visualizacao sem cintilacao de temperaturas, frequencias, potencias e cargas.
    - `FluentBadge.cs`: Indicador em pilula arredondada com temas semanticos (`Success`, `Warning`, `Danger`, `Accent`, `Neutral`).
    - `FluentSectionCard.cs`: Painel de agrupamento com cantos arredondados e borda sutil.
    - `FluentComboBox.cs`: Caixa de combinacao estilizada com cantos curvos e chevron vetorial.
    - Atualizacao de `FanControlPanel.cs` e `FanChannelCard.cs` para utilizacao integral dos novos componentes Fluent.

---

## 14. Fase 14: Unificacao Exclusiva em .NET 8.0 LTS e Blindagem de Dependencias

* **Objetivo:** Eliminar a sobrecarga de compilacao multi-target legada (.NET Framework 4.7.2 e .NET Standard 2.0), blindar o Dependabot contra versoes instaveis do ecossistema .NET 11 e unificar toda a solucao exclusivamente em .NET 8.0 LTS.
* **Acoes Efetuadas:**
  - **Tratamento de Dependencias e Dependabot (PR #4 e PR #5):**
    - Identificada duplicacao identica entre PR #4 e PR #5, bem como a tentativa de atualizacao dos pacotes `System.*` (`System.IO.Ports`, `System.Management`, `System.Resources.Extensions`, `System.Text.Json`, `System.Threading.AccessControl`) para `11.0.0`, que geravam 29 avisos de incompatibilidade e conflitos de resolucao de tipos (`MSB3277`).
    - Mantidos todos os pacotes `System.*` na versao estavel `10.0.14`.
    - Atualizado isoladamente o gerador de codigo nativo `Microsoft.Windows.CsWin32` de `0.3.367` para `0.3.371` em `LibreHardwareMonitorLib.csproj`.
    - Blindado o `.github/dependabot.yml` com regra `ignore` para versoes `>= 11.0.0` e `semver-major` nos pacotes `System.*`.
  - **Unificacao de TargetFramework em .NET 8.0:**
    - `LibreHardwareMonitorLib.csproj`: Unificado em `<TargetFramework>net8.0</TargetFramework>`, removendo `net472`, `netstandard2.0`, condicoes `Condition="'$(TargetFramework)' == 'net472'"` (como `System.Memory` 4.6.3), `<GeneratePackageOnBuild>` e `<Import Project="main.nuget.target">`.
    - `Aga.Controls.csproj`: Unificado em `<TargetFramework>net8.0-windows</TargetFramework>`, eliminadas referencias legadas a `System.Design` e adicionada supressao pontual de avisos obsoletos de 2009 (`SYSLIB0003`, `SYSLIB0006`, `SYSLIB0050`).
    - `LibreHardwareMonitor.Windows.Forms.csproj`: Migrado o SDK para `Microsoft.NET.Sdk` com `<TargetFramework>net8.0-windows</TargetFramework>`, eliminando o aviso `NETSDK1137` e removendo referencias legadas a `System.Configuration.Install` e `System.Web`.
  - **Limpeza de Diretivas Condicionais (#if Legadas):**
    - `UpdateVisitor.cs`: Removida diretiva `#if NETCOREAPP || NET8_0_OR_GREATER`, passando a invocar diretamente `Environment.TickCount64`.
    - `Mutexes.cs`: Removida compilacao condicional `#if NETFRAMEWORK`, mantendo uso direto de `MutexAcl.Create`.
    - `OpCode.cs`: Removidas diretivas e metodos de reflexao de fallback para Mono.Posix em .NET Framework, utilizando diretamente `Syscall.mmap` e `Syscall.munmap`.
    - `ByteExtensions.cs`: Removida diretiva redundante `#if NET8_0_OR_GREATER`.
  - **Ajuste de Pipelines CI (`.github/workflows/`):**
    - Atualizados `master.yml` e `pull requests.yml` para disparar em `branches: [main]` com `dotnet-version: '8.0.x'`, removendo artefatos e passos legados de `net472`, `net10.0` e Avalonia.
  - **Validacao:**
    - Compilacao de toda a solucao `LibreHardwareMonitor.sln` em Release x64 concluida com **0 Warnings e 0 Errors** em ~4 segundos.
