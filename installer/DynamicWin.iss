; DynamicWin kurulum programı (Inno Setup 6). build-release.ps1 tarafından şöyle derlenir:
;   ISCC.exe /DMyAppVersion=1.2.0 /DPublishDir=<publish klasörü> /O<çıktı> DynamicWin.iss
; Yönetici izni istemez; kullanıcının kendi Programs klasörüne kurar.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "out\publish"
#endif

#define MyAppName "DynamicWin"
#define MyAppExeName "DynamicWin.exe"

[Setup]
; Bu fork'a ait yeni AppId (orijinal DynamicWin kurulumuyla çakışmasın)
AppId={{5B7E2C64-8F1D-4C2A-9E3B-6A0D7F41C2E9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=DynamicWin (Florian Butz V1 tabanlı)
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputBaseFilename=DynamicWin-Setup-{#MyAppVersion}
SetupIconFile=..\Other\InstallerIcon\SetupIcon.ico
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "install.ps1,uninstall.ps1,Kur.cmd,claude-code-hooks.json"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; Açılışta başlatma kısayolunu uygulamanın kendisi yönetir (Ayarlar > Oturum açılınca başlat).

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/IM {#MyAppExeName} /F"; Flags: runhidden; RunOnceId: "KillApp"
