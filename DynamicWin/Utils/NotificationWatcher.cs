using System.Collections.Concurrent;
using System.Diagnostics;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicWin.Utils
{
    public class IslandNotification
    {
        public string App = "";
        public string Title = "";
        public string Body = "";
        public DateTime Received = DateTime.Now;
    }

    /// <summary>
    /// Windows'un bildirim merkezine düşen yeni toast bildirimlerini adada göstermek için yakalar.
    /// Paketlenmemiş (MSIX olmayan) uygulamada NotificationChanged olayı çalışmaz; bu yüzden
    /// GetNotificationsAsync 1.5 sn'de bir yoklanır ve daha önce görülmemiş kimlikler yeni sayılır.
    /// İlk açılışta merkezde zaten duran bildirimler gösterilmez.
    /// </summary>
    public static class NotificationWatcher
    {
        public static readonly ConcurrentQueue<IslandNotification> Queue = new ConcurrentQueue<IslandNotification>();

        /// <summary>Bu süreden eski kuyruk öğeleri gösterilmez (ör. tam ekrandan dönünce eski bildirim yağmuru olmasın).</summary>
        public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(20);

        public static string Status { get; private set; } = "Başlatılmadı";

        static readonly HashSet<uint> seen = new HashSet<uint>();
        static bool started;

        public static void Start()
        {
            if (started) return;
            started = true;
            Task.Run(Loop);
        }

        static async Task Loop()
        {
            UserNotificationListener listener;
            try
            {
                listener = UserNotificationListener.Current;
                var access = await listener.RequestAccessAsync();
                if (access != UserNotificationListenerAccessStatus.Allowed)
                {
                    Status = "İzin yok: " + access + " (Ayarlar › Gizlilik › Bildirimler)";
                    Debug.WriteLine(Status);
                    return;
                }
            }
            catch (Exception e)
            {
                Status = "Bildirim API'si kullanılamıyor: " + e.Message;
                Debug.WriteLine(Status);
                return;
            }

            bool first = true;
            Status = "Çalışıyor";

            while (true)
            {
                try
                {
                    var list = await listener.GetNotificationsAsync(NotificationKinds.Toast);
                    var current = new HashSet<uint>();

                    foreach (var n in list)
                    {
                        current.Add(n.Id);
                        if (seen.Contains(n.Id)) continue;
                        seen.Add(n.Id);

                        if (first || !Main.Settings.ShowNotifications) continue;

                        var item = Convert(n);
                        if (item != null) Queue.Enqueue(item);
                    }

                    // Merkezden silinenleri unut ki küme sonsuza kadar büyümesin.
                    seen.IntersectWith(current);
                    first = false;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Bildirimler okunamadı: " + e.Message);
                }

                await Task.Delay(1500);
            }
        }

        static IslandNotification? Convert(UserNotification n)
        {
            string app = "";
            try { app = n.AppInfo?.DisplayInfo?.DisplayName ?? ""; } catch { }

            var binding = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (binding == null) return null;

            var texts = binding.GetTextElements().Select(t => t.Text?.Trim() ?? "").Where(t => t.Length > 0).ToList();
            if (texts.Count == 0) return null;

            return new IslandNotification
            {
                App = app,
                Title = texts[0],
                Body = string.Join(" ", texts.Skip(1)),
                Received = DateTime.Now
            };
        }

        /// <summary>Gösterilecek bir sonraki (çok eski olmayan) bildirim.</summary>
        public static bool TryDequeueFresh(out IslandNotification? notification)
        {
            while (Queue.TryDequeue(out var n))
            {
                if (DateTime.Now - n.Received <= MaxAge)
                {
                    notification = n;
                    return true;
                }
            }
            notification = null;
            return false;
        }
    }
}
