using DynamicWin.Main;
using DynamicWin.UI.Menu;
using DynamicWin.Utils;
using SkiaSharp;

namespace DynamicWin.UI.UIElements
{
    public class IslandObject : UIObject
    {
        public float topOffset = 15f;

        public SecondOrder scaleSecondOrder;

        public float[] secondOrderValuesExpand = [2.5f, 0.6f, 0.1f];
        public float[] secondOrderValuesContract = [3f, 0.9f, 0.1f];

        public bool hidden = false;
        
        public Vec2 currSize;

        public enum IslandMode { Island, Notch };
        public IslandMode mode = Settings.IslandMode;

        float dropShadowStrength = 0f;
        float dropShadowSize = 0f;

        Col borderCol = Col.Transparent;

        public IslandObject() : base(null, Vec2.zero, new Vec2(250, 50), UIAlignment.TopCenter)
        {
            currSize = Size;

            Anchor = new Vec2(0.5f, 0f);

            roundRadius = 35f;

            LocalPosition = new Vec2(0, topOffset);

            scaleSecondOrder = new SecondOrder(Size, secondOrderValuesExpand[0], secondOrderValuesExpand[1], secondOrderValuesExpand[2]);
            expandInteractionRect = 20;

            maskInToIsland = false;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (!hidden)
            {
                if (IsHovering)
                {
                    scaleSecondOrder.SetValues(secondOrderValuesExpand[0], secondOrderValuesExpand[1], secondOrderValuesExpand[2]);
                    currSize = MenuManager.Instance.ActiveMenu.IslandSizeBig();
                }
                else
                {
                    scaleSecondOrder.SetValues(secondOrderValuesContract[0], secondOrderValuesContract[1], secondOrderValuesContract[2]);
                    currSize = MenuManager.Instance.ActiveMenu.IslandSize();
                }

                Size = scaleSecondOrder.Update(deltaTime, currSize);
                //Size = Vec2.lerp(Size, currSize, deltaTime * 15f);

                LocalPosition.Y = Mathf.Lerp(LocalPosition.Y, topOffset, 15f * deltaTime);
            }
            else
            {
                scaleSecondOrder.SetValues(secondOrderValuesContract[0], secondOrderValuesContract[1], secondOrderValuesContract[2]);

                Size = scaleSecondOrder.Update(deltaTime, new Vec2(500, 15));
                //Size = Vec2.lerp(Size, new Vec2(500, 10), deltaTime * 15f);

                LocalPosition.Y = Mathf.Lerp(LocalPosition.Y, -Size.Y / 1.5f, 25f * deltaTime);
            }

            MainForm.Instance.Opacity = hidden ? 0.85f : 1f;

            mode = Settings.IslandMode;

            topOffset = Mathf.Lerp(topOffset, (mode == IslandMode.Island) ? 7.5f : -2.5f, 15f * deltaTime);

            dropShadowStrength = Mathf.Lerp(dropShadowStrength, IsHovering ? 0.75f : 0.25f, 10f * deltaTime);
            dropShadowSize = Mathf.Lerp(dropShadowSize, IsHovering ? 35f : 7.5f, 10f * deltaTime);

            borderCol = Col.Lerp(borderCol, MenuManager.Instance.ActiveMenu.IslandBorderColor(), 10f  * deltaTime);
        }

        public override void Draw(SKCanvas canvas)
        {
            var paint = GetPaint();
            paint.IsAntialias = Settings.AntiAliasing;

            paint.Color = Theme.IslandBackground.Value();

            if (!hidden)
            {
                // Gölge: ImageFilter.DropShadow her karede adanın tüm alanını CPU'da bulanıklaştırıyordu (genişlemiş
                // adada kare başına birkaç ms). MaskFilter blur yuvarlak dikdörtgen için analitik hızlı yolu kullanır.
                DrawCachedShadow(canvas, GetRect(), dropShadowSize, dropShadowStrength);
            }

            // Border
            var rect = GetRect();
            var paint2 = GetPaint();
            rect.Inflate(2.5f / 2, 2.5f / 2);
            paint2.Color = borderCol.Override(a: borderCol.a * 0.35f).Value();
            paint2.IsStroke = true;
            paint2.StrokeWidth = 2.5f;

            canvas.DrawRoundRect(rect, paint2);

            paint.ImageFilter = null;

            if (mode == IslandMode.Notch && !hidden)
            {
                var path = new SKPath();

                var awidth = (float)(Math.Max(0f, Size.Y / 45));
                var aheight = (float)(Math.Max(Size.Y / 45, 15)) + (LocalPosition.Y - topOffset);
                var y = 0;

                { // Left notch curve

                    var x = Position.X - awidth;

                    path.MoveTo(x - awidth, y);
                    path.CubicTo(
                        x + 0, y,
                        x + awidth, y,
                        x + awidth, y + aheight);
                    path.LineTo(x + awidth, y);
                    path.LineTo(x + 0, y);
                }

                { // Right notch curve

                    var x = Position.X + Size.X + awidth;

                    path.MoveTo(x + awidth, y);
                    path.CubicTo(
                        x - 0, y,
                        x - awidth, y,
                        x - awidth, y + aheight);
                    path.LineTo(x - awidth, y);
                    path.LineTo(x - 0, y);
                }

                var r = SKRect.Create(Position.X, 0, Size.X, (Position.Y - topOffset) + topOffset + Size.Y / 2);
                path.AddRect(r);

                canvas.DrawPath(path, paint);
            }

            canvas.DrawRoundRect(GetRect(), paint);

            if (Settings.IslandStyle == 1) DrawGlass(canvas);
        }

