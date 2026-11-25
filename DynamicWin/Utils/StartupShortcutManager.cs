using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DynamicWin.Utils
{

    public class StartupShortcutManager
    {
        private static string GetStartupFolderPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        }

        private static string GetShortcutPath(string shortcutName)
        {
            return Path.Combine(GetStartupFolderPath(), $"{shortcutName}.lnk");
        }

        public static void CreateShortcut()
        {
            string appPath = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            string shortcutPath = GetShortcutPath(appPath);

            // Kısayol varsa bile yeniden yaz: derleme çıktı klasörü değişince (ör. hedef platform
            // güncellemesi) eski kısayol artık olmayan bir exe'yi gösterir.
            string exePath = Process.GetCurrentProcess().MainModule.FileName;

            // WScript.Shell geç bağlama (late binding) ile kullanılıyor; COMReference
            // .NET SDK'nın dotnet build'inde desteklenmediği için kaldırıldı.
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic wshShell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = wshShell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
            shortcut.Description = "Launches the app on system startup.";
            shortcut.Save();

            Console.WriteLine("Shortcut created successfully.");
        }

        public static bool RemoveShortcut()
        {
            string appPath = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            string shortcutPath = GetShortcutPath(appPath);

            if (System.IO.File.Exists(shortcutPath))
            {
                System.IO.File.Delete(shortcutPath);
                Console.WriteLine("Shortcut removed successfully.");
                return true;
            }

            Console.WriteLine("Shortcut does not exist.");
            return false;
        }
    }

}
