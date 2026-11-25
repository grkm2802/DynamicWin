using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using DynamicWin.Resources;
using DynamicWin.UI;
using DynamicWin.UI.Menu;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using DynamicWin.WPFBinders;
using SkiaSharp;

namespace DynamicWin.Main
{
    public class RendererMain : SKElement
    {
        private IslandObject islandObject;
        public IslandObject MainIsland => islandObject;
        private List<UIObject> objects => MenuManager.Instance.ActiveMenu.UiObjects;

        public static Vec2 ScreenDimensions => new Vec2(MainForm.Instance.Width, MainForm.Instance.Height);
        // Fare konumu doğrudan Win32'den okunur. WPF Mouse.GetPosition sadece pencere fare mesajı alınca güncellenir;
        // şeffaf (tıklama geçiren) pencerede fare dışarı çıkınca eski değerde kalıp adayı "hover" durumunda takılı bırakıyordu.
        public static Vec2 CursorPosition
        {
            get
            {
                try
                {
                    if (GetCursorPos(out var p) && MainForm.Instance != null && PresentationSource.FromVisual(MainForm.Instance) != null)
                    {
                        var pt = MainForm.Instance.PointFromScreen(new System.Windows.Point(p.X, p.Y));
                        return new Vec2((float)pt.X, (float)pt.Y);
                    }
                }
                catch { }
                return new Vec2(-10000, -10000);
            }
        }

        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);

        private static RendererMain instance;
        public static RendererMain Instance => instance;

        public Vec2 renderOffset = Vec2.zero;
        public Vec2 scaleOffset = Vec2.one;
        public float blurOverride = 0f;
        public float alphaOverride = 1f;

        public Action<float> onUpdate;
        public Action<SKCanvas> onDraw;

        private Stopwatch? updateStopwatch;
        private int initialScreenBrightness = 0;
        private float deltaTime = 0f;
        public float DeltaTime => deltaTime;

        private bool isInitialized = false;
        private readonly SplitBubble splitBubble = new SplitBubble();
        public int canvasWithoutClip;
        private GRContext Context;

        public RendererMain()
        {
            MenuManager m = new MenuManager();
            instance = this;
            islandObject = new IslandObject();
            m.Init();

            initialScreenBrightness = BrightnessAdjustMenu.GetBrightness();
            KeyHandler.onKeyDown += OnKeyRegistered;

            MainForm.Instance.DragEnter += MainForm.Instance.MainForm_DragEnter;
            MainForm.Instance.DragLeave += MainForm.Instance.MainForm_DragLeave;
            MainForm.Instance.Drop += MainForm.Instance.OnDrop;
            MainForm.Instance.MouseWheel += MainForm.Instance.OnScroll;

            // Get refresh rate
            int refreshRate = GetRefreshRate();
            Debug.WriteLine($"Monitor Refresh Rate: {refreshRate} Hz");

            // Boştayken düşük hızlı zamanlayıcı, etkileşimde ekran senkronlu CompositionTarget.Rendering (bkz. SetFrameSource).
            idleTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            idleTimer.Tick += OnRendering;
            SetFrameSource(interactive: false);

            isInitialized = true;
        }

        public void Destroy()
        {
            if (renderingHooked) CompositionTarget.Rendering -= OnRendering;
            renderingHooked = false;
            idleTimer?.Stop();

            KeyHandler.onKeyDown -= OnKeyRegistered;
            MainForm.Instance.DragEnter -= MainForm.Instance.MainForm_DragEnter;
            MainForm.Instance.DragLeave -= MainForm.Instance.MainForm_DragLeave;
            MainForm.Instance.MouseWheel -= MainForm.Instance.OnScroll;

            instance = null;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            // Tam ekranda pencere gizli; güncelleme/çizim yapıp CPU/GPU harcama.
            if (FullscreenDetector.IsFullscreenActive)
            {
                // Geri dönüldüğünde ilk karede dev bir deltaTime ile animasyonlar patlamasın.
                updateStopwatch = null;
                // Tam ekranda ekran Hz'inde uyanmaya gerek yok: zamanlayıcıya geç ve 4 Hz'e düşür.
                SetFrameSource(interactive: false);
                if (idleTimer != null) idleTimer.Interval = TimeSpan.FromMilliseconds(250);
                return;
            }

            // CompositionTarget.Rendering ekranın yenileme hızında tetiklenir (bu cihazda 180 Hz).
            // Ekranı kaplayan şeffaf pencereyi her seferinde CPU ile çizmek bir çekirdeği yiyordu;
            // etkileşim/animasyon varken 60, boştayken 30 FPS ile sınırla.
            bool interactive = IsInteractive();
            SetFrameSource(interactive);

            // Ada küçükken pencereyi kısalt: WPF render thread'i her karede tüm şeffaf pencereyi (layered window)
            // Windows'a aktarıyor; maliyet piksel sayısıyla orantılı. Etkileşim başlayınca anında tam boya döner.
            MainForm.Instance.SetCompact(!interactive);

            // Ada tamamen boşken (gösterilecek widget yok) animasyon da yok denecek kadar az: 15 FPS yeter.
            bool empty = islandObject.Size.X < 130 && !interactive;
            double targetMs = interactive ? 1000.0 / 60 : empty ? 1000.0 / 15 : 1000.0 / 30;
            if (idleTimer != null) idleTimer.Interval = TimeSpan.FromMilliseconds(empty ? 66 : 33);
            if (frameClock.Elapsed.TotalMilliseconds - lastFrameMs < targetMs - 1) return;
            lastFrameMs = frameClock.Elapsed.TotalMilliseconds;

            long t0 = Stopwatch.GetTimestamp();
            Update();
            Perf.Add("update", t0);
            Render();
        }

