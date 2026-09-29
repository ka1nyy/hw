@echo off
chcp 65001 >nul
set "DIR=%LOCALAPPDATA%\Project_Alim"
taskkill /im Project_Alim.exe /f >nul 2>&1
if not exist "%DIR%" mkdir "%DIR%"
copy /y "%~dp0Project_Alim.exe" "%DIR%\" >nul
"%DIR%\Project_Alim.exe" --install
start "" "%DIR%\Project_Alim.exe"
echo Gotovo.
pause
