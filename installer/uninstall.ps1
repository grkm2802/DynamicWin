# DynamicWin'i kaldırır. Ayarlar (%APPDATA%\DynamicWin) korunur; silmek için -RemoveSettings.
param([switch]$RemoveSettings)
$dir = $PSScriptRoot

Get-Process DynamicWin -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'DynamicWin.lnk') -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Startup')) 'DynamicWin.exe.lnk') -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DynamicWin' -Recurse -ErrorAction SilentlyContinue
if ($RemoveSettings) { Remove-Item (Join-Path $env:APPDATA 'DynamicWin') -Recurse -Force -ErrorAction SilentlyContinue }

# Çalışan betik kendi klasörünü silemez; ayrı bir süreçte birkaç saniye sonra sil.
Start-Process powershell -WindowStyle Hidden -ArgumentList '-NoProfile', '-Command', "Start-Sleep 2; Remove-Item -LiteralPath '$dir' -Recurse -Force"
Write-Host 'DynamicWin kaldırıldı.'
