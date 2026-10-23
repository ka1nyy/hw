@echo off
chcp 65001 >nul
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "DIR=%LOCALAPPDATA%\Project_Alim"
set "SRC=%~dp0Jarvis.cs"
set "AUTH_SRC=%~dp0Subscriptions.cs"
set "APP=%DIR%\Edge.exe"

if not exist "%CSC%" (
    echo .NET Framework not found.
    pause
    exit /b 1
)
if not exist "%SRC%" (
    echo Source file is missing.
    pause
    exit /b 1
)
if not exist "%AUTH_SRC%" (
    echo Account source file is missing.
    pause
    exit /b 1
)

powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { $expected=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Project_Alim'; if ([IO.Path]::GetFullPath($env:DIR) -ne [IO.Path]::GetFullPath($expected)) { throw 'Unexpected installation folder.' }; for ($d=New-Object IO.DirectoryInfo($expected); $null -ne $d; $d=$d.Parent) { if ($d.Exists -and ($d.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked installation folders are not supported.' } }; $release=(Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release).Release; if ($release -lt 528040) { throw '.NET Framework 4.8 is required.' } } catch { Write-Host $_.Exception.Message; exit 1 }"
if errorlevel 1 (
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
    echo Automatic removal is pending. If it does not finish, run uninstall.cmd before retrying.
    pause
    exit /b 1
)

powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { $old=Join-Path $env:DIR 'Windows Audio.exe'; Get-Process -Name 'Edge','Windows Audio' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $env:APP -or $_.Path -eq $old } | ForEach-Object { Stop-Process -InputObject $_ -ErrorAction Stop; if (-not $_.WaitForExit(10000)) { throw 'Previous process did not stop.' } }; exit 0 } catch { Write-Host $_.Exception.Message; exit 1 }"
set "PROCESS_CHECK_RESULT=%ERRORLEVEL%"
if not "%PROCESS_CHECK_RESULT%"=="0" (
    echo Previous-process check failed. Installation stopped. Exit code: %PROCESS_CHECK_RESULT%.
    pause
    exit /b 1
)

if exist "%DIR%\.edge-removing" (
    echo Automatic removal is pending. If it does not finish, run uninstall.cmd before retrying.
    pause
    exit /b 1
)

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /define:FRIEND /out:"%APP%" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll "%SRC%" "%AUTH_SRC%"
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)

if exist "%DIR%\.edge-removing" (
    echo Automatic removal is pending. If it does not finish, run uninstall.cmd before retrying.
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

powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { $p=Start-Process -FilePath $env:APP -ArgumentList '--check' -WindowStyle Hidden -PassThru; if (-not $p.WaitForExit(15000)) { $p.Kill(); throw 'Setup check timed out.' }; exit $p.ExitCode } catch { Write-Host $_.Exception.Message; exit 24 }"
set "CHECK_RESULT=%ERRORLEVEL%"
if not "%CHECK_RESULT%"=="0" (
    echo Setup check result: %CHECK_RESULT%.
    pause
    exit /b %CHECK_RESULT%
)

powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { $state=Join-Path $env:DIR '.edge-expiry'; if (Test-Path -LiteralPath $state) { [long]$ticks=0; $file=Get-Item -LiteralPath $state; $valid=$file.Length -le 128 -and [long]::TryParse([IO.File]::ReadAllText($state).Trim(),[ref]$ticks); if (-not $valid -or $ticks -le [DateTime]::UtcNow.Ticks -or $ticks -gt [DateTime]::MaxValue.Ticks) { Remove-Item -LiteralPath $state -Force } } } catch { Write-Host $_.Exception.Message; exit 26 }"
if errorlevel 1 (
    pause
    exit /b 26
)

powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { $p=Start-Process -FilePath $env:APP -ArgumentList '--install' -WindowStyle Hidden -PassThru; if (-not $p.WaitForExit(10000)) { $p.Kill(); throw 'Autostart setup timed out.' }; exit $p.ExitCode } catch { Write-Host $_.Exception.Message; exit 25 }"
if errorlevel 1 (
    echo Autostart registration failed.
)

powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; $ready=$null; $p=$null; try { $name='Local\ProjectAlim_ready_'+[Guid]::NewGuid().ToString('N'); $ready=New-Object Threading.EventWaitHandle($false,[Threading.EventResetMode]::AutoReset,$name); $p=Start-Process -FilePath $env:APP -ArgumentList @('--ready-event',$name) -WindowStyle Hidden -PassThru; $clock=[Diagnostics.Stopwatch]::StartNew(); while (-not $ready.WaitOne(100)) { if ($p.HasExited) { throw ('Edge did not initialize. Exit code: '+$p.ExitCode) }; if ($clock.ElapsedMilliseconds -ge 30000) { $p.Kill(); throw 'Edge initialization timed out.' } } } catch { Write-Host $_.Exception.Message; exit 1 } finally { if ($null -ne $ready) { $ready.Dispose() } }"
if errorlevel 1 (
    echo Launch was not confirmed.
    pause
    exit /b 1
)

exit /b 0
