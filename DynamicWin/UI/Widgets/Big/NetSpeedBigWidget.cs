using DynamicWin.Resources;
using DynamicWin.UI.UIElements;
using DynamicWin.UI.Widgets.Small;
using DynamicWin.Utils;
using SkiaSharp;

namespace DynamicWin.UI.Widgets.Big
{
    class RegisterNetSpeedBigWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "Network Speed (on demand)";
        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
            => new NetSpeedBigWidget(parent, position, alignment);
    }

    /// <summary>
    /// Ağ hızı, sadece istenince (#15, kullanıcı isteği): genişletilmiş adada "Ağ hızı" kutusu olarak durur,
    /// tıklanınca anlık indirme/yükleme hızı ve son 60 saniyenin grafiği görünür. Ada küçükken hiç görünmez.
    /// </summary>
    public class NetSpeedBigWidget : WidgetBase
    {
        public override string TileTitle => L.T("Network", "Ağ hızı");
        public override string TileGlyph => "";

        static readonly Col DownCol = Col.FromHex("#0A84FF");
        static readonly Col UpCol = Col.FromHex("#BF5AF2");

        public NetSpeedBigWidget(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, alignment)
        {
            NetSpeed.Start();
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            using var paint = GetPaint();
            paint.Color = GetColor(Theme.WidgetBackground).Value();
            canvas.DrawRoundRect(GetRect(), paint);

            float x = Position.X + 12, y = Position.Y + 10;
            Gfx.Text(canvas, "↓ " + L.T("Download", "İndirme"), x, y, 9.5f, GetColor(DownCol));
            Gfx.Text(canvas, NetSpeed.Format(NetSpeed.DownBps), x, y + 13, 16, GetColor(Theme.TextMain), Res.InterBold);
            float x2 = Position.X + Size.X / 2 + 6;
            Gfx.Text(canvas, "↑ " + L.T("Upload", "Yükleme"), x2, y, 9.5f, GetColor(UpCol));
            Gfx.Text(canvas, NetSpeed.Format(NetSpeed.UpBps), x2, y + 13, 16, GetColor(Theme.TextMain), Res.InterBold);

            // Son 60 sn grafiği
            List<(double down, double up)> h;
            lock (NetSpeed.History) h = NetSpeed.History.ToList();
            var area = SKRect.Create(Position.X + 12, Position.Y + 50, Size.X - 24, Size.Y - 60);
            if (h.Count < 2) return;

            double max = Math.Max(64 * 1024, h.Max(p => Math.Max(p.down, p.up)));
            DrawLine(canvas, h.Select(p => p.down).ToList(), area, max, DownCol, fill: true);
            DrawLine(canvas, h.Select(p => p.up).ToList(), area, max, UpCol, fill: false);
        }

        void DrawLine(SKCanvas canvas, List<double> values, SKRect area, double max, Col col, bool fill)
        {
            using var path = new SKPath();
            float step = area.Width / 59f;
            float startX = area.Right - (values.Count - 1) * step;
            for (int i = 0; i < values.Count; i++)
            {
                float px = startX + i * step;
                float py = area.Bottom - (float)(values[i] / max) * area.Height;
                if (i == 0) path.MoveTo(px, py); else path.LineTo(px, py);
            }

            using var p = new SKPaint { IsAntialias = true };
            if (fill)
            {
                using var f = new SKPath(path);
                f.LineTo(area.Right, area.Bottom);
                f.LineTo(startX, area.Bottom);
                f.Close();
                p.Color = GetColor(col.Override(a: 0.18f)).Value();
                canvas.DrawPath(f, p);
            }
            p.IsStroke = true; p.StrokeWidth = 1.5f;
            p.Color = GetColor(col).Value();
            canvas.DrawPath(path, p);
        }
    }
}
