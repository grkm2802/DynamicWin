using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace DynamicWin.UI.Widgets.Big
{
    class RegisterCameraWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "Camera Mirror";
        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
            => new CameraWidget(parent, position, alignment);
    }

    /// <summary>
    /// Kamera aynası (#16). Gizlilik için kendiliğinden açılmaz: tıklayınca başlar, tekrar tıklayınca
    /// ya da ada küçülünce (widget görünmez olunca) kapanır. Kamera paylaşımlı-salt-okunur açılır,
    /// böylece toplantı uygulaması kamerayı kullanıyorsa onunla çakışmaz.
    /// </summary>
    public static class CameraMirror
    {
        public static bool Running { get; private set; }
        public static bool Starting { get; private set; }
        public static string? Error { get; private set; }

        static MediaCapture? capture;
        static MediaFrameReader? reader;
        // Kare sahipliği: kamera thread'i yeni kareyi sadece "pending"e bırakır. Render (UI) thread'i
        // TakeLatestFrame ile onu "current" olarak devralır ve eski current'ı KENDİSİ siler. Böylece çizilmekte
        // olan bir bitmap başka thread'den asla Dispose edilmez (önceki sürüm burada AccessViolation ile çöktü).
        static SKBitmap? pending;
        static SKBitmap? current;            // sadece UI thread'i dokunur
        static readonly object frameLock = new object();
        static DateTime lastFrameAt;

        /// <summary>UI thread'inden çağrılır. Kamera kapalıysa elindeki kareyi bırakır ve null döner.</summary>
        public static SKBitmap? TakeLatestFrame()
        {
            SKBitmap? next = null;
            lock (frameLock) { next = pending; pending = null; }
            if (next != null) { current?.Dispose(); current = next; }
            if (!Running && current != null) { current.Dispose(); current = null; }
            return current;
        }

        public static async Task Start()
        {
            if (Running || Starting) return;
            Starting = true; Error = null;
            try
            {
                var groups = await MediaFrameSourceGroup.FindAllAsync();
                var pick = groups.Select(g => (g, s: g.SourceInfos.FirstOrDefault(i => i.SourceKind == MediaFrameSourceKind.Color)))
                                 .FirstOrDefault(x => x.s != null);
                if (pick.s == null) { Error = L.T("No camera found", "Kamera bulunamadı"); return; }

                capture = new MediaCapture();
                await capture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    SourceGroup = pick.g,
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                    StreamingCaptureMode = StreamingCaptureMode.Video
                });

                reader = await capture.CreateFrameReaderAsync(capture.FrameSources[pick.s.Id], MediaEncodingSubtypes.Bgra8);
                reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
                reader.FrameArrived += OnFrame;
                await reader.StartAsync();
                Running = true;
            }
            catch (UnauthorizedAccessException)
            {
                Error = L.T("Camera access is off in Windows privacy settings", "Kamera izni Windows gizlilik ayarlarında kapalı");
                await Stop();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Kamera açılamadı: " + e);
                Error = L.T("Camera could not be opened", "Kamera açılamadı");
                await Stop();
            }
            finally { Starting = false; }
        }

        public static async Task Stop()
        {
            Running = false;
            try
            {
                if (reader != null) { reader.FrameArrived -= OnFrame; await reader.StopAsync(); reader.Dispose(); }
            }
            catch { }
            try { capture?.Dispose(); } catch { }
            reader = null; capture = null;
            lock (frameLock) { pending?.Dispose(); pending = null; }
            // current'ı UI thread'i bir sonraki TakeLatestFrame'de bırakır.
        }

        static void OnFrame(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            // ~20 FPS yeter; fazlası CPU harcar.
            if ((DateTime.Now - lastFrameAt).TotalMilliseconds < 50) return;
            lastFrameAt = DateTime.Now;

            using var f = sender.TryAcquireLatestFrame();
            var sb = f?.VideoMediaFrame?.SoftwareBitmap;
            if (sb == null) return;

            SoftwareBitmap? converted = null;
            try
            {
                if (sb.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || sb.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                    converted = SoftwareBitmap.Convert(sb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                var src = converted ?? sb;

                int w = src.PixelWidth, h = src.PixelHeight;
                var bytes = new byte[w * h * 4];
                src.CopyToBuffer(bytes.AsBuffer());

                var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
                Marshal.Copy(bytes, 0, bmp.GetPixels(), bytes.Length);

                lock (frameLock)
                {
                    if (!Running) { bmp.Dispose(); return; }
                    pending?.Dispose(); // UI henüz almadıysa eski bekleyeni at (UI'ye hiç verilmedi, güvenli)
                    pending = bmp;
                }
            }
            catch (Exception e) { Debug.WriteLine("Kamera karesi: " + e.Message); }
            finally { converted?.Dispose(); sb.Dispose(); }
        }
    }

    public class CameraWidget : WidgetBase
    {
        public override string TileTitle => L.T("Camera", "Kamera");
        public override string TileGlyph => Icons.Camera;
        public override string TileStatus => CameraMirror.Running ? L.T("on", "açık") : "";

        DateTime lastVisible = DateTime.Now;

        public CameraWidget(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, alignment) { }

        public override void OnMouseUp()
        {
            base.OnMouseUp();
            if (CameraMirror.Running) _ = CameraMirror.Stop();
            else _ = CameraMirror.Start();
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            // Ada küçüldüyse (widget görünmüyor) bir saniye sonra kamerayı kapat.
            if (RendererMain.Instance.MainIsland.IsHovering) lastVisible = DateTime.Now;
            else if (CameraMirror.Running && (DateTime.Now - lastVisible).TotalSeconds > 1) _ = CameraMirror.Stop();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (CameraMirror.Running) _ = CameraMirror.Stop();
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            using var paint = GetPaint();
            paint.Color = GetColor(Theme.WidgetBackground).Value();
            var rect = GetRect();
            canvas.DrawRoundRect(rect, paint);

            var f = CameraMirror.TakeLatestFrame();
            if (CameraMirror.Running && f != null)
            {
                // Ayna: yatayda çevir, widget'ı doldurana kadar ortadan kırp.
                int save = canvas.Save();
                canvas.ClipRoundRect(rect, antialias: true);
                float scale = Math.Max(rect.Rect.Width / f.Width, rect.Rect.Height / f.Height);
                float dw = f.Width * scale, dh = f.Height * scale;
                float cx = rect.Rect.MidX, cy = rect.Rect.MidY;
                canvas.Translate(cx, cy);
                canvas.Scale(-1, 1);
                using var p = new SKPaint { FilterQuality = SKFilterQuality.Low, Color = SKColors.White.WithAlpha((byte)(255 * Alpha)) };
                canvas.DrawBitmap(f, SKRect.Create(-dw / 2, -dh / 2, dw, dh), p);
                canvas.RestoreToCount(save);

                // Kayıt göstergesi gibi küçük yeşil nokta: kamera açık.
                using var dot = new SKPaint { IsAntialias = true, Color = GetColor(Col.FromHex("#34C759")).Value() };
                canvas.DrawCircle(rect.Rect.Right - 10, rect.Rect.Top + 10, 3.5f, dot);
                return;
            }

            Icons.DrawGlyph(canvas, Icons.Camera, rect.Rect.MidX, rect.Rect.MidY - 10, 20, GetColor(Theme.TextSecond));
            string msg = CameraMirror.Starting ? L.T("Opening camera…", "Kamera açılıyor…")
                       : CameraMirror.Error ?? L.T("Click for camera mirror", "Kamera aynası için tıklayın");
            Gfx.Text(canvas, Gfx.Fit(msg, rect.Rect.Width - 16, 10.5f), rect.Rect.MidX, rect.Rect.MidY + 10, 10.5f,
                GetColor(Theme.TextSecond), null, SKTextAlign.Center);
        }
    }
}
