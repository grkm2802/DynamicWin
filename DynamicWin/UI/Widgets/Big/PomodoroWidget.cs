using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using SkiaSharp;
using System.Windows.Controls;

namespace DynamicWin.UI.Widgets.Big
{
    class RegisterPomodoroWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => false;
        public string WidgetName => "Pomodoro";
        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
            => new PomodoroWidget(parent, position, alignment);
    }

    public enum PomodoroPhase { Focus, ShortBreak, LongBreak }

    /// <summary>
    /// Pomodoro zamanlayıcısı (#14): 25 dk odak / 5 dk mola, her 4 odakta 15 dk uzun mola.
    /// Durum statik tutulur; widget kapansa da (ada küçülünce) sayaç devam eder ve canlı etkinlik olarak görünür.
    /// </summary>
    public static class Pomodoro
    {
        public static readonly TimeSpan FocusTime = TimeSpan.FromMinutes(25);
        public static readonly TimeSpan ShortBreakTime = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan LongBreakTime = TimeSpan.FromMinutes(15);

        public static PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;
        public static bool Running { get; private set; }
        public static int CompletedFocus { get; private set; }

        static TimeSpan remainingWhenPaused = FocusTime;
        static DateTime endsAt;
        static System.Threading.Timer? ticker;

        public static TimeSpan Length => Phase switch
        {
            PomodoroPhase.ShortBreak => ShortBreakTime,
            PomodoroPhase.LongBreak => LongBreakTime,
            _ => FocusTime
        };

        public static TimeSpan Remaining
        {
            get
            {
                if (!Running) return remainingWhenPaused;
                var r = endsAt - DateTime.Now;
                return r < TimeSpan.Zero ? TimeSpan.Zero : r;
            }
        }

        public static float Progress => 1f - (float)(Remaining.TotalSeconds / Length.TotalSeconds);

        public static string PhaseName => Phase switch
        {
            PomodoroPhase.ShortBreak => L.T("Short break", "Kısa mola"),
            PomodoroPhase.LongBreak => L.T("Long break", "Uzun mola"),
            _ => L.T("Focus", "Odak")
        };

        public static Col PhaseColor => Phase == PomodoroPhase.Focus ? Col.FromHex("#FF6B5B") : Col.FromHex("#34C759");

        public static void Toggle()
        {
            if (Running) { remainingWhenPaused = Remaining; Running = false; }
            else
            {
                endsAt = DateTime.Now + remainingWhenPaused;
                Running = true;
                ticker ??= new System.Threading.Timer(_ => Tick(), null, 500, 500);
            }
        }

        public static void Reset()
        {
            Running = false;
            Phase = PomodoroPhase.Focus;
            CompletedFocus = 0;
            remainingWhenPaused = FocusTime;
        }

        public static void Skip() => NextPhase(announce: false);

        static void Tick()
        {
            if (Running && Remaining <= TimeSpan.Zero) NextPhase(announce: true);
        }

        static void NextPhase(bool announce)
        {
            bool wasFocus = Phase == PomodoroPhase.Focus;
            if (wasFocus) CompletedFocus++;
            Phase = wasFocus ? (CompletedFocus % 4 == 0 ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak) : PomodoroPhase.Focus;

            remainingWhenPaused = Length;
            bool keepRunning = Running;
            Running = false;
            if (keepRunning) Toggle(); // bir sonraki aşama otomatik başlasın

            if (announce)
            {
                System.Media.SystemSounds.Asterisk.Play();
                IslandPopups.Show(new PopupInfo
                {
                    Glyph = wasFocus ? Icons.Coffee : Icons.Timer,
                    Title = wasFocus ? L.T("Time for a break", "Mola zamanı") : L.T("Back to focus", "Odaklanma zamanı"),
                    Subtitle = wasFocus
                        ? L.T($"{CompletedFocus} focus sessions done · {(int)Length.TotalMinutes} min break", $"{CompletedFocus} odak tamamlandı · {(int)Length.TotalMinutes} dk mola")
                        : L.T("25 minutes of focus", "25 dakika odak"),
                    Accent = PhaseColor,
                    Seconds = 6f,
                    Key = "pomodoro"
                });
            }
        }
    }

    public class PomodoroLiveActivity : LiveActivity
    {
        public override string Id => "pomodoro";
        public override bool IsActive => Pomodoro.Running;
        public override int Priority => 12;
        public override Col Accent => Pomodoro.PhaseColor;
        public override string ShortText => Gfx.Time(Pomodoro.Remaining);
    }

    public class PomodoroWidget : WidgetBase
    {
        public override string TileTitle => "Pomodoro";
        public override string TileGlyph => Icons.Timer;
        public override string TileStatus => Pomodoro.Running ? Gfx.Time(Pomodoro.Remaining) : "";

        float shownProgress;

        public PomodoroWidget(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, alignment)
        {
            LiveActivityManager.Register(new PomodoroLiveActivity());
        }

        public override void OnMouseUp()
        {
            base.OnMouseUp();
            Pomodoro.Toggle();
        }

        public override ContextMenu? GetContextMenu()
        {
            var baseMenu = base.GetContextMenu();
            if (baseMenu != null) return baseMenu; // widget düzenleme modundaysa onun menüsü

            var ctx = new ContextMenu();
            var skip = new MenuItem { Header = L.T("Skip to next phase", "Sonraki aşamaya geç") };
            skip.Click += (_, _) => Pomodoro.Skip();
            var reset = new MenuItem { Header = L.T("Reset", "Sıfırla") };
            reset.Click += (_, _) => Pomodoro.Reset();
            ctx.Items.Add(skip);
            ctx.Items.Add(reset);
            return ctx;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            shownProgress = Mathf.Lerp(shownProgress, Pomodoro.Progress, 6f * deltaTime);
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            using var paint = GetPaint();
            paint.Color = GetColor(Theme.WidgetBackground).Value();
            canvas.DrawRoundRect(GetRect(), paint);

            var col = Pomodoro.PhaseColor;
            float cx = Position.X + 50, cy = Position.Y + Size.Y / 2, r = 30;

            using var ring = new SKPaint { IsAntialias = true, IsStroke = true, StrokeWidth = 5, StrokeCap = SKStrokeCap.Round };
            ring.Color = GetColor(Theme.TextMain.Override(a: 0.1f)).Value();
            canvas.DrawCircle(cx, cy, r, ring);
            ring.Color = GetColor(col).Value();
            using (var path = new SKPath())
            {
                path.AddArc(SKRect.Create(cx - r, cy - r, r * 2, r * 2), -90, 360 * Math.Clamp(shownProgress, 0.001f, 1f));
                canvas.DrawPath(path, ring);
            }
            Icons.DrawGlyph(canvas, Pomodoro.Running ? "" : "", cx, cy, 16, GetColor(col)); // duraklat / oynat

            float tx = Position.X + 94;
            Gfx.Text(canvas, Pomodoro.PhaseName, tx, Position.Y + 22, 11, GetColor(Theme.TextSecond));
            Gfx.Text(canvas, Gfx.Time(Pomodoro.Remaining), tx, Position.Y + 38, 22, GetColor(Theme.TextMain), Res.InterBold);
            string hint = Pomodoro.Running ? L.T("Click to pause", "Durdurmak için tıkla") : L.T("Click to start", "Başlatmak için tıkla");
            Gfx.Text(canvas, Gfx.Fit(hint, Size.X - (tx - Position.X) - 8, 9.5f), tx, Position.Y + 68, 9.5f, GetColor(Theme.TextThird));
        }
    }
}
