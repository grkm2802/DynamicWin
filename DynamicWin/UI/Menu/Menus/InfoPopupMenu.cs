using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using SkiaSharp;
using System.Collections.Concurrent;

namespace DynamicWin.UI.Menu.Menus
{
    /// <summary>Adada kısa süre gösterilecek ikonlu sistem uyarısı (Bluetooth, şarj, sessiz, odak, kayıt...).</summary>
    public class PopupInfo
    {
        public string Glyph = Icons.Info;     // Segoe Fluent Icons karakteri
        public string Title = "";
        public string Subtitle = "";
        public Col Accent = Col.White;
        /// <summary>0..1 ise sağda dairesel gösterge çizilir (ör. pil yüzdesi). Negatifse çizilmez.</summary>
        public float Ring = -1f;
        public string RingText = "";
        public float Seconds = 3f;
        public DateTime Created = DateTime.Now;
        /// <summary>Ikon yerine solda gösterilecek küçük resim (ör. ekran görüntüsü önizlemesi).</summary>
        public SKBitmap? Image;
        /// <summary>Aynı anahtarlı eski uyarı kuyruktaysa yenisi onun yerine geçer (ör. sessiz aç/kapa hızlıca).</summary>
        public string? Key;
    }

    /// <summary>Uyarı kuyruğu. Herhangi bir thread'den <see cref="Show"/> çağrılabilir; RendererMain ana menüdeyken açar.</summary>
    public static class IslandPopups
    {
        static readonly ConcurrentQueue<PopupInfo> queue = new ConcurrentQueue<PopupInfo>();

        public static void Show(PopupInfo info) => queue.Enqueue(info);

        public static bool TryDequeue(out PopupInfo? info)
        {
            while (queue.TryDequeue(out var p))
            {
                if (DateTime.Now - p.Created > TimeSpan.FromSeconds(15)) continue; // bayat
                if (p.Key != null && queue.Any(q => q.Key == p.Key)) continue;      // daha yenisi var
                info = p;
                return true;
            }
            info = null;
            return false;
        }
    }

    /// <summary>Segoe Fluent Icons (Win11) / Segoe MDL2 Assets (Win10) karakterleri.</summary>
    public static class Icons
    {
        public const string Info = "";
        public const string Bluetooth = "";
        public const string Headphone = "";
        public const string BatteryCharging = "";
        public const string Battery = "";
        public const string Plug = "";
        public const string Volume = "";
        public const string Mute = "";
        public const string Mic = "";
        public const string MicOff = "";
        public const string Moon = "";
        public const string Unlock = "";
        public const string Face = "";
        public const string Record = "";
        public const string Camera = "";
        public const string Screenshot = "";
        public const string Timer = "";
        public const string Coffee = "";
        public const string Clipboard = "";
        public const string Note = "";

        static SKTypeface? font;
        public static SKTypeface Font => font ??=
            SKTypeface.FromFamilyName("Segoe Fluent Icons") is { } f && f.FamilyName == "Segoe Fluent Icons"
                ? f : SKTypeface.FromFamilyName("Segoe MDL2 Assets");

        public static void DrawGlyph(SKCanvas canvas, string glyph, float cx, float cy, float size, Col color)
        {
            using var p = new SKPaint { Typeface = Font, TextSize = size, Color = color.Value(), IsAntialias = true, TextAlign = SKTextAlign.Center };
            var b = new SKRect(); p.MeasureText(glyph, ref b);
            canvas.DrawText(glyph, cx, cy - b.MidY, p);
        }
    }

    public class InfoPopupMenu : BaseMenu
    {
        PopupInfo info = new PopupInfo();
        float appear = 0f;
        float openedFor = 0f;
        float ringShown = 0f;

        [ThreadStatic] static PopupInfo? pending;   // bkz. ClaudeCodeMenu: BaseMenu kurucusu InitializeMenu'yu önce çağırır

        public static InfoPopupMenu Create(PopupInfo p)
        {
            pending = p;
            try { return new InfoPopupMenu(); }
            finally { pending = null; }
        }

        private InfoPopupMenu() { }

        public float Seconds => info.Seconds;

        public override List<UIObject> InitializeMenu(IslandObject island)
        {
            var objects = base.InitializeMenu(island);
            info = pending ?? new PopupInfo();
            objects.Add(new PopupContent(island, this));
            return objects;
        }

