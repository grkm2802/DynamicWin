using SkiaSharp;
using System.Diagnostics;
using System.IO;
using Windows.Media.Control;

namespace DynamicWin.Utils
{
    public enum MediaKind { Any, Spotify, Browser }

    /// <summary>
    /// Tek bir medya kaynağının (Spotify, tarayıcı/YouTube...) durumu ve kontrolleri.
    /// Arka plan thread'i günceller; UI thread'i sadece okur (referans atamaları atomik).
    /// </summary>
    public class MediaSource
    {
        public MediaKind Kind { get; }
        public string Name { get; }
        public Col BrandColor { get; }

        public bool HasSession { get; private set; }
        public bool IsPlaying { get; private set; }
        public string Title { get; private set; } = "";
        public string Artist { get; private set; } = "";
        public string SourceApp { get; private set; } = "";
        public SKBitmap? Thumbnail { get; private set; }
        public Col AccentColor { get; private set; }

        TimeSpan position, duration;
        DateTime positionUpdatedAt = DateTime.Now;
        string lastTrackKey = "";
        GlobalSystemMediaTransportControlsSession? session;

        public MediaSource(MediaKind kind, string name, Col brand)
        {
            Kind = kind; Name = name; BrandColor = brand; AccentColor = brand;
        }

        public TimeSpan Duration => duration;
        public TimeSpan Position
        {
            get
            {
                var p = position + (IsPlaying ? DateTime.Now - positionUpdatedAt : TimeSpan.Zero);
                return p > duration && duration > TimeSpan.Zero ? duration : p;
            }
        }
        public float Progress => duration.TotalSeconds > 1 ? (float)(Position.TotalSeconds / duration.TotalSeconds) : 0f;

        internal void Clear()
        {
            session = null; HasSession = false; IsPlaying = false;
        }

        internal async Task Update(GlobalSystemMediaTransportControlsSession s)
        {
            session = s;
            HasSession = true;
            SourceApp = s.SourceAppUserModelId ?? "";
            IsPlaying = s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var tl = s.GetTimelineProperties();
            duration = tl.EndTime - tl.StartTime;
            // Bazı oynatıcılar (Spotify) konumu seyrek günceller; LastUpdatedTime'dan bu yana geçen süreyi ekle.
            var since = DateTimeOffset.Now - tl.LastUpdatedTime;
            if (since < TimeSpan.Zero || since > TimeSpan.FromHours(1)) since = TimeSpan.Zero;
            position = tl.Position - tl.StartTime;
            positionUpdatedAt = DateTime.Now - (IsPlaying ? since : TimeSpan.Zero);

            var props = await s.TryGetMediaPropertiesAsync();
            Title = props?.Title ?? "";
            Artist = props?.Artist ?? "";

            string key = SourceApp + "|" + Title + "|" + Artist;
            if (key != lastTrackKey)
            {
                lastTrackKey = key;
                await LoadThumbnail(props);
            }
        }

        async Task LoadThumbnail(GlobalSystemMediaTransportControlsSessionMediaProperties? props)
        {
            try
            {
                if (props?.Thumbnail == null) { Thumbnail = null; AccentColor = BrandColor; return; }
                using var ras = await props.Thumbnail.OpenReadAsync();
                using var stream = ras.AsStreamForRead();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;
                var bmp = SKBitmap.Decode(ms);
                if (bmp == null) { Thumbnail = null; AccentColor = BrandColor; return; }
                AccentColor = MediaSession.DominantColor(bmp, BrandColor);
                Thumbnail = bmp;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Kapak yüklenemedi: " + e.Message);
                Thumbnail = null; AccentColor = BrandColor;
            }
        }

        // ---- Kontroller (bu kaynağın kendi oturumuna gider) ----
        public void TogglePlayPause() => Fire(x => x.TryTogglePlayPauseAsync().AsTask());
        public void Next() => Fire(x => x.TrySkipNextAsync().AsTask());
        public void Previous() => Fire(x => x.TrySkipPreviousAsync().AsTask());

        public void SeekTo(float fraction)
        {
            if (duration.TotalSeconds < 1) return;
            var target = TimeSpan.FromSeconds(duration.TotalSeconds * Math.Clamp(fraction, 0f, 1f));
            position = target; positionUpdatedAt = DateTime.Now; // anında geri bildirim
            Fire(x => x.TryChangePlaybackPositionAsync(target.Ticks).AsTask());
        }

        void Fire(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
        {
            var s = session;
            if (s == null) return;
            Task.Run(async () => { try { await action(s); } catch (Exception e) { Debug.WriteLine("Medya komutu: " + e.Message); } });
        }
    }

