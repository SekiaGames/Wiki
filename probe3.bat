@echo on
rem ---------------------------------------------------------------------------
rem  Asset Wiki autostart (interactive)
rem  Double-click this file and pick a number.
rem  It only touches one shortcut inside the current user's Startup folder:
rem    %APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\WorldEditor Asset Wiki.lnk
rem ---------------------------------------------------------------------------

setlocal enableextensions
title Asset Wiki - autostart

set "PS1=%~dp0tools\autostart.ps1"
if not exist "%PS1%" (
  echo [ERROR] Missing tools\autostart.ps1
  goto fail
)
if not exist "%~dp0Run.bat" (
  echo [ERROR] Missing Run.bat
  goto fail
)

:menu
cls
echo ==========================================================
echo   Asset Wiki - autostart
echo ==========================================================
call :show_status
echo.
echo    1   Enable autostart   (start the wiki at logon)
echo    2   Disable autostart
echo    3   Refresh status
echo    0   Exit
echo.
set "PICK="
set /p "PICK=Choose 1/2/3/0 : "
if "%PICK%"=="0" goto bye
if "%PICK%"=="1" goto enable
if "%PICK%"=="2" goto disable
if "%PICK%"=="3" goto menu
echo.
echo   Invalid choice, pick 1, 2, 3 or 0.
timeout /t 1 >nul
goto menu

:enable
echo.
set "WANT_BROWSER="
set /p "WANT_BROWSER=Open the wiki in the browser at logon? [Y/n] : "
if /i "%WANT_BROWSER%"=="n" goto enable_server_only
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -Action "add" -ScriptPath "%~dp0Run.bat" -Arguments ""
echo.
pause
goto menu

:enable_server_only
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -Action "add" -ScriptPath "%~dp0Run.bat" -Arguments "-nobrowser"
echo.
pause
goto menu

:disable
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -Action "remove" -ScriptPath "%~dp0Run.bat" -Arguments ""
echo.
pause
goto menu

:show_status
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -Action "status" -ScriptPath "%~dp0Run.bat" -Arguments ""
goto :eof

:bye
cls
echo Closed. Nothing was changed by exiting.
timeout /t 1 >nul
endlocal
exit /b 0

:fail
echo.
echo [FAILED] See the message above.
pause
endlocal
exit /b 1
