@echo off
chcp 65001 >nul
set "DIR=%LOCALAPPDATA%\Project_Alim"
taskkill /im Project_Alim.exe /f >nul 2>&1
if not exist "%DIR%" mkdir "%DIR%"
copy /y "%~dp0Project_Alim.exe" "%DIR%\" >nul
"%DIR%\Project_Alim.exe" --install
start "" "%DIR%\Project_Alim.exe"
echo Gotovo. Ctrl+yo vkl/vykl, Tab skrinshot, F1 vopros tekstom, koleso skryt otvet, F12 istoriya.
pause
