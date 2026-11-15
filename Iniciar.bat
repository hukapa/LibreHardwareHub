@echo off
setlocal EnableDelayedExpansion
title LibreHardwareHub
cd /d "%~dp0"

set "TARGET_DIR=%~dp0bin\Publish-SingleFile"
set "TARGET_EXE=%TARGET_DIR%\LibreHardwareHub.exe"

if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
    set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;!PATH!"
)

echo ==========================================================
echo                      LibreHardwareHub
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
        powershell -Command "Start-Process taskkill -ArgumentList '/f /im LibreHardwareHub.exe' -Verb RunAs -Wait" >nul 2>&1
        timeout /t 1 /nobreak >nul 2>&1
        del /f /q "%TARGET_EXE%" >nul 2>&1
    )
)

echo.
echo ==========================================================
echo  [2/3] A compilar e publicar versao atualizada (.NET 8.0)...
echo ==========================================================
dotnet publish LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj -c Release -f net8.0-windows -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o "bin\Publish-SingleFile"
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
    echo [INFO] A iniciar LibreHardwareHub com permissoes de Administrador...
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
taskkill /im LibreHardwareHub.exe >nul 2>&1
taskkill /im LibreHardware.exe >nul 2>&1
call :WAIT_EXIT 5
call :IS_RUNNING || goto :eof
:: A app corre como Administrador: pedir o fecho normal com elevacao
powershell -Command "Start-Process taskkill -ArgumentList '/im LibreHardwareHub.exe' -Verb RunAs -Wait" >nul 2>&1
powershell -Command "Start-Process taskkill -ArgumentList '/im LibreHardware.exe' -Verb RunAs -Wait" >nul 2>&1
call :WAIT_EXIT 8
call :IS_RUNNING || goto :eof
echo [AVISO] A app nao fechou normalmente. A terminar a forca...
taskkill /f /im LibreHardwareHub.exe >nul 2>&1
taskkill /f /im LibreHardware.exe >nul 2>&1
call :IS_RUNNING || goto :eof
powershell -Command "Start-Process taskkill -ArgumentList '/f /im LibreHardwareHub.exe' -Verb RunAs -Wait" >nul 2>&1
powershell -Command "Start-Process taskkill -ArgumentList '/f /im LibreHardware.exe' -Verb RunAs -Wait" >nul 2>&1
goto :eof

:IS_RUNNING
tasklist /fi "imagename eq LibreHardwareHub.exe" 2>nul | find /i "LibreHardwareHub.exe" >nul
if %errorlevel% == 0 exit /b 0
tasklist /fi "imagename eq LibreHardware.exe" 2>nul | find /i "LibreHardware.exe" >nul
exit /b %errorlevel%

:WAIT_EXIT
set /a _wait=%1
:WAIT_LOOP
call :IS_RUNNING || exit /b 0
set /a _wait-=1
if !_wait! leq 0 exit /b 1
timeout /t 1 /nobreak >nul
goto WAIT_LOOP
