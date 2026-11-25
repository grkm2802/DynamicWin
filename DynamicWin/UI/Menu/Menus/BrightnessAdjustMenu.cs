using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.UIElements;
using DynamicWin.UI.UIElements.Custom;
using DynamicWin.Utils;
using NAudio.CoreAudioApi;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System;
using System.Threading.Tasks;
using System.Management;

namespace DynamicWin.UI.Menu.Menus
{
    internal class BrightnessAdjustMenu : BaseMenu
    {

        DWImage brightnessImage;
        DWProgressBar brightness;

        static BrightnessAdjustMenu instance;

        float islandScale = 1.25f;

        public BrightnessAdjustMenu()
        {
            instance = this;
            timerUntilClose = 0f;
        }

        public static void PressBK()
        {
            instance.islandScale = 1.025f;
        }

        public override List<UIObject> InitializeMenu(IslandObject island)
        {
            var objects = base.InitializeMenu(island);

            brightnessImage = new DWImage(island, Res.Brightness, new Vec2(20, 0), new Vec2(20, 20), UIAlignment.MiddleLeft);
            brightnessImage.Anchor.X = 0;
            objects.Add(brightnessImage);

            brightness = new DWProgressBar(island, new Vec2(-20, 0), new Vec2(150, 5f), UIAlignment.MiddleRight);
            brightness.Anchor.X = 1;
            objects.Add(brightness);

            return objects;
        }

        public static float timerUntilClose = 0f;

        public override void Update()
        {
            base.Update();

            if (timerUntilClose > 2.75f) MenuManager.CloseOverlay();

            islandScale = Mathf.Lerp(islandScale, 1f, 5f * RendererMain.Instance.DeltaTime);

            timerUntilClose += RendererMain.Instance.DeltaTime;

            this.brightness.value = (float)WindowsSettingsBrightnessController.Get() / 100f;

            var volXOffset = KeyHandler.keyDown.Contains(System.Windows.Forms.Keys.VolumeUp) ? 2f :
                KeyHandler.keyDown.Contains(System.Windows.Forms.Keys.VolumeDown) ? -2f : 0;

            this.brightness.LocalPosition.X = Mathf.Lerp(this.brightness.LocalPosition.X, volXOffset, 
                (Math.Abs(volXOffset) > Math.Abs(this.brightness.LocalPosition.X) ? 4.5f : 2.5f) * RendererMain.Instance.DeltaTime);
        }

        public override Vec2 IslandSize()
        {
            return new Vec2(250, 35) * islandScale;
        }

        public override Vec2 IslandSizeBig()
        {
            return base.IslandSizeBig() * 1.05f;
        }

        internal static int GetBrightness()
        {
            return WindowsSettingsBrightnessController.Get();
        }

        public override Col IslandBorderColor()
        {
            return new Col(0.5f, 0.5f, 0.5f);
        }
    }

    static class WindowsSettingsBrightnessController
    {
        static bool notSupported = false;

        // WMI sorgusu ~6 ms sürüyor ve RendererMain parlaklığı her karede soruyor. UI thread'inde hiç sorgu yapılmaz:
        // değer WMI'ın parlaklık olayı (WmiMonitorBrightnessEvent) ile güncellenir; olay kurulamazsa arka planda
        // saniyede bir yoklanır. Get() sadece önbellekteki değeri döndürür.
        static volatile int cachedValue = 100;
        static bool started;
        static ManagementEventWatcher? watcher;
        static System.Threading.Timer? fallbackTimer;

        public static int Get()
        {
            if (notSupported) return 100;
            if (!started) Start();
            return cachedValue;
        }

        static void Start()
        {
            started = true;
            Task.Run(() =>
            {
                cachedValue = Query();
                if (notSupported) return;
                try
                {
                    watcher = new ManagementEventWatcher(new ManagementScope(@"\\.\root\wmi"),
                        new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
                    watcher.EventArrived += (_, e) =>
                    {
                        try { cachedValue = Convert.ToInt32(e.NewEvent.Properties["Brightness"].Value); }
                        catch { cachedValue = Query(); }
                    };
                    watcher.Start();
                }
                catch
                {
                    fallbackTimer = new System.Threading.Timer(_ => cachedValue = Query(), null, 1000, 1000);
                }
            });
        }

        static int Query()
        {
            if(notSupported)
                return 100;

            try
            {
                using var mclass = new ManagementClass("WmiMonitorBrightness")
                {
                    Scope = new ManagementScope(@"\\.\root\wmi")
                };
                using var instances = mclass.GetInstances();
                foreach (ManagementObject instance in instances)
                {
                    return (byte)instance.GetPropertyValue("CurrentBrightness");
                }
                return 0;
            }catch(System.Management.ManagementException e)
            {
                notSupported = true;
                return 100;
            }
        }

        public static void Set(int brightness)
        {
            try
            {
                using var mclass = new ManagementClass("WmiMonitorBrightnessMethods")
                {
                    Scope = new ManagementScope(@"\\.\root\wmi")
                };
                using var instances = mclass.GetInstances();
                var args = new object[] { 1, brightness };
                foreach (ManagementObject instance in instances)
                {
                    instance.InvokeMethod("WmiSetBrightness", args);
                }
            }catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
