# Libre Hardware — Guia Master do Projeto e Navegacao de Documentacao

> **Versao:** 0.9.6.0 (Fork hukapa)  
> **Arquitetura:** .NET 8.0 LTS Exclusivo (`net8.0` / `net8.0-windows`) + WinForms + Web Server HTTP Embutido  
> **Publico:** Desenvolvedores, Engenheiros de Software e Agentes de IA  
> **Regra Fundamental:** Zero Emojis em toda a documentacao, codigo e commits.  

---

## 1. Visao Geral do Sistema

O **LibreHardwareMonitor (Fork hukapa)** e uma suite avancada, moderna e resiliente de monitorizacao de hardware para Windows, desenhada para inspecionar em tempo real os sensores de Motherboard, Processador (CPU), Placa Grafica (GPU), Memoria RAM, Discos (HDD/SSD/NVMe), Controladores de Ventoinhas, Fontes de Alimentacao (PSU) e Baterias.

Este fork mantem o foco na modernizacao do ecossistema para .NET 8.0 LTS, melhorias de usabilidade na interface desktop, desacoplamento arquitetural e um servidor web embutido completamente redesenhado.

### Capacidades Nucleares
1. **Monitorizacao Abrangente de Sensores:** Leitura de temperaturas, tensoes, frequencias de relogio, potencias (Watts), percentagens de uso, taxas de fluxo e nivel de desgaste.
2. **Grelha de Cartoes Bento Inteligentes (`HardwareBentoPanel`):** Cartoes de alta densidade (KPIs, matriz de nucleos, VRAM, armazenamento) com a mesma altura por linha. Um clique expande o cartao para a largura total da grelha e mostra todos os sensores agrupados por tipo ao lado do resumo; selecionar um dispositivo na barra lateral abre o respetivo cartao ja expandido. Alternador no topo para a tabela bruta avancada.
3. **In-Game Overlay (HUD) e Keybind Global (`UI/Overlay/`):** HUD transparente sobreposto aos jogos acionado por tecla de atalho (`RegisterHotKey`, sem hooks globais), `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE` e modo click-through. O estudio de personalizacao mostra uma pre-visualizacao identica ao HUD real (mesmo renderizador, leituras reais), permite adicionar qualquer sensor do PC, escolher disposicao (lista, barra, grelha, livre por arrasto), cores com seletor Fluent proprio, limites de severidade, ecra, ancora e margem.
4. **Controlo Ativo de Ventoinhas (Fan Control):** Motor inteligente de controlo de ventilacao por curvas personalizadas com histerese assimetrica, suavizacao temporal e Hard Thermal Override de seguranca (>=90 C -> 100%).
5. **Modulo de Hardware Tuning (GPU e CPU Tuning Seguro):** Afinacao e controlo de placas graficas e processadores com limites estritos (clamping), teste temporizado de 15 segundos (15s Watchdog) e protecao automatica contra falhas de arranque (Dirty-Boot Watchdog com janela de estabilidade de 120s).
6. **Design System Fluent Unificado (`UI/Controls/`):** Componentes de alta performance (`FluentMetricCard`, `FluentBadge`, `FluentSectionCard`) integrados em Fan Control, CPU Tuning e GPU Tuning com tipografia Segoe UI, suporte nativo a temas (Dark/Light/Black) e desenho livre de cintilacao.
7. **Minimizar para Segundo Plano ao Clicar no "X":** O encerramento no botao "X" da janela principal coloca a aplicacao em segundo plano na barra de tarefas (System Tray), mantendo a monitorizacao, curvas de ventilacao e servidor web ativos.
8. **Dashboard Web Remoto Moderno:** Interface web zero-dependencies servida diretamente pelo executavel via HTTP (porta 8085 por omissao), com deteccao nativa de Dark/Light mode (`light-dark()`), cards responsivos e pesquisa instantanea em tempo real.
9. **Resiliencia ao Windows 11 Smart App Control (SAC):** Isolamento de drivers de terceiros nao assinados com carregamento tardio protegido (`NoInlining`), assegurando que a aplicacao inicia mesmo em ambientes com politicas de seguranca empresariais ativas.
10. **Arquitetura Modular em Classes Parciais:** Decomposicao de classes monoliticas (como `MainForm.cs`), respeitando o Principio da Responsabilidade Unica (SRP) e o limite de linhas.
11. **Navegacao por Seccoes (`SidebarPanel`):** Barra lateral com seccoes recolhiveis (Ferramentas, Hardware, Memoria, Armazenamento, Rede), botao de modo compacto, navegacao por teclado (setas, Home, End) e atalhos `Ctrl+1` a `Ctrl+5` para a visao geral e as quatro ferramentas.
12. **Resolucao Unica de Sensores (`UI/SensorLookup.cs`):** Um unico ponto escolhe o hardware principal (GPU dedicada antes da integrada, memoria fisica) e formata valores; cartoes, resumo KPI e overlay partilham as mesmas regras.

---

## 2. Mapa e Indice de Documentacao (`/docs`)

A documentacao esta estruturada tematicamente no diretorio `/docs`:

