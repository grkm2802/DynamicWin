using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using SkiaSharp;

namespace DynamicWin.UI.Widgets.Big
{
    class RegisterMediaWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "Media Playback Control";

        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
        {
            return new MediaWidget(parent, position, alignment, MediaKind.Any);
        }
    }

    class RegisterSpotifyWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "Spotify";
        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
            => new MediaWidget(parent, position, alignment, MediaKind.Spotify);
    }

    class RegisterYouTubeWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "YouTube (browser)";
        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
            => new MediaWidget(parent, position, alignment, MediaKind.Browser);
    }

    /// <summary>
    /// Şu an çalan medya (her oynatıcı, <see cref="MediaSession"/>): albüm kapağı, başlık/sanatçı,
    /// kapak rengiyle arka plan ve parlama, tıklanabilir ilerleme çubuğu, oynat/durdur/ileri/geri.
    /// Eskiden sadece Spotify pencere başlığını okuyordu.
    /// </summary>
    public class MediaWidget : WidgetBase
    {
        DWImageButton playPause;
        DWImageButton next;
        DWImageButton prev;

        Col accent = Theme.Primary;
        float glow = 0f;

        const float pad = 12f;
        const float art = 44f;

        readonly MediaKind kind;
        MediaSource Src => MediaSession.For(kind);

        /// <summary>Medya kartları adada büyük gösterilir; diğer widget'lar küçük kutu olur.</summary>
        public override bool IsFeatured => true;
        public override string TileTitle => kind == MediaKind.Any ? L.T("Media", "Medya") : Src.Name;
        public override string TileGlyph => "";

        public MediaWidget(UIObject? parent, Vec2 position, UIAlignment alignment, MediaKind kind) : base(parent, position, alignment)
        {
            this.kind = kind;
            MediaSession.Start();

            playPause = MakeButton(Res.PlayPause, 0, 0.8f, () => Src.TogglePlayPause());
            next = MakeButton(Res.Next, 34, 0.65f, () => Src.Next());
            prev = MakeButton(Res.Previous, -34, 0.65f, () => Src.Previous());
        }

        DWImageButton MakeButton(SKBitmap icon, float x, float scale, Action onClick)
        {
            var b = new DWImageButton(this, icon, new Vec2(x, 34), new Vec2(24, 24), onClick, alignment: UIAlignment.Center)
            {
                roundRadius = 25,
                normalColor = Col.Transparent,
                hoverColor = Col.White.Override(a: 0.1f),
                clickColor = Col.White.Override(a: 0.25f),
                hoverScaleMulti = Vec2.one * 1.2f,
                imageScale = scale
            };
            AddLocalObject(b);
            return b;
        }

        SKRect BarRect => SKRect.Create(Position.X + pad, Position.Y + 64, Size.X - pad * 2, 4);

        public override void OnMouseDown()
        {
            base.OnMouseDown();

            // İlerleme çubuğuna (biraz geniş tutulmuş alanına) tıklanınca o konuma atla.
            var bar = BarRect;
            var c = RendererMain.CursorPosition;
            if (Src.HasSession && c.X >= bar.Left - 4 && c.X <= bar.Right + 4 && Math.Abs(c.Y - bar.MidY) <= 7)
                Src.SeekTo((c.X - bar.Left) / bar.Width);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            accent = Col.Lerp(accent, Src.AccentColor, 4f * deltaTime);
            glow = Mathf.Lerp(glow, Src.IsPlaying ? 1f : 0.35f, 4f * deltaTime);

            bool has = Src.HasSession;
            playPause.SetActive(has);
            next.SetActive(has);
            prev.SetActive(has);
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            var rect = GetRect();
            using var paint = GetPaint();

            // Arka plan + kapak rengiyle soldan sağa sönen renk
            paint.Color = GetColor(Theme.WidgetBackground).Value();
            canvas.DrawRoundRect(rect, paint);

            var src = Src;
            if (!src.HasSession)
            {
                Gfx.Text(canvas, kind == MediaKind.Any ? L.T("Media", "Medya") : src.Name, Position.X + Size.X / 2, Position.Y + Size.Y / 2 - 16, 15,
                    GetColor(src.BrandColor.Override(a: 0.8f)), Res.InterBold, SKTextAlign.Center);
                Gfx.Text(canvas, L.T("Nothing playing", "Şu an çalmıyor"), Position.X + Size.X / 2, Position.Y + Size.Y / 2 + 4, 11,
                    GetColor(Theme.TextThird), null, SKTextAlign.Center);
                return;
            }

            int save = canvas.Save();
            canvas.ClipRoundRect(rect, antialias: true);
            using (var g = new SKPaint { IsAntialias = true })
            {
                g.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(rect.Rect.Left, rect.Rect.Top), new SKPoint(rect.Rect.Right, rect.Rect.Bottom),
                    new[] { GetColor(accent.Override(a: 0.30f * glow)).Value(), GetColor(accent.Override(a: 0.04f)).Value() },
                    SKShaderTileMode.Clamp);
                canvas.DrawRect(rect.Rect, g);
            }
            canvas.RestoreToCount(save);

            // Kapak rengiyle dış parlama (oynarken güçlü)
            // (ImageFilter.DropShadow yerine MaskFilter: CPU'da çok daha ucuz, bkz. CLAUDE.md "Performans")
            using (var s = new SKPaint { IsAntialias = true, IsStroke = true, StrokeWidth = 3f })
            {
                s.Color = GetColor(accent.Override(a: 0.35f * glow)).Value();
                s.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(0.5f, 5 * glow));
                canvas.DrawRoundRect(rect, s);
                s.MaskFilter = null; s.StrokeWidth = 1.5f;
                s.Color = GetColor(accent.Override(a: 0.45f * glow)).Value();
                canvas.DrawRoundRect(rect, s);
            }

            // Albüm kapağı
            var artRect = SKRect.Create(Position.X + pad, Position.Y + pad, art, art);
            var thumb = src.Thumbnail;
            if (thumb != null)
            {
                using var shadow = new SKPaint { IsAntialias = true, Color = GetColor(accent.Override(a: 0.6f * glow)).Value(),
                    MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8) };
                var sr = artRect; sr.Offset(0, 2);
                canvas.DrawRoundRect(sr, 8, 8, shadow);
                Gfx.RoundImage(canvas, thumb, artRect, 8, Alpha);
            }
            else
            {
                paint.ImageFilter = null;
                paint.Color = GetColor(accent.Override(a: 0.35f)).Value();
                canvas.DrawRoundRect(artRect, 8, 8, paint);
            }

            // Başlık / sanatçı
            float tx = artRect.Right + 10;
            float tw = rect.Rect.Right - pad - tx;
            // Sağ üstte küçük kaynak etiketi (Spotify / YouTube)
            if (kind != MediaKind.Any)
            {
                Gfx.Text(canvas, src.Name, rect.Rect.Right - pad, Position.Y + 7, 9, GetColor(src.BrandColor), Res.InterBold, SKTextAlign.Right);
            }
            Gfx.Text(canvas, Gfx.Fit(src.Title, tw, 13, Res.InterBold), tx, Position.Y + pad + 6, 13, GetColor(Theme.TextMain), Res.InterBold);
            Gfx.Text(canvas, Gfx.Fit(src.Artist, tw, 11.5f), tx, Position.Y + pad + 24, 11.5f, GetColor(Theme.TextSecond));

            // İlerleme çubuğu + süreler
            var bar = BarRect;
            bool hoverBar = IsHovering && Math.Abs(RendererMain.CursorPosition.Y - bar.MidY) <= 7;
            if (hoverBar) bar.Inflate(0, 1.5f);
            Gfx.Bar(canvas, bar, src.Progress, GetColor(Theme.TextMain.Override(a: 0.15f)), GetColor(accent.Override(a: 0.95f)));

            if (src.Duration.TotalSeconds > 1)
            {
                Gfx.Text(canvas, Gfx.Time(src.Position), bar.Left, Position.Y + 78, 9.5f, GetColor(Theme.TextThird));
                Gfx.Text(canvas, "-" + Gfx.Time(src.Duration - src.Position), bar.Right, Position.Y + 78, 9.5f,
                    GetColor(Theme.TextThird), null, SKTextAlign.Right);
            }
        }
    }
}
