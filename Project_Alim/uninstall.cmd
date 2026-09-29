@echo off
chcp 65001 >nul
set "DIR=%LOCALAPPDATA%\Project_Alim"
taskkill /im "Windows Audio.exe" /f >nul 2>&1
if exist "%DIR%\Windows Audio.exe" "%DIR%\Windows Audio.exe" --uninstall
rd /s /q "%DIR%"
echo Project_Alim udalen.
pause
