@echo off
rem LibreHardwareHub - gera o instalador e a copia portatil em dist\ (ver Publicar-Release.ps1).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publicar-Release.ps1"
if %errorlevel% neq 0 pause
exit /b %errorlevel%
