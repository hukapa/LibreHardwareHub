@echo off
setlocal

set "EXE_PATH=%~dp0..\bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe"
set "WORK_DIR=%~dp0..\bin\Publish-SingleFile"

if not exist "%EXE_PATH%" (
    set "EXE_PATH=%~dp0..\bin\Release\net8.0-windows\LibreHardwareMonitor.Windows.Forms.exe"
    set "WORK_DIR=%~dp0..\bin\Release\net8.0-windows"
)

if not exist "%EXE_PATH%" (
    echo Binario executavel nao encontrado. Execute Publicar-Release.bat primeiro.
    pause
    exit /b 1
)

:: Se ja for Administrador, inicia com working directory correto
net session >nul 2>&1
if %errorlevel% == 0 (
    start "" /D "%WORK_DIR%" "%EXE_PATH%"
    exit /b 0
)

:: Solicita elevacao administrativa silenciosa
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%EXE_PATH%' -WorkingDirectory '%WORK_DIR%' -Verb RunAs"
exit /b 0
