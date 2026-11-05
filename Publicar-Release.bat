@echo off
title Publicar LibreHardwareMonitor Standalone Single-File (.NET 8.0 x64)
cd /d "%~dp0"
echo Compilando e gerando binario standalone de ficheiro unico...
set "DOTNET_ROOT=C:\Users\tiago\AppData\Local\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
dotnet publish LibreHardwareMonitor.Windows.Forms\LibreHardwareMonitor.Windows.Forms.csproj -c Release -f net8.0-windows -r win-x64 --self-contained false -p:PublishSingleFile=true -p:Platform=x64 -o "bin\Publish-SingleFile"

if exist "tools\PawnIO_setup.exe" (
    echo A copiar instalador PawnIO para o diretorio de distribuicao...
    copy /y "tools\PawnIO_setup.exe" "bin\Publish-SingleFile\PawnIO_setup.exe" >nul
)

echo.
echo Publicacao concluida! O executavel independente esta em:
echo %~dp0bin\Publish-SingleFile\LibreHardwareMonitor.Windows.Forms.exe
pause
