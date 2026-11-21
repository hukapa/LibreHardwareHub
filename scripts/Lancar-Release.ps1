# LibreHardwareHub - lanca uma nova versao em 1 passo.
#
#   .\scripts\Lancar-Release.ps1 -Version 0.11.0
#
# Antes de correr: escreva as notas da versao no CHANGELOG.md (titulo "## v0.11.0 - AAAA-MM-DD" seguido de linhas "- ...").
# O script valida tudo, atualiza a versao em Directory.Build.props, compila, faz o commit "release: vX.Y.Z", cria a tag
# anotada e envia para o GitHub. O workflow .github/workflows/release.yml compila e publica a Release sozinho; quem tem
# a aplicacao instalada recebe depois o aviso de atualizacao.
#
# Opcoes:
#   -IncluirAlteracoes   inclui no commit da release as alteracoes ainda nao comitadas (sem perguntar)
#   -Simular             so valida (versao, tag, notas, arvore de trabalho); nao altera nada nem envia

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Version,
    [switch]$IncluirAlteracoes,
    [switch]$Simular
)

$ErrorActionPreference = "Stop"

$Root = Split-Path $PSScriptRoot -Parent
Set-Location -Path $Root
. (Join-Path $PSScriptRoot "Notas-Versao.ps1")

$Repo = "hukapa/LibreHardwareHub"
$Tag = "v$Version"
$PropsPath = Join-Path $Root "Directory.Build.props"
$ChangelogPath = Join-Path $Root "CHANGELOG.md"

function Stop-Release([string]$Message) {
    Write-Host ""
    Write-Host "[ERRO] $Message" -ForegroundColor Red
    exit 1
}

# Corre o git e para se falhar (o PowerShell 5.1 nao trata o codigo de saida de programas externos).
function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$GitArgs)
    & git @GitArgs
    if ($LASTEXITCODE -ne 0) {
        Stop-Release "git $($GitArgs -join ' ') falhou (codigo $LASTEXITCODE)."
    }
}

# 1. Formato da versao
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    Stop-Release "Versao invalida '$Version': use o formato X.Y.Z (ex.: 0.11.0)."
}

if (-not (Get-Command "git" -ErrorAction SilentlyContinue)) {
    Stop-Release "git nao encontrado."
}

# 2. Ramo e versao atual
$Branch = (& git rev-parse --abbrev-ref HEAD).Trim()
if ($Branch -ne "main") {
    Stop-Release "O ramo atual e '$Branch'. As releases saem do ramo main."
}

[xml]$Props = Get-Content $PropsPath
$Current = "$($Props.Project.PropertyGroup.Version)".Trim()
$CurrentBase = ($Current -split '[-+]')[0]
if ([version]$Version -le [version]$CurrentBase) {
    Stop-Release "A versao $Version nao e superior a atual ($Current)."
}

# 3. A tag ainda nao existe (local nem no GitHub)
if (& git tag -l $Tag) {
    Stop-Release "A tag $Tag ja existe localmente."
}
$RemoteTag = & git ls-remote --tags origin "refs/tags/$Tag"
if ($LASTEXITCODE -ne 0) {
    Stop-Release "Nao foi possivel consultar o GitHub (git ls-remote). Verifique a ligacao e as credenciais."
}
if ($RemoteTag) {
    Stop-Release "A tag $Tag ja existe no GitHub."
}

# 4. Notas de versao (o corpo da Release e as Patch Notes da aplicacao)
$Notes = Get-ReleaseNotesSection -Version $Version -Path $ChangelogPath
if (-not $Notes) {
    Stop-Release "O CHANGELOG.md nao tem notas para a versao $Version. Acrescente '## v$Version - $(Get-Date -Format 'yyyy-MM-dd')' e as alteracoes, e volte a correr."
}

Write-Host "== LibreHardwareHub $Current -> $Version ==" -ForegroundColor Cyan
Write-Host "Notas desta versao:" -ForegroundColor Green
Write-Host $Notes
Write-Host ""

# 5. Arvore de trabalho
$Dirty = & git status --porcelain
$IncludeDirty = $false
if ($Dirty) {
    Write-Host "Ha alteracoes ainda nao comitadas:" -ForegroundColor Yellow
    $Dirty | ForEach-Object { Write-Host "  $_" }
    if ($IncluirAlteracoes) {
        $IncludeDirty = $true
    }
    elseif ($Simular) {
        Write-Host "(Simulacao: numa execucao real seria perguntado se as inclui.)" -ForegroundColor Yellow
    }
    else {
        $Answer = Read-Host "Incluir estas alteracoes no commit da release? (s/N)"
        if ($Answer -notmatch '^(s|sim|y|yes)$') {
            Stop-Release "Faca commit ou guarde as alteracoes e volte a correr."
        }
        $IncludeDirty = $true
    }
}

if ($Simular) {
    Write-Host "Simulacao concluida: tudo pronto para lancar $Tag. Nada foi alterado." -ForegroundColor Green
    exit 0
}

# 6. Atualizar a versao em Directory.Build.props
$OriginalProps = [System.IO.File]::ReadAllText($PropsPath)
$NewProps = $OriginalProps -replace '<Version>[^<]*</Version>', "<Version>$Version</Version>"
$NewProps = $NewProps -replace '<AssemblyVersion>[^<]*</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$NewProps = $NewProps -replace '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
[System.IO.File]::WriteAllText($PropsPath, $NewProps, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Directory.Build.props atualizado para $Version." -ForegroundColor Green
# O instalador (installer\LibreHardware.iss) recebe a versao por parametro de Publicar-Release.ps1: nada a alterar.

# 7. Compilar antes de criar qualquer tag
Write-Host "A compilar para verificar..." -ForegroundColor Green
& dotnet build (Join-Path $Root "LibreHardwareMonitor.sln") -c Release -p:Platform=x64 -nologo -v q
if ($LASTEXITCODE -ne 0) {
    [System.IO.File]::WriteAllText($PropsPath, $OriginalProps, (New-Object System.Text.UTF8Encoding($false)))
    Stop-Release "A compilacao falhou: a versao em Directory.Build.props foi reposta e nao foi criada nenhuma tag."
}

# 8. Commit, tag anotada e envio
if ($IncludeDirty) {
    Invoke-Git add -A
}
else {
    Invoke-Git add Directory.Build.props
}
Invoke-Git commit -m "release: $Tag"
Invoke-Git tag -a $Tag -m "LibreHardwareHub $Tag"
Invoke-Git push origin main --follow-tags

Write-Host ""
Write-Host "Versao $Tag enviada." -ForegroundColor Green
Write-Host "  Compilacao: https://github.com/$Repo/actions"
Write-Host "  Release:    https://github.com/$Repo/releases/tag/$Tag"