        public override void Update()
        {
            base.Update();
            var dt = RendererMain.Instance.DeltaTime;
            openedFor += dt;
            appear = Mathf.Lerp(appear, 1f, 8f * dt);
            if (info.Ring >= 0) ringShown = Mathf.Lerp(ringShown, info.Ring, 4f * dt);

            if (RendererMain.Instance.MainIsland.IsHovering && openedFor > 0.75f)
                MenuManager.CloseOverlay();
        }

        public override Vec2 IslandSize()
        {
            float imgW = ImageWidth();
            float w = 70 + imgW + Math.Max(Gfx.Measure(info.Title, 14, Res.InterBold), Gfx.Measure(info.Subtitle, 11.5f)) + (info.Ring >= 0 ? 50 : 20);
            w = Math.Clamp(w, 220, 380);
            float h = info.Image != null ? 86 : string.IsNullOrEmpty(info.Subtitle) ? 46 : 56;
            if (info.Image != null) w = Math.Clamp(w, 220, 460);
            return new Vec2(w, h) * (0.85f + 0.15f * appear);
        }

        public override Vec2 IslandSizeBig() => IslandSize();

        float ImageWidth()
        {
            if (info.Image == null) return 0;
            return Math.Min(110f, 64f * info.Image.Width / Math.Max(1, info.Image.Height));
        }

        public override Col IslandBorderColor() => info.Accent.Override(a: 0.5f);

        /// <summary>Uyarının içeriğini çizen nesne (adanın içinde, menü değişince yok olur).</summary>
        class PopupContent : UIObject
        {
            readonly InfoPopupMenu menu;
            public PopupContent(IslandObject island, InfoPopupMenu menu) : base(island, Vec2.zero, Vec2.zero, UIAlignment.TopLeft)
            {
                this.menu = menu;
            }

            public override void Draw(SKCanvas canvas)
            {
                var island = RendererMain.Instance.MainIsland;
                var p = island.Position; var s = island.Size;
                var i = menu.info;
                float a = Alpha * menu.appear;

                float cy = p.Y + s.Y / 2;
                float tx;
                if (i.Image != null)
                {
                    float iw = menu.ImageWidth();
                    var r = SKRect.Create(p.X + 14, cy - 32, iw, 64);
                    Gfx.RoundImage(canvas, i.Image, r, 8, a);
                    tx = r.Right + 12;
                }
                else
                {
                    // İkon dairesi
                    using (var bg = new SKPaint { IsAntialias = true, Color = i.Accent.Override(a: 0.18f * a).Value() })
                        canvas.DrawCircle(p.X + 28, cy, 15, bg);
                    Icons.DrawGlyph(canvas, i.Glyph, p.X + 28, cy, 16, i.Accent.Override(a: a));
                    tx = p.X + 52;
                }
                float right = p.X + s.X - (i.Ring >= 0 ? 52 : 16);
                if (string.IsNullOrEmpty(i.Subtitle))
                    Gfx.Text(canvas, Gfx.Fit(i.Title, right - tx, 14, Res.InterBold), tx, cy - 8, 14, Theme.TextMain.Override(a: a), Res.InterBold);
                else
                {
                    Gfx.Text(canvas, Gfx.Fit(i.Title, right - tx, 14, Res.InterBold), tx, cy - 16, 14, Theme.TextMain.Override(a: a), Res.InterBold);
                    Gfx.Text(canvas, Gfx.Fit(i.Subtitle, right - tx, 11.5f), tx, cy + 2, 11.5f, Theme.TextSecond.Override(a: a));
                }

                if (i.Ring >= 0)
                {
                    float rx = p.X + s.X - 28;
                    using var ring = new SKPaint { IsAntialias = true, IsStroke = true, StrokeWidth = 3, StrokeCap = SKStrokeCap.Round };
                    ring.Color = Theme.TextMain.Override(a: 0.15f * a).Value();
                    canvas.DrawCircle(rx, cy, 15, ring);
                    ring.Color = i.Accent.Override(a: a).Value();
                    using var path = new SKPath();
                    path.AddArc(SKRect.Create(rx - 15, cy - 15, 30, 30), -90, 360 * Math.Clamp(menu.ringShown, 0, 1));
                    canvas.DrawPath(path, ring);
                    Gfx.Text(canvas, i.RingText, rx, cy - 5, 9.5f, Theme.TextMain.Override(a: a), Res.InterBold, SKTextAlign.Center);
                }
            }
        }
    }
}
