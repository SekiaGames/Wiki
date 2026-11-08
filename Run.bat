@echo off
rem ---------------------------------------------------------------------------
rem  WorldEditor Asset Wiki launcher
rem  Usage:  Run.bat                 start server on 8123 and open browser
rem          Run.bat 9000            custom port
rem          Run.bat -nobrowser      start server only (for automation)
rem  The server is read-only: it never writes into Godot/assets/converted.
rem ---------------------------------------------------------------------------

setlocal enableextensions

set "PORT=8123"
set "OPEN=1"

:parse_args
if "%~1"=="" goto args_done
if /i "%~1"=="-nobrowser" (
  set "OPEN=0"
) else (
  set "PORT=%~1"
)
shift
goto parse_args
:args_done

cd /d "%~dp0"

if not exist "..\Godot\assets\converted\Tables" (
  echo [ERROR] Converted assets not found: ..\Godot\assets\converted\Tables
  echo         Run extract.bat in the repository root first.
  goto fail
)

set "PY="
where py >nul 2>nul
if not errorlevel 1 set "PY=py -3"
if not defined PY (
  where python >nul 2>nul
  if not errorlevel 1 set "PY=python"
)
if not defined PY (
  echo [ERROR] Python 3 was not found in PATH.
  echo         Install Python 3, or start the server manually:
  echo         python tools\serve.py --port %PORT%
  goto fail
)

netstat -ano | findstr /r /c:":%PORT% .*LISTENING" >nul
if not errorlevel 1 (
  echo [INFO] Port %PORT% is already listening, reusing that server.
  if "%OPEN%"=="1" start "" "http://127.0.0.1:%PORT%/Wiki/index.html"
  goto done
)

echo.
echo   Asset Wiki   http://127.0.0.1:%PORT%/Wiki/index.html
echo   Assets root  ..\Godot\assets\converted
echo   Read-only. Press Ctrl+C to stop.
echo.

if "%OPEN%"=="1" start "" /min cmd /c "timeout /t 2 >nul & rundll32 url.dll,FileProtocolHandler http://127.0.0.1:%PORT%/Wiki/index.html"

%PY% -u "tools\serve.py" --port %PORT%
if errorlevel 1 goto fail
goto done

:fail
echo.
echo [FAILED] See the message above.
pause
endlocal
exit /b 1

:done
endlocal
exit /b 0
