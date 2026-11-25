using DynamicWin.UI.Menu.Menus;
using System.Windows.Forms;

namespace DynamicWin.Utils
{
    /// <summary>
    /// Şarj kablosu takılınca/çıkınca pil yüzdesiyle uyarı (yol haritası #10) ve %20 / %10'da düşük pil uyarısı.
    /// SystemInformation.PowerStatus ucuz bir çağrı; 2 sn'de bir yoklanır.
    /// </summary>
    public static class PowerWatcher
    {
        static System.Threading.Timer? timer;
        static PowerLineStatus lastLine = PowerLineStatus.Unknown;
        static int lastWarned = 101;

        public static void Start()
        {
            var s = SystemInformation.PowerStatus;
            lastLine = s.PowerLineStatus;
            timer = new System.Threading.Timer(_ => Poll(), null, 2000, 2000);
        }

        static void Poll()
        {
            var s = SystemInformation.PowerStatus;
            if (s.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery)) return;

            int pct = (int)Math.Round(s.BatteryLifePercent * 100);
            var line = s.PowerLineStatus;

            if (line != lastLine && lastLine != PowerLineStatus.Unknown)
            {
                bool plugged = line == PowerLineStatus.Online;
                if (Main.Settings.SystemPopups) IslandPopups.Show(new PopupInfo
                {
                    Glyph = plugged ? Icons.BatteryCharging : Icons.Battery,
                    Title = plugged ? L.T("Charging", "Şarj oluyor") : L.T("On battery", "Pilde çalışıyor"),
                    Subtitle = plugged ? RemainingText(s, true) : RemainingText(s, false),
                    Accent = plugged ? Col.FromHex("#34C759") : (pct <= 20 ? Col.FromHex("#FF453A") : Theme.TextSecond),
                    Ring = pct / 100f,
                    RingText = pct + "%",
                    Key = "power",
                    Seconds = 3f
                });
                if (plugged) lastWarned = 101;
            }
            lastLine = line;

            // Düşük pil: pildeyken %20 ve %10 eşiklerini geçince bir kez uyar.
            if (line != PowerLineStatus.Online)
            {
                foreach (int threshold in new[] { 10, 20 })
                {
                    if (pct <= threshold && lastWarned > threshold)
                    {
                        lastWarned = threshold;
                        if (Main.Settings.SystemPopups) IslandPopups.Show(new PopupInfo
                        {
                            Glyph = Icons.Battery,
                            Title = L.T("Low battery", "Pil azaldı"),
                            Subtitle = L.T("Plug in your charger", "Şarj cihazını takın"),
                            Accent = Col.FromHex("#FF453A"),
                            Ring = pct / 100f,
                            RingText = pct + "%",
                            Key = "power",
                            Seconds = 5f
                        });
                        break;
                    }
                }
            }
        }

        static string RemainingText(PowerStatus s, bool charging)
        {
            if (charging) return s.BatteryLifePercent >= 0.995f ? L.T("Fully charged", "Tam dolu") : L.T("Plugged in", "Kablo takıldı");
            if (s.BatteryLifeRemaining > 0)
            {
                var t = TimeSpan.FromSeconds(s.BatteryLifeRemaining);
                return L.T($"{(int)t.TotalHours}h {t.Minutes}m left", $"{(int)t.TotalHours} sa {t.Minutes} dk kaldı");
            }
            return L.T("Charger unplugged", "Kablo çıkarıldı");
        }
    }
}