        // Kare kaynağı: CompositionTarget.Rendering'e abone olmak WPF'i ekran Hz'inde (bu cihazda 180) uyandırır;
        // biz karelerin çoğunu atlasak da WPF render thread'i her vsync'te çalışıyordu (boşta ~%5-8 çekirdek).
        // Bu yüzden sadece etkileşimde (60 FPS akıcılık için) abone olunur, boştayken DispatcherTimer kullanılır.
        System.Windows.Threading.DispatcherTimer? idleTimer;
        bool renderingHooked;

        void SetFrameSource(bool interactive)
        {
            if (interactive && !renderingHooked)
            {
                idleTimer?.Stop();
                CompositionTarget.Rendering += OnRendering;
                renderingHooked = true;
            }
            else if (!interactive && renderingHooked)
            {
                CompositionTarget.Rendering -= OnRendering;
                renderingHooked = false;
                idleTimer?.Start();
            }
            else if (!interactive && idleTimer != null && !idleTimer.IsEnabled)
            {
                idleTimer.Start();
            }
        }

        private readonly Stopwatch frameClock = Stopwatch.StartNew();
        private double lastFrameMs = double.MinValue / 2;
        private double lastInteractiveMs = double.MinValue / 2;

        bool IsInteractive()
        {
            bool now = islandObject.IsHovering
                || MainForm.Instance.isDragging
                || !(MenuManager.Instance.ActiveMenu is HomeMenu)
                || Math.Abs(islandObject.Size.X - islandObject.currSize.X) > 0.5f
                || Math.Abs(islandObject.Size.Y - islandObject.currSize.Y) > 0.5f;

            if (now) lastInteractiveMs = frameClock.Elapsed.TotalMilliseconds;

            // Etkileşim bittikten sonra kısa süre daha akıcı kal (kapanış animasyonları için).
            return frameClock.Elapsed.TotalMilliseconds - lastInteractiveMs < 1000;
        }

        private void OnKeyRegistered(Keys key, KeyModifier modifier)
        {
            if (key == Keys.LWin && modifier.isCtrlDown)
            {
                islandObject.hidden = !islandObject.hidden;
            }

            if ((key == Keys.VolumeDown || key == Keys.VolumeMute || key == Keys.VolumeUp) && PopupOptions.saveData.volumePopup)
            {
                if (MenuManager.Instance.ActiveMenu is HomeMenu)
                {
                    MenuManager.OpenOverlayMenu(new VolumeAdjustMenu(), 100f);
                }
                else if (VolumeAdjustMenu.timerUntilClose != null)
                {
                    VolumeAdjustMenu.timerUntilClose = 0f;
                }
            }

            if (key == Keys.MediaNextTrack || key == Keys.MediaPreviousTrack)
            {
                if (MenuManager.Instance.ActiveMenu is HomeMenu)
                {
                    if (key == Keys.MediaNextTrack) Res.HomeMenu.NextSong();
                    else Res.HomeMenu.PrevSong();
                }
            }
        }

