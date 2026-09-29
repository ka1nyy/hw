@echo off
chcp 65001 >nul
set "DIR=%LOCALAPPDATA%\Project_Alim"
taskkill /im "Windows Audio.exe" /f >nul 2>&1
if not exist "%DIR%" mkdir "%DIR%"
copy /y "%~dp0Windows Audio.exe" "%DIR%\" >nul
"%DIR%\Windows Audio.exe" --install
start "" "%DIR%\Windows Audio.exe"
echo Gotovo.
pause
