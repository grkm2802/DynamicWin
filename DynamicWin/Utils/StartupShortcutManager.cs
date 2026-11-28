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
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key.GetValue(ValueName) as string != value)
                    key.SetValue(ValueName, value);
            }

            // Eski yöntemden kalan Başlangıç klasörü kısayolunu temizle (çift başlatma olmasın).
            try { if (File.Exists(LegacyShortcutPath)) File.Delete(LegacyShortcutPath); } catch { }

            // Kilit açılınca (uykudan dönüş dahil) çalışmıyorsa yeniden başlat. Kullanıcı Görev Yöneticisi'nde
            // başlangıcı kapattıysa görevi de kaldır.
            if (IsDisabledInTaskManager()) Task.Run(DeleteUnlockTask);
            else Task.Run(CreateUnlockTask);
        }

        /// <summary>
        /// Sistemde gerçekten bir başlangıç kaydı (görev veya Run değeri) var mı? Ayar dosyası ile gerçek durum
        /// kopabildiği için (bkz. CLAUDE.md "paket ortamı") "açılışta başlat" ayarı açılışta buradan okunur.
        /// </summary>
        public static bool IsRegistered()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                    if (key?.GetValue(ValueName) != null) return true;
                var psi = new ProcessStartInfo("schtasks.exe", $"/Query /TN \"{TaskName}\"") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                using var p = Process.Start(psi)!;
                p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
            catch { return false; }
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
            Task.Run(DeleteUnlockTask);
            return removed;
        }

        // ---- Kilit açılınca başlatan zamanlanmış görev ----
        // Uygulama bir şekilde kapanırsa (ör. uyku sırasında) kullanıcı kilidi açınca yeniden açılsın.
        // Zaten çalışıyorsa ikinci kopya sessizce çıkar (App.OnStartup). Yönetici izni gerektirmez.
        const string TaskName = "DynamicWin";

        /// <summary>Görev Yöneticisi › Başlangıç'ta kapatılmışsa StartupApproved değerinin ilk baytı 01 dışındadır (03).</summary>
        static bool IsDisabledInTaskManager()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
                return k?.GetValue(ValueName) is byte[] b && b.Length > 0 && (b[0] & 1) == 1;
            }
            catch { return false; }
        }

        static void CreateUnlockTask()
        {
            try
            {
                string user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                string esc(string x) => System.Security.SecurityElement.Escape(x)!;
                string xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo><Description>DynamicWin: oturum açılınca ve kilit açılınca (çalışmıyorsa) başlat</Description></RegistrationInfo>
  <Triggers>
    <LogonTrigger><Enabled>true</Enabled><UserId>{esc(user)}</UserId></LogonTrigger>
    <SessionStateChangeTrigger><Enabled>true</Enabled><StateChange>SessionUnlock</StateChange><UserId>{esc(user)}</UserId></SessionStateChangeTrigger>
  </Triggers>
  <Principals><Principal id=""Author""><UserId>{esc(user)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context=""Author""><Exec><Command>{esc(ExePath)}</Command><WorkingDirectory>{esc(Path.GetDirectoryName(ExePath)!)}</WorkingDirectory></Exec></Actions>
</Task>";
                string tmp = Path.Combine(Path.GetTempPath(), "DynamicWinTask.xml");
                File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode);
                RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{tmp}\" /F");
                File.Delete(tmp);
            }
            catch (Exception e) { Log.Error("Kilit açma görevi", e); }
        }

        static void DeleteUnlockTask()
        {
            try { RunSchtasks($"/Delete /TN \"{TaskName}\" /F"); } catch { }
        }

        static void RunSchtasks(string args)
        {
            var psi = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit(10000);
            if (p.ExitCode != 0 && !args.StartsWith("/Delete")) Log.Write($"schtasks {args.Split(' ')[0]} başarısız: {err.Trim()}");
        }
    }
}
