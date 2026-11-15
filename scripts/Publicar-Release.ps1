# Libre Hardware - gera os pacotes de distribuicao em dist\:
#   LibreHardware-<versao>-Setup.exe     instalador (requer Inno Setup 6)
#   LibreHardware-<versao>-portable.zip  copia portatil (guarda as definicoes na propria pasta)
# A aplicacao e publicada como executavel unico e autonomo: nao precisa do .NET instalado no PC de destino.
$ErrorActionPreference = "Stop"

$Root = Split-Path $PSScriptRoot -Parent
Set-Location -Path $Root

$Project = "LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj"
$PublishDir = Join-Path $Root "bin\Publish\LibreHardware"
$DistDir = Join-Path $Root "dist"
$ExeName = "LibreHardware.exe"

# 1. .NET SDK (instalacao por utilizador tem prioridade, como nos restantes scripts)
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
Write-Host "== Libre Hardware $Version ==" -ForegroundColor Cyan

# 3. Publicar para uma pasta limpa
if (Test-Path $PublishDir) {
    try {
        Remove-Item -Path $PublishDir -Recurse -Force
    }
    catch {
        Write-Error "[ERRO] Nao foi possivel limpar $PublishDir. Feche o Libre Hardware se estiver a correr a partir dessa pasta."
        exit 1
    }
}

Write-Host "[1/3] A publicar executavel autonomo..." -ForegroundColor Green
& dotnet publish $Project -c Release -f net8.0-windows -r win-x64 -p:Platform=x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:DebugSymbols=false -p:GenerateDocumentationFile=false -o $PublishDir -v q -nologo

$TargetExe = Join-Path $PublishDir $ExeName
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $TargetExe)) {
    Write-Error "[ERRO] Falha durante a publicacao."
    exit 1
}

# So o que e distribuido fica na pasta: executavel, instalador do driver e licenca.
Get-ChildItem $PublishDir -Exclude $ExeName | Remove-Item -Recurse -Force
$PawnIoSetup = Join-Path $Root "tools\PawnIO_setup.exe"
if (Test-Path $PawnIoSetup) {
    Copy-Item $PawnIoSetup (Join-Path $PublishDir "PawnIO_setup.exe") -Force
}
Copy-Item (Join-Path $Root "LICENSE") (Join-Path $PublishDir "LICENSE.txt") -Force

New-Item -ItemType Directory -Force $DistDir | Out-Null

# 4. Copia portatil
Write-Host "[2/3] A criar a copia portatil..." -ForegroundColor Green
$Zip = Join-Path $DistDir "LibreHardware-$Version-portable.zip"
$Marker = Join-Path $PublishDir "portable.txt"
Set-Content -Path $Marker -Encoding ascii -Value "Modo portatil: com este ficheiro presente, as definicoes e os registos ficam nesta pasta."
if (Test-Path $Zip) { Remove-Item $Zip -Force }
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $Zip
Remove-Item $Marker -Force

# 5. Instalador
Write-Host "[3/3] A compilar o instalador..." -ForegroundColor Green
$Iscc = @(
    (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

$Setup = Join-Path $DistDir "LibreHardware-$Version-Setup.exe"
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
Write-Host "Pacotes em $DistDir" -ForegroundColor Green
Get-ChildItem $DistDir -Filter "LibreHardware-$Version-*" | ForEach-Object {
    Write-Host ("  {0}  ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB))
}