```text
LibreHardwareMonitor/
├── docs/
│   ├── INDEX.md                             # Este ficheiro: Guia Master e sitemap do projeto
│   │
│   ├── architecture/
│   │   └── FILE_TREE.md                     # Registo mestre de consulta rapida: mapa de ficheiros, funcoes e limites
│   │
│   ├── reports/
│   │   └── RELATORIO_ESTADO_E_MELHORIAS.md  # Relatorio tecnico detalhado (.NET 8, Smart App Control, Tray, Web)
│   │
│   ├── history/
│   │   └── DOCUMENTATION.md                 # Historico cronologico de todas as alteracoes e sessoes de engenharia
│   │
│   └── agents/
│       └── AGENTS.md                        # Playbook para Agentes de IA: padroes DRY/KISS, regras de codigo e Zero Emojis
```

---

## 3. Padrao Arquitetural e Limites Rigorosos de Linhas

Para manter a codebase manutenivel, legivel e facilmente operavel por humanos e agentes:
* **Classes C# (.cs):** Limite recomendado de **250 a 450 linhas** por ficheiro ou classe parcial.
* **Ficheiros Web (.html, .css, .js):** Limite de **300 a 400 linhas**, mantendo modularidade e zero dependencias pesadas.
* **Principio da Responsabilidade Unica (SRP):** Cada ficheiro cumpre uma finalidade logica bem delimitada.
* **Regra Zero Emojis:** Todos os artefactos, codigos, mensagens de commit e documentacoes utilizam texto formal tecnico sem iconografia emoji.

---

## 4. Estrutura Modular da Solucao

```text
LibreHardwareMonitor/
├── Aga.Controls/                            # Controlo WinForms TreeViewAdv com suporte multicoluna para sensores
├── LibreHardwareMonitorLib/                 # Biblioteca central de deteccao de hardware e leitura de sensores
│   ├── Hardware/                            # Deteccao por categorias (CPU, GPU, Memory, Storage, etc.)
│   ├── Interop/                             # PInvoke e chamadas nativas Win32
│   └── PawnIO/                              # Modulos de comunicacao I/O de baixo nivel
├── LibreHardwareMonitor.Windows.Forms/      # Interface desktop principal WinForms
│   ├── UI/                                  # Formularios, controlos e classes parciais de MainForm
│   │   ├── MainForm.cs                      # Ciclo de vida principal, construtor e estado
│   │   ├── MainForm.Tray.cs                 # Integracao com a bandeja do sistema e intercepcao de fecho
│   │   ├── MainForm.WebServer.cs            # Controlo e configuracao do servidor web
│   │   ├── MainForm.Plot.cs                 # Graficos, escalas e selecao de sensores
│   │   └── MainForm.Tree.cs                 # Arvore de sensores, menus de contexto e eventos de rato
│   │   ├── MainForm.Layout.cs               # Shell Fluent: barra lateral por seccoes, toolbar e alternancia de paginas
│   │   ├── MainForm.Overlay.cs              # Ligacao do overlay: configuracao, telemetria partilhada, atalhos e refresh
│   │   ├── SensorLookup.cs                  # Escolha de hardware/sensores e formatacao de valores (partilhado)
│   ├── Utilities/                           # HttpServer embutido, configuracoes persistentes e logger
│   └── Resources/                           # Recursos graficos, manifest e dashboard Web
├── installer/LibreHardware.iss               # Script Inno Setup do instalador
├── scripts/Publicar-Release.ps1             # Gera instalador e copia portatil em dist/
└── Iniciar.bat                              # Compila e arranca a aplicacao durante o desenvolvimento
```

---

## 5. Como Compilar, Executar e Distribuir

### Pre-requisitos de desenvolvimento
* Windows 10/11 x64
* .NET 8.0 SDK (versao 8.0.400 ou superior)
* Inno Setup 6 (apenas para gerar o instalador): `winget install JRSoftware.InnoSetup`

### Ciclo de desenvolvimento
`Iniciar.bat` compila e arranca a aplicacao a partir de `bin\Publish-SingleFile\LibreHardware.exe` (build rapida, dependente do .NET instalado).

```powershell
dotnet build LibreHardwareMonitor.sln -c Release /p:Platform=x64
```

### Pacotes de distribuicao
```powershell
scripts\Publicar-Release.bat
```
Gera em `dist\` (pasta ignorada pelo Git):
* `LibreHardware-<versao>-Setup.exe`: instalador (Program Files, atalho no Menu Iniciar, desinstalador, opcoes de atalho no ambiente de trabalho, arranque com o Windows e driver PawnIO).
* `LibreHardware-<versao>-portable.zip`: copia portatil; o ficheiro `portable.txt` faz com que as definicoes fiquem na propria pasta.

O executavel distribuido e autonomo (inclui o .NET) e nao requer nada instalado no PC de destino. A versao vem de `Directory.Build.props`.

### Onde ficam os dados
| Modo | Definicoes, registos CSV e `crash.log` |
|---|---|
| Instalado ou desenvolvimento | `%AppData%\Libre Hardware\` |
| Portatil (`portable.txt` ao lado do executavel) | Pasta do executavel |

Na primeira execucao, um `.config` antigo existente ao lado do executavel e copiado para a nova localizacao.

### Linha de comandos
| Argumento | Efeito |
|---|---|
| `--exit` | Pede a instancia em execucao que feche de forma limpa (ventoinhas devolvidas a BIOS); so termina a forca se nao responder em 10 s |
| `--register-startup` / `--unregister-startup` | Cria ou remove a tarefa agendada de arranque com o Windows (usado pelo instalador) |
| `--elevate` | Reinicia com privilegios de administrador |
