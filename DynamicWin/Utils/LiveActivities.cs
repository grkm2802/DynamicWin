using DynamicWin.UI.Widgets.Big;
using DynamicWin.UI.Widgets.Small;
using System.Diagnostics;

namespace DynamicWin.Utils
{
    /// <summary>
    /// iPhone'daki Live Activity karşılığı: süren bir iş (Claude, zamanlayıcı, medya...).
    /// Yeni bir etkinlik eklemek için bu sınıftan türetip <see cref="LiveActivityManager.Register"/> çağır.
    /// </summary>
    public abstract class LiveActivity
    {
        /// <summary>Sabit kimlik; ayarlar ve sıralama için.</summary>
        public abstract string Id { get; }

        /// <summary>Şu an gösterilecek bir şey var mı.</summary>
        public abstract bool IsActive { get; }

        /// <summary>Küçük sayı = daha önemli. Dikkat isteyen durumlarda düşürülebilir.</summary>
        public abstract int Priority { get; }

        /// <summary>Etkinliğin vurgu rengi (nokta / kenarlık).</summary>
        public abstract Col Accent { get; }

        /// <summary>Minimal görünümde (ayrık balon) gösterilen kısa metin. Boş olabilir.</summary>
        public abstract string ShortText { get; }

        /// <summary>
        /// Bu etkinliği zaten adada gösteren küçük widget'ın kayıt adı (ör. "...RegisterClaudeCodeWidget").
        /// Widget adadaysa etkinlik ayrık balona taşınmaz, tekrar gösterilmez.
        /// </summary>
        public virtual string? RepresentedByWidget => null;

        /// <summary>Dikkat istiyor mu (balon nabız gibi atar).</summary>
        public virtual bool NeedsAttention => false;

        /// <summary>False ise etkinlik hiçbir zaman ayrık balonda gösterilmez (ör. medya; kullanıcı istemedi).</summary>
        public virtual bool AllowBubble => true;
    }

    public static class LiveActivityManager
    {
        static readonly List<LiveActivity> activities = new List<LiveActivity>();

        public static void Register(LiveActivity activity)
        {
            if (activities.Any(a => a.Id == activity.Id)) return;
            activities.Add(activity);
        }

        public static void RegisterBuiltIns()
        {
            Register(new ClaudeLiveActivity());
            Register(new TimerLiveActivity());
            Register(new MediaLiveActivity());
            Register(new UI.Widgets.Big.PomodoroLiveActivity());
        }

        /// <summary>Aktif etkinlikler, önem sırasına göre.</summary>
        public static List<LiveActivity> GetActive()
        {
            var result = new List<LiveActivity>();
            foreach (var a in activities)
            {
                try { if (a.IsActive) result.Add(a); }
                catch (Exception e) { Debug.WriteLine($"LiveActivity {a.Id}: {e.Message}"); }
            }
            return result.OrderBy(a => a.Priority).ToList();
        }

        /// <summary>Adada bir widget'ı olmayan, yani ayrık balonda gösterilmesi gereken en önemli etkinlik.</summary>
        public static LiveActivity? GetDetached()
        {
            var shown = new HashSet<string>(Main.Settings.smallWidgetsLeft
                .Concat(Main.Settings.smallWidgetsRight)
                .Concat(Main.Settings.smallWidgetsMiddle));

            return GetActive().FirstOrDefault(a => a.AllowBubble &&
                (a.RepresentedByWidget == null || !shown.Contains(a.RepresentedByWidget)));
        }
    }

    // ---------------- Yerleşik etkinlikler ----------------

    public class ClaudeLiveActivity : LiveActivity
    {
        ClaudeSession? cached;
        int count;
        DateTime cachedAt;

        ClaudeSession? Session
        {
            get
            {
                // Aynı karede birden çok özellik okunuyor; kilidi her seferinde almamak için kısa önbellek.
                if ((DateTime.Now - cachedAt).TotalMilliseconds > 100)
                {
                    cached = ClaudeBridge.GetPrimary(out count);
                    cachedAt = DateTime.Now;
                }
                return cached;
            }
        }

        public override string Id => "claude";
        public override bool IsActive => Session != null;
        public override int Priority => Session?.State == ClaudeState.WaitingPermission ? 0 : 20;
        public override Col Accent => Session != null ? ClaudeCodeWidget.ColorFor(Session.State) : ClaudeCodeWidget.ClaudeOrange;
        public override string ShortText => Session?.State switch
        {
            ClaudeState.WaitingPermission => "İzin",
            ClaudeState.WaitingInput => "Bekliyor",
            ClaudeState.Done => "Bitti",
            _ => "Claude"
        };
        public override bool NeedsAttention => Session?.State == ClaudeState.WaitingPermission;
        public override string? RepresentedByWidget => "DynamicWin.UI.Widgets.Small.RegisterClaudeCodeWidget";
    }

    public class TimerLiveActivity : LiveActivity
    {
        public override string Id => "timer";
        public override bool IsActive => TimerWidget.instance != null && TimerWidget.instance.IsTimerRunning;
        public override int Priority => 10;
        public override Col Accent => Theme.Primary;
        public override string ShortText
        {
            get
            {
                if (TimerWidget.instance == null) return "";
                var t = TimeSpan.FromSeconds(Math.Max(0, TimerWidget.instance.CurrentTime));
                return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
            }
        }
        public override string? RepresentedByWidget => "DynamicWin.UI.Widgets.Small.RegisterActiveTimerWidget";
    }

    /// <summary>
    /// Windows'un genel medya oturumu (Spotify, tarayıcı, Groove, VLC...). Spotify pencere başlığına
    /// bağlı eski yöntemden farklı olarak her uygulamayı görür. Arka planda saniyede bir yoklanır.
    /// </summary>
    public class MediaLiveActivity : LiveActivity
    {
        public MediaLiveActivity() { MediaSession.Start(); }

        public override string Id => "media";
        public override bool IsActive => MediaSession.IsPlaying;
        public override int Priority => 30;
        public override Col Accent => MediaSession.AccentColor;
        public override string ShortText => DynamicWin.UI.UIElements.DWText.Truncate(MediaSession.Title, 18);
        public override string? RepresentedByWidget => "DynamicWin.UI.Widgets.Small.RegisterSmallVisualizerWidget";
        // Müzik adanın yanında ayrı balon olarak görünmesin (kullanıcı tercihi, 26.11.2026).
        public override bool AllowBubble => false;
    }
}
