@echo off
chcp 65001 >nul
set "DIR=%LOCALAPPDATA%\Project_Alim"
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { $expected=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Project_Alim'; if ([IO.Path]::GetFullPath($env:DIR) -ne [IO.Path]::GetFullPath($expected)) { throw 'Unexpected installation folder.' }; for ($d=New-Object IO.DirectoryInfo($expected); $null -ne $d; $d=$d.Parent) { if ($d.Exists -and ($d.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked installation folders are not supported.' } }; $pending=New-Object 'Collections.Generic.Stack[string]'; if (Test-Path -LiteralPath $expected) { $pending.Push($expected) }; while ($pending.Count -gt 0) { foreach ($item in Get-ChildItem -LiteralPath $pending.Pop() -Force) { if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked contents cannot be removed automatically.' }; if ($item.PSIsContainer) { $pending.Push($item.FullName) } } }; $app=Join-Path $expected 'Edge.exe'; $old=Join-Path $expected 'Windows Audio.exe'; Get-Process -Name 'Edge','Windows Audio' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $app -or $_.Path -eq $old } | ForEach-Object { Stop-Process -InputObject $_ -ErrorAction Stop; if (-not $_.WaitForExit(10000)) { throw 'Process did not stop.' } }; $run=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run',$true); if ($null -ne $run) { try { foreach ($name in @('Edge','Windows Audio')) { $value=$run.GetValue($name); if ($value -is [string] -and ($value.Trim([char]34) -eq $app -or $value.Trim([char]34) -eq $old)) { $run.DeleteValue($name,$false) } } } finally { $run.Dispose() } }; if (Test-Path -LiteralPath $expected) { Remove-Item -LiteralPath $expected -Recurse -Force } } catch { Write-Host $_.Exception.Message; exit 1 }"
if errorlevel 1 (
    echo Removal failed.
    pause
    exit /b 1
)
exit /b 0
