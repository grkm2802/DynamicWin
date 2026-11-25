using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace DynamicWin.Utils
{

    public class DeviceUsageChecker
    {
        private static readonly string MicrophoneSubkey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
        private static readonly string WebcamSubkey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";
        private static readonly string TimestampValueName = "LastUsedTimeStop";

        // Kayıt defteri ağacını taramak pahalı ve widget her karede soruyor. Tarama arka planda 500 ms'de bir yapılır;
        // UI thread'i sadece son sonucu okur.
        static volatile bool micCached, camCached;
        static System.Threading.Timer? timer;

        static void EnsureStarted()
        {
            timer ??= new System.Threading.Timer(_ =>
            {
                micCached = IsDeviceInUse(MicrophoneSubkey);
                camCached = IsDeviceInUse(WebcamSubkey);
            }, null, 0, 500);
        }

        public static bool IsMicrophoneInUse()
        {
            EnsureStarted();
            return micCached;
        }

        public static bool IsWebcamInUse()
        {
            EnsureStarted();
            return camCached;
        }

        private static readonly string ScreenCaptureSubkey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\graphicsCaptureProgrammatic";

        /// <summary>
        /// Şu an ekranı yakalayan (kaydeden / paylaşan) uygulamanın adı; yoksa null. Windows Graphics Capture
        /// kullananları görür (Ekran Alıntısı Aracı kaydı, Discord/Teams ekran paylaşımı...). OBS'in DXGI yakalaması burada görünmez.
        /// Kayıt defteri taraması olduğu için çağıran taraf seyrek çağırmalı.
        /// </summary>
        public static string? GetScreenCaptureApp()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ScreenCaptureSubkey);
                if (key == null) return null;
                foreach (var name in key.GetSubKeyNames())
                {
                    if (name == "NonPackaged")
                    {
                        using var np = key.OpenSubKey(name);
                        if (np == null) continue;
                        foreach (var n in np.GetSubKeyNames())
                            if (GetSubkeyTimestamp($@"{ScreenCaptureSubkey}\NonPackaged\{n}") == 0)
                                return System.IO.Path.GetFileNameWithoutExtension(n.Replace('#', '\\'));
                    }
                    else if (GetSubkeyTimestamp($@"{ScreenCaptureSubkey}\{name}") == 0)
                    {
                        var pretty = name.Split('_')[0];
                        return pretty.Contains('.') ? pretty.Substring(pretty.LastIndexOf('.') + 1) : pretty;
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool IsDeviceInUse(string subkey)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subkey))
                {
                    if (key == null) return false;

                    foreach (string subkeyName in key.GetSubKeyNames())
                    {
                        string subkeyPath = $@"{subkey}\{subkeyName}";
                        if (subkeyName.Equals("NonPackaged"))
                        {
                            using (RegistryKey nonPackagedKey = Registry.CurrentUser.OpenSubKey(subkeyPath))
                            {
                                if (nonPackagedKey == null) continue;

                                foreach (string npSubkeyName in nonPackagedKey.GetSubKeyNames())
                                {
                                    string npSubkeyPath = $@"{subkeyPath}\{npSubkeyName}";
                                    if (GetSubkeyTimestamp(npSubkeyPath) == 0)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (GetSubkeyTimestamp(subkeyPath) == 0)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return false;
        }

        private static long GetSubkeyTimestamp(string subkeyPath)
        {
            try
            {
                using (RegistryKey subkey = Registry.CurrentUser.OpenSubKey(subkeyPath))
                {
                    if (subkey == null) return -1;
                    object value = subkey.GetValue(TimestampValueName);
                    if (value != null && long.TryParse(value.ToString(), out long timestamp))
                    {
                        return timestamp;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return -1;
        }
    }

}
