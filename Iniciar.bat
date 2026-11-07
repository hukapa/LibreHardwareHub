@echo off
setlocal EnableDelayedExpansion
title LibreHardwareMonitor, LHM
cd /d "%~dp0"

:: 1. Garantir Privilégios de Administrador para leitura direta de hardware (Ring 0 / MSR / PawnIO)
net session >nul 2>&1
if !errorlevel! neq 0 (
    echo ==========================================================
    echo  A solicitar privilegios de Administrador...
    echo ==========================================================
    powershell -Command "Start-Process cmd -ArgumentList '/c `"%~f0`"' -Verb RunAs"
    exit /b 0
)

set "TARGET_DIR=%~dp0bin\Publish-SingleFile"
set "TARGET_EXE=%TARGET_DIR%\LibreHardwareMonitor.Windows.Forms.exe"

if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
    set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;!PATH!"
)

echo ==========================================================
echo                LibreHardwareMonitor, LHM
echo ==========================================================
echo  [1] Compilar e Iniciar (Garante todas as alteracoes)
echo  [2] Iniciar Rapido (Sem compilar)
echo  [3] Apenas Publicar Release
echo ==========================================================
echo  A arrancar a opcao [1] automaticamente em 2 segundos...
echo.

choice /C 123 /N /T 2 /D 1 /M "Escolha [1, 2 ou 3]: "
set "CHOICE_VAL=!errorlevel!"

if "!CHOICE_VAL!"=="2" goto RUN_FAST
if "!CHOICE_VAL!"=="3" goto BUILD_ONLY
goto BUILD_AND_RUN

:BUILD_AND_RUN
echo.
echo ==========================================================
echo  [1/3] A fechar instancias anteriores em segundo plano...
echo ==========================================================
taskkill /f /im LibreHardwareMonitor.Windows.Forms.exe >nul 2>&1
taskkill /f /im LibreHardwareMonitor.exe >nul 2>&1

:: Garantir que o executavel antigo nao esta bloqueado
if exist "%TARGET_EXE%" (
    del /f /q "%TARGET_EXE%" >nul 2>&1
    if exist "%TARGET_EXE%" (
        echo [AVISO] A aguardar libertacao do executavel...
        timeout /t 1 /nobreak >nul 2>&1
        del /f /q "%TARGET_EXE%" >nul 2>&1
    )
)

echo.
echo ==========================================================
echo  [2/3] A compilar e publicar versao atualizada (.NET 8.0)...
echo ==========================================================
dotnet publish LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj -c Release -f net8.0-windows -r win-x64 --self-contained false -p:PublishSingleFile=true -p:Platform=x64 -o "bin\Publish-SingleFile"
if !errorlevel! neq 0 (
    echo.
    echo [ERRO] Falha na compilacao. Verifique as mensagens de erro acima.
    pause
    exit /b !errorlevel!
)

if exist "tools\PawnIO_setup.exe" (
    copy /y "tools\PawnIO_setup.exe" "bin\Publish-SingleFile\PawnIO_setup.exe" >nul 2>&1
)

echo.
echo ==========================================================
echo  [3/3] Aplicacao compilada e iniciada com sucesso!
echo ==========================================================
goto LAUNCH_APP

:RUN_FAST
echo.
echo [INFO] A fechar instancias anteriores...
taskkill /f /im LibreHardwareMonitor.Windows.Forms.exe >nul 2>&1
taskkill /f /im LibreHardwareMonitor.exe >nul 2>&1
if not exist "%TARGET_EXE%" (
    echo [INFO] Executavel nao encontrado. A compilar primeiro...
    goto BUILD_AND_RUN
)
echo [INFO] A iniciar aplicacao existente...
goto LAUNCH_APP

:BUILD_ONLY
echo.
echo [INFO] A compilar release...
call "scripts\Publicar-Release.bat"
goto END

:LAUNCH_APP
if exist "%TARGET_EXE%" (
    start "" /D "%TARGET_DIR%" "%TARGET_EXE%"
    timeout /t 2 /nobreak >nul 2>&1
    exit /b 0
) else (
    echo [ERRO] O executavel nao foi encontrado em "%TARGET_EXE%".
    pause
    exit /b 1
)

:END
