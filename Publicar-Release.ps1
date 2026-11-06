# Script PowerShell Automatizado para Publicar LibreHardwareMonitor Hub Standalone (.NET 8.0 x64)
# Inclui auto-elevacao UAC, encerramento de instancias antigas e prevencao de binarios obsoletos.

$ErrorActionPreference = "Stop"

# 1. Garantir Privilegios de Administrador (necessario para encerrar instancias anteriores e instalar drivers)
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[UAC] A solicitar privilegios de Administrador para gestao de processos e publicacao..." -ForegroundColor Yellow
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "powershell.exe"
    $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    $psi.Verb = "runas"
    try {
        [System.Diagnostics.Process]::Start($psi) | Out-Null
        exit 0
    }
    catch {
        Write-Error "[ERRO] Privilegios de Administrador recusados pelo utilizador."
        exit 1
    }
}

Set-Location -Path $PSScriptRoot
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "     LibreHardwareHub - Publicacao Standalone Release     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. Encerrar quaisquer instancias em execucao para libertar bloqueios de ficheiros
$processNames = @("LibreHardwareMonitor.Windows.Forms", "LibreHardwareMonitor")
foreach ($name in $processNames) {
    $running = Get-Process -Name $name -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "[INFO] A encerrar instancia ativa de $name (PID: $($running.Id -join ', '))..." -ForegroundColor Yellow
        $running | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 800
    }
}

# 3. Limpeza Previa Obrigatoria: Garantir que nao existem ficheiros obsoletos
$OutputDir = Join-Path $PSScriptRoot "bin\Publish-SingleFile"
$TargetExe = Join-Path $OutputDir "LibreHardwareMonitor.Windows.Forms.exe"

if (Test-Path $TargetExe) {
    try {
        Remove-Item -Path $TargetExe -Force -ErrorAction Stop
        Write-Host "[INFO] Executavel anterior removido com sucesso." -ForegroundColor DarkGray
    }
    catch {
        Write-Error "[ERRO CRITICO] Nao foi possivel remover o executavel anterior: $($_.Exception.Message)`nVerifique se o processo ainda esta a correr ou bloqueado por outro utilizador."
        exit 1
    }
}

# 4. Configurar .NET SDK
$UserDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
if (Test-Path (Join-Path $UserDotnet "dotnet.exe")) {
    $env:DOTNET_ROOT = $UserDotnet
    $env:PATH = "$UserDotnet;$env:PATH"
}

$DotnetCmd = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $DotnetCmd) {
    Write-Error "[ERRO] .NET SDK nao encontrado no sistema. Por favor instale o .NET 8.0 SDK."
    exit 1
}

# 5. Executar dotnet publish
$Project = "LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj"
Write-Host "[BUILD] Compilando e gerando pacote de ficheiro unico..." -ForegroundColor Green

& dotnet publish $Project -c Release -f net8.0-windows -r win-x64 --self-contained false -p:PublishSingleFile=true -p:Platform=x64 -o $OutputDir

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $TargetExe)) {
    Write-Error "[ERRO] Falha durante o processo de publicacao do .NET SDK."
    exit 1
}

# 6. Copiar dependencias necessarias (PawnIO Setup)
$PawnIoSetup = Join-Path $PSScriptRoot "tools\PawnIO_setup.exe"
if (Test-Path $PawnIoSetup) {
    Copy-Item -Path $PawnIoSetup -Destination (Join-Path $OutputDir "PawnIO_setup.exe") -Force
}

$BuildTime = (Get-Item $TargetExe).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
$FileSizeMb = [math]::Round((Get-Item $TargetExe).Length / 1MB, 2)

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " SUCESSO: Nova versao compilada e pronta a usar!          " -ForegroundColor Green
Write-Host " Data do Build : $BuildTime" -ForegroundColor Green
Write-Host " Tamanho       : $FileSizeMb MB" -ForegroundColor Green
Write-Host " Destino       : $TargetExe" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""

# 7. Opcao de Execucao Imediata
$res = Read-Host "Deseja iniciar a nova versao agora? (S/N) [Predefinicao: S]"
if ([string]::IsNullOrWhiteSpace($res) -or $res.Trim().ToUpper() -eq "S") {
    Write-Host "[RUN] A iniciar $TargetExe com privilegios elevados..." -ForegroundColor Cyan
    Start-Process -FilePath $TargetExe -WorkingDirectory $OutputDir
}
