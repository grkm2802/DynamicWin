using DynamicWin.UI.Menu.Menus;
using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DynamicWin.Utils
{
    /// <summary>
    /// Bluetooth cihazı (kulaklık, hoparlör, mouse...) bağlanınca/kopunca adada uyarı gösterir (yol haritası #9).
    /// Pil yüzdesi, cihaz Windows'a bildiriyorsa (Hands-Free pil özelliği) halka içinde gösterilir.
    /// </summary>
    public static class BluetoothWatcher
    {
        // DEVPKEY_Bluetooth_Battery: Windows'un Ayarlar'da gösterdiği kulaklık pil yüzdesi.
        const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
        const string ContainerKey = "System.Devices.Aep.ContainerId";

        static readonly List<DeviceWatcher> watchers = new List<DeviceWatcher>();
        static readonly Dictionary<string, string> connected = new Dictionary<string, string>(); // id -> ad

        public static void Start()
        {
            try
            {
                Watch(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
                Watch(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
            }
            catch (Exception e) { Debug.WriteLine("Bluetooth izlenemiyor: " + e.Message); }
        }

        static void Watch(string selector)
        {
            var w = DeviceInformation.CreateWatcher(selector, new[] { ContainerKey });
            bool enumerated = false; // bu izleyicinin ilk taraması bitti mi
            w.Added += (_, info) =>
            {
                lock (connected)
                {
                    if (connected.ContainsKey(info.Id)) return;
                    connected[info.Id] = info.Name;
                }
                // İlk taramada zaten bağlı olanlar için uyarı gösterme.
                if (enumerated) Task.Run(() => Announce(info, true));
            };
            w.Removed += (_, upd) =>
            {
                string? name;
                lock (connected)
                {
                    if (!connected.TryGetValue(upd.Id, out name)) return;
                    connected.Remove(upd.Id);
                }
                if (Main.Settings.SystemPopups) IslandPopups.Show(new PopupInfo
                {
                    Glyph = GlyphFor(name), Title = name, Subtitle = L.T("Disconnected", "Bağlantı kesildi"),
                    Accent = Theme.TextSecond, Key = "bt:" + upd.Id
                });
            };
            w.Updated += (_, _) => { };
            w.EnumerationCompleted += (_, _) => enumerated = true;
            w.Start();
            watchers.Add(w);
        }

        static async Task Announce(DeviceInformation info, bool connectedNow)
        {
            int? battery = null;
            try
            {
                if (info.Properties.TryGetValue(ContainerKey, out var c) && c is Guid container)
                {
                    var devices = await DeviceInformation.FindAllAsync(
                        $"System.Devices.ContainerId:=\"{{{container}}}\"", new[] { BatteryKey }, DeviceInformationKind.Device);
                    foreach (var d in devices)
                        if (d.Properties.TryGetValue(BatteryKey, out var v) && v is byte b) { battery = b; break; }
                }
            }
            catch (Exception e) { Debug.WriteLine("Bluetooth pil okunamadı: " + e.Message); }

            if (Main.Settings.SystemPopups) IslandPopups.Show(new PopupInfo
            {
                Glyph = GlyphFor(info.Name),
                Title = info.Name,
                Subtitle = L.T("Connected", "Bağlandı"),
                Accent = Col.FromHex("#3B82F6"),
                Ring = battery.HasValue ? battery.Value / 100f : -1f,
                RingText = battery.HasValue ? battery + "%" : "",
                Key = "bt:" + info.Id,
                Seconds = 3.5f
            });
        }

        static string GlyphFor(string name)
        {
            var n = name.ToLowerInvariant();
            string[] audio = { "bud", "pod", "head", "kulak", "wh-", "wf-", "ear", "jbl", "sound", "speaker", "hoparl" };
            return audio.Any(n.Contains) ? Icons.Headphone : Icons.Bluetooth;
        }
    }
}
