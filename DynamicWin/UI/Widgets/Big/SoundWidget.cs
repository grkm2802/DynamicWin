using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using SkiaSharp;
using System.Windows.Input;

namespace DynamicWin.UI.Widgets.Big
{
    class RegisterSoundWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "Sound";
        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
            => new SoundWidget(parent, position, alignment);
    }

    /// <summary>
    /// Ses (kullanıcı isteği): ses seviyesi kaydırıcısı (sürükle veya tıkla), sessiz düğmesi ve çıkış cihazı seçimi.
    /// Genişletilmiş adada "Ses" kutusu olarak durur; kutuda anlık seviye yazar. Veriyi <see cref="AudioService"/> sağlar.
    /// </summary>
    public class SoundWidget : WidgetBase
    {
        public override string TileTitle => L.T("Sound", "Ses");
        public override string TileGlyph => AudioService.Muted ? Icons.Mute : Icons.Volume;
        public override string TileStatus => AudioService.Muted ? L.T("muted", "sessiz") : $"%{(int)Math.Round(AudioService.Volume * 100)}";

        const float W = 412f;
        const float RowH = 26f;
        const int MaxRows = 6;
        const float ListTop = 62f;

        static readonly Col Accent = Col.FromHex("#0A84FF");

        bool dragging;
        float shownVolume;
        int lastDeviceCount = -1;

        public SoundWidget(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, alignment)
        {
            AudioService.Start();
            shownVolume = AudioService.Volume;
        }

        int Rows => Math.Min(MaxRows, Math.Max(1, AudioService.Outputs.Count));

        protected override float GetWidgetWidth() => W;
        protected override float GetWidgetHeight() => ListTop + Rows * RowH + 8;

        // ---- Bölgeler ----
        SKRect MuteRect => SKRect.Create(Position.X + 10, Position.Y + 10, 30, 30);
        SKRect SliderRect => SKRect.Create(Position.X + 52, Position.Y + 22, Size.X - 52 - 60, 6);
        SKRect RowRect(int i) => SKRect.Create(Position.X + 8, Position.Y + ListTop + i * RowH, Size.X - 16, RowH - 3);

        static bool Hit(SKRect r, Vec2 c, float pad = 0) =>
            c.X >= r.Left - pad && c.X <= r.Right + pad && c.Y >= r.Top - pad && c.Y <= r.Bottom + pad;

        public override void OnMouseDown()
        {
            base.OnMouseDown();
            var c = RendererMain.CursorPosition;

            if (Hit(MuteRect, c)) { AudioService.ToggleMute(); return; }

            if (Hit(SliderRect, c, 9))
            {
                dragging = true;
                SetFromCursor(c);
                return;
            }

            var outs = AudioService.Outputs;
            for (int i = 0; i < Math.Min(outs.Count, MaxRows); i++)
            {
                if (Hit(RowRect(i), c))
                {
                    if (outs[i].Id != AudioService.DefaultId) AudioService.SetDefault(outs[i].Id);
                    return;
                }
            }
        }

        void SetFromCursor(Vec2 c)
        {
            var s = SliderRect;
            float v = (c.X - s.Left) / s.Width;
            v = (float)Math.Round(Math.Clamp(v, 0f, 1f) * 100) / 100f; // %1 adımlar
            if (Math.Abs(v - AudioService.Volume) >= 0.005f) AudioService.SetVolume(v);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (dragging)
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed) SetFromCursor(RendererMain.CursorPosition);
                else dragging = false;
            }

            shownVolume = dragging ? AudioService.Volume : Mathf.Lerp(shownVolume, AudioService.Volume, 12f * deltaTime);

            // Widget ilk açıldığında cihaz listesini tazele (Windows bildirimleri ara sıra kaçabiliyor).
            if (IsEnabled && lastDeviceCount < 0) { AudioService.RefreshOutputs(); lastDeviceCount = AudioService.Outputs.Count; }
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            using var paint = GetPaint();
            paint.Color = GetColor(Theme.WidgetBackground).Value();
            canvas.DrawRoundRect(GetRect(), paint);
            paint.ImageFilter = null;

            var c = RendererMain.CursorPosition;

            // Sessiz düğmesi
            var mr = MuteRect;
            if (IsHovering && Hit(mr, c))
            {
                paint.Color = GetColor(Theme.TextMain.Override(a: 0.08f)).Value();
                canvas.DrawRoundRect(mr, 15, 15, paint);
            }
            Icons.DrawGlyph(canvas, AudioService.Muted ? Icons.Mute : Icons.Volume, mr.MidX, mr.MidY, 15,
                GetColor(AudioService.Muted ? Col.FromHex("#FF453A") : Theme.TextMain));

            // Kaydırıcı
            var s = SliderRect;
            bool hoverSlider = dragging || IsHovering && Hit(s, c, 9);
            if (hoverSlider) s.Inflate(0, 1.5f);
            var fill = AudioService.Muted ? Theme.TextThird : Accent;
            Gfx.Bar(canvas, s, shownVolume, GetColor(Theme.TextMain.Override(a: 0.15f)), GetColor(fill));
            using (var knob = new SKPaint { IsAntialias = true, Color = GetColor(Col.White).Value() })
                canvas.DrawCircle(s.Left + s.Width * Math.Clamp(shownVolume, 0, 1), s.MidY, hoverSlider ? 7.5f : 6f, knob);

            string pct = AudioService.Muted ? L.T("Muted", "Sessiz") : $"%{(int)Math.Round(shownVolume * 100)}";
            Gfx.Text(canvas, pct, Position.X + Size.X - 14, Position.Y + 17, 13, GetColor(Theme.TextMain), Res.InterBold, SKTextAlign.Right);

            // Çıkış cihazları
            Gfx.Text(canvas, L.T("Output", "Çıkış"), Position.X + 14, Position.Y + 44, 10, GetColor(Theme.TextThird), Res.InterBold);

            var outs = AudioService.Outputs;
            if (outs.Count == 0)
            {
                Gfx.Text(canvas, L.T("No output device found", "Çıkış cihazı bulunamadı"), Position.X + 14, Position.Y + ListTop + 5, 11, GetColor(Theme.TextSecond));
                return;
            }

            for (int i = 0; i < Math.Min(outs.Count, MaxRows); i++)
            {
                var r = RowRect(i);
                bool selected = outs[i].Id == AudioService.DefaultId;
                bool hover = IsHovering && Hit(r, c);

                if (selected || hover)
                {
                    paint.Color = GetColor((selected ? Accent : Theme.TextMain).Override(a: selected ? 0.16f : 0.06f)).Value();
                    canvas.DrawRoundRect(r, 8, 8, paint);
                }

                Icons.DrawGlyph(canvas, GlyphFor(outs[i].Name), r.Left + 15, r.MidY, 12, GetColor(selected ? Accent : Theme.TextSecond));
                Gfx.Text(canvas, Gfx.Fit(outs[i].Name, r.Width - 60, 11.5f, selected ? Res.InterBold : null), r.Left + 32, r.MidY - 7, 11.5f,
                    GetColor(selected ? Theme.TextMain : Theme.TextSecond), selected ? Res.InterBold : null);
                if (selected)
                    Icons.DrawGlyph(canvas, "", r.Right - 14, r.MidY, 11, GetColor(Accent)); // onay işareti
            }
        }

        static string GlyphFor(string name)
        {
            var n = name.ToLowerInvariant();
            if (new[] { "head", "kulak", "bud", "pod", "earphone", "wh-", "wf-" }.Any(n.Contains)) return Icons.Headphone;
            if (n.Contains("bluetooth")) return Icons.Bluetooth;
            if (new[] { "hdmi", "display", "monitor", "nvidia", "tv" }.Any(n.Contains)) return ""; // ekran
            return Icons.Volume;
        }
    }
}
