@echo off
setlocal
if "%~1"=="" goto :interactive
set "first=%~1"
if "%first:~0,1%"=="-" goto :passthrough
if exist "%~1\" goto :with_game
if exist "%~1" goto :with_receipt
goto :passthrough

:interactive
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Install.ps1" -Mode Restore
goto :done

:with_game
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Install.ps1" -Mode Restore -GameRoot "%~1"
goto :done

:with_receipt
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Install.ps1" -Mode Restore -Receipt "%~1"
goto :done

:passthrough
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Install.ps1" -Mode Restore %*

:done
set "result=%errorlevel%"
echo.
if not "%result%"=="0" echo Restore stopped. Read the error above. No game was launched.
pause
exit /b %result%
