@echo off
setlocal enabledelayedexpansion
title Publicar LibreHardwareMonitor Hub Release (.NET 8.0 x64)
cd /d "%~dp0"

rem 1. Verificar Privilegios de Administrador; se necessario, auto-elevar via UAC
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [UAC] A solicitar privilegios de Administrador...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b 0
)

echo ==========================================================
echo      LibreHardwareHub - Publicacao Standalone Release     
echo ==========================================================

rem 2. Encerrar instancias anteriores para libertar bloqueios de ficheiros
echo [INFO] A verificar e encerrar instancias anteriores...
taskkill /f /im LibreHardwareMonitor.Windows.Forms.exe >nul 2>&1
taskkill /f /im LibreHardwareMonitor.exe >nul 2>&1
timeout /t 1 /nobreak >nul

rem 3. Limpeza previa do executavel antigo para garantir que nunca se executa versao obsoleta
if exist "bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe" (
    del /f /q "bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe" >nul 2>&1
    if exist "bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe" (
        echo [ERRO CRITICO] Nao foi possivel remover o executavel antigo.
        echo O ficheiro continua bloqueado por outro processo.
        pause
        exit /b 1
    )
)

rem 4. Configurar .NET SDK
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

rem 5. Compilar com dotnet publish
echo [BUILD] Compilando e gerando binario standalone de ficheiro unico...
dotnet publish LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj -c Release -f net8.0-windows -r win-x64 --self-contained false -p:PublishSingleFile=true -p:Platform=x64 -o "bin\Publish-SingleFile"
if %errorlevel% neq 0 (
    echo.
    echo [ERRO] Falha durante o processo de publicacao.
    pause
    exit /b %errorlevel%
)

rem 6. Copiar instalador PawnIO
if exist "tools\PawnIO_setup.exe" (
    echo [INFO] A copiar instalador PawnIO para a pasta de distribuicao...
    copy /y "tools\PawnIO_setup.exe" "bin\Publish-SingleFile\PawnIO_setup.exe" >nul
)

echo.
echo ==========================================================
echo  SUCESSO: Nova versao compilada e pronta a usar!
echo  Destino: %~dp0bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe
echo ==========================================================
echo.

set /p RESP="Deseja iniciar a nova versao agora? (S/N) [Predefinicao: S]: "
if /i "%RESP%"=="" set RESP=S
if /i "%RESP%"=="S" (
    echo [RUN] A iniciar executavel recem-compilado...
    start "" "%~dp0bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe"
)
