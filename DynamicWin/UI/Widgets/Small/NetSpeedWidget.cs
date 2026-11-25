using DynamicWin.Resources;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using SkiaSharp;
using System.Net.NetworkInformation;

namespace DynamicWin.UI.Widgets.Small
{
    class RegisterNetSpeedWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => true;
        public string WidgetName => "Network Speed";
        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
            => new NetSpeedWidget(parent, position, alignment);
    }

    /// <summary>Saniyede bir tüm ağ arayüzlerinin bayt sayaçlarından indirme/yükleme hızı hesaplar.</summary>
    public static class NetSpeed
    {
        public static double DownBps { get; private set; }
        public static double UpBps { get; private set; }

        /// <summary>Son 60 saniyenin (indirme, yükleme) örnekleri; grafik için.</summary>
        public static readonly List<(double down, double up)> History = new List<(double, double)>();

        static System.Threading.Timer? timer;
        static long lastRx = -1, lastTx = -1;
        static DateTime lastAt;

        public static void Start()
        {
            timer ??= new System.Threading.Timer(_ => Sample(), null, 0, 1000);
        }

        static void Sample()
        {
            try
            {
                long rx = 0, tx = 0;
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                    var st = ni.GetIPStatistics();
                    rx += st.BytesReceived; tx += st.BytesSent;
                }
                var now = DateTime.Now;
                if (lastRx >= 0)
                {
                    double sec = Math.Max(0.2, (now - lastAt).TotalSeconds);
                    DownBps = Math.Max(0, (rx - lastRx) / sec);
                    UpBps = Math.Max(0, (tx - lastTx) / sec);
                    lock (History)
                    {
                        History.Add((DownBps, UpBps));
                        if (History.Count > 60) History.RemoveAt(0);
                    }
                }
                lastRx = rx; lastTx = tx; lastAt = now;
            }
            catch { }
        }

        public static string Format(double bps)
        {
            if (bps >= 1024 * 1024) return (bps / 1024 / 1024).ToString(bps >= 10 * 1024 * 1024 ? "0" : "0.0") + " MB/s";
            if (bps >= 1024) return (bps / 1024).ToString("0") + " KB/s";
            return bps.ToString("0") + " B/s";
        }
    }

    /// <summary>
    /// Ağ hızı (#15). Ada sade kalsın diye sadece trafik varken (toplam > 100 KB/s) görünür; boştayken genişliği 0.
    /// </summary>
    public class NetSpeedWidget : SmallWidgetBase
    {
        const double ShowAbove = 100 * 1024;
        float width;
        string text = "";

        public NetSpeedWidget(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, alignment)
        {
            NetSpeed.Start();
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            bool show = NetSpeed.DownBps + NetSpeed.UpBps > ShowAbove;
            text = "↓" + NetSpeed.Format(NetSpeed.DownBps) + "  ↑" + NetSpeed.Format(NetSpeed.UpBps);
            float target = show ? Gfx.Measure(text, 10.5f) + 8 : 0;
            width = Mathf.Lerp(width, target, 8f * deltaTime);
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            if (width < 4) return;
            int save = canvas.Save();
            canvas.ClipRect(SKRect.Create(Position.X, Position.Y - 10, width, Size.Y + 20));
            Gfx.Text(canvas, text, Position.X + 4, Position.Y + Size.Y / 2 - 6, 10.5f, GetColor(Theme.TextSecond));
            canvas.RestoreToCount(save);
        }

        protected override float GetWidgetWidth() => width;
    }
}
