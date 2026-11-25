# DynamicWin sürüm paketi oluşturur:
#   1) dotnet publish  -> installer\out\publish\
#   2) zip             -> installer\out\DynamicWin-<sürüm>.zip  (içinde install.ps1 / uninstall.ps1)
#   3) Inno Setup (ISCC.exe) kuruluysa -> installer\out\DynamicWin-Setup-<sürüm>.exe
# Kullanım:  powershell -ExecutionPolicy Bypass -File installer\build-release.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $PSScriptRoot 'out'
$pub = Join-Path $out 'publish'

# Sürümü koddan oku (Main/App.xaml.cs -> Version => "x.y.z")
$app = Get-Content (Join-Path $root 'DynamicWin\Main\App.xaml.cs') -Raw
$version = [regex]::Match($app, 'Version\s*=>\s*"([^"]+)"').Groups[1].Value
if (-not $version) { throw 'Sürüm App.xaml.cs içinde bulunamadı.' }
Write-Host "Sürüm: $version"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $pub | Out-Null

# Self-contained: .NET çalışma zamanı pakete gömülür, hedef bilgisayarda .NET kurulu olması gerekmez.
& dotnet publish (Join-Path $root 'DynamicWin\DynamicWin.csproj') -c Release -r win-x64 --self-contained true -o $pub -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish başarısız.' }

Copy-Item (Join-Path $PSScriptRoot 'install.ps1'), (Join-Path $PSScriptRoot 'uninstall.ps1'), (Join-Path $PSScriptRoot 'Kur.cmd') $pub
Copy-Item (Join-Path $root 'hooks\claude-code-hooks.json') $pub
Copy-Item (Join-Path $root 'LICENSE') $pub -ErrorAction SilentlyContinue

$zip = Join-Path $out "DynamicWin-$version.zip"
Compress-Archive -Path (Join-Path $pub '*') -DestinationPath $zip
Write-Host "Zip: $zip"

$iscc = @("$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) {
    & $iscc "/DMyAppVersion=$version" "/DPublishDir=$pub" "/O$out" (Join-Path $PSScriptRoot 'DynamicWin.iss') | Out-Host
    Write-Host "Kurulum programı: $out\DynamicWin-Setup-$version.exe"
} else {
    Write-Host 'Inno Setup bulunamadı; sadece zip üretildi. (winget install JRSoftware.InnoSetup ile kurulabilir)'
}
