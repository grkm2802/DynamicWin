using DynamicWin.Main;
using DynamicWin.UI.Menu.Menus;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using System.Diagnostics;

namespace DynamicWin.Utils
{
    /// <summary>
    /// Küçük sistem olayları için ada uyarıları:
    ///  #21 Kilit açılınca (Windows Hello / PIN) "hoş geldiniz" animasyonu
    ///  #22 Hoparlör veya mikrofon sessize alınınca / açılınca
    ///  #23 Rahatsız Etmeyin (Odak) açılıp kapanınca
    ///  #24 Ekran kaydı / paylaşımı başlayınca; sürerken canlı etkinlik (kırmızı nokta + süre)
    /// </summary>
    public static class SystemStateWatcher
    {
        static System.Threading.Timer? timer;
        static bool? lastDnd;
        static string? captureApp;
        static DateTime captureSince;
        static bool? speakerMuted, micMuted;

        public static string? CaptureApp => captureApp;
        public static DateTime CaptureSince => captureSince;

        public static void Start()
        {
            // #21
            SystemEvents.SessionSwitch += (_, e) =>
            {
                if (e.Reason == SessionSwitchReason.SessionUnlock)
                    Show(new PopupInfo
                    {
                        Glyph = Icons.Face,
                        Title = L.T("Welcome back", "Tekrar hoş geldiniz"),
                        Subtitle = L.T("Unlocked", "Kilit açıldı"),
                        Accent = Col.FromHex("#34C759"),
                        Seconds = 2.2f,
                        Key = "unlock"
                    });
            };

            // #22
            HookMute(DynamicWinMain.defaultDevice, isMic: false);
            // Çıkış cihazı değişince sessiz uyarısı yeni cihazı izlesin.
            AudioService.DefaultChanged += d => HookMute(d, isMic: false);
            HookMute(DynamicWinMain.defaultMicrophone, isMic: true);

            // #23, #24: kayıt defteri yoklaması (2 sn)
            lastDnd = ReadDnd();
            captureApp = DeviceUsageChecker.GetScreenCaptureApp();
            if (captureApp != null) captureSince = DateTime.Now;
            timer = new System.Threading.Timer(_ => Poll(), null, 2000, 2000);

            LiveActivityManager.Register(new ScreenRecordingActivity());
        }

        static void Show(PopupInfo p)
        {
            if (Settings.SystemPopups) IslandPopups.Show(p);
        }

        static void HookMute(MMDevice? device, bool isMic)
        {
            if (device == null) return;
            try
            {
                var vol = device.AudioEndpointVolume;
                if (isMic) micMuted = vol.Mute; else speakerMuted = vol.Mute;
                vol.OnVolumeNotification += data =>
                {
                    bool muted = data.Muted;
                    ref bool? last = ref isMic ? ref micMuted : ref speakerMuted;
                    if (last == muted) return; // sadece ses seviyesi değişti
                    last = muted;
                    // Hoparlör için V1'in ses göstergesi (VolumeAdjustMenu) açıksa o zaten "Silent" gösteriyor; çift uyarı olmasın.
                    if (!isMic && PopupOptions.saveData.volumePopup) return;
                    Show(new PopupInfo
                    {
                        Glyph = isMic ? (muted ? Icons.MicOff : Icons.Mic) : (muted ? Icons.Mute : Icons.Volume),
                        Title = isMic
                            ? (muted ? L.T("Microphone muted", "Mikrofon kapatıldı") : L.T("Microphone on", "Mikrofon açıldı"))
                            : (muted ? L.T("Sound muted", "Ses kapatıldı") : L.T("Sound on", "Ses açıldı")),
                        Accent = muted ? Col.FromHex("#FF453A") : Col.FromHex("#34C759"),
                        Seconds = 1.8f,
                        Key = isMic ? "mic" : "speaker"
                    });
                };
            }
            catch (Exception e) { Debug.WriteLine("Sessiz izlenemiyor: " + e.Message); }
        }

        /// <summary>Windows 11 Rahatsız Etmeyin: NOC_GLOBAL_SETTING_TOASTS_ENABLED = 0 ise açık. Değer yoksa kapalı.</summary>
        static bool ReadDnd()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings");
                return k?.GetValue("NOC_GLOBAL_SETTING_TOASTS_ENABLED") is int v && v == 0;
            }
            catch { return false; }
        }

        static void Poll()
        {
            bool dnd = ReadDnd();
            if (lastDnd.HasValue && dnd != lastDnd.Value)
                Show(new PopupInfo
                {
                    Glyph = Icons.Moon,
                    Title = dnd ? L.T("Do not disturb on", "Rahatsız Etmeyin açık") : L.T("Do not disturb off", "Rahatsız Etmeyin kapalı"),
                    Subtitle = dnd ? L.T("Notifications are silenced", "Bildirimler sessize alındı") : L.T("Notifications are back", "Bildirimler yeniden açık"),
                    Accent = Col.FromHex("#5E5CE6"),
                    Seconds = 2.5f,
                    Key = "dnd"
                });
            lastDnd = dnd;

            var app = DeviceUsageChecker.GetScreenCaptureApp();
            if (app != null && captureApp == null)
            {
                captureSince = DateTime.Now;
                Show(new PopupInfo
                {
                    Glyph = Icons.Record,
                    Title = L.T("Screen is being captured", "Ekran kaydediliyor"),
                    Subtitle = app,
                    Accent = Col.FromHex("#FF453A"),
                    Seconds = 3f,
                    Key = "capture"
                });
            }
            captureApp = app;
        }
    }

    /// <summary>#24: ekran kaydı/paylaşımı sürerken ayrık balonda kırmızı nokta + süre.</summary>
    public class ScreenRecordingActivity : LiveActivity
    {
        public override string Id => "screencapture";
        public override bool IsActive => SystemStateWatcher.CaptureApp != null && Settings.SystemPopups;
        public override int Priority => 5;
        public override Col Accent => Col.FromHex("#FF453A");
        public override string ShortText => Gfx.Time(DateTime.Now - SystemStateWatcher.CaptureSince);
        public override bool NeedsAttention => true;
    }
}
