using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using SkiaSharp;

namespace DynamicWin.UI.Widgets.Big
{
    class RegisterClipboardWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "Clipboard History";
        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
            => new ClipboardWidget(parent, position, alignment);
    }

    /// <summary>Pano geçmişi (#11): son kopyalanan metinler; satıra tıklayınca tekrar panoya kopyalanır.</summary>
    public class ClipboardWidget : WidgetBase
    {
        public override string TileTitle => L.T("Clipboard", "Pano");
        public override string TileGlyph => Icons.Clipboard;
        public override string TileStatus => "";

        const int Rows = 4;
        const float rowH = 19f;
        const float top = 24f;

        ClipEntry? flashed;
        float flash;

        public ClipboardWidget(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, alignment) { }

        List<ClipEntry> Items()
        {
            lock (ClipboardWatcher.History) return ClipboardWatcher.History.Take(Rows).ToList();
        }

        int RowAtCursor()
        {
            var c = RendererMain.CursorPosition;
            float y = c.Y - (Position.Y + top);
            if (c.X < Position.X || c.X > Position.X + Size.X || y < 0) return -1;
            int i = (int)(y / rowH);
            return i < Rows ? i : -1;
        }

        public override void OnMouseDown()
        {
            base.OnMouseDown();
            var items = Items();
            int i = RowAtCursor();
            if (i >= 0 && i < items.Count)
            {
                ClipboardWatcher.CopyBack(items[i]);
                flashed = items[i];
                flash = 1f;
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            flash = Mathf.Lerp(flash, 0f, 3f * deltaTime);
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            using var paint = GetPaint();
            paint.Color = GetColor(Theme.WidgetBackground).Value();
            canvas.DrawRoundRect(GetRect(), paint);

            Icons.DrawGlyph(canvas, Icons.Clipboard, Position.X + 18, Position.Y + 13, 11, GetColor(Theme.TextThird));
            Gfx.Text(canvas, L.T("Clipboard", "Pano"), Position.X + 30, Position.Y + 7, 10.5f, GetColor(Theme.TextThird), Res.InterBold);

            var items = Items();
            if (items.Count == 0)
            {
                Gfx.Text(canvas, L.T("Copied text appears here", "Kopyaladığınız metinler burada"), Position.X + Size.X / 2, Position.Y + 50, 11,
                    GetColor(Theme.TextSecond), null, SKTextAlign.Center);
                return;
            }

            int hover = IsHovering ? RowAtCursor() : -1;
            for (int i = 0; i < items.Count; i++)
            {
                float y = Position.Y + top + i * rowH;
                var row = SKRect.Create(Position.X + 6, y, Size.X - 12, rowH - 2);

                if (i == hover || items[i] == flashed && flash > 0.05f)
                {
                    float a = items[i] == flashed ? Math.Max(0.08f, 0.35f * flash) : 0.08f;
                    paint.ImageFilter = null;
                    paint.Color = GetColor((items[i] == flashed ? Theme.Success : Theme.TextMain).Override(a: a)).Value();
                    canvas.DrawRoundRect(row, 6, 6, paint);
                }

                string line = items[i].Text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
                Gfx.Text(canvas, Gfx.Fit(line, row.Width - 12, 11), row.Left + 6, y + 3, 11, GetColor(i == 0 ? Theme.TextMain : Theme.TextSecond));
            }
        }
    }
}
