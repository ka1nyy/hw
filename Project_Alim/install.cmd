@echo off
chcp 65001 >nul
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "DIR=%LOCALAPPDATA%\Project_Alim"
set "SRC=%~dp0Jarvis.cs"
set "APP=%DIR%\Edge.exe"

if not exist "%CSC%" (
    echo .NET Framework not found.
    pause
    exit /b 1
)
if not exist "%SRC%" (
    echo Jarvis.cs is missing. Extract the complete Project_Alim folder first.
    pause
    exit /b 1
)

if not exist "%DIR%" mkdir "%DIR%"
if not exist "%DIR%" (
    echo Cannot create the installation folder.
    pause
    exit /b 1
)

if exist "%DIR%\.edge-removing" (
    echo Automatic removal is in progress. Wait a few seconds before installing again.
    echo If this message persists, run uninstall.cmd before a fresh installation.
    pause
    exit /b 1
)

echo Closing previous copies from the Project_Alim installation folder...
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { $old=Join-Path $env:DIR 'Windows Audio.exe'; Get-Process -Name 'Edge','Windows Audio' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $env:APP -or $_.Path -eq $old } | ForEach-Object { Stop-Process -InputObject $_ -ErrorAction Stop; if (-not $_.WaitForExit(10000)) { throw 'Previous process did not stop.' } }; exit 0 } catch { Write-Host $_.Exception.Message; exit 1 }"
set "PROCESS_CHECK_RESULT=%ERRORLEVEL%"
if not "%PROCESS_CHECK_RESULT%"=="0" (
    echo Previous-process check failed. Installation stopped. Exit code: %PROCESS_CHECK_RESULT%.
    echo Check the PowerShell error above or the security notification for the cause.
    pause
    exit /b 1
)

if exist "%DIR%\.edge-removing" (
    echo Automatic removal started while closing the previous copy. Installation stopped.
    echo Wait for removal to finish, then install again.
    pause
    exit /b 1
)

echo Building...
"%CSC%" /nologo /target:winexe /optimize+ /define:FRIEND /out:"%APP%" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll "%SRC%"
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)

if exist "%DIR%\.edge-removing" (
    echo Automatic removal started during installation. Installation stopped.
    echo Wait for removal to finish, then install again.
    pause
    exit /b 1
)

if exist "%~dp0icon.png" (
    copy /y "%~dp0icon.png" "%DIR%\icon.png" >nul
    if errorlevel 1 (
        echo Cannot copy the tray icon. Installation stopped.
        pause
        exit /b 1
    )
)

echo Checking hotkey setup without starting the interface...
start "" /wait "%APP%" --check
set "CHECK_RESULT=%ERRORLEVEL%"
if "%CHECK_RESULT%"=="0" (
    echo Keyboard and mouse handlers could be installed during this check.
) else (
    echo Hotkey setup was not confirmed. Check result: %CHECK_RESULT%.
    echo This does not identify Kaspersky as the cause. Check its reports if it showed a notification.
)

"%APP%" --install
if errorlevel 1 (
    echo Autostart registration failed. Continuing with manual startup.
    echo You can launch Edge.exe from the Project_Alim installation folder.
)

echo Starting Edge...
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { Start-Process -FilePath $env:APP; Start-Sleep -Seconds 3; $running=Get-Process -Name 'Edge' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $env:APP }; if (-not $running) { throw 'Edge exited or was blocked. Check the security notification.' } } catch { Write-Host $_.Exception.Message; exit 1 }"
if errorlevel 1 (
    echo Launch was not confirmed.
    echo If company security blocked it, contact your IT administrator.
    pause
    exit /b 1
)

echo Edge is installed and its process is running.
echo This does not verify keyboard shortcuts or network access.
echo This copy expires and attempts to remove its own files three hours after its first launch.
echo Closing or restarting the app does not reset that deadline.
echo Ctrl + the key below Esc toggles activation. The green dot flashes briefly.
echo Ctrl + that key selects the classic Tab / F1 / F2 / middle-click controls.
echo Hold M and press comma for the second mode: M+N screenshot, M+B text, M+K answer.
echo Bindings use physical keys, so Russian layouts work too.
echo You can also right-click the Edge tray icon to enable it or exit.
pause
exit /b 0
