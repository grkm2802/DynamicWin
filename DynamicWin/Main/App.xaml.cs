using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.Utils;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace DynamicWin
{
    public partial class DynamicWinMain : Application
    {
        public static MMDevice defaultDevice;
        public static MMDevice defaultMicrophone;

        public static string Version => "1.4.3";

        [STAThread]
        public static void Main()
        {
            DynamicWinMain m = new DynamicWinMain();
            m.Run();
        }

        /// <param name="userChanged">
        /// true: kullanıcı Ayarlar'da değiştirdi → kapalıysa başlangıç kaydını sil.
        /// false (uygulama açılışı): sadece açıksa kaydı oluştur/güncelle, ASLA silme. Eskiden açılışta "kapalı"
        /// görünen bir ayar (ör. kurulumdan önce oluşmuş ayar dosyası) kurulumun yazdığı başlangıç kaydını siliyordu.
        /// </param>
        public static void UpdateStartup(bool userChanged = false)
        {
            try
            {
                // Açılışta: sistemde gerçek bir başlangıç kaydı varsa ayarı ona uydur (ayar dosyası eskimiş olabilir).
                if (!userChanged && !Settings.RunOnStartup && StartupShortcutManager.IsRegistered())
                {
                    Settings.RunOnStartup = true;
                    Settings.Save();
                    Log.Write("Başlangıç kaydı bulundu; 'açılışta başlat' ayarı açıldı.");
                }

                if (Settings.RunOnStartup)
                {
                    StartupShortcutManager.CreateShortcut();
                }
                else if (userChanged)
                {
                    StartupShortcutManager.RemoveShortcut();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Açılışta başlatma ayarı", ex);
            }
        }


        Mutex mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Handle unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Dispatcher.UnhandledException += Dispatcher_UnhandledException;
            TaskScheduler.UnobservedTaskException += (_, ev) => { Log.Error("Task", ev.Exception); ev.SetObserved(); };

            bool result;
            mutex = new System.Threading.Mutex(true, "FlorianButz.DynamicWin", out result);

            if (!result)
            {
                // Zaten çalışıyor: sessizce çık. Kilit açılınca çalışan zamanlanmış görev ve açılış kaydı aynı anda
                // tetiklenebilir; eskiden burada "Only one instance" hata kutusu çıkıyordu.
                Log.Write("İkinci kopya başlatılmak istendi, zaten çalışıyor; çıkılıyor.");
                Shutdown();
                return;
            }

            Log.Write($"Başladı: sürüm {Version}, {Environment.ProcessPath}");
            Microsoft.Win32.SystemEvents.PowerModeChanged += (_, ev) => Log.Write("Güç: " + ev.Mode);
            Microsoft.Win32.SystemEvents.SessionSwitch += (_, ev) => Log.Write("Oturum: " + ev.Reason);
            Microsoft.Win32.SystemEvents.SessionEnding += (_, ev) => Log.Write("Oturum kapanıyor: " + ev.Reason);

            // Windows çökme/donma sonrası uygulamayı yeniden başlatsın (en az 60 sn çalışmış olmalı).
            try { RegisterApplicationRestart(null, 0); } catch { }

            //SetHighPriority();

            try
            {
                var devEnum = new MMDeviceEnumerator();
                defaultDevice = devEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                defaultMicrophone = devEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            }catch(Exception exception)
            {
                defaultDevice = null;
                defaultMicrophone = null;
            }

            SaveManager.LoadData();

            Res.Load();
            KeyHandler.Start();
            new Theme();

            ClaudeBridge.Start();
            LiveActivityManager.RegisterBuiltIns();
            NotificationWatcher.Start();
            BluetoothWatcher.Start();
            PowerWatcher.Start();
            AudioService.Start();
            SystemStateWatcher.Start();
            UpdateChecker.Start();

            Settings.InitializeSettings();

            // CPU/RAM sensörlerini (LibreHardwareMonitor) sadece "System Usage" widget'ı adadaysa saniyede bir oku.
            const string usageWidget = "DynamicWin.UI.Widgets.Small.RegisterSystemUsageWidget";
            if (Settings.smallWidgetsLeft.Contains(usageWidget) || Settings.smallWidgetsRight.Contains(usageWidget) || Settings.smallWidgetsMiddle.Contains(usageWidget))
                new HardwareMonitor();
            UpdateStartup();

            MainForm mainForm = new MainForm();
            mainForm.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            Log.Write($"Kapandı (OnExit, kod {e.ApplicationExitCode})");
            if (MainForm.Instance == null) return; // ikinci kopya: başlatılmadı

            SaveManager.SaveAll();
            HardwareMonitor.Stop();
            ClaudeBridge.Stop();

            MainForm.Instance.DisposeTrayIcon();

            KeyHandler.Stop();
            GC.KeepAlive(mutex); // Important
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error("AppDomain, sonlanıyor=" + e.IsTerminating, e.ExceptionObject);
        }

        static int dispatcherErrors;

        private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Uygulama devam etsin; hatayı kaydet. Render döngüsündeki bir hata her karede tekrarlanabileceği için
            // eskisi gibi her seferinde hata kutusu açılmaz, sadece ilk 20 kayıt yazılır.
            e.Handled = true;
            if (++dispatcherErrors <= 20) Log.Error("UI", e.Exception);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int RegisterApplicationRestart(string? commandLine, int flags);


        private static readonly DateTime Jan1st1970 = new DateTime
            (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long CurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
        }

        public static long NanoTime()
        {
            long nano = 10000L * Stopwatch.GetTimestamp();
            nano /= TimeSpan.TicksPerMillisecond;
            nano *= 100L;
            return nano;
        }
    }

}
