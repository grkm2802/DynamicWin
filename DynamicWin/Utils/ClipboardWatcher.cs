using DynamicWin.UI.Menu.Menus;
using SkiaSharp;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace DynamicWin.Utils
{
    public class ClipEntry
    {
        public string Text = "";
        public DateTime Time = DateTime.Now;
    }

    /// <summary>
    /// Panoyu dinler (AddClipboardFormatListener):
    ///  - Metin kopyalanınca son 25 metni hafızada tutar (#11, diske yazılmaz).
    ///  - Resim kopyalanınca (Win+Shift+S vb.) Tray'e PNG olarak kaydeder ve adada önizleme gösterir (#12).
    /// Parola yöneticilerinin "geçmişe alma" işaretlerine uyar.
    /// </summary>
    public static class ClipboardWatcher
    {
        const int WM_CLIPBOARDUPDATE = 0x031D;
        const int MaxEntries = 25;

        public static readonly List<ClipEntry> History = new List<ClipEntry>();
        public static event Action? Changed;

        static bool ignoreNext;
        static DateTime lastImageAt = DateTime.MinValue;

        public static void Start(Window window)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            AddClipboardFormatListener(hwnd);
        }

        static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                try { OnClipboardChanged(); }
                catch (Exception e) { Debug.WriteLine("Pano okunamadı: " + e.Message); }
            }
            return IntPtr.Zero;
        }

        /// <summary>Geçmişten bir öğeyi panoya geri koyar (bu değişiklik geçmişe tekrar eklenmez).</summary>
        public static void CopyBack(ClipEntry e)
        {
            ignoreNext = true;
            try { Clipboard.SetText(e.Text); } catch { ignoreNext = false; }
            lock (History) { History.Remove(e); History.Insert(0, e); }
            Changed?.Invoke();
        }

        static void OnClipboardChanged()
        {
            if (ignoreNext) { ignoreNext = false; return; }

            var data = Clipboard.GetDataObject();
            if (data == null) return;

            // Parola yöneticileri bu biçimleri koyarak "beni kaydetme" der.
            if (data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing")) return;
            if (data.GetDataPresent("CanIncludeInClipboardHistory") &&
                data.GetData("CanIncludeInClipboardHistory") is MemoryStream ms && ms.Length >= 4 && ms.ReadByte() == 0) return;

            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;
                if (text.Length > 4000) text = text.Substring(0, 4000);
                lock (History)
                {
                    History.RemoveAll(x => x.Text == text);
                    History.Insert(0, new ClipEntry { Text = text });
                    if (History.Count > MaxEntries) History.RemoveRange(MaxEntries, History.Count - MaxEntries);
                }
                Changed?.Invoke();
            }
            else if (Clipboard.ContainsImage() && Main.Settings.ScreenshotPreview)
            {
                // Aynı görüntü için art arda gelen birden fazla bildirimi yut.
                if ((DateTime.Now - lastImageAt).TotalMilliseconds < 800) return;
                lastImageAt = DateTime.Now;
                var img = Clipboard.GetImage();
                if (img != null) SaveScreenshot(img);
            }
        }

        static void SaveScreenshot(BitmapSource img)
        {
            var dir = Path.Combine(SaveManager.SavePath, "TrayFiles");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, L.T("Screenshot", "Ekran görüntüsü") + " " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(img));
            using (var fs = File.Create(path)) encoder.Save(fs);

            SKBitmap? thumb = null;
            try
            {
                using var full = SKBitmap.Decode(path);
                float scale = Math.Min(1f, 160f / Math.Max(full.Width, full.Height));
                thumb = full.Resize(new SKImageInfo(Math.Max(1, (int)(full.Width * scale)), Math.Max(1, (int)(full.Height * scale))), SKFilterQuality.Medium);
            }
            catch { }

            IslandPopups.Show(new PopupInfo
            {
                Glyph = Icons.Screenshot,
                Image = thumb,
                Title = L.T("Screenshot saved to Tray", "Ekran görüntüsü Tray'e eklendi"),
                Subtitle = L.T("Drag it from the Tray to share", "Paylaşmak için Tray'den sürükleyin"),
                Accent = Col.FromHex("#0A84FF"),
                Seconds = 4f
            });
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AddClipboardFormatListener(IntPtr hwnd);
    }
}
