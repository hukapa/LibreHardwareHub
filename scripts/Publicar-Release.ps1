# LibreHardwareHub - gera os pacotes de distribuicao em dist\:
#   LibreHardwareHub-v<versao>-Setup.exe     instalador (requer Inno Setup 6)
#   LibreHardwareHub-v<versao>-Portable.zip  copia portatil (guarda as definicoes na propria pasta)
# A aplicacao e publicada como executavel unico e autonomo: nao precisa do .NET instalado no PC de destino.
$ErrorActionPreference = "Stop"

$Root = Split-Path $PSScriptRoot -Parent
Set-Location -Path $Root

$Project = "LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj"
$PublishDir = Join-Path $Root "bin\Publish-SingleFile"
$DistDir = Join-Path $Root "dist"
$PortableDir = Join-Path $DistDir "LibreHardwareHub-Portable"
$ExeName = "LibreHardwareHub.exe"

# 1. .NET SDK
$UserDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
if (Test-Path (Join-Path $UserDotnet "dotnet.exe")) {
    $env:DOTNET_ROOT = $UserDotnet
    $env:PATH = "$UserDotnet;$env:PATH"
}
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Error "[ERRO] .NET SDK nao encontrado. Instale o .NET 8.0 SDK."
    exit 1
}

# 2. Versao (fonte unica: Directory.Build.props)
[xml]$Props = Get-Content (Join-Path $Root "Directory.Build.props")
$Version = "$($Props.Project.PropertyGroup.Version)".Trim()
Write-Host "== LibreHardwareHub $Version ==" -ForegroundColor Cyan

# 3. Publicar executavel unico e autonomo
Write-Host "[1/3] A publicar executavel autonomo (Self-Contained)..." -ForegroundColor Green
New-Item -ItemType Directory -Force $PublishDir | Out-Null

& dotnet publish $Project -c Release -f net8.0-windows -r win-x64 -p:Platform=x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:DebugSymbols=false -p:GenerateDocumentationFile=false -o $PublishDir -v q -nologo

$TargetExe = Join-Path $PublishDir $ExeName
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $TargetExe)) {
    Write-Error "[ERRO] Falha durante a publicacao."
    exit 1
}

# 4. Copia portatil
Write-Host "[2/3] A criar a copia portatil..." -ForegroundColor Green
New-Item -ItemType Directory -Force $DistDir | Out-Null

if (Test-Path $PortableDir) {
    Remove-Item -Path $PortableDir -Recurse -Force
}
New-Item -ItemType Directory -Force $PortableDir | Out-Null

# Copiar executavel principal
Copy-Item $TargetExe (Join-Path $PortableDir $ExeName) -Force

# Copiar PawnIO_setup.exe
$PawnIoSetup = Join-Path $Root "tools\PawnIO_setup.exe"
if (Test-Path $PawnIoSetup) {
    Copy-Item $PawnIoSetup (Join-Path $PortableDir "PawnIO_setup.exe") -Force
}

# Ficheiro marcador portable.txt
$Marker = Join-Path $PortableDir "portable.txt"
Set-Content -Path $Marker -Encoding ascii -Value "Modo portatil ativo: as definicoes e os registos ficam guardados nesta pasta."

# Ficheiro LEIAME.txt com instrucoes claras
$LeiameContent = @"
======================================================================
                         LibreHardwareHub Portable
======================================================================

Instrucoes de Utilizacao:

1. Extrair todos os ficheiros deste arquivo ZIP para uma pasta da sua
   escolha (por exemplo: C:\Programas\LibreHardwareHub ou no Ambiente
   de Trabalho).

2. Executar o "LibreHardwareHub.exe" dando duplo clique.
   A aplicacao solicitara automaticamente privilegios de Administrador
   atraves do Controlo de Conta de Utilizador (UAC), necessarios para
   comunicar com os sensores de hardware da motherboard, processador
   e placa grafica.

3. Na primeira execucao, caso o driver de baixo nivel PawnIO nao esteja
   instalado no seu sistema, aceite a instalacao recomendada clicando em
   "OK". O assistente ira configurar o driver de leitura Ring-0 de
   forma limpa e segura.

4. Modo Portatil Ativo:
   Gracas ao ficheiro "portable.txt" incluido, todas as suas definicoes,
   curvas de ventilacao, perfis de telemetria e o layout do HUD Overlay
   ficam guardados diretamente no ficheiro "LibreHardwareHub.config"
   dentro desta pasta, sem espalhar ficheiros pelo sistema.

======================================================================
"@
Set-Content -Path (Join-Path $PortableDir "LEIAME.txt") -Encoding utf8 -Value $LeiameContent

if (Test-Path (Join-Path $Root "LICENSE")) {
    Copy-Item (Join-Path $Root "LICENSE") (Join-Path $PortableDir "LICENSE.txt") -Force
}

# Comprimir arquivo ZIP portatil
$Zip = Join-Path $DistDir "LibreHardwareHub-v$Version-Portable.zip"
if (Test-Path $Zip) { Remove-Item $Zip -Force }
Compress-Archive -Path (Join-Path $PortableDir "*") -DestinationPath $Zip -Force

# 5. Instalador
Write-Host "[3/3] A compilar o instalador..." -ForegroundColor Green
$Iscc = @(
    (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

$Setup = Join-Path $DistDir "LibreHardwareHub-v$Version-Setup.exe"
if ($Iscc) {
    & $Iscc "/DAppVersion=$Version" "/DSourceDir=$PublishDir" "/Qp" (Join-Path $Root "installer\LibreHardware.iss")
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Setup)) {
        Write-Error "[ERRO] Falha ao compilar o instalador."
        exit 1
    }
}
else {
    Write-Warning "Inno Setup 6 nao encontrado: o instalador nao foi criado. Instale com: winget install JRSoftware.InnoSetup"
}

Write-Host ""
Write-Host ("Pacotes gerados em {0}:" -f $DistDir) -ForegroundColor Green
Get-ChildItem $DistDir -Filter "LibreHardwareHub-*" | ForEach-Object {
    if (-not $_.PSIsContainer) {
        Write-Host ("  {0}  ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB))
    }
}