        // ---- Gölge önbelleği ----
        // Genişlemiş adada 35 px'lik bulanık gölgeyi her karede hesaplamak kare başına ~2 ms tutuyordu.
        // Gölge, boyut 8 px adımlarına yuvarlanarak bir kez görüntüye çizilir ve sonra sadece kopyalanır.
        static readonly Dictionary<(int w, int h, int r, int s, int dpi), SKImage> shadowCache = new();

        static void DrawCachedShadow(SKCanvas canvas, SKRoundRect rect, float sigma, float strength)
        {
            if (strength <= 0.01f) return;
            var rr = rect.Rect;
            int qw = (int)Math.Round(rr.Width / 8) * 8, qh = (int)Math.Round(rr.Height / 8) * 8;
            int qs = Math.Max(1, (int)Math.Round(sigma / 5) * 5);
            int qr = (int)Math.Round(rect.Radii[0].X);
            float scale = Math.Abs(canvas.TotalMatrix.ScaleX);
            int dpi = (int)Math.Round(scale * 100);
            if (qw <= 0 || qh <= 0) return;

            var key = (qw, qh, qr, qs, dpi);
            if (!shadowCache.TryGetValue(key, out var img))
            {
                if (shadowCache.Count > 12) { foreach (var v in shadowCache.Values) v.Dispose(); shadowCache.Clear(); }
                float pad = qs * 3;
                int pw = (int)Math.Ceiling((qw + pad * 2) * scale), ph = (int)Math.Ceiling((qh + pad * 2) * scale);
                using var surface = SKSurface.Create(new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul));
                var c = surface.Canvas;
                c.Clear(SKColors.Transparent);
                c.Scale(scale);
                using var p = new SKPaint { IsAntialias = true, Color = SKColors.Black, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, qs) };
                c.DrawRoundRect(SKRect.Create(pad, pad, qw, qh), qr, qr, p);
                img = surface.Snapshot();
                shadowCache[key] = img;
            }

            float padD = qs * 3;
            var dest = SKRect.Create(rr.MidX - qw / 2f - padD + 1, rr.MidY - qh / 2f - padD + 1, qw + padD * 2, qh + padD * 2);
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(255 * Math.Clamp(strength, 0, 1))), FilterQuality = SKFilterQuality.Low };
            canvas.DrawImage(img, dest, paint);
        }

        /// <summary>
        /// Cam görünümü (#17). Pencere şeffaf olduğu için arkadaki masaüstü görünür; gerçek bulanıklık
        /// (Acrylic) layered pencerede mümkün değil, bu yüzden yarı saydam koyu dolgu + üstten parlama
        /// + ince açık kenar ile cam hissi verilir. Arka plan rengi bu modda Theme'de yarı saydam yapılır.
        /// </summary>
        void DrawGlass(SKCanvas canvas)
        {
            var r = GetRect();
            using var p = new SKPaint { IsAntialias = true };

            // Üst yarıda yumuşak beyaz parlama
            p.Shader = SKShader.CreateLinearGradient(
                new SKPoint(r.Rect.Left, r.Rect.Top), new SKPoint(r.Rect.Left, r.Rect.Top + Math.Min(r.Rect.Height, 60)),
                new[] { new SKColor(255, 255, 255, 38), new SKColor(255, 255, 255, 0) }, SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(r, p);
            p.Shader = null;

            // İnce açık kenar (üstte daha parlak)
            p.IsStroke = true;
            p.StrokeWidth = 1f;
            p.Shader = SKShader.CreateLinearGradient(
                new SKPoint(r.Rect.Left, r.Rect.Top), new SKPoint(r.Rect.Left, r.Rect.Bottom),
                new[] { new SKColor(255, 255, 255, 70), new SKColor(255, 255, 255, 18) }, SKShaderTileMode.Clamp);
            var inner = new SKRoundRect(r.Rect, r.Radii[0].X, r.Radii[0].Y);
            inner.Deflate(0.5f, 0.5f);
            canvas.DrawRoundRect(inner, p);
        }

        public override SKRoundRect GetInteractionRect()
        {
            var rect = SKRect.Create(Position.X, Position.Y, Size.X, Size.Y);

            if(IsHovering)
                rect.Inflate(expandInteractionRect + 5, expandInteractionRect + 5);

            rect.Inflate(expandInteractionRect, expandInteractionRect);
            var r = new SKRoundRect(rect, roundRadius);

            return r;
        }

        public override SKRoundRect GetRect()
        {
            var rect = base.GetRect();
            return rect;
        }
    }
}
