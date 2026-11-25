using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.Widgets;
using DynamicWin.Utils;
using SkiaSharp;

namespace DynamicWin.UI.UIElements
{
    /// <summary>
    /// Genişletilmiş adada bir widget'ın küçük hali: ikon + isim (+ varsa canlı durum, ör. Pomodoro süresi).
    /// Tıklanınca HomeMenu o widget'ın tam halini açar/kapatır.
    /// </summary>
    public class WidgetTile : UIObject
    {
        public readonly WidgetBase Widget;
        readonly Action<WidgetTile> onClick;
        public bool Selected;

        float hover;
        float selected;

        public WidgetTile(UIObject parent, WidgetBase widget, Action<WidgetTile> onClick)
            : base(parent, Vec2.zero, new Vec2(96, 30), UIAlignment.BottomCenter)
        {
            Widget = widget;
            this.onClick = onClick;
            roundRadius = 10f;
            Anchor = new Vec2(0.5f, 1f);
        }

        public override void OnMouseUp()
        {
            base.OnMouseUp();
            onClick(this);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            hover = Mathf.Lerp(hover, IsHovering ? 1f : 0f, 12f * deltaTime);
            selected = Mathf.Lerp(selected, Selected ? 1f : 0f, 12f * deltaTime);
        }

        public override void Draw(SKCanvas canvas)
        {
            var r = SKRect.Create(Position.X, Position.Y, Size.X, Size.Y);
            using var p = new SKPaint { IsAntialias = true };

            p.Color = GetColor(Theme.WidgetBackground).Value();
            canvas.DrawRoundRect(r, roundRadius, roundRadius, p);

            float extra = Math.Max(hover * 0.05f, selected * 0.12f);
            if (extra > 0.005f)
            {
                p.Color = GetColor(Theme.TextMain.Override(a: extra)).Value();
                canvas.DrawRoundRect(r, roundRadius, roundRadius, p);
            }

            if (selected > 0.02f)
            {
                p.IsStroke = true; p.StrokeWidth = 1f;
                p.Color = GetColor(Theme.TextMain.Override(a: 0.25f * selected)).Value();
                canvas.DrawRoundRect(r, roundRadius, roundRadius, p);
                p.IsStroke = false;
            }

            Icons.DrawGlyph(canvas, Widget.TileGlyph, r.Left + 15, r.MidY, 12, GetColor(Theme.TextSecond));

            string status = Widget.TileStatus ?? "";
            float statusW = string.IsNullOrEmpty(status) ? 0 : Gfx.Measure(status, 9.5f) + 6;
            float tx = r.Left + 28;
            Gfx.Text(canvas, Gfx.Fit(Widget.TileTitle, r.Right - 8 - statusW - tx, 11, Res.InterBold), tx, r.MidY - 6.5f, 11,
                GetColor(Theme.TextMain), Res.InterBold);
            if (statusW > 0)
                Gfx.Text(canvas, status, r.Right - 8, r.MidY - 5.5f, 9.5f, GetColor(Theme.TextSecond), null, SKTextAlign.Right);
        }
    }
}
