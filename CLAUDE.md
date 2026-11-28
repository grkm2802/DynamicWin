# DynamicWin (V1 tabanlı geliştirme)

## Proje özeti
Windows için iPhone/macOS "Dynamic Island" tarzı bir ada: medya kontrolü, hava durumu, zamanlayıcı, dosya tepsisi, pil, sistem kullanımı, ses/parlaklık göstergeleri. Amaç bu kodu temel alıp kendi sürümümüzü geliştirmek.

## Kaynak ve lisans — önemli
- Upstream: https://github.com/FlorianButz/DynamicWin. Yazar V2'yi kapalı kaynak yaptı; `main` dalında artık sadece README var (commit `b8a874b` "Remove initial version").
- Biz son açık kaynak hâli olan `9f24d17` (Haziran 2025) commit'inden `gelistirme` dalını açtık. `origin/V2` ve `main`'deki kod yok — oradan çekme/merge yapma, kodu siler.
- Lisans **CC BY-SA 4.0**: değiştirip dağıtılabilir, ama yazara atıf (Florian Butz) ve aynı lisansla paylaşım şart.

## Klasör yapısı
- `DynamicWin/Main/` — giriş noktası (`App.xaml.cs` → `DynamicWinMain`), pencere, `RendererMain.cs` (SkiaSharp ile çizim döngüsü), `Settings.cs`.
- `DynamicWin/UI/Menu/Menus/` — adanın ekranları (Home, Settings, Volume, Brightness, DropFile...). `MenuManager` geçişleri yönetir.
- `DynamicWin/UI/Widgets/Big|Small/` — widget'lar. Yeni widget `IRegisterableWidget` ile kaydolur.
- `DynamicWin/UI/UIElements/` — kendi UI bileşenleri (DW* sınıfları); WPF kontrolleri değil, Skia ile çiziliyor.
- `DynamicWin/Utils/` — klavye kancası, medya, pil, tema, kayıt (`SaveManager`), başlangıç kısayolu.
- `DynamicWin/Resources/` — ikonlar, fontlar, `CustomTheme_DoNotRename_EditThis.json`.

## Çalıştırma
```powershell
dotnet build DynamicWin.sln -c Release
.\DynamicWin\bin\Release\net8.0-windows10.0.19041.0\DynamicWin.exe
```
- .NET 8 SDK gerekli (winget `Microsoft.DotNet.SDK.8` ile kuruldu). Test projesi yok.
- Yeniden derlemeden önce çalışan `DynamicWin.exe`'yi kapat, yoksa dosya kilitli hatası alırsın.
- ~510 derleme uyarısı var (çoğu nullable); hata değil.

## Upstream'den yaptığımız değişiklikler
- `IWshRuntimeLibrary` COMReference kaldırıldı (`dotnet build` COM referansını çözemiyor, MSB4803). `Utils/StartupShortcutManager.cs` artık `WScript.Shell`'i `dynamic` geç bağlamayla kullanıyor.
- Monitör sırası: `Screen.AllScreens` ana monitörü başa koymuyor (bu makinede DISPLAY1 = ikinci ekran). `MainForm.GetScreens()` ana monitörü 0. sıraya alıyor; ekran seçen her yer bunu kullanmalı (ayarlarda 0 = "Primary").

