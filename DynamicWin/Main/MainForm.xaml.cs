using DynamicWin.Resources;
using DynamicWin.UI.Menu;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.Utils;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace DynamicWin.Main
{
    public partial class MainForm : Window
    {
        private static MainForm instance;
        public static MainForm Instance { get => instance; }

        public static Action<System.Windows.Input.MouseWheelEventArgs> onScrollEvent;

        private readonly Forms.NotifyIcon _trayIcon;

        private DateTime _lastRenderTime;
        private readonly TimeSpan _targetElapsedTime = TimeSpan.FromMilliseconds(16); // ~60 FPS

        public Action onMainFormRender;

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr window, int idx, int val);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr window, int idx);

        // Define integer values
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;

        public MainForm()
        {
            InitializeComponent();

            _trayIcon = new Forms.NotifyIcon();

            // CompositionTarget.Rendering'e abone olmak WPF'i ekran Hz'inde (180) sürekli çalıştırır; burada kimse
            // onMainFormRender'ı kullanmadığı için abonelik kaldırıldı (CPU). Çizim döngüsü RendererMain'de.

            instance = this;

            this.WindowStyle = WindowStyle.None;
            this.WindowState = WindowState.Maximized;
            this.ResizeMode = ResizeMode.NoResize;
            this.Topmost = true;
            this.AllowsTransparency = true;
            this.ShowInTaskbar = false;
            this.Title = "DynamicWin Overlay";

            // Loaded event to ensure that this does not show the application on the Alt+Tab switcher

            this.Loaded += (s, e) =>
            {
                IntPtr handle = new WindowInteropHelper(this).Handle; // Define Handle
                int winStyle = GetWindowLong(handle, GWL_EXSTYLE); // Fetch defined GWL_EXSTYLE

                // Apply WS_EX_TOOLWINDOW and remove WS_EX_APPWINDOW if it exists
                winStyle = (winStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
                SetWindowLong(handle, GWL_EXSTYLE, winStyle);

                FullscreenDetector.FullscreenChanged += OnFullscreenChanged;
                FullscreenDetector.Start(this);
                ClipboardWatcher.Start(this);
            };

            SetMonitor(Settings.ScreenIndex);

            AddRenderer();

            Res.extensions.ForEach((x) => x.LoadExtension());
            MainForm.Instance.AllowDrop = true;

            // Tray icon

            _trayIcon.Icon = new System.Drawing.Icon("Resources/icons/TrayIcon.ico");
            _trayIcon.Text = "DynamicWin";

            _trayIcon.ContextMenuStrip = new Forms.ContextMenuStrip();

            _trayIcon.ContextMenuStrip.Items.Add(L.T("Restart Control", "Yeniden başlat"), null, (x, y) =>
            {
                if (RendererMain.Instance != null) RendererMain.Instance.Destroy();
                this.Content = new Grid();

                AddRenderer();
            });

            _trayIcon.ContextMenuStrip.Items.Add(L.T("Settings", "Ayarlar"), null, (x, y) =>
            {
                MenuManager.OpenMenu(new SettingsMenu());
            });

            _trayIcon.ContextMenuStrip.Items.Add(L.T("Exit", "Çıkış"), null, (x, y) =>
            {
                SaveManager.SaveAll();
                Process.GetCurrentProcess().Kill();
            });

            // Güncelleme bulununca tepsi menüsünün başına indirme bağlantısı ekle (#19).
            UpdateChecker.UpdateFound += () => Dispatcher.Invoke(() =>
                _trayIcon.ContextMenuStrip.Items.Insert(0, new Forms.ToolStripMenuItem(
                    L.T("Download update ", "Güncellemeyi indir ") + UpdateChecker.LatestVersion, null,
                    (x, y) => UpdateChecker.OpenReleasePage())));

            _trayIcon.Visible = true;
        }


        public const double OverlayWidth = 1100;
        public const double OverlayHeight = 620;
        // Ada küçükken pencere yüksekliği: ada (~35) + üst boşluk + gölge + Claude/bildirim kartı (≤ ~95) için yeterli.
        public const double CompactHeight = 140;
        // Ada küçükken genişlik: ada + yanındaki balon + en geniş bildirim kartı (~460) için yeterli.
        public const double CompactWidth = 640;

        bool compact;
        Rect fullRect = new Rect(0, 0, OverlayWidth, OverlayHeight); // DIP; SetMonitor belirler

        /// <summary>
        /// Ada küçükken pencereyi üst ortadaki küçük bir alana daraltır (CPU: WPF render thread'i her karede tüm
        /// şeffaf pencereyi aktarıyor), etkileşimde tam boya döndürür. Konum ve boyut tek bir SetWindowPos ile
        /// değişir; WPF özellikleriyle ayrı ayrı yapılınca ada bir kare kayıyordu.
        /// </summary>
        public void SetCompact(bool value)
        {
            if (value == compact) return;
            compact = value;
            ApplyBounds();
        }

        void ApplyBounds()
        {
            var r = fullRect;
            if (compact)
            {
                double cw = Math.Min(CompactWidth, r.Width), ch = Math.Min(CompactHeight, r.Height);
                r = new Rect(r.Left + (r.Width - cw) / 2, r.Top, cw, ch);
            }

            var hwnd = new WindowInteropHelper(this).Handle;
            var src = PresentationSource.FromVisual(this);
            if (hwnd == IntPtr.Zero || src == null)
            {
                Left = r.Left; Top = r.Top; Width = r.Width; Height = r.Height;
                return;
            }
            if (parkedFrom != null) return; // tam ekranda ekran dışında; dönüşte RestoreWindow yerine koyar
            var m = src.CompositionTarget.TransformToDevice;
            SetWindowPos(hwnd, HWND_TOPMOST, (int)Math.Round(r.Left * m.M11), (int)Math.Round(r.Top * m.M22),
                (int)Math.Round(r.Width * m.M11), (int)Math.Round(r.Height * m.M22), SWP_NOACTIVATE);
        }

        public void SetMonitor(int monitorIndex)
        {
            var screen = GetScreens()[Math.Clamp(monitorIndex, 0, GetMonitorCount() - 1)];
            Settings.ScreenIndex = Math.Clamp(monitorIndex, 0, GetMonitorCount() - 1);

            if (screen != null)
            {
                if (!this.IsLoaded)
                    this.WindowStartupLocation = WindowStartupLocation.Manual;

                this.WindowState = WindowState.Normal;
                this.ResizeMode = ResizeMode.CanResize;

                var workingArea = screen.WorkingArea;

                // Pencere eskiden tüm çalışma alanını kaplıyordu; şeffaf katmanın tamamı her karede
                // CPU ile çizildiği için pahalıydı. Ada üst ortada durduğundan sadece o bölgeyi kapla.
                // En büyük menü (Ayarlar) 525x425; balon, gölge ve açılış animasyonu için pay bırakıldı.
                double w = Math.Min(workingArea.Width, OverlayWidth);
                double h = Math.Min(workingArea.Height, OverlayHeight);

                fullRect = new Rect(workingArea.Left + (workingArea.Width - w) / 2, workingArea.Top, w, h);
                ApplyBounds();

                this.ResizeMode = ResizeMode.NoResize;
            }
        }

        // Screen.AllScreens ana monitörü her zaman 0. sıraya koymaz; ayarlarda 0 = "Primary"
        // olduğu için ana monitörü başa alıp diğerlerini sırasıyla dizeriz.
        public static System.Windows.Forms.Screen[] GetScreens()
        {
            return System.Windows.Forms.Screen.AllScreens
                .OrderByDescending(s => s.Primary)
                .ToArray();
        }

        public static int GetMonitorCount()
        {
            return System.Windows.Forms.Screen.AllScreens.Length;
        }

        // Tam ekranda pencere oyunun/videonun monitöründen çekilmeli: üstte duran şeffaf topmost bir pencere
        // oyunun doğrudan ekrana çizmesini (independent flip) engeller ve FPS düşürür.
        // ÖNEMLİ: WPF Hide()/Show() kullanma. AllowsTransparency'li pencerede Show() sonrası HWND gizli kalabiliyor
        // (WPF IsVisible=true, Win32 IsWindowVisible=false) ve ada kalıcı olarak kayboluyordu (26.11.2026).
        // Bunun yerine pencere Win32 ile ekran dışına taşınır, sonra geri getirilir; görünürlük durumuna dokunulmaz.
        RECT? parkedFrom;

        void OnFullscreenChanged(bool fullscreen)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (fullscreen)
            {
                if (GetWindowRect(hwnd, out var r) && r.Left > -20000) parkedFrom = r;
                SetWindowPos(hwnd, HWND_TOPMOST, -32000, -32000, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
            }
            else
            {
                RestoreWindow();
            }
        }

        /// <summary>Pencereyi görünür, en üstte ve doğru yerde olmaya zorlar (tam ekrandan dönüş + bekçi).</summary>
        public void RestoreWindow()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (!IsWindowVisible(hwnd)) ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            if (parkedFrom is RECT r)
                SetWindowPos(hwnd, HWND_TOPMOST, r.Left, r.Top, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
            else
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            parkedFrom = null;
            ApplyBounds();
        }

        /// <summary>Bekçi: tam ekran yokken pencere gizli kalmış ya da ekran dışında unutulmuşsa düzelt.</summary>
        public void EnsureVisible()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            bool hidden = !IsWindowVisible(hwnd);
            bool parked = GetWindowRect(hwnd, out var r) && r.Left <= -20000;
            if (hidden || parked)
            {
                System.Diagnostics.Debug.WriteLine($"Bekçi: pencere düzeltiliyor (gizli={hidden}, dışarıda={parked})");
                if (parked && parkedFrom == null) { SetMonitor(Settings.ScreenIndex); }
                RestoreWindow();
            }
        }

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
        const int SW_SHOWNOACTIVATE = 4;

        private void OnRendering(object? sender, EventArgs e)
        {
            if (FullscreenDetector.IsFullscreenActive) return;

            var currentTime = DateTime.Now;
            if (currentTime - _lastRenderTime >= _targetElapsedTime)
            {
                _lastRenderTime = currentTime;

                onMainFormRender?.Invoke();
            }
        }

        public bool isDragging = false;

        public void OnScroll(object? sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            onScrollEvent?.Invoke(e);
        }

        public void AddRenderer()
        {
            if (RendererMain.Instance != null) RendererMain.Instance.Destroy();

            var customControl = new RendererMain();
            
            var parent = new Grid();
            parent.Children.Add(customControl);

            this.Content = parent;
        }

        public void MainForm_DragEnter(object? sender, DragEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("DragEnter");

            isDragging = true;
            e.Effects = DragDropEffects.Copy;

            if (!(MenuManager.Instance.ActiveMenu is DropFileMenu)
                && !(MenuManager.Instance.ActiveMenu is ConfigureShortcutMenu))
            {
                MenuManager.OpenMenu(new DropFileMenu());
            }
        }

        public void MainForm_DragLeave(object? sender, EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("DragLeave");

            isDragging = false;

            if (MenuManager.Instance.ActiveMenu is ConfigureShortcutMenu) return;
            MenuManager.OpenMenu(Res.HomeMenu);
        }

        bool isLocalDrag = false;

        internal void StartDrag(string[] files, Action callback)
        {
            if (isLocalDrag) return;

            Array.ForEach(files, file => { System.Diagnostics.Debug.WriteLine(file); });

            if (files == null) return;
            else if (files.Length <= 0) return;

            try
            {
                isLocalDrag = true;

                DataObject dataObject = new DataObject(DataFormats.FileDrop, files);
                var effects = DragDrop.DoDragDrop((DependencyObject)this, dataObject, DragDropEffects.Move | DragDropEffects.Copy);

                if (RendererMain.Instance != null) RendererMain.Instance.Destroy();
                this.Content = new Grid();
                AddRenderer();

                callback?.Invoke();

                isLocalDrag = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred: " + ex.Message);
            }
        }

        protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e)
        {
            if (e.Action == DragAction.Cancel)
            {
                isLocalDrag = false;
            }
            else if (e.Action == DragAction.Continue)
            {
                isLocalDrag = true;
            }
            else if (e.Action == DragAction.Drop)
            {
                isLocalDrag = false;
            }
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            base.OnDragOver(e);
        }

        public void OnDrop(object sender, System.Windows.DragEventArgs e)
        {
            isDragging = false;

            if(MenuManager.Instance.ActiveMenu is ConfigureShortcutMenu)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    ConfigureShortcutMenu.DropData(e);
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DropFileMenu.Drop(e);
                MenuManager.Instance.QueueOpenMenu(Res.HomeMenu);
                Res.HomeMenu.isWidgetMode = false;
            }
        }

        internal void DisposeTrayIcon()
        {
            _trayIcon.Dispose();
        }
    }
}
