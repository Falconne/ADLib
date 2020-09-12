@echo off
setlocal
if not exist %1 (
        echo %1 not found
        exit /b 1
)

set PowerShellCmd=powershell.exe

echo Executing:
echo %PowerShellCmd% -ExecutionPolicy Unrestricted -Command %*
%PowerShellCmd% -ExecutionPolicy Unrestricted -Command %*
endlocal
exit /b %ERRORLEVEL%