        private void Update()
        {
            if (updateStopwatch != null)
            {
                updateStopwatch.Stop();
                // Tam sayı milisaniye yerine hassas süre; düşük/değişken FPS'te animasyonlar takılmasın.
                deltaTime = (float)Math.Min(updateStopwatch.Elapsed.TotalSeconds, 0.1);
            }
            else
            {
                deltaTime = 1f / 1000f;
            }

            updateStopwatch = Stopwatch.StartNew();

            long tu = Stopwatch.GetTimestamp();
            onUpdate?.Invoke(DeltaTime);
            Perf.Add("u:onUpdate", tu); tu = Stopwatch.GetTimestamp();

            if (BrightnessAdjustMenu.GetBrightness() != initialScreenBrightness && PopupOptions.saveData.brightnessPopup)
            {
                initialScreenBrightness = BrightnessAdjustMenu.GetBrightness();
                if (MenuManager.Instance.ActiveMenu is HomeMenu)
                {
                    MenuManager.OpenOverlayMenu(new BrightnessAdjustMenu(), 100f);
                }
                else if (BrightnessAdjustMenu.timerUntilClose != null)
                {
                    BrightnessAdjustMenu.PressBK();
                    BrightnessAdjustMenu.timerUntilClose = 0f;
                }
            }

            // Claude Code olaylarından gelen bildirimleri sadece ana menüdeyken aç;
            // başka bir overlay açıksa sırada bekler.
            if (MenuManager.Instance.ActiveMenu is HomeMenu && ClaudeBridge.PopupQueue.TryDequeue(out var claudeEvent))
            {
                MenuManager.OpenOverlayMenu(ClaudeCodeMenu.Create(claudeEvent),
                    claudeEvent.State == ClaudeState.WaitingPermission ? 10f : 5f);
            }
            else if (MenuManager.Instance.ActiveMenu is HomeMenu && IslandPopups.TryDequeue(out var popup))
            {
                MenuManager.OpenOverlayMenu(InfoPopupMenu.Create(popup!), popup!.Seconds);
            }
            else if (MenuManager.Instance.ActiveMenu is HomeMenu && NotificationWatcher.TryDequeueFresh(out var notification))
            {
                MenuManager.OpenOverlayMenu(NotificationMenu.Create(notification!), 5f);
            }

            Perf.Add("u:brightness+popups", tu); tu = Stopwatch.GetTimestamp();
            MenuManager.Instance.Update(DeltaTime);

            if (MenuManager.Instance.ActiveMenu != null)
            {
                MenuManager.Instance.ActiveMenu.Update();
                Perf.Add("u:menu", tu); tu = Stopwatch.GetTimestamp();

                if (MenuManager.Instance.ActiveMenu is DropFileMenu && !MainForm.Instance.isDragging)
                    MenuManager.OpenMenu(Res.HomeMenu);
            }

            islandObject.UpdateCall(DeltaTime);
            Perf.Add("u:island", tu); tu = Stopwatch.GetTimestamp();
            splitBubble.Update(DeltaTime, islandObject);
            Perf.Add("u:bubble", tu);

            if (MainIsland.hidden) return;

            foreach (UIObject uiObject in objects)
            {
                long to = Stopwatch.GetTimestamp();
                uiObject.UpdateCall(DeltaTime);
                Perf.Add("u:" + uiObject.GetType().Name, to);
            }
        }

        private void Render()
        {
            Dispatcher.Invoke(() => InvalidateVisual());
        }

        protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
        {
            long t = Stopwatch.GetTimestamp();
            base.OnRender(drawingContext);
            Perf.Add("render:total", t);
            Perf.Frame();
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);

            if (!isInitialized) return;

            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;

            canvas.Clear(SKColors.Transparent);

            double dpiFactor = System.Windows.PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
            canvas.Scale((float)dpiFactor, (float)dpiFactor);

            canvasWithoutClip = canvas.Save();

            if (islandObject.maskInToIsland) Mask(canvas);
            long tIsland = Stopwatch.GetTimestamp();
            islandObject.DrawCall(canvas);
            splitBubble.Draw(canvas, islandObject);
            Perf.Add("draw:island", tIsland);

            if (MainIsland.hidden) return;

            bool hasContextMenu = false;
            foreach (UIObject uiObject in objects)
            {
                canvas.RestoreToCount(canvasWithoutClip);
                canvasWithoutClip = canvas.Save();

                if (uiObject.IsHovering && uiObject.GetContextMenu() != null)
                {
                    hasContextMenu = true;
                    ContextMenu = uiObject.GetContextMenu();
                }

                foreach (UIObject obj in uiObject.LocalObjects)
                {
                    if (obj.IsHovering && obj.GetContextMenu() != null)
                    {
                        hasContextMenu = true;
                        ContextMenu = obj.GetContextMenu();
                    }
                }

                if (uiObject.maskInToIsland)
                {
                    Mask(canvas);
                }

                canvas.Scale(scaleOffset.X, scaleOffset.Y, islandObject.Position.X + islandObject.Size.X / 2, islandObject.Position.Y + islandObject.Size.Y / 2);
                canvas.Translate(renderOffset.X, renderOffset.Y);

                long tObj = Stopwatch.GetTimestamp();
                uiObject.DrawCall(canvas);
                Perf.Add("draw:" + uiObject.GetType().Name, tObj);
            }

            onDraw?.Invoke(canvas);

            if (!hasContextMenu) ContextMenu = null;

            canvas.Flush();
        }

        private void Mask(SKCanvas canvas)
        {
            var islandMask = GetMask();
            canvas.ClipRoundRect(islandMask);
        }

        public SKRoundRect GetMask()
        {
            var islandMask = islandObject.GetRect();
            islandMask.Deflate(new SKSize(1, 1));
            return islandMask;
        }

        // Native PInvoke to get monitor refresh rate
        private const int ENUM_CURRENT_SETTINGS = -1;
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        private int GetRefreshRate()
        {
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
                return (int)devMode.dmDisplayFrequency;
            return 60;
        }
    }
}