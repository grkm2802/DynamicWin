# DynamicWin Yol Haritası

Bu dosyada güncel dinamik ada uygulamalarındaki özellikler yer alıyor: iPhone Dynamic Island / Live Activities, macOS'taki NotchNook, Alcove ve boring.notch, Windows'taki DynamicWin V2, BrimDeck, fly-island ve Kiro. Özellikler eklenecekleri sıraya göre dizildi. Her özellik bitince durum sütununu güncelliyoruz.

Durum: ✅ var (V1'de mevcut) · 🟡 kısmen var · ⬜ yapılacak · 🔨 üzerinde çalışılıyor

## Sıradaki özellikler

| # | Özellik | Ne yapar | Örnek uygulama | Durum |
|---|---|---|---|---|
| 1 | **Claude Code izleme** | Claude Code'un ne yaptığını gösterir (çalışıyor, izin bekliyor, bitti), izin isteği ve bitiş anında bildirim açar | fly-island, BrimDeck | ✅ |
| 1b | Adadan Claude'a soru sorma | Adaya yazılan soruyu Anthropic API'ye gönderir (API anahtarı gerekir) | Kiro | ⬜ |
| 2 | Tam ekranda gizlenme | Oyun veya video tam ekrana geçince ada gizlenir ve FPS'e dokunmaz | Kiro, DynamicWin V2 | ✅ |
| 3 | Kompakt / genişletilmiş / minimal durumlar | Apple'daki gibi üç boyut: üzerine gelince büyür, iki etkinlik varsa ada ikiye bölünür | iOS Dynamic Island | ✅ (bölünmüş balon; kaydırarak gizleme yok) |
| 4 | Canlı Etkinlik (Live Activity) altyapısı | Zamanlayıcı, indirme, Claude, medya gibi etkinlikleri sıraya koyan ortak sistem | iOS, DynamicWin V2 | ✅ |
| 5 | Windows bildirimlerini adada gösterme | Gelen bildirim adadan "düşer" (tıklayınca uygulamayı açma henüz yok: API izin vermiyor) | NotchNook, Alcove | ✅ |
| 6 | Medya: albüm kapağı rengiyle parlama + ilerleme çubuğu | Kapak rengine göre kenar parlaması, sürüklenebilir zaman çubuğu | boring.notch, Ostrov | ✅ (her oynatıcı, kapak, ilerleme çubuğu, kenar parlaması) |
| 7 | Şarkı sözleri | Çalan şarkının sözlerini senkron gösterir (LRCLIB) | Atoll, MyNotch | ⬜ |
| 8 | Takvim ve yaklaşan etkinlik | Outlook/Google takvimi, toplantıya 5 dakika kala uyarı | NotchNook, DynamicWin V2 | ⬜ |
| 9 | Bluetooth cihaz bağlantısı ve pil durumu | Kulaklık bağlanınca animasyon ve pil yüzdesi | iOS, DynamicWin V2 | ✅ (pil: cihaz bildiriyorsa) |
| 10 | Şarj animasyonu | Şarj kablosu takılınca pil animasyonu | iOS, Alcove | ✅ (+ %20/%10 düşük pil uyarısı) |
| 11 | Pano geçmişi | Son kopyalananları listeler, tıklayınca yapıştırır | Ostrov, NotchNook | ✅ (son 25 metin, sadece hafızada) |
| 12 | Ekran görüntüsü önizleme | Win+Shift+S sonrası küçük resim, adaya sürükleyerek paylaşma | Atoll | ✅ (Tray'e PNG kaydeder) |
| 13 | Hızlı not | Adada tek tıkla açılan not defteri | NotchNook, wjjkld | ✅ |
| 14 | Odak / Pomodoro zamanlayıcısı | Çalışma ve mola döngüsü, kompakt gösterge | d4kw1n, waktu-shalat | ✅ (25/5, 4'te bir 15 dk) |
| 15 | Ağ hızı göstergesi | İndirme/yükleme hızını küçük widget olarak gösterir | NetSpeed-Dynamic | ✅ (sadece trafik > 100 KB/s iken görünür) |
| 16 | Kamera aynası | Toplantı öncesi adada kamera önizlemesi | NotchNook | ✅ (tıklayınca açılır) |
| 17 | Görünüm: buzlu cam / sıvı cam temaları | Mica ve Acrylic arka plan, yumuşak gölge | Kiro, DynamicWin V2 | ✅ (yarı saydam cam; gerçek bulanıklık yok) |
| 18 | Türkçe arayüz | Tüm metinleri çevrilebilir hale getirmek | — | ✅ (Ayarlar > Dil) |
| 19 | Kurulum programı + otomatik güncelleme | Tek tıkla kurulum, GitHub Releases'tan güncelleme | Hepsi | 🟡 (zip + PowerShell kurulum hazır; Setup.exe için Inno Setup, güncelleme için GitHub deposu gerekli) |
| 20 | Eklenti / widget API'si | Dışarıdan DLL ile widget ekleme (`IDynamicWinExtension` mevcut) | DynamicWin V1 | 🟡 |
| 21 | Windows Hello girişi animasyonu | Kilit açılırken / Hello ile doğrulamada iPhone'daki Face ID gibi kısa animasyon | iOS (Face ID) | ✅ (kilit açılınca) |
| 22 | Sessize alma göstergesi | Ses veya mikrofon kapatılınca/açılınca zil ikonuyla kısa uyarı | iOS (sessiz mod) | ✅ (mikrofon; hoparlör için V1 ses göstergesi) |
| 23 | Odak modu bildirimi | Windows Odak / Rahatsız Etmeyin açılıp kapanınca adada gösterir | iOS (Focus) | ✅ (Rahatsız Etmeyin) |
| 24 | Ekran kaydı göstergesi | Win+Alt+R, Ekran Alıntısı Aracı veya OBS kayıttayken kırmızı nokta ve süre | iOS (ekran kaydı) | ✅ (Windows Graphics Capture kullananlar; OBS görünmez) |

## V1'de zaten olanlar

| Özellik | Kodda nerede |
|---|---|
| Medya kontrolü (oynat, durdur, ileri, geri) + ses görselleştirici | `UI/Widgets/Big/MediaWidget.cs`, `Small/SmallVisualizerWidget.cs` |
| Hava durumu | `UI/Widgets/Big/WeatherWidget.cs` |
| Zamanlayıcı | `UI/Widgets/Big/TimerWidget.cs`, `Small/ActiveTimerWidget.cs` |
| Uygulama kısayolları | `UI/Widgets/Big/ShortcutsWidget.cs` |
| Saat, pil, CPU/RAM kullanımı | `UI/Widgets/Small/` |
| Kamera/mikrofon kullanım göstergesi | `UI/Widgets/Small/UsedDevicesWidget.cs` |
| Ses ve parlaklık göstergesi (HUD) | `UI/Menu/Menus/VolumeAdjustMenu.cs`, `BrightnessAdjustMenu.cs` |
| Dosya tepsisi (dosyayı adaya sürükle-bırak) | `UI/Menu/Menus/DropFileMenu.cs`, `UIElements/Custom/Tray.cs` |
| Temalar, bulanıklık, monitör seçimi, başlangıçta çalışma | `Main/Settings.cs`, `Utils/Theme.cs` |
