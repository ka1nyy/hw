@echo off
chcp 65001 >nul
set "DIR=%LOCALAPPDATA%\Project_Alim"
taskkill /im Project_Alim.exe /f >nul 2>&1
if exist "%DIR%\Project_Alim.exe" "%DIR%\Project_Alim.exe" --uninstall
rd /s /q "%DIR%"
echo Project_Alim udalen.
pause
