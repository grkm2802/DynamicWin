using System.Diagnostics;
using System.IO;

namespace DynamicWin.Utils
{
    /// <summary>
    /// Basit kayıt dosyası: %AppData%\DynamicWin\log.txt (1 MB'ı geçince log.old.txt'ye döner).
    /// Açılış/kapanış, uyku/uyanma, kilit ve yakalanmamış hatalar yazılır. Uygulamanın "sessizce kapanma"
    /// sorunlarını teşhis etmek için var; her karede yazma yapılmaz.
    /// </summary>
    public static class Log
    {
        static readonly object sync = new object();
        static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicWin");
        public static string FilePath => Path.Combine(Dir, "log.txt");

        public static void Write(string message)
        {
            try
            {
                lock (sync)
                {
                    Directory.CreateDirectory(Dir);
                    var fi = new FileInfo(FilePath);
                    if (fi.Exists && fi.Length > 1024 * 1024)
                    {
                        var old = Path.Combine(Dir, "log.old.txt");
                        File.Delete(old);
                        File.Move(FilePath, old);
                    }
                    File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.ProcessId}] {message}{Environment.NewLine}");
                }
            }
            catch { /* kayıt yazılamaması uygulamayı durdurmamalı */ }
            Debug.WriteLine(message);
        }

        public static void Error(string where, object? ex) => Write($"HATA ({where}): {ex}");
    }
}
