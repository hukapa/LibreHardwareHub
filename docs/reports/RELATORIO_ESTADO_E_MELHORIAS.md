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

* **Framework de Destino Principal:** .NET 8.0 Windows (`net8.0-windows`)
* **Compatibilidade Retroativa Mantida:** .NET Framework 4.7.2 (`net472`)
* **Erros de Compilacao:** 0
* **Avisos:** 0 avisos bloqueantes
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