    /// <summary>
    /// Windows'un genel medya oturumları (GSMTC). Spotify ve tarayıcı (YouTube) ayrı ayrı izlenir,
    /// ikisi aynı anda çalabilir. Diğer oynatıcılar "Other" kaynağına düşer.
    /// Statik özellikler (IsPlaying, Title, ...) şu an en önemli kaynağı (<see cref="Primary"/>) yansıtır.
    /// </summary>
    public static class MediaSession
    {
        public static readonly MediaSource Spotify = new MediaSource(MediaKind.Spotify, "Spotify", Col.FromHex("#1DB954"));
        public static readonly MediaSource Browser = new MediaSource(MediaKind.Browser, "YouTube", Col.FromHex("#FF0033"));
        public static readonly MediaSource Other = new MediaSource(MediaKind.Any, L.T("Media", "Medya"), Col.FromHex("#6988b7"));

        static readonly string[] browserIds = { "chrome", "msedge", "firefox", "308046b0af4a39cb", "opera", "brave", "vivaldi", "arc", "youtube" };

        public static MediaSource For(MediaKind kind) => kind switch
        {
            MediaKind.Spotify => Spotify,
            MediaKind.Browser => Browser,
            _ => Primary
        };

        /// <summary>Çalan kaynak öncelikli (Spotify > tarayıcı > diğer), hiçbiri çalmıyorsa oturumu olan ilk kaynak.</summary>
        public static MediaSource Primary
        {
            get
            {
                foreach (var s in new[] { Spotify, Browser, Other }) if (s.IsPlaying) return s;
                foreach (var s in new[] { Spotify, Browser, Other }) if (s.HasSession) return s;
                return Other;
            }
        }

        // Eski tek-kaynak API'si (LiveActivity, ada kenarı vb. kullanıyor)
        public static bool HasSession => Primary.HasSession;
        public static bool IsPlaying => Primary.IsPlaying;
        public static string Title => Primary.Title;
        public static string Artist => Primary.Artist;
        public static Col AccentColor => Primary.AccentColor;

        static GlobalSystemMediaTransportControlsSessionManager? manager;
        static bool started;

        public static void Start()
        {
            if (started) return;
            started = true;
            Task.Run(Loop);
        }

        static async Task Loop()
        {
            while (true)
            {
                try { await Poll(); }
                catch (Exception e)
                {
                    Debug.WriteLine("Medya oturumları okunamadı: " + e.Message);
                    Spotify.Clear(); Browser.Clear(); Other.Clear(); manager = null;
                }
                await Task.Delay(1000);
            }
        }

        static MediaSource Classify(string aumid)
        {
            var id = aumid.ToLowerInvariant();
            if (id.Contains("spotify")) return Spotify;
            if (browserIds.Any(id.Contains)) return Browser;
            return Other;
        }

        static async Task Poll()
        {
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions = manager.GetSessions();

            // Her kaynak için en uygun oturumu seç: çalan varsa o, yoksa ilk bulunan.
            var picked = new Dictionary<MediaSource, GlobalSystemMediaTransportControlsSession>();
            foreach (var s in sessions)
            {
                var src = Classify(s.SourceAppUserModelId ?? "");
                bool playing = s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                if (!picked.ContainsKey(src) || playing) picked[src] = s;
            }

            foreach (var src in new[] { Spotify, Browser, Other })
            {
                if (picked.TryGetValue(src, out var s)) await src.Update(s);
                else src.Clear();
            }
        }

        /// <summary>Kapağı küçültüp doygun, çok karanlık/aydınlık olmayan piksellerin ağırlıklı ortalaması.</summary>
        public static Col DominantColor(SKBitmap src, Col fallback)
        {
            using var small = src.Resize(new SKImageInfo(24, 24), SKFilterQuality.Low);
            if (small == null) return fallback;

            double r = 0, g = 0, b = 0, wsum = 0;
            for (int y = 0; y < small.Height; y++)
                for (int x = 0; x < small.Width; x++)
                {
                    var c = small.GetPixel(x, y);
                    float max = Math.Max(c.Red, Math.Max(c.Green, c.Blue)) / 255f;
                    float min = Math.Min(c.Red, Math.Min(c.Green, c.Blue)) / 255f;
                    float sat = max <= 0 ? 0 : (max - min) / max;
                    if (max < 0.15f || max > 0.97f && sat < 0.1f) continue;
                    double w = 0.05 + sat * sat;
                    r += c.Red * w; g += c.Green * w; b += c.Blue * w; wsum += w;
                }
            if (wsum <= 0) return fallback;

            var col = new Col((float)(r / wsum / 255), (float)(g / wsum / 255), (float)(b / wsum / 255));
            float m = Math.Max(col.r, Math.Max(col.g, col.b));
            if (m > 0 && m < 0.6f) col = col * (0.6f / m);
            return col.Override(a: 1f);
        }
    }
}
