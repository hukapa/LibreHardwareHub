@echo off
setlocal
title LibreHardwareHub
cd /d "%~dp0"

set "EXE=%~dp0bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe"
if not exist "%EXE%" set "EXE=%~dp0bin\Publish-Test\LibreHardwareMonitor.Windows.Forms.exe"

echo ==========================================================
echo                   LibreHardwareHub
echo ==========================================================
echo  1. Iniciar Aplicacao (Auto-arranque em 3s)
echo  2. Compilar e Publicar Nova Versao
echo ==========================================================

choice /C 12 /N /T 3 /D 1 /M "Prima [1] para Iniciar ou [2] para Compilar: "
if %errorlevel% equ 2 goto BUILD
goto RUN

:BUILD
call "scripts\Publicar-Release.bat"
goto END

:RUN
if not exist "%EXE%" (
    echo [INFO] Binario nao encontrado. A compilar nova versao...
    call "scripts\Publicar-Release.bat"
    set "EXE=%~dp0bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe"
)

if exist "%EXE%" (
    echo [INFO] A iniciar aplicacao...
    start "" "%EXE%"
    exit /b 0
) else (
    echo [ERRO] O executavel nao foi encontrado.
    pause
)
goto END

:END
