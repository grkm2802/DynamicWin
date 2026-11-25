using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DynamicWin.Utils
{
    /// <summary>
    /// Adanın bulunduğu monitörde tam ekran bir uygulama (oyun, video, sunum) olup olmadığını
    /// yarım saniyede bir kontrol eder. Render döngüsünden bağımsız bir DispatcherTimer kullanır;
    /// böylece ada gizliyken çizim tamamen durdurulabilir.
    /// </summary>
    public static class FullscreenDetector
    {
        public static bool IsFullscreenActive { get; private set; }
        public static event Action<bool>? FullscreenChanged;

        static DispatcherTimer? timer;
        static IntPtr ownWindow;
        static int positiveStreak;
        static IntPtr islandMonitor;

        // Gizlemek için art arda bu kadar pozitif ölçüm gerekir (geçici pencere animasyonlarında titremesin).
        const int hideAfterSamples = 2;

        public static void Start(System.Windows.Window window)
        {
            ownWindow = new WindowInteropHelper(window).EnsureHandle();
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (_, _) => Poll();
            timer.Start();
        }

        public static void Stop() => timer?.Stop();

        static void Poll()
        {
            bool detected = Main.Settings.HideInFullscreen && Detect();

            if (detected) positiveStreak++;
            else positiveStreak = 0;

            bool newState = detected ? positiveStreak >= hideAfterSamples : false;

            // Bekçi: tam ekran yokken pencere gizli/ekran dışında kalmışsa düzelt.
            if (!newState && !IsFullscreenActive) Main.MainForm.Instance?.EnsureVisible();

            if (newState == IsFullscreenActive) return;

            IsFullscreenActive = newState;
            Debug.WriteLine("Tam ekran: " + newState);
            FullscreenChanged?.Invoke(newState);
        }

        static bool Detect()
        {
            // D3D özel tam ekran veya sunum modu: Windows'un kendisi söylüyor.
            if (SHQueryUserNotificationState(out int state) == 0 &&
                (state == QUNS_RUNNING_D3D_FULL_SCREEN || state == QUNS_PRESENTATION_MODE))
                return true;

            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == ownWindow) return false;

            GetWindowThreadProcessId(fg, out uint pid);
            if (pid == Environment.ProcessId) return false;

            string cls = GetClassName(fg);
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
                or "Windows.UI.Core.CoreWindow" or "XamlExplorerHostIslandWindow")
                return false;

            // Ön plandaki pencere adanın monitöründe değilse adayı gizlemeye gerek yok.
            IntPtr fgMonitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONULL);
            // Tam ekranda pencere ekran dışına taşınıyor; o sırada MonitorFromWindow yanlış monitörü verir.
            // Bu yüzden adanın monitörünü sadece pencere yerindeyken güncelle.
            if (!IsFullscreenActive) islandMonitor = MonitorFromWindow(ownWindow, MONITOR_DEFAULTTONEAREST);
            IntPtr ourMonitor = islandMonitor;
            if (fgMonitor == IntPtr.Zero || fgMonitor != ourMonitor) return false;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(fgMonitor, ref mi)) return false;
            if (!GetWindowRect(fg, out RECT r)) return false;

            // Pencere monitörün tamamını kaplıyorsa (kenarlıksız tam ekran dahil) tam ekran say.
            return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top &&
                   r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
        }

        static string GetClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
        const int QUNS_PRESENTATION_MODE = 4;
        const uint MONITOR_DEFAULTTONULL = 0;
        const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO mi);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    }
}
