using DynamicWin.Resources;
using SkiaSharp;

namespace DynamicWin.Utils
{
    /// <summary>Widget'ların ortak çizim yardımcıları (metin kısaltma, yuvarlak resim, ilerleme çubuğu).</summary>
    public static class Gfx
    {
        static readonly SKPaint measure = new SKPaint();

        public static float Measure(string text, float size, SKTypeface? font = null)
        {
            lock (measure)
            {
                measure.Typeface = font ?? Res.InterRegular;
                measure.TextSize = size;
                return measure.MeasureText(text ?? "");
            }
        }

        /// <summary>Metni verilen genişliğe sığacak şekilde sonuna "…" koyarak kısaltır.</summary>
        public static string Fit(string text, float maxWidth, float size, SKTypeface? font = null)
        {
            text ??= "";
            if (Measure(text, size, font) <= maxWidth) return text;
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Measure(text.Substring(0, mid) + "…", size, font) <= maxWidth) lo = mid; else hi = mid - 1;
            }
            return text.Substring(0, lo).TrimEnd() + "…";
        }

        /// <summary>Sol üst köşesi (x, y) olan metni taban çizgisine göre doğru yerde çizer.</summary>
        public static void Text(SKCanvas canvas, string text, float x, float y, float size, Col color, SKTypeface? font = null, SKTextAlign align = SKTextAlign.Left)
        {
            using var paint = new SKPaint
            {
                Typeface = font ?? Res.InterRegular,
                TextSize = size,
                Color = color.Value(),
                IsAntialias = true,
                TextAlign = align
            };
            canvas.DrawText(text ?? "", x, y + size * 0.8f, paint);
        }

        public static void RoundImage(SKCanvas canvas, SKBitmap bmp, SKRect rect, float radius, float alpha = 1f)
        {
            int save = canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(rect, radius), antialias: true);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.Low, IsAntialias = true, Color = SKColors.White.WithAlpha((byte)(255 * alpha)) };
            canvas.DrawImage(Scaled(canvas, bmp, rect), rect, paint);
            canvas.RestoreToCount(save);
        }

        // ---- Ölçeklenmiş resim önbelleği ----
        // Büyük bir SKBitmap'i (250px ikon, 1280x720 kapak) her karede küçük çizmek pahalı: Skia her çizimde
        // bitmap'i görüntüye çevirip yüksek kaliteyle küçültüyor. Hedef piksel boyutuna bir kez küçültüp SKImage
        // olarak saklıyoruz. Bitmap çöpe gidince önbellek girdisi de gider (ConditionalWeakTable).
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SKBitmap, Dictionary<long, SKImage>> scaledCache = new();

        /// <summary>bmp'nin, canvas'ın o anki ölçeğinde rect'i dolduracak boyuta küçültülmüş hali (önbellekli).</summary>
        public static SKImage Scaled(SKCanvas canvas, SKBitmap bmp, SKRect rect)
        {
            var m = canvas.TotalMatrix;
            int w = Math.Max(1, (int)Math.Ceiling(rect.Width * Math.Abs(m.ScaleX)));
            int h = Math.Max(1, (int)Math.Ceiling(rect.Height * Math.Abs(m.ScaleY)));
            // Animasyonlu boyutlarda sonsuz girdi oluşmasın: 4 piksellik adımlara yuvarla.
            w = (w + 3) / 4 * 4; h = (h + 3) / 4 * 4;
            if (w >= bmp.Width || h >= bmp.Height) { w = bmp.Width; h = bmp.Height; }

            var dict = scaledCache.GetOrCreateValue(bmp);
            long key = ((long)w << 32) | (uint)h;
            lock (dict)
            {
                if (dict.TryGetValue(key, out var img)) return img;
                if (dict.Count > 8) { foreach (var v in dict.Values) v.Dispose(); dict.Clear(); }

                SKImage result;
                if (w == bmp.Width && h == bmp.Height) result = SKImage.FromBitmap(bmp);
                else
                {
                    using var small = bmp.Resize(new SKImageInfo(w, h, bmp.ColorType, bmp.AlphaType), SKFilterQuality.High) ?? bmp.Copy();
                    result = SKImage.FromBitmap(small);
                }
                dict[key] = result;
                return result;
            }
        }

        public static void Bar(SKCanvas canvas, SKRect rect, float value, Col track, Col fill)
        {
            using var paint = new SKPaint { IsAntialias = true };
            float r = rect.Height / 2;
            paint.Color = track.Value();
            canvas.DrawRoundRect(rect, r, r, paint);
            var f = SKRect.Create(rect.Left, rect.Top, Math.Max(rect.Height, rect.Width * Math.Clamp(value, 0f, 1f)), rect.Height);
            paint.Color = fill.Value();
            canvas.DrawRoundRect(f, r, r, paint);
        }

        public static string Time(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }
}
