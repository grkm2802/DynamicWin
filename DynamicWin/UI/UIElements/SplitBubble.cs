using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.Utils;
using SkiaSharp;

namespace DynamicWin.UI.UIElements
{
    /// <summary>
    /// iPhone'daki "minimal" görünüm: adada widget'ı olmayan bir canlı etkinlik varsa adanın sağında
    /// ayrık küçük bir balon olarak görünür. Ada genişletilince (hover) balon adaya geri karışır.
    /// UIObject değil; RendererMain her karede Update + Draw çağırır, menü değişiminden etkilenmez.
    /// </summary>
    public class SplitBubble
    {
        LiveActivity? activity;
        string text = "";
        Col accent = Col.Transparent;

        float appear = 0f;        // 0 = adanın içinde/gizli, 1 = tamamen ayrık
        float width = 0f;
        float pulse = 0f;
        SecondOrder? gap;
        float gapValue = gapSize;

        const float height = 30f;
        const float gapSize = 8f;
        const float dotRadius = 4f;

        public SKRect LastRect { get; private set; }

        public void Update(float dt, IslandObject island)
        {
            var current = LiveActivityManager.GetDetached();

            // Ada genişlemişken veya gizliyken balonu adaya geri çek.
            bool show = current != null && !island.IsHovering && !island.hidden;

            if (current != null)
            {
                activity = current;
                text = current.ShortText ?? "";
                accent = current.Accent;
            }

            appear = Mathf.Lerp(appear, show ? 1f : 0f, (show ? 7f : 12f) * dt);

            using var measure = new SKPaint { Typeface = Res.InterRegular, TextSize = 12 };
            float textW = string.IsNullOrEmpty(text) ? 0 : measure.MeasureText(text);
            float targetW = string.IsNullOrEmpty(text) ? height : 22f + textW + 12f;
            width = Mathf.Lerp(width, targetW, 10f * dt);

            gap ??= new SecondOrder(new Vec2(0, 0), 2.5f, 0.45f, 0.1f);
            if (dt > 0) gapValue = gap.Update(dt, new Vec2(show ? gapSize : 0f, 0)).X;

            pulse += (activity?.NeedsAttention == true ? 6f : 2.5f) * dt;
        }

        public void Draw(SKCanvas canvas, IslandObject island)
        {
            if (appear < 0.02f || activity == null) return;

            var ip = island.Position;
            var isz = island.Size;

            // Balon dikeyde adanın ortasına hizalı, yatayda sağ kenarından "kopuyor".
            float x = ip.X + isz.X + gapValue * appear;
            float y = ip.Y + isz.Y / 2 - height / 2;
            float w = width * (0.6f + 0.4f * appear);

            var rect = SKRect.Create(x, y, w, height);
            LastRect = rect;
            var rrect = new SKRoundRect(rect, height / 2);

            using var paint = new SKPaint { IsAntialias = Settings.AntiAliasing };

            // Gölge + arka plan (adayla aynı renk, ondan kopmuş gibi dursun).
            using (var shadow = new SKPaint { IsAntialias = true, Color = new Col(0, 0, 0).Override(a: 0.25f * appear).Value(),
                       MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7.5f) })
            {
                var sr = new SKRoundRect(SKRect.Create(x + 1, y + 1, w, height), height / 2);
                canvas.DrawRoundRect(sr, shadow);
            }
            paint.Color = Theme.IslandBackground.Override(a: Theme.IslandBackground.a * appear).Value();
            canvas.DrawRoundRect(rrect, paint);

            // Dikkat isteyen etkinlikte vurgu renginde kenarlık.
            if (activity.NeedsAttention)
            {
                float t = Mathf.Remap((float)Math.Sin(pulse), -1, 1, 0.2f, 0.8f);
                paint.IsStroke = true;
                paint.StrokeWidth = 1.5f;
                paint.Color = accent.Override(a: t * appear).Value();
                canvas.DrawRoundRect(rrect, paint);
                paint.IsStroke = false;
            }

            // Nabız noktası
            float pt = Mathf.Remap((float)Math.Sin(pulse), -1, 1, 0, 1);
            paint.Color = Col.Lerp(accent, accent * 0.55f, pt).Override(a: appear).Value();
            float cx = string.IsNullOrEmpty(text) ? x + w / 2 : x + 14f;
            canvas.DrawCircle(cx, y + height / 2, dotRadius * appear, paint);

            // Kısa metin
            if (!string.IsNullOrEmpty(text) && appear > 0.3f)
            {
                using var font = new SKFont(Res.InterRegular, 12);
                paint.Color = Theme.TextMain.Override(a: Mathf.Clamp((appear - 0.3f) / 0.7f, 0, 1)).Value();
                canvas.Save();
                canvas.ClipRoundRect(rrect, antialias: true);
                canvas.DrawText(text, x + 24f, y + height / 2 + 4.5f, font, paint);
                canvas.Restore();
            }
        }
    }
}
