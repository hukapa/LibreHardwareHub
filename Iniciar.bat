@echo off
setlocal EnableDelayedExpansion
title LibreHardwareMonitor, LHM
cd /d "%~dp0"

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
call :CLOSE_APP

:: Garantir que o executavel antigo nao esta bloqueado
if exist "%TARGET_EXE%" (
    del /f /q "%TARGET_EXE%" >nul 2>&1
    if exist "%TARGET_EXE%" (
        powershell -Command "Start-Process taskkill -ArgumentList '/f /im LibreHardwareMonitor.Windows.Forms.exe' -Verb RunAs -Wait" >nul 2>&1
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
echo  [3/3] Aplicacao compilada e pronta!
echo ==========================================================
goto LAUNCH_APP

:RUN_FAST
echo.
echo [INFO] A fechar instancias anteriores...
call :CLOSE_APP
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
    echo [INFO] A iniciar LibreHardwareMonitor com permissoes de Administrador...
    start "" /D "%TARGET_DIR%" "%TARGET_EXE%"
    exit /b 0
) else (
    echo [ERRO] O executavel nao foi encontrado em "%TARGET_EXE%".
    pause
    exit /b 1
)

:END
exit /b 0

:: ==========================================================
:: Fecho seguro: primeiro o fecho normal, que devolve as ventoinhas a BIOS.
:: So se a app nao responder e que o processo e terminado a forca (as
:: ventoinhas podem ficar na ultima velocidade ate reiniciar o PC).
:: ==========================================================
:CLOSE_APP
call :IS_RUNNING || goto :eof
taskkill /im LibreHardwareMonitor.Windows.Forms.exe >nul 2>&1
call :WAIT_EXIT 5
call :IS_RUNNING || goto :eof
:: A app corre como Administrador: pedir o fecho normal com elevacao
powershell -Command "Start-Process taskkill -ArgumentList '/im LibreHardwareMonitor.Windows.Forms.exe' -Verb RunAs -Wait" >nul 2>&1
call :WAIT_EXIT 8
call :IS_RUNNING || goto :eof
echo [AVISO] A app nao fechou normalmente. A terminar a forca...
taskkill /f /im LibreHardwareMonitor.Windows.Forms.exe >nul 2>&1
call :IS_RUNNING || goto :eof
powershell -Command "Start-Process taskkill -ArgumentList '/f /im LibreHardwareMonitor.Windows.Forms.exe' -Verb RunAs -Wait" >nul 2>&1
goto :eof

:IS_RUNNING
tasklist /fi "imagename eq LibreHardwareMonitor.Windows.Forms.exe" 2>nul | find /i "LibreHardwareMonitor.Windows.Forms.exe" >nul
exit /b %errorlevel%

:WAIT_EXIT
set /a _wait=%1
:WAIT_LOOP
call :IS_RUNNING || exit /b 0
set /a _wait-=1
if !_wait! leq 0 exit /b 1
timeout /t 1 /nobreak >nul
goto WAIT_LOOP