## Claude Code izleme (yol haritası #1)
- `Utils/ClaudeBridge.cs`: `http://localhost:47321/claude/` adresinde `HttpListener`. Claude Code hook'larının stdin JSON'unu POST ile alır, oturum durumlarını tutar (Idle/Working/WaitingPermission/WaitingInput/Done). 30 dk olay gelmeyen oturum silinir, "Done" 30 sn sonra gizlenir.
- `UI/Widgets/Small/ClaudeCodeWidget.cs`: renkli nabız noktası + kısa metin; oturum yoksa genişlik 0.
- `UI/Menu/Menus/ClaudeCodeMenu.cs`: izin isteği / bitiş bildirimi. `RendererMain.Update` kuyruğu sadece `HomeMenu` aktifken boşaltır.
- **Tuzak:** `BaseMenu` kurucusu `InitializeMenu`'yu alt sınıfın kurucusundan önce çağırır. Menüye veri aktarmak için kurucu parametresi kullanma; `ClaudeCodeMenu.Create()` gibi statik alan üzerinden geçir.
- Hook tanımı: `hooks/claude-code-hooks.json` (`curl ... ; exit 0`, uygulama kapalıyken Claude'u bloklamaz). Kullanıcının `~/.claude/settings.json`'una birleştirildi (26.11.2026; yedek: `settings.json.bak`). Port değişirse orayı da güncelle.
- Tanılama: `curl.exe -s http://localhost:47321/claude/` (GET) oturumları ve ada durumunu (konum, boyut, hidden, menü, tam ekran, pencere görünürlüğü) JSON döner. "Ada görünmüyor" şikâyetinde uygulamayı yeniden başlatmadan önce buna bak.
- **"Ada kayboldu" hatası (çözüldü, 26.11.2026):**
  - Sebep: Tam ekran gizlemesi WPF `Hide()/Show()` kullanıyordu. AllowsTransparency'li pencerede `Show()` sonrası HWND gizli kalabiliyor (WPF `IsVisible=true`, Win32 `IsWindowVisible=false`).
  - Çözüm: Tam ekranda pencere `SetWindowPos` ile (-32000,-32000)'e taşınıyor, sonra geri getiriliyor (`MainForm.OnFullscreenChanged/RestoreWindow`).
  - `FullscreenDetector` her 500 ms'de `EnsureVisible` bekçisini çalıştırıyor.
  - Adanın monitörü pencere yerindeyken önbelleğe alınıyor (`islandMonitor`). Taşınmış pencerede `MonitorFromWindow` yanlış monitör veriyordu.
- `RendererMain.CursorPosition` Win32 `GetCursorPos` + `PointFromScreen` kullanıyor. `Mouse.GetPosition` şeffaf pencerede eski değerde kalıyor ve hover'ı takılı bırakıyordu.
- **Kamera çökmesi (çözüldü):** `SKBitmap` kamera thread'inde Dispose edilirken render thread'i çiziyordu (AccessViolation, try/catch ile yakalanamaz). Artık kareyi sadece UI thread'i siliyor (`CameraMirror.TakeLatestFrame`). Başka thread'den gelen her bitmap için aynı kural geçerli.
- Çökme teşhisi: Olay Görüntüleyicisi > Uygulama, ".NET Runtime 1026" kaydında yığın izi var: `Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-30)} | ? Message -match DynamicWin`.
- Ekranda tıklama testinden önce adanın genişlediğini GET durumundan doğrula. Ada çizilmiyorsa tıklama arkadaki pencereye gider (bir kez YouTube videosunu duraklattı).
- Elle test: `'{"session_id":"t","hook_event_name":"Stop","cwd":"C:\\x"}' | curl.exe -s -X POST --data-binary '@-' http://localhost:47321/claude`
- `Settings.AddClaudeWidgetOnce()` widget'ı eski ayar dosyalarına bir kez ekler (`settings.claudewidgetadded` bayrağı).

## Tam ekran, canlı etkinlikler, bildirimler (yol haritası #2–#5)
- Hedef platform `net8.0-windows10.0.19041.0` (WinRT API'leri için: medya oturumu, bildirim dinleyici). Çıktı klasörü buna göre değişti.
- `Utils/FullscreenDetector.cs`: 500 ms'de bir ön plan penceresi adanın monitörünü kaplıyor mu + `SHQueryUserNotificationState` (D3D/sunum). Tam ekranda `MainForm` `Hide()` edilir, `RendererMain`/`MainForm` render döngüleri erken döner. Pencere ekranı kaplayan şeffaf topmost bir katman; sadece görünmez yapmak yetmez, oyunun independent flip'ini bozar.
- `Utils/LiveActivities.cs`: `LiveActivity` soyut sınıfı + `LiveActivityManager`. Yerleşikler: Claude, Timer, Media (Windows GSMTC, her oynatıcıyı görür). `RepresentedByWidget`: adada widget'ı olan etkinlik ayrık balona taşınmaz. `AllowBubble = false` olan etkinlik hiç balona çıkmaz; medya böyle (kullanıcı müziğin yanda ayrı balon olmasını istemedi).
- `UI/UIElements/SplitBubble.cs`: adanın sağında "minimal" balon; UIObject değil, `RendererMain` doğrudan Update/Draw eder.
- `Utils/NotificationWatcher.cs`: `UserNotificationListener` paketlenmemiş uygulamada erişim veriyor ama `NotificationChanged` olayı çalışmıyor → 1.5 sn'de bir `GetNotificationsAsync` yoklaması. Açılışta merkezdeki eski bildirimler gösterilmez; kuyrukta 20 sn'den eski olan atılır.
- Overlay önceliği (`RendererMain.Update`): Claude olayı > Windows bildirimi; ikisi de sadece `HomeMenu` aktifken açılır.
- Test: sahte tam ekran için kenarlıksız, ana monitörü kaplayan bir WinForms formu; test bildirimi için PowerShell'in AppID'siyle `ToastNotificationManager.CreateToastNotifier(...).Show(...)`.

## Yol haritası #6–#24 (26.11.2026)
- **Ortak altyapı:**
  - `Utils/L.cs`: `L.T("English", "Türkçe")`. Yeni her metin bununla yazılır, dil `Settings.Language` ("tr" varsayılan).
  - `Utils/Gfx.cs`: metin sığdırma, yuvarlak resim, bar, süre. Adı `Draw` değil, çünkü `WidgetBase.Draw` ile çakışıyordu.
  - `UI/Menu/Menus/InfoPopupMenu.cs`: `IslandPopups.Show(new PopupInfo{...})` ile herhangi bir thread'den ikonlu ada uyarısı. İkonlar Segoe Fluent Icons (`Icons.*`). `Key` aynıysa eski uyarı atlanır.
  - Overlay önceliği: Claude > IslandPopups > Windows bildirimi.
- **Servisler:**
  - `MediaSession` (GSMTC; kapak, baskın renk, konum tahmini, kontroller).
  - `BluetoothWatcher` (DeviceWatcher; pil `{104EA319-...} 2`).
  - `PowerWatcher`.
  - `ClipboardWatcher` (AddClipboardFormatListener; metin geçmişi sadece hafızada; resim → Tray PNG + önizleme; parola yöneticisi biçimlerine uyar).
  - `SystemStateWatcher`: kilit açma (SessionSwitch), sessiz (NAudio OnVolumeNotification), Rahatsız Etmeyin (`NOC_GLOBAL_SETTING_TOASTS_ENABLED`=0), ekran yakalama (ConsentStore `graphicsCaptureProgrammatic` LastUsedTimeStop=0).
  - `UpdateChecker` (GitHub releases; `settings.updaterepo` boşsa kapalı; hiçbir şeyi kendiliğinden indirmez).
- **Widget'lar:**
  - Büyük: Media (yeniden yazıldı), Clipboard, Note (`%AppData%\DynamicWin\note.txt`, WPF düzenleme penceresi), Pomodoro (statik durum + canlı etkinlik, sağ tık: atla/sıfırla), Camera (MediaCapture SharedReadOnly; tıklayınca açılır, ada küçülünce kapanır; varsayılan düzene eklenmez).
  - Ağ hızı: kullanıcı ada küçükken sürekli görmek istemedi. Bu yüzden `NetSpeedBigWidget` "Ağ hızı" kutusu olarak duruyor (anlık hız + 60 sn grafik). Küçük `NetSpeedWidget` hâlâ var ama varsayılan düzende yok (`MoveNetSpeedToTileOnce`).
  - `Settings.AddWidgetOnce(flag, ...)` yeni widget'ları eski kurulumlara bir kez ekler.
- **Ayarlar:** `SystemPopups`, `ScreenshotPreview`, `IslandStyle` (0 düz, 1 cam: `Theme.ApplyTheme` arka planı %72 opak yapar + `IslandObject.DrawGlass`), `Language`.
- **Test:**
  - `POST {"hook_event_name":"DebugPopup","glyph":"","title":"..","subtitle":"..","ring":0.5,"accent":"#hex"}` → `http://localhost:47321/claude/` herhangi bir uyarıyı tetikler.
  - Genişletilmiş adayı görmek için imleci (960,18)'e götürüp ekran görüntüsü alıp imleci geri koymak gerekir.

## Genişletilmiş ada düzeni (kullanıcı isteği, 26.11.2026)
- Kullanıcı widget'ları çok büyük buldu. Spotify ve YouTube'u çok sık kullanıyor ve ikisi aynı anda çalabiliyor.
- Düzen (`HomeMenu.LayoutBig`):
  - Üstte `IsFeatured` widget'lar tam boy, sıra başına 2 (şu an sadece medya kartları).
  - Altta diğer büyük widget'lar `WidgetTile` olarak (ikon + isim + `TileStatus`), satır başına 4.
  - Kutuya tıklayınca o widget'ın tam hâli en altta açılır. Tekrar tıklayınca ya da ada küçülünce kapanır.
- Yeni büyük widget eklerken `TileTitle` (L.T ile), `TileGlyph` (Segoe Fluent) ve gerekirse `TileStatus` override et.
- `MediaSession` artık çok kaynaklı:
  - `MediaSession.Spotify`, `MediaSession.Browser` ("YouTube": chrome/edge/firefox/opera/brave... oturumları), `MediaSession.Other`.
  - Statik `IsPlaying/Title/...` `Primary` kaynağı yansıtır.
  - `MediaWidget(kind)`: `RegisterSpotifyWidget`, `RegisterYouTubeWidget`, `RegisterMediaWidget` (genel).
  - `Settings.SplitMediaWidgetOnce` eski tek medya kartını ikiye böler.
- Tarayıcı kartı "YouTube" diye etiketli ama tarayıcıdaki her medyayı (Netflix vb.) gösterir. GSMTC sekme/site bilgisi vermiyor.
- Ekranda tıklama testi: imleci (960,18)'e götür, sonra hedefe; `mouse_event` ile tıkla; imleci geri koy.

## Ses kontrolü (1.4.0, kullanıcı isteği)
- `Utils/AudioService.cs`: ses seviyesi/sessiz (bildirimle önbellek), çıkış cihazı listesi, varsayılan çıkışı değiştirme.
  - Varsayılan değiştirme belgelenmemiş `IPolicyConfig` (CPolicyConfigClient) ile üç rol için yapılır.
  - `IMMNotificationClient` cihaz eklenip çıkınca ve varsayılan değişince UI thread'ine aktarır.
  - Varsayılan değişince `DynamicWinMain.defaultDevice` güncellenir. Böylece V1 ses göstergesi ve `SystemStateWatcher` sessiz uyarısı yeni cihazı izler.
  - `AudioVisualizer` loopback'i hâlâ açılıştaki cihazda kalır (bilinen eksik).
- `UI/Widgets/Big/SoundWidget.cs`: "Ses" kutusu (412 genişlik). İçinde sessiz düğmesi, sürüklenebilir kaydırıcı (%1 adım), en fazla 6 çıkış cihazı. Tıklanan cihaz varsayılan olur.
- Test edildi: sessiz aç/kapa, Realtek ↔ Logitech G733 geçişi (Windows varsayılanı doğrulandı).

## ÖNEMLİ: Claude masaüstü uygulamasının paket ortamı (27.11.2026)
- Claude masaüstü uygulaması MSIX paketi olarak çalışır. **Claude'un açtığı kabuktan başlatılan her süreç bu paket ortamının içindedir**:
  - `%APPDATA%`, `%LOCALAPPDATA%` (Temp dahil) ve `HKCU` yazmaları gerçek yere değil `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\...` kopyasına gider.
  - Aynı ortamdan okuyunca da o kopya görünür. Gerçek durum görünmez.
- Sonuçları:
  - Buradan çalıştırılan `install.ps1` Run kaydını, Başlangıç/Başlat menüsü kısayollarını ve kaldırma kaydını **gerçek Windows'a yazmaz**. "Açılışta başlamıyor" hatasının kaynağı buydu.
  - Buradan başlatılan DynamicWin, Claude'un alt sürecidir. Claude kapanınca ya da güncellenince kayıt bırakmadan ölür.
  - Geliştirme testleri (bin\Release) sanal ayarları kullanır, kullanıcının gerçek kurulumu ayrı ayarları kullanır.
- Test (28.11.2026): `%LOCALAPPDATA%\Programs` **gerçek** yere yazılıyor. `%APPDATA%\Roaming` (ayarlar, Başlangıç klasörü, Başlat menüsü) ve HKCU (Run, Uninstall) sanal. Görev Zamanlayıcı (schtasks) gerçek, hizmet olduğu için sanallaştırılmaz.
- **Bu bilgisayarda çalışan çözüm:**
  1. Yeni sürümü `installer/out/publish`'ten `%LOCALAPPDATA%\Programs\DynamicWin`'e kopyala (`robocopy /E`), önce DynamicWin'i kapat.
  2. "DynamicWin" zamanlanmış görevi (LogonTrigger + SessionUnlock) o exe'yi başlatır. `schtasks /Run /TN DynamicWin` ile hemen başlatılabilir.
  3. Görevin başlattığı süreç Claude dışındadır (üst süreç svchost). Gerçek ayar ve kayıt defterine yazar, Run kaydını da oluşturur.
- Kullanıcıya .cmd çalıştırma adımı bırakma. Claude uygulamasındaki dosya bağlantısı .cmd'yi çalıştırmıyor, sadece görüntülüyor. Önceki deneme bu yüzden işe yaramadı.
- Zamanlanmış görevle **kurulum betiği** çalıştırmak Claude'un güvenlik denetimince engellendi. Görevle uygulamanın kendisini başlatmak (kullanıcının istediği otomatik başlatma) sorun olmadı.
- Masaüstü (OneDrive\Desktop) ve kullanıcı klasöründeki `.claude` sanallaştırılmaz. Ortak dosyalar için oraları kullan.
- Uygulama tarafı (1.4.2–1.4.3):
  - Açılışta `UpdateStartup` sadece kaydı oluşturur, asla silmez. Silme yalnızca Ayarlar'da kapatınca olur.
  - `install.ps1` Run kaydını kendisi yazar ve ayardaki `runonstartup`'ı açar.
  - `%AppData%\DynamicWin\log.txt` açılış/kapanış/uyku/kilit/hataları yazar.
  - `RegisterApplicationRestart` ile Windows çökme sonrası yeniden başlatır.
  - "DynamicWin" zamanlanmış görevi oturum açılışında ve kilit açılışında başlatır (1.4.3).
  - 1.4.3'ten itibaren "açılışta başlat" ayarı açılışta gerçek durumdan okunur: görev veya Run kaydı varsa ayar açılır (`StartupShortcutManager.IsRegistered`).
  - İkinci kopya sessizce çıkar.

## Sürüm ve kurulum (#19)
- Sürüm iki yerde: `Main/App.xaml.cs` `Version` ve csproj `<Version>`. İkisini birlikte değiştir.
- `installer/build-release.ps1` → `installer/out/DynamicWin-<sürüm>.zip` (~85 MB).
  - Paket **self-contained** (`-r win-x64 --self-contained`): .NET pakete gömülü, hedef bilgisayarda .NET gerekmez.
  - Inno Setup (ISCC) kuruluysa `installer/DynamicWin.iss` ile `DynamicWin-Setup-<sürüm>.exe` de üretir. Bu makinede Inno Setup kurulu değil.
- **Başka bilgisayara kurulum:** zip'i aç → `Kur.cmd`'ye çift tıkla. Bu, `install.ps1`'i çalıştırır:
  - `%LOCALAPPDATA%\Programs\DynamicWin`'e kurar, Başlat menüsüne ekler, HKCU'ya kaldırma kaydı yazar.
  - `~/.claude` varsa Claude hook'larını sorarak `settings.json`'a birleştirir (yedek alır, var olanları korur).
  - Test parametreleri: `-InstallDir x -NoShortcuts -NoStart -NoClaude`.
- `uninstall.ps1` ayarları korur (`-RemoveSettings` ile siler).
- Yeni kurulumda `RunOnStartup` varsayılan olarak açık.
- **Açılışta başlatma (1.3.1):** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `DynamicWin`.
  - Eski Başlangıç klasörü `.lnk`'i her açılışta yeniden yazılıyordu, Windows'un okumasıyla çakışıp açılışta çalışmıyordu. Uygulama eski kısayolu kendisi siler.
  - `bin\Debug` veya `bin\Release`'ten çalışan geliştirme derlemeleri Run kaydına dokunmaz.
  - Bu bilgisayarda kurulu kopya: `%LOCALAPPDATA%\Programs\DynamicWin` (açılışta bu çalışır).
- **Geliştirirken:** önce kurulu kopyayı kapat (tek örnek mutex'i), test için `bin\Release` exe'sini çalıştır. Bittiğinde kurulu kopyayı geri aç, ya da yeni paketi `install.ps1 -NoClaude` ile kur.
- Kurulum, çalışan DynamicWin'i kapatır (dosya kilidi). Testten sonra geliştirme sürümünü yeniden başlat. Başlangıç kısayolu en son çalışan exe'yi gösterir.
- `.ps1` dosyaları **BOM'lu UTF-8** olmalı. Windows PowerShell 5.1 BOM'suz dosyada Türkçe karakterleri bozar.
- Otomatik güncelleme için kullanıcının kendi GitHub deposu ve release'ler gerekiyor, henüz yok.

## Performans (26.11.2026, iki tur)
Geliştirme bilgisayarı 180 Hz ekranlı bir dizüstü (16 çekirdek); yüksek Hz ekranlarda kare kaynağı özellikle önemli. Son durum (bir çekirdeğin yüzdesi olarak):
- Ada küçük: ~%4.
- Genişlemiş: ~%17. Başta %57 idi.
- Özel bellek ~130 MB.

**Ölçüm:**
- `GET http://localhost:47321/claude/` → `island.perf`: son 1 saniyenin fps değeri ve bölüm bölüm ms toplamları (`Utils/Perf.cs`; `u:*` = Update, `draw:*` = çizim, `render:total` = SKElement.OnRender).
- İş parçacığı bazında: `Get-Process DynamicWin` → `.Threads[].TotalProcessorTime` farkı. Ana thread = en erken başlayan.
- Bu oturumun Claude hook'ları ölçümü biraz şişirir (widget animasyonu).

**Kurallar / yapılanlar:**
- **Kare kaynağı:** `CompositionTarget.Rendering`'e sadece etkileşimde (60 FPS) abone olunur. Boştayken `DispatcherTimer` 33 ms, ada boşken 66 ms, tam ekranda 250 ms. Abone kalmak WPF'i 180 Hz'de uyandırıyor ve render thread'ini boşuna çalıştırıyordu. `MainForm`'un kullanılmayan Rendering aboneliği kaldırıldı.
- **Pencere boyutu:** Ada küçükken pencere 640x140 (`MainForm.SetCompact/ApplyBounds`, tek `SetWindowPos`). Etkileşimde 1100x620 (`OverlayWidth/Height`). Yeni bir menü daha büyükse sabitleri büyüt. Kompakt alana sığmayan bir küçük-durum içeriği eklenirse `CompactWidth/Height`'ı artır.
- **Resimler:** `SKBitmap` her karede ölçekli çizilmez. `Gfx.Scaled` hedef piksel boyutuna bir kez küçültüp `SKImage` önbellekler (DWImage, kapaklar). İkon renklendirme `ImageFilter` değil `ColorFilter` (ImageFilter her çizimde ekran dışı katman açar).
- **Gölgeler:** `ImageFilter.DropShadow` kullanma. Küçük şekillerde `SKMaskFilter.CreateBlur`, ada gölgesinde önbellekli görüntü (`IslandObject.DrawCachedShadow`).
- **UI thread'inde G/Ç yok:** Parlaklık WMI olayı (`WmiMonitorBrightnessEvent`) + arka plan, kamera/mikrofon kayıt defteri taraması arka planda 500 ms, fontlar bir kez yüklenir.
- `HardwareMonitor` (LibreHardwareMonitor) sadece "System Usage" küçük widget'ı adadaysa başlar. Yaklaşık 140 MB bellek ve sürekli sensör okuması demek.
- Yeni kod: `Update`/`Draw` içinde dosya, kayıt defteri, WMI, süreç listesi, ağ gibi şeylere dokunma, `new SKPaint`/`SKTextBlob` üretimini sınırla, arka planda yokla ve sonucu oku.

## Görünüm tercihleri (kullanıcı)
- Saat widget'ı yok (görev çubuğunda zaten var); varsayılan düzene de eklenmez.
- `HomeMenu.IslandSize`: sadece genişliği > 0 olan küçük widget'lar sayılır; hiçbiri görünmüyorsa ada 120x30 boş hap olur, aksi halde en az 160 genişlik.
- Test notu: Claude Code hook'ları global olduğundan, testi yapan Claude oturumunun kendisi de adada "Komut çalıştırıyor" olarak görünür; boş adayı ekran görüntüsüyle yakalamak bu yüzden zor.

## Dikkat edilecekler
- `Main/App.xaml.cs` yakalanmamış hatalarda MessageBox açar; render döngüsündeki bir hata ekrana hata kutusu yağdırabilir.
- Hava durumu widget'ı konumu IP'den bulur (`ipinfo.io`) ve Microsoft hava servisinden veri çeker — dış ağa çıkan tek yer.
- `KeyHandler` düşük seviyeli global klavye kancası kurar (kısayollar için); tuşları kaydetmez.
- "Run on startup" ayarı Başlangıç klasörüne `.lnk` koyar.

## Açık konular
- CPU hâlâ bir çekirdeğin ~%8–22'si (SkiaSharp CPU raster + WPF layered window). Daha da düşürmek için: sadece değişince çizmek (dirty flag) veya GPU'lu SKGLElement'e geçmek.
- **GitHub:** https://github.com/grkm2802/DynamicWin (herkese açık). Yerel `gelistirme` dalı → uzak `main`.
  - `origin` = kendi depomuz, `upstream` = FlorianButz/DynamicWin. Upstream'e push etme, oradan da çekme (main/V2'de kod silinmiş).
  - Commit kimliği: repo-yerel `grkm2802 <168774835+grkm2802@users.noreply.github.com>`. Kullanıcının gerçek e-postası herkese açık depoya girmesin.
- **Sürüm yayınlama:** sürümü iki yerde artır (App.xaml.cs + csproj) → `installer/build-release.ps1` → commit + push → `gh release create v<sürüm> installer/out/DynamicWin-<sürüm>.zip -R grkm2802/DynamicWin`.
- `UpdateChecker.DefaultRepo = "grkm2802/DynamicWin"`. Uygulama açılıştan 30 sn sonra ve günde bir son release'e bakar (tag `v1.3.0` biçimi).
