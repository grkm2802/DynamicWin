using System.Diagnostics;

namespace DynamicWin.Utils
{
    /// <summary>
    /// Basit kare profilleyici (sadece UI thread'inden çağrılır). Son tam saniyenin toplamlarını tutar;
    /// tanılama adresi (GET http://localhost:47321/claude/) "perf" altında gösterir. Ölçüm maliyeti ihmal edilebilir.
    /// </summary>
    public static class Perf
    {
        static readonly Dictionary<string, double> current = new Dictionary<string, double>();
        static Dictionary<string, double> last = new Dictionary<string, double>();
        static int frames, lastFrames;
        static long windowStart = Stopwatch.GetTimestamp();

        public static void Add(string key, long startTimestamp)
        {
            double ms = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
            current[key] = current.TryGetValue(key, out var v) ? v + ms : ms;
        }

        public static void Frame()
        {
            frames++;
            if ((Stopwatch.GetTimestamp() - windowStart) * 1.0 / Stopwatch.Frequency >= 1.0)
            {
                last = new Dictionary<string, double>(current);
                lastFrames = frames;
                current.Clear(); frames = 0;
                windowStart = Stopwatch.GetTimestamp();
            }
        }

        /// <summary>Son saniye: kare sayısı ve her bölümün toplam ms'i (büyükten küçüğe).</summary>
        public static string Report()
        {
            var parts = last.OrderByDescending(x => x.Value).Select(x => $"{x.Key}={x.Value:0.0}");
            return $"fps={lastFrames} " + string.Join(" ", parts);
        }
    }
}
