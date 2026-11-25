# DynamicWin (Türkçe sürüm)

Windows için iPhone'daki Dynamic Island'a benzeyen bir ada. Ekranın üst ortasında durur; müzik, bildirimler, Claude Code, Pomodoro ve sistem uyarılarını gösterir. Fareyle üzerine gelince genişler.

> Bu proje, Florian Butz'un [DynamicWin](https://github.com/FlorianButz/DynamicWin) uygulamasının açık kaynak V1 sürümü üzerine geliştirilmiştir. Orijinal proje ve bu sürüm **CC BY-SA 4.0** lisanslıdır (aşağıya bakın).

<p align="center">
  <img src="ReadmeFiles/IslandGif-1_Volume.gif" style="border-radius:15px" alt="DynamicWin" width="800" />
</p>

## İndir ve kur

1. [Son sürümü indirin](https://github.com/grkm2802/DynamicWin/releases/latest): `DynamicWin-<sürüm>.zip`
2. Zip'i bir klasöre çıkarın ve **`Kur.cmd`** dosyasına çift tıklayın.

- Yönetici izni istemez. Uygulamayı `%LOCALAPPDATA%\Programs\DynamicWin` klasörüne kurar, Başlat menüsüne ekler ve Windows açılışında başlar.
- .NET kurmanıza gerek yok, paketin içinde gelir.
- Bilgisayarda Claude Code varsa, kurulum sırasında adanın Claude'u gösterip göstermeyeceği sorulur.
- Kaldırmak için: Ayarlar › Uygulamalar › DynamicWin › Kaldır.
- Uygulama dijital olarak imzalı değil. Windows "bilgisayarınızı korudu" derse "Ek bilgi" → "Yine de çalıştır" ile devam edebilirsiniz.

## Özellikler

| | |
|---|---|
| **Medya** | Spotify ve YouTube (tarayıcı) ayrı kartlarda, aynı anda. Albüm kapağı, kapak rengiyle parlama, tıklanabilir ilerleme çubuğu |
| **Claude Code** | Claude'un ne yaptığı (çalışıyor, izin bekliyor, bitti), izin isteği ve görev bitince bildirim |
| **Bildirimler** | Windows bildirimleri adada görünür |
| **Widget'lar** | Pano geçmişi, hızlı not, Pomodoro, ağ hızı, kamera aynası, kısayollar, hava durumu. Küçük kutu olarak durur, tıklayınca açılır |
| **Sistem uyarıları** | Bluetooth cihaz bağlanınca (pil yüzdesiyle), şarj ve düşük pil, sessize alma, Rahatsız Etmeyin, ekran kaydı, kilit açma |
| **Ekran görüntüsü** | Win+Shift+S ile alınan görüntü adada önizlenir ve Tray'e kaydedilir |
| **Tray** | Dosyaları adaya sürükleyip bırakarak geçici olarak saklayabilirsiniz |
| **Oyun dostu** | Tam ekran oyun ya da video açıkken ada tamamen çekilir, FPS'i etkilemez |
| **Görünüm** | Düz veya cam görünüm, temalar, Türkçe / İngilizce arayüz |

## Kaynaktan derleme

Gereken: Windows 10/11 ve [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
dotnet build DynamicWin.sln -c Release
.\DynamicWin\bin\Release\net8.0-windows10.0.19041.0\DynamicWin.exe
```

Sürüm paketi üretmek için:

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-release.ps1
```

Geliştirici notları ve mimari için [CLAUDE.md](CLAUDE.md), yol haritası için [ROADMAP.md](ROADMAP.md) dosyalarına bakın.

## Lisans ve atıf

<p xmlns:cc="http://creativecommons.org/ns#" xmlns:dct="http://purl.org/dc/terms/"><a property="dct:title" rel="cc:attributionURL" href="https://github.com/FlorianButz/DynamicWin">DynamicWin</a> by <a rel="cc:attributionURL dct:creator" property="cc:attributionName" href="https://github.com/FlorianButz">Florian Butz</a> is licensed under <a href="https://creativecommons.org/licenses/by-sa/4.0/" target="_blank" rel="license noopener noreferrer">CC BY-SA 4.0</a>.</p>

Bu sürüm orijinal V1 kaynak kodunu (commit `9f24d17`) değiştirerek oluşturulmuştur. Değişikliklerin listesi git geçmişinde ve [ROADMAP.md](ROADMAP.md) dosyasındadır. Aynı **CC BY-SA 4.0** lisansıyla paylaşılır.
