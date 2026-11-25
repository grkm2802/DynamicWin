using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace DynamicWin.Utils
{
    public enum ClaudeState
    {
        Idle,               // Oturum açık, kullanıcıdan komut bekliyor
        Working,            // Claude düşünüyor / araç çalıştırıyor
        WaitingPermission,  // Bir araç için izin istiyor
        WaitingInput,       // Uzun süredir kullanıcıdan yanıt bekliyor
        Done                // Son isteği bitirdi
    }

    public class ClaudeSession
    {
        public string Id = "";
        public string Project = "";
        public ClaudeState State = ClaudeState.Idle;
        public string Detail = "";
        public DateTime StateSince = DateTime.Now;
        public DateTime LastUpdate = DateTime.Now;
    }

    public class ClaudeEvent
    {
        public ClaudeState State;
        public string Project = "";
        public string Detail = "";
    }

    /// <summary>
    /// Claude Code hook'larının gönderdiği JSON olaylarını yerel bir HTTP uç noktasından alır
    /// (http://localhost:47321/claude) ve oturum durumlarını tutar. Sadece loopback'i dinler.
    /// Hook komutu stdin'deki JSON'u olduğu gibi POST eder; alanlar Claude Code hook girdisidir:
    /// session_id, hook_event_name, cwd, tool_name, tool_input, message, notification_type.
    /// </summary>
    public static class ClaudeBridge
    {
        public const int Port = 47321;
        public static readonly string Url = $"http://localhost:{Port}/claude/";

        static readonly object sync = new object();
        static readonly Dictionary<string, ClaudeSession> sessions = new Dictionary<string, ClaudeSession>();

        /// <summary>Adada açılır pencere gösterilmesi gereken olaylar (render thread'i boşaltır).</summary>
        public static readonly ConcurrentQueue<ClaudeEvent> PopupQueue = new ConcurrentQueue<ClaudeEvent>();

        static HttpListener? listener;

        // Bu kadar süre olay gelmeyen oturum kapanmış sayılır (SessionEnd her zaman gelmeyebilir).
        static readonly TimeSpan staleAfter = TimeSpan.FromMinutes(30);
        // "Bitti" durumu bu süre sonra boşta durumuna düşer; widget gizlenir.
        static readonly TimeSpan doneVisibleFor = TimeSpan.FromSeconds(30);

        public static void Start()
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(Url);
                listener.Start();
                Task.Run(ListenLoop);
                Debug.WriteLine($"ClaudeBridge dinliyor: {Url}");
            }
            catch (Exception e)
            {
                // Port doluysa veya HttpListener açılamazsa uygulama çalışmaya devam etsin.
                Debug.WriteLine("ClaudeBridge başlatılamadı: " + e.Message);
                listener = null;
            }
        }

        public static void Stop()
        {
            try { listener?.Stop(); listener?.Close(); } catch { }
            listener = null;
        }

        static async Task ListenLoop()
        {
            while (listener != null && listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); }
                catch { break; }

                try
                {
                    if (ctx.Request.HttpMethod == "POST")
                    {
                        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                        var body = await reader.ReadToEndAsync();
                        HandleEvent(JObject.Parse(body));
                        ctx.Response.StatusCode = 204;
                    }
                    else
                    {
                        // GET: tanılama (oturumlar + ada durumu). Sadece localhost'tan erişilir.
                        var bytes = Encoding.UTF8.GetBytes(DebugState());
                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentType = "application/json; charset=utf-8";
                        await ctx.Response.OutputStream.WriteAsync(bytes);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("ClaudeBridge hatalı olay: " + e.Message);
                    ctx.Response.StatusCode = 400;
                }
                finally
                {
                    try { ctx.Response.Close(); } catch { }
                }
            }
        }

        static string DebugState()
        {
            var o = new JObject();
            lock (sync)
            {
                o["sessions"] = new JArray(sessions.Values.Select(s => new JObject
                {
                    ["id"] = s.Id, ["project"] = s.Project, ["state"] = s.State.ToString(),
                    ["detail"] = s.Detail, ["lastUpdate"] = s.LastUpdate.ToString("HH:mm:ss")
                }));
            }
            try
            {
                // UI nesneleri sadece kendi thread'inden okunabilir.
                System.Windows.Application.Current.Dispatcher.Invoke(() => FillIslandState(o));
            }
            catch (Exception e) { o["islandError"] = e.Message; }
            return o.ToString();
        }

        static void FillIslandState(JObject o)
        {
            {
                var r = Main.RendererMain.Instance;
                var island = r?.MainIsland;
                if (island != null)
                {
                    o["island"] = new JObject
                    {
                        ["pos"] = $"{island.Position.X:0.#},{island.Position.Y:0.#}",
                        ["size"] = $"{island.Size.X:0.#}x{island.Size.Y:0.#}",
                        ["target"] = $"{island.currSize.X:0.#}x{island.currSize.Y:0.#}",
                        ["hidden"] = island.hidden,
                        ["hovering"] = island.IsHovering,
                        ["menu"] = UI.Menu.MenuManager.Instance?.ActiveMenu?.GetType().Name,
                        ["fullscreen"] = FullscreenDetector.IsFullscreenActive,
                        ["alphaOverride"] = r!.alphaOverride,
                        ["windowVisible"] = Main.MainForm.Instance?.IsVisible,
                        ["windowOpacity"] = Main.MainForm.Instance?.Opacity,
                        ["perf"] = Perf.Report()
                    };
                }
            }
        }

        public static void HandleEvent(JObject e)
        {
            // Geliştirme testi: {"hook_event_name":"DebugPopup","glyph":"bt|charge|mute|...","title":"..","subtitle":"..","ring":0.5}
            if ((string?)e["hook_event_name"] == "DebugPopup")
            {
                UI.Menu.Menus.IslandPopups.Show(new UI.Menu.Menus.PopupInfo
                {
                    Glyph = (string?)e["glyph"] ?? UI.Menu.Menus.Icons.Info,
                    Title = (string?)e["title"] ?? "Test",
                    Subtitle = (string?)e["subtitle"] ?? "",
                    Ring = (float?)e["ring"] ?? -1f,
                    RingText = (string?)e["ringText"] ?? "",
                    Accent = Col.FromHex((string?)e["accent"] ?? "#3B82F6")
                });
                return;
            }

            string id = (string?)e["session_id"] ?? "default";
            string evt = (string?)e["hook_event_name"] ?? "";
            string cwd = (string?)e["cwd"] ?? "";
            string project = string.IsNullOrEmpty(cwd) ? "Claude" : Path.GetFileName(cwd.TrimEnd('\\', '/'));

            ClaudeEvent? popup = null;

            lock (sync)
            {
                if (evt == "SessionEnd")
                {
                    sessions.Remove(id);
                    return;
                }

                if (!sessions.TryGetValue(id, out var s))
                {
                    s = new ClaudeSession { Id = id };
                    sessions[id] = s;
                }
                s.Project = project;
                s.LastUpdate = DateTime.Now;

                switch (evt)
                {
                    case "SessionStart":
                        SetState(s, ClaudeState.Idle, "");
                        break;
                    case "UserPromptSubmit":
                        SetState(s, ClaudeState.Working, "Düşünüyor");
                        break;
                    case "PreToolUse":
                        SetState(s, ClaudeState.Working, DescribeTool(e));
                        break;
                    case "PostToolUse":
                        if (s.State != ClaudeState.Working) SetState(s, ClaudeState.Working, "Düşünüyor");
                        break;
                    case "PermissionRequest":
                        SetState(s, ClaudeState.WaitingPermission, DescribeTool(e));
                        popup = new ClaudeEvent { State = s.State, Project = project, Detail = s.Detail };
                        break;
                    case "Notification":
                        {
                            string type = (string?)e["notification_type"] ?? "";
                            string msg = (string?)e["message"] ?? "";
                            bool isPermission = type == "permission_prompt" ||
                                                msg.Contains("permission", StringComparison.OrdinalIgnoreCase);
                            if (isPermission)
                            {
                                // PermissionRequest zaten geldiyse aynı popup'ı tekrar açma.
                                bool already = s.State == ClaudeState.WaitingPermission;
                                SetState(s, ClaudeState.WaitingPermission, already ? s.Detail : "İzin bekliyor");
                                if (!already) popup = new ClaudeEvent { State = s.State, Project = project, Detail = s.Detail };
                            }
                            else if (type == "idle_prompt" || msg.Contains("waiting", StringComparison.OrdinalIgnoreCase))
                            {
                                SetState(s, ClaudeState.WaitingInput, "Yanıtını bekliyor");
                            }
                            break;
                        }
                    case "Stop":
                        SetState(s, ClaudeState.Done, "Bitti");
                        popup = new ClaudeEvent { State = s.State, Project = project, Detail = "Görev tamamlandı" };
                        break;
                }
            }

            if (popup != null) PopupQueue.Enqueue(popup);
        }

        static void SetState(ClaudeSession s, ClaudeState state, string detail)
        {
            if (s.State != state) s.StateSince = DateTime.Now;
            s.State = state;
            s.Detail = detail;
        }

        static string DescribeTool(JObject e)
        {
            string tool = (string?)e["tool_name"] ?? "";
            var input = e["tool_input"] as JObject;

            string? file = (string?)input?["file_path"];
            string fileName = string.IsNullOrEmpty(file) ? "" : " " + Path.GetFileName(file);

            return tool switch
            {
                "Bash" or "PowerShell" => "Komut çalıştırıyor",
                "Edit" or "MultiEdit" => "Düzenliyor" + fileName,
                "Write" => "Yazıyor" + fileName,
                "Read" => "Okuyor" + fileName,
                "Grep" or "Glob" => "Arıyor",
                "WebFetch" or "WebSearch" => "Web'e bakıyor",
                "Agent" or "Task" => "Alt ajan çalıştırıyor",
                "" => "Çalışıyor",
                _ => tool.StartsWith("mcp__") ? "Araç: " + tool.Split("__").Last() : tool
            };
        }

        /// <summary>
        /// Adada gösterilecek en önemli oturum: izin bekleyen &gt; yanıt bekleyen &gt; çalışan &gt; yeni biten.
        /// Gösterilecek bir şey yoksa null. Ayrıca kaç oturumun aktif olduğunu döndürür.
        /// </summary>
        public static ClaudeSession? GetPrimary(out int activeCount)
        {
            lock (sync)
            {
                var now = DateTime.Now;
                foreach (var stale in sessions.Values.Where(x => now - x.LastUpdate > staleAfter).Select(x => x.Id).ToList())
                    sessions.Remove(stale);

                foreach (var s in sessions.Values)
                    if (s.State == ClaudeState.Done && now - s.StateSince > doneVisibleFor)
                        SetState(s, ClaudeState.Idle, "");

                var visible = sessions.Values.Where(x => x.State != ClaudeState.Idle).ToList();
                activeCount = visible.Count;

                var best = visible
                    .OrderBy(x => x.State switch
                    {
                        ClaudeState.WaitingPermission => 0,
                        ClaudeState.WaitingInput => 1,
                        ClaudeState.Working => 2,
                        _ => 3
                    })
                    .ThenByDescending(x => x.LastUpdate)
                    .FirstOrDefault();

                if (best == null) return null;

                // Kilit dışında güvenle okunabilsin diye kopya döndür.
                return new ClaudeSession
                {
                    Id = best.Id, Project = best.Project, State = best.State,
                    Detail = best.Detail, StateSince = best.StateSince, LastUpdate = best.LastUpdate
                };
            }
        }
    }
}
