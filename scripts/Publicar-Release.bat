@echo off
setlocal
title Publicar LibreHardwareHub (.NET 8.0 x64)
cd /d "%~dp0.."

echo ==========================================================
echo      LibreHardwareHub - Publicacao Standalone Release     
echo ==========================================================

rem 1. Configurar .NET SDK
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
    set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
)

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERRO] .NET SDK nao encontrado no sistema. Por favor instale o .NET 8.0 SDK.
    pause
    exit /b 1
)

rem 2. Tentar fechar instancia em execucao para evitar bloqueio
taskkill /f /im LibreHardwareMonitor.Windows.Forms.exe >nul 2>&1
taskkill /f /im LibreHardwareMonitor.exe >nul 2>&1

rem 3. Limpeza previa do executavel antigo
set "TARGET_EXE=bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe"
if exist "%TARGET_EXE%" (
    del /f /q "%TARGET_EXE%" >nul 2>&1
    if exist "%TARGET_EXE%" (
        echo.
        echo [AVISO] O programa ainda esta aberto em segundo plano e bloqueia o ficheiro.
        echo Por favor feche o LibreHardwareMonitor no tabuleiro do sistema (relogio)
        echo ou termine o processo no Gestor de Tarefas antes de compilar.
        echo.
        pause
        exit /b 1
    )
)

rem 4. Compilar com dotnet publish
echo [BUILD] A compilar e gerar pacote de ficheiro unico...
dotnet publish LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj -c Release -f net8.0-windows -r win-x64 --self-contained false -p:PublishSingleFile=true -p:Platform=x64 -o "bin\Publish-SingleFile"
if %errorlevel% neq 0 (
    echo.
    echo [ERRO] Falha durante o processo de publicacao.
    pause
    exit /b %errorlevel%
)

rem 5. Copiar instalador do driver PawnIO
if exist "tools\PawnIO_setup.exe" (
    echo [INFO] A copiar instalador PawnIO para o diretorio de distribuicao...
    copy /y "tools\PawnIO_setup.exe" "bin\Publish-SingleFile\PawnIO_setup.exe" >nul
)

echo.
echo ==========================================================
echo  SUCESSO: Nova versao compilada e pronta a usar!
echo  Destino: %~dp0..\bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe
echo ==========================================================
echo.
pause
