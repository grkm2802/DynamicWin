using DynamicWin.Resources;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DynamicWin.Main
{
    public class Settings
    {
        private static IslandObject.IslandMode islandMode;
        private static bool allowBlur;
        private static bool allowAnimation;
        private static bool antiAliasing;
        private static bool runOnStartup;
        private static int theme;
        private static int activeScreenIndex;

        public static IslandObject.IslandMode IslandMode { get => islandMode; set => islandMode = value; }
        public static bool AllowBlur { get => allowBlur; set => allowBlur = value; }
        public static bool AllowAnimation { get => allowAnimation; set => allowAnimation = value; }
        public static bool AntiAliasing { get => antiAliasing; set => antiAliasing = value; }
        public static bool RunOnStartup { get => runOnStartup; set => runOnStartup = value; }
        public static int Theme { get => theme; set => theme = value; }
        public static int ScreenIndex { get => activeScreenIndex; set => activeScreenIndex = value; }
        // Tam ekran uygulama (oyun/video) açıkken adayı tamamen gizle ve çizimi durdur.
        public static bool HideInFullscreen { get; set; } = true;
        // Windows bildirimlerini adada göster.
        public static bool ShowNotifications { get; set; } = true;
        // Arayüz dili: "tr" veya "en" (bkz. Utils/L.cs).
        public static string Language { get; set; } = "tr";
        // Ekran görüntüsü panoya gelince Tray'e kaydet ve önizleme göster (#12).
        public static bool ScreenshotPreview { get; set; } = true;
        // Bluetooth, şarj, sessize alma, odak, ekran kaydı, kilit açma uyarıları (#9, #10, #21-24).
        public static bool SystemPopups { get; set; } = true;
        // Ada görünümü: 0 = düz siyah, 1 = buzlu cam (#17).
        public static int IslandStyle { get; set; } = 0;

        public static List<string> smallWidgetsLeft;
        public static List<string> smallWidgetsRight;
        public static List<string> smallWidgetsMiddle;
        public static List<string> bigWidgets;

        public static void InitializeSettings()
        {
            try
            {

                if (SaveManager.Contains("settings"))
                {
                    IslandMode = ((Int64)SaveManager.Get("settings.islandmode") == 0) ? IslandObject.IslandMode.Island : IslandObject.IslandMode.Notch;

                    AllowBlur = (bool)SaveManager.Get("settings.allowblur");
                    AllowAnimation = (bool)SaveManager.Get("settings.allowanimtion");
                    AntiAliasing = (bool)SaveManager.Get("settings.antialiasing");
                    RunOnStartup = (bool)SaveManager.Get("settings.runonstartup");

                    Theme = (int)((Int64)SaveManager.Get("settings.theme"));
                    ScreenIndex = (int)((Int64)SaveManager.Get("settings.screenindex"));
                    if (SaveManager.Contains("settings.hideinfullscreen"))
                        HideInFullscreen = (bool)SaveManager.Get("settings.hideinfullscreen");
                    if (SaveManager.Contains("settings.shownotifications"))
                        ShowNotifications = (bool)SaveManager.Get("settings.shownotifications");
                    if (SaveManager.Contains("settings.language"))
                        Language = (string)SaveManager.Get("settings.language");
                    if (SaveManager.Contains("settings.screenshotpreview"))
                        ScreenshotPreview = (bool)SaveManager.Get("settings.screenshotpreview");
                    if (SaveManager.Contains("settings.systempopups"))
                        SystemPopups = (bool)SaveManager.Get("settings.systempopups");
                    if (SaveManager.Contains("settings.islandstyle"))
                        IslandStyle = (int)(Int64)SaveManager.Get("settings.islandstyle");

                    Settings.smallWidgetsLeft = new List<string>();
                    Settings.smallWidgetsRight = new List<string>();
                    Settings.smallWidgetsMiddle = new List<string>();
                    Settings.bigWidgets = new List<string>();

                    var smallWidgetsLeft = (JArray)SaveManager.Get("settings.smallwidgetsleft");
                    var smallWidgetsRight = (JArray)SaveManager.Get("settings.smallwidgetsright");
                    var smallWidgetsMiddle = (JArray)SaveManager.Get("settings.smallwidgetsmiddle");
                    var bigWidgets = (JArray)SaveManager.Get("settings.bigwidgets");

                    foreach (var x in smallWidgetsLeft)
                        Settings.smallWidgetsLeft.Add(x.ToString());
                    foreach (var x in smallWidgetsRight)
                        Settings.smallWidgetsRight.Add(x.ToString());
                    foreach (var x in smallWidgetsMiddle)
                        Settings.smallWidgetsMiddle.Add(x.ToString());
                    foreach (var x in bigWidgets)
                        Settings.bigWidgets.Add(x.ToString());
                }
                else
                {
                    smallWidgetsLeft = new List<string>();
                    smallWidgetsRight = new List<string>();
                    smallWidgetsMiddle = new List<string>();
                    bigWidgets = new List<string>();

                    smallWidgetsRight.Add("DynamicWin.UI.Widgets.Small.RegisterUsedDevicesWidget");
                    // Saat varsayılan olarak eklenmez: görev çubuğunda zaten var (kullanıcı tercihi).
                    bigWidgets.Add("DynamicWin.UI.Widgets.Big.RegisterSpotifyWidget");
                    bigWidgets.Add("DynamicWin.UI.Widgets.Big.RegisterYouTubeWidget");

                    IslandMode = IslandObject.IslandMode.Island;
                    AllowBlur = true;
                    AllowAnimation = true;
                    AntiAliasing = true;
                    RunOnStartup = true; // ilk kurulumda Windows açılışında başlasın (Ayarlar'dan kapatılabilir)

                    Settings.Theme = 0;

                    SaveManager.SaveData.Add("settings", 1);
                }


                // This must be run after loading all settings
                AfterSettingsLoaded();
            }catch(Exception e)
            {
                MessageBox.Show("An error occured trying to load the settings. Please revert back to the default settings by deleting the \"Settings.json\" file located under \"%appdata%/DynamicWin/\".");

                smallWidgetsLeft = new List<string>();
                smallWidgetsRight = new List<string>();
                smallWidgetsMiddle = new List<string>();
                bigWidgets = new List<string>();

                AfterSettingsLoaded();
            }
        }

        static void AfterSettingsLoaded()
        {
            DynamicWin.Utils.Theme.Instance.UpdateTheme();

            var customOptions = SettingsMenu.LoadCustomOptions();

            foreach (var item in customOptions)
            {
                item.LoadSettings();
            }

            AddClaudeWidgetOnce();
            // Yeni büyük widget'ları mevcut kurulumlara bir kez ekle (kullanıcı sonra kaldırabilir).
            AddWidgetOnce("settings.widgetsadded.v2", bigWidgets,
                "DynamicWin.UI.Widgets.Big.RegisterClipboardWidget",
                "DynamicWin.UI.Widgets.Big.RegisterPomodoroWidget",
                "DynamicWin.UI.Widgets.Big.RegisterNoteWidget");
            SplitMediaWidgetOnce();
            MoveNetSpeedToTileOnce();
        }

        // Tek "Medya" kartı yerine Spotify ve YouTube ayrı kartlar (ikisi aynı anda çalabilir). Bir kez yapılır.
        static void SplitMediaWidgetOnce()
        {
            const string flag = "settings.widgetsadded.v3.media";
            if (SaveManager.Contains(flag)) return;
            const string media = "DynamicWin.UI.Widgets.Big.RegisterMediaWidget";
            const string spotify = "DynamicWin.UI.Widgets.Big.RegisterSpotifyWidget";
            const string youtube = "DynamicWin.UI.Widgets.Big.RegisterYouTubeWidget";

            int i = bigWidgets.IndexOf(media);
            if (i >= 0) bigWidgets.RemoveAt(i); else i = 0;
            if (!bigWidgets.Contains(youtube)) bigWidgets.Insert(i, youtube);
            if (!bigWidgets.Contains(spotify)) bigWidgets.Insert(i, spotify);

            SaveManager.Add(flag, true);
            Save();
        }

        // Ağ hızı ada küçükken sürekli görünmesin, istenince bakılsın (kullanıcı isteği): küçük widget'ı kaldır,
        // genişletilmiş adaya "Ağ hızı" kutusu ekle. Bir kez yapılır.
        static void MoveNetSpeedToTileOnce()
        {
            const string flag = "settings.widgetsadded.v4.netspeed";
            if (SaveManager.Contains(flag)) return;
            const string small = "DynamicWin.UI.Widgets.Small.RegisterNetSpeedWidget";
            const string big = "DynamicWin.UI.Widgets.Big.RegisterNetSpeedBigWidget";
            smallWidgetsLeft.Remove(small); smallWidgetsRight.Remove(small); smallWidgetsMiddle.Remove(small);
            if (!bigWidgets.Contains(big)) bigWidgets.Add(big);
            SaveManager.Add(flag, true);
            Save();
        }

        static void AddWidgetOnce(string flag, List<string> target, params string[] widgets)
        {
            if (SaveManager.Contains(flag)) return;
            foreach (var w in widgets)
                if (!smallWidgetsLeft.Contains(w) && !smallWidgetsRight.Contains(w) && !smallWidgetsMiddle.Contains(w) && !bigWidgets.Contains(w))
                    target.Add(w);
            SaveManager.Add(flag, true);
            Save();
        }

        // Claude Code widget'ı sonradan eklendi; eski ayar dosyası olanlarda da bir kez sağ tarafa yerleştir.
        // Kullanıcı sonra kaldırırsa bayrak sayesinde geri eklenmez.
        static void AddClaudeWidgetOnce()
        {
            const string flag = "settings.claudewidgetadded";
            const string widget = "DynamicWin.UI.Widgets.Small.RegisterClaudeCodeWidget";

            if (SaveManager.Contains(flag)) return;

            if (!smallWidgetsLeft.Contains(widget) && !smallWidgetsRight.Contains(widget) && !smallWidgetsMiddle.Contains(widget))
                smallWidgetsRight.Insert(0, widget);

            SaveManager.Add(flag, true);
            Save();
        }

        public static void Save()
        {
            SaveManager.Add("settings.islandmode", (IslandMode == IslandObject.IslandMode.Island) ? 0 : 1);

            SaveManager.Add("settings.allowblur", AllowBlur);
            SaveManager.Add("settings.allowanimtion", AllowAnimation);
            SaveManager.Add("settings.antialiasing", AntiAliasing);
            SaveManager.Add("settings.runonstartup", RunOnStartup);

            SaveManager.Add("settings.theme", Theme);
            SaveManager.Add("settings.screenindex", ScreenIndex);
            SaveManager.Add("settings.hideinfullscreen", HideInFullscreen);
            SaveManager.Add("settings.shownotifications", ShowNotifications);
            SaveManager.Add("settings.language", Language);
            SaveManager.Add("settings.screenshotpreview", ScreenshotPreview);
            SaveManager.Add("settings.systempopups", SystemPopups);
            SaveManager.Add("settings.islandstyle", IslandStyle);

            SaveManager.Add("settings.smallwidgetsleft", smallWidgetsLeft);
            SaveManager.Add("settings.smallwidgetsright", smallWidgetsRight);
            SaveManager.Add("settings.smallwidgetsmiddle", smallWidgetsMiddle);
            SaveManager.Add("settings.bigwidgets", bigWidgets);

            SaveManager.SaveAll();
        }
    }
}
