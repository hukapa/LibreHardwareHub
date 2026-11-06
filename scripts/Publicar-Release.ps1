# Script PowerShell para Publicar LibreHardwareHub Release (.NET 8.0 x64)
$ErrorActionPreference = "Stop"

$Root = Split-Path $PSScriptRoot -Parent
Set-Location -Path $Root

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "     LibreHardwareHub - Publicacao Standalone Release     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Configurar .NET SDK
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

# 2. Tentar fechar instancias anteriores
Get-Process -Name "LibreHardwareMonitor.Windows.Forms", "LibreHardwareMonitor" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 3. Limpeza previa do executavel antigo
$OutputDir = Join-Path $Root "bin\Publish-SingleFile"
$TargetExe = Join-Path $OutputDir "LibreHardwareMonitor.Windows.Forms.exe"

if (Test-Path $TargetExe) {
    try {
        Remove-Item -Path $TargetExe -Force -ErrorAction Stop
    }
    catch {
        Write-Warning "[AVISO] O programa ainda esta aberto em segundo plano e bloqueia o ficheiro."
        Write-Host "Por favor feche o LibreHardwareMonitor no tabuleiro do sistema ou Gestor de Tarefas." -ForegroundColor Red
        exit 1
    }
}

# 4. Compilar com dotnet publish
$Project = "LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj"
Write-Host "[BUILD] A compilar e gerar pacote de ficheiro unico..." -ForegroundColor Green

& dotnet publish $Project -c Release -f net8.0-windows -r win-x64 --self-contained false -p:PublishSingleFile=true -p:Platform=x64 -o $OutputDir

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $TargetExe)) {
    Write-Error "[ERRO] Falha durante o processo de publicacao."
    exit 1
}

# 5. Copiar PawnIO Setup
$PawnIoSetup = Join-Path $Root "tools\PawnIO_setup.exe"
if (Test-Path $PawnIoSetup) {
    Copy-Item -Path $PawnIoSetup -Destination (Join-Path $OutputDir "PawnIO_setup.exe") -Force
}

$BuildTime = (Get-Item $TargetExe).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " SUCESSO: Nova versao compilada e pronta a usar!          " -ForegroundColor Green
Write-Host " Data do Build: $BuildTime" -ForegroundColor Green
Write-Host " Destino      : $TargetExe" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
