using DynamicWin.Main;
using DynamicWin.UI.Menu.Menus;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.Http;

namespace DynamicWin.Utils
{
    /// <summary>
    /// GitHub Releases'tan yeni sürüm denetimi (#19). Açılışta ve günde bir kez bakar; yeni sürüm varsa
    /// adada haber verir ve tepsi menüsüne "Güncellemeyi indir" ekler. Güvenlik için hiçbir şeyi kendiliğinden
    /// indirip çalıştırmaz; kullanıcı sürüm sayfasını açıp kurulumu kendisi indirir.
    /// Depo <c>settings.updaterepo</c> ("kullanici/depo") ile ayarlanır; boşsa denetim yapılmaz.
    /// </summary>
    public static class UpdateChecker
    {
        public static string? LatestVersion { get; private set; }
        public static string? ReleaseUrl { get; private set; }
        public static event Action? UpdateFound;

        static System.Threading.Timer? timer;

        /// <summary>Varsayılan güncelleme deposu; ayarlarda "settings.updaterepo" ile değiştirilebilir (boş = kapalı).</summary>
        public const string DefaultRepo = "grkm2802/DynamicWin";

        public static string Repo => SaveManager.Contains("settings.updaterepo") ? (string)SaveManager.Get("settings.updaterepo") : DefaultRepo;

        public static void Start()
        {
            if (string.IsNullOrWhiteSpace(Repo)) return;
            timer = new System.Threading.Timer(async _ => await Check(), null, TimeSpan.FromSeconds(30), TimeSpan.FromHours(24));
        }

        static async Task Check()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicWin-UpdateChecker");
                var json = JObject.Parse(await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
                var tag = ((string?)json["tag_name"] ?? "").TrimStart('v', 'V');
                if (!IsNewer(tag, DynamicWinMain.Version)) return;

                LatestVersion = tag;
                ReleaseUrl = (string?)json["html_url"];
                UpdateFound?.Invoke();
                IslandPopups.Show(new PopupInfo
                {
                    Glyph = "", // indir
                    Title = L.T("Update available", "Güncelleme var"),
                    Subtitle = $"DynamicWin {tag} · " + L.T("see tray menu", "tepsi menüsünden indirin"),
                    Accent = Theme.Primary,
                    Seconds = 5f,
                    Key = "update"
                });
            }
            catch (Exception e) { Debug.WriteLine("Güncelleme denetlenemedi: " + e.Message); }
        }

        /// <summary>"1.2.10" &gt; "1.2.9" gibi sayısal karşılaştırma; sondaki harfleri (1.1.0b) yok sayar.</summary>
        public static bool IsNewer(string candidate, string current)
        {
            static int[] Parse(string v) => v.Split('.', '-', '+')
                .Select(p => new string(p.TakeWhile(char.IsDigit).ToArray()))
                .Select(p => int.TryParse(p, out var n) ? n : 0).Take(4).ToArray();
            var a = Parse(candidate); var b = Parse(current);
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }

        public static void OpenReleasePage()
        {
            if (ReleaseUrl != null) Process.Start(new ProcessStartInfo(ReleaseUrl) { UseShellExecute = true });
        }
    }
}
