using DynamicWin.Resources;
using DynamicWin.Utils;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicWin.UI.UIElements
{
    public class DWImage : UIObject
    {
        private SKBitmap image;

        public SKBitmap Image { get { return image; } set => image = value; }

        public bool maskOwnRect = false;
        public bool allowIconThemeColor = true;

        public DWImage(UIObject? parent, SKBitmap sprite, Vec2 position, Vec2 size, UIAlignment alignment = UIAlignment.TopCenter, bool maskOwnRect = false) : base(parent, position, size, alignment)
        {
            image = sprite;
            this.maskOwnRect = maskOwnRect;

            Color = Theme.IconColor;
        }

        public override void Draw(SKCanvas canvas)
        {
            var paint = GetPaint();

            if (image == null) return;

            if (allowIconThemeColor)
            {
                // İkonu tema rengiyle boya. Eskiden ImageFilter (BlendMode + ColorFilter) kullanılıyordu; ImageFilter her
                // çizimde ekran dışı katman açtığı için pahalıydı. ColorFilter aynı sonucu (renk x ikon, alfa korunur) katmansız verir.
                paint.ColorFilter = SKColorFilter.CreateBlendMode(Color.Value(), SKBlendMode.Modulate);
                // Bulanıklık sadece menü geçişlerinde (GetPaint zaten ImageFilter blur koyuyor).
            }

            paint.FilterQuality = SKFilterQuality.Low; // önbellekteki görüntü zaten hedef boyutta
            var dest = SKRect.Create(Position.X, Position.Y, Size.X, Size.Y);
            var scaled = Gfx.Scaled(canvas, image, dest);

            if (maskOwnRect)
            {
                int save = canvas.Save();
                canvas.ClipRoundRect(GetRect(), antialias: true);
                canvas.DrawImage(scaled, dest, paint);
                canvas.RestoreToCount(save);
            }
            else
            {
                canvas.DrawImage(scaled, dest, paint);
            }
        }
    }
}
