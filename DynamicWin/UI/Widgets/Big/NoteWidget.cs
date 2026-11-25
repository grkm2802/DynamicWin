using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using SkiaSharp;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicWin.UI.Widgets.Big
{
    class RegisterNoteWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "Quick Note";
        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
            => new NoteWidget(parent, position, alignment);
    }

    /// <summary>Hızlı not (#13): notun ilk satırlarını gösterir, tıklayınca küçük bir düzenleme penceresi açar.</summary>
    public class NoteWidget : WidgetBase
    {
        public override string TileTitle => L.T("Note", "Not");
        public override string TileGlyph => Icons.Note;
        public override string TileStatus => "";

        public static string NotePath => Path.Combine(SaveManager.SavePath, "note.txt");

        public NoteWidget(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, alignment) { }

        public override void OnMouseUp()
        {
            base.OnMouseUp();
            NoteEditor.Open();
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            using var paint = GetPaint();
            paint.Color = GetColor(Theme.WidgetBackground).Value();
            canvas.DrawRoundRect(GetRect(), paint);

            if (IsHovering)
            {
                paint.ImageFilter = null;
                paint.Color = GetColor(Theme.TextMain.Override(a: 0.05f)).Value();
                canvas.DrawRoundRect(GetRect(), paint);
            }

            Icons.DrawGlyph(canvas, Icons.Note, Position.X + 18, Position.Y + 13, 11, GetColor(Col.FromHex("#FFD60A")));
            Gfx.Text(canvas, L.T("Note", "Not"), Position.X + 30, Position.Y + 7, 10.5f, GetColor(Theme.TextThird), Res.InterBold);

            var text = NoteEditor.CachedText;
            if (string.IsNullOrWhiteSpace(text))
            {
                Gfx.Text(canvas, L.T("Click to write a note", "Not yazmak için tıklayın"), Position.X + Size.X / 2, Position.Y + 50, 11,
                    GetColor(Theme.TextSecond), null, SKTextAlign.Center);
                return;
            }

            var lines = text.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).Take(4).ToList();
            for (int i = 0; i < lines.Count; i++)
                Gfx.Text(canvas, Gfx.Fit(lines[i], Size.X - 24, 11), Position.X + 12, Position.Y + 26 + i * 17, 11,
                    GetColor(i == 0 ? Theme.TextMain : Theme.TextSecond), i == 0 ? Res.InterBold : null);
        }
    }

    /// <summary>Notu düzenlemek için koyu, kenarlıksız küçük pencere. Her değişiklikte kaydeder; Esc veya odak kaybında kapanır.</summary>
    public static class NoteEditor
    {
        static Window? window;
        static string? cached;

        public static string CachedText
        {
            get
            {
                if (cached == null)
                {
                    try { cached = File.Exists(NoteWidget.NotePath) ? File.ReadAllText(NoteWidget.NotePath) : ""; }
                    catch { cached = ""; }
                }
                return cached;
            }
        }

        public static void Open()
        {
            if (window != null) { window.Activate(); return; }

            var box = new TextBox
            {
                Text = CachedText,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                Padding = new Thickness(4)
            };

            var title = new TextBlock
            {
                Text = L.T("Quick note  ·  Esc to close", "Hızlı not  ·  kapatmak için Esc"),
                Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                FontSize = 11,
                Margin = new Thickness(6, 0, 0, 6)
            };

            var panel = new DockPanel();
            DockPanel.SetDock(title, Dock.Top);
            panel.Children.Add(title);
            panel.Children.Add(box);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 18, 18, 18)),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Child = panel
            };

            var main = Main.MainForm.Instance;
            window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                Width = 380,
                Height = 260,
                Content = border,
                Left = main.Left + main.Width / 2 - 190,
                Top = main.Top + 60
            };

            box.TextChanged += (_, _) =>
            {
                cached = box.Text;
                try { File.WriteAllText(NoteWidget.NotePath, box.Text); } catch { }
            };
            window.KeyDown += (_, e) => { if (e.Key == Key.Escape) window?.Close(); };
            window.Deactivated += (_, _) => window?.Close();
            window.Closed += (_, _) => window = null;

            window.Show();
            window.Activate();
            box.Focus();
            box.CaretIndex = box.Text.Length;
        }
    }
}
