@echo off
setlocal
cd /d "%~dp0"

if "%GODOT_BIN%"=="" set GODOT_BIN=godot

dotnet build Wiki.csproj -v q --nologo || goto :fail
"%GODOT_BIN%" --path .
goto :eof

:fail
echo build failed
exit /b 1
