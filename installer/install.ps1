# DynamicWin'i yönetici izni olmadan, sadece bu kullanıcı için kurar.
#   - Dosyalar: %LOCALAPPDATA%\Programs\DynamicWin
#   - Başlat menüsü kısayolu
#   - Ayarlar > Uygulamalar listesinde kaldırma kaydı (HKCU)
#   - (Sorarak) Claude Code yüklüyse ada bağlantısı için hook'lar
# Zip'i açtığınız klasörde Kur.cmd'ye çift tıklayın.
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\DynamicWin'),
    [switch]$NoShortcuts,   # test için: kısayol ve kayıt oluşturma
    [switch]$NoStart,
    [switch]$NoClaude       # test için: Claude sorusunu atla
)
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot

if (-not (Test-Path (Join-Path $src 'DynamicWin.exe'))) { throw "DynamicWin.exe bu klasörde yok: $src" }

# Paket .NET'i içeriyorsa (self-contained) kontrol gerekmez; içermiyorsa .NET 8 Masaüstü çalışma zamanı lazım.
if (-not (Test-Path (Join-Path $src 'coreclr.dll'))) {
    $runtimes = & dotnet --list-runtimes 2>$null
    if (-not ($runtimes -match 'Microsoft\.WindowsDesktop\.App 8\.')) {
        Write-Warning '.NET 8 Masaüstü Çalışma Zamanı bulunamadı. Kurmak için: winget install Microsoft.DotNet.DesktopRuntime.8'
    }
}
Write-Host "DynamicWin kuruluyor..."

# Çalışıyorsa kapat (dosyalar kilitli olmasın)
Get-Process DynamicWin -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

New-Item -ItemType Directory -Force $InstallDir | Out-Null
Copy-Item (Join-Path $src '*') $InstallDir -Recurse -Force
$exe = Join-Path $InstallDir 'DynamicWin.exe'

if (-not $NoShortcuts) {
    $lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'DynamicWin.lnk'
    $sh = New-Object -ComObject WScript.Shell
    $s = $sh.CreateShortcut($lnk); $s.TargetPath = $exe; $s.WorkingDirectory = $InstallDir; $s.Save()

    $ver = (Get-Item $exe).VersionInfo.ProductVersion
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DynamicWin'
    New-Item -Force $key | Out-Null
    Set-ItemProperty $key DisplayName 'DynamicWin'
    Set-ItemProperty $key DisplayVersion $ver
    Set-ItemProperty $key Publisher 'DynamicWin (Florian Butz V1 tabanlı)'
    Set-ItemProperty $key DisplayIcon $exe
    Set-ItemProperty $key InstallLocation $InstallDir
    Set-ItemProperty $key UninstallString "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$InstallDir\uninstall.ps1`""
    Set-ItemProperty $key NoModify 1 -Type DWord
    Set-ItemProperty $key NoRepair 1 -Type DWord
}

Write-Host "DynamicWin kuruldu: $InstallDir"

# ---- Claude Code bağlantısı (isteğe bağlı) ----
# Claude Code bu bilgisayarda kullanılıyorsa (~/.claude var), adanın Claude'un ne yaptığını göstermesi için
# ~/.claude/settings.json'a hook'lar eklenir. Kullanıcıya sorulur; dosyanın yedeği alınır.
$claudeDir = Join-Path $env:USERPROFILE '.claude'
$template = Join-Path $src 'claude-code-hooks.json'
if (-not $NoClaude -and (Test-Path $claudeDir) -and (Test-Path $template)) {
    $answer = Read-Host "Claude Code bulundu. Ada, Claude'un ne yaptığını göstersin mi? (E/H)"
    if ($answer -match '^[EeYy]') {
        $settingsPath = Join-Path $claudeDir 'settings.json'
        $settings = if (Test-Path $settingsPath) { Get-Content $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json } else { New-Object psobject }
        if (Test-Path $settingsPath) { Copy-Item $settingsPath "$settingsPath.bak" -Force }
        if (-not $settings.PSObject.Properties['hooks']) { $settings | Add-Member -NotePropertyName hooks -NotePropertyValue (New-Object psobject) }
        $newHooks = (Get-Content $template -Raw -Encoding UTF8 | ConvertFrom-Json).hooks
        foreach ($ev in $newHooks.PSObject.Properties) {
            $existing = $settings.hooks.PSObject.Properties[$ev.Name]
            $list = @(); if ($existing) { $list = @($existing.Value) }
            $already = ($list | ConvertTo-Json -Depth 20) -match '47321'
            if (-not $already) { $list += @($ev.Value) }
            if ($existing) { $settings.hooks.($ev.Name) = $list } else { $settings.hooks | Add-Member -NotePropertyName $ev.Name -NotePropertyValue $list }
        }
        $json = $settings | ConvertTo-Json -Depth 20
        [System.IO.File]::WriteAllText($settingsPath, $json, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "Claude Code bağlantısı eklendi (yedek: settings.json.bak). Yeni açılan Claude oturumlarında görünür."
    }
}
if (-not $NoStart) { Start-Process $exe -WorkingDirectory $InstallDir }
