using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace DynamicWin.Utils
{
    /// <summary>
    /// Windows açılışında başlatma. Eskiden Başlangıç klasörüne .lnk yazılıyordu; uygulama her açılışta kısayolu
    /// yeniden yazdığı için Windows'un açılışta okumasıyla çakışabiliyor ("dosya bulunamadı") ve kısayol en son
    /// çalışan exe'yi (geliştirme klasörünü de) gösteriyordu. Artık HKCU\...\Run kaydı kullanılır (Görev Yöneticisi
    /// › Başlangıç'ta görünür) ve geliştirme derlemeleri (bin\Debug, bin\Release) bu kayda dokunmaz.
    /// Metot adları eski çağıranlar için korunmuştur.
    /// </summary>
    public class StartupShortcutManager
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "DynamicWin";

        static string ExePath => Process.GetCurrentProcess().MainModule!.FileName!;

        /// <summary>Geliştirme çıktısından mı çalışıyoruz? O zaman başlangıç ayarını değiştirme.</summary>
        static bool IsDevBuild
        {
            get
            {
                var p = ExePath.Replace('/', '\\');
                return p.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase)
                    || p.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase);
            }
        }

        static string LegacyShortcutPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "DynamicWin.exe.lnk");

        public static void CreateShortcut()
        {
            if (IsDevBuild) return;

            string value = $"\"{ExePath}\"";
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key.GetValue(ValueName) as string != value)
                key.SetValue(ValueName, value);

            // Eski yöntemden kalan Başlangıç klasörü kısayolunu temizle (çift başlatma olmasın).
            try { if (File.Exists(LegacyShortcutPath)) File.Delete(LegacyShortcutPath); } catch { }
        }

        public static bool RemoveShortcut()
        {
            if (IsDevBuild) return false;

            bool removed = false;
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            {
                if (key?.GetValue(ValueName) != null) { key.DeleteValue(ValueName); removed = true; }
            }
            try { if (File.Exists(LegacyShortcutPath)) { File.Delete(LegacyShortcutPath); removed = true; } } catch { }
            return removed;
        }
    }
}
