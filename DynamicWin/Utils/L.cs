namespace DynamicWin.Utils
{
    /// <summary>
    /// Basit iki dilli metin yardımcısı: <c>L.T("Settings", "Ayarlar")</c>.
    /// Anahtar dosyası yerine metni yazıldığı yerde tutar; yeni bir dil gerekirse buradan genişletilir.
    /// Dil <see cref="Main.Settings.Language"/> ile seçilir ("tr" veya "en").
    /// </summary>
    public static class L
    {
        public static bool IsTurkish => Main.Settings.Language != "en";

        public static string T(string en, string tr) => IsTurkish ? tr : en;
    }
}
