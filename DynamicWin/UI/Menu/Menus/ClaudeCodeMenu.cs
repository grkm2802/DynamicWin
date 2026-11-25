using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.UIElements;
using DynamicWin.UI.Widgets.Small;
using DynamicWin.Utils;

namespace DynamicWin.UI.Menu.Menus
{
    /// <summary>
    /// Claude Code izin istediğinde veya görevi bitirdiğinde adadan kısa süre "düşen" bildirim.
    /// Üzerine gelince kapanır. <see cref="RendererMain"/> bunu <see cref="ClaudeBridge.PopupQueue"/>'dan açar.
    /// </summary>
    public class ClaudeCodeMenu : BaseMenu
    {
        ClaudeEvent evt = new ClaudeEvent();

        DWText title;
        DWText subtitle;

        float sinCycle = 0f;
        float sizeMulti = 1.15f;
        float openedFor = 0f;

        // BaseMenu kurucusu InitializeMenu'yu bu sınıfın kurucusundan ÖNCE çağırır;
        // bu yüzden olay, kurucuya parametre olarak değil bu alan üzerinden aktarılır.
        [ThreadStatic] static ClaudeEvent? pendingEvent;

        public static ClaudeCodeMenu Create(ClaudeEvent e)
        {
            pendingEvent = e;
            try { return new ClaudeCodeMenu(); }
            finally { pendingEvent = null; }
        }

        private ClaudeCodeMenu() { }

        static string TitleFor(ClaudeState s) => s switch
        {
            ClaudeState.WaitingPermission => "Claude izin istiyor",
            ClaudeState.Done => "Claude bitirdi",
            ClaudeState.WaitingInput => "Claude yanıtını bekliyor",
            _ => "Claude"
        };

        public override List<UIObject> InitializeMenu(IslandObject island)
        {
            var objects = base.InitializeMenu(island);
            evt = pendingEvent ?? new ClaudeEvent();

            title = new DWText(island, TitleFor(evt.State), new Vec2(0, -9), UIAlignment.Center)
            {
                TextSize = 16,
                Font = Res.InterBold
            };
            title.Color = ClaudeCodeWidget.ColorFor(evt.State);
            objects.Add(title);

            string sub = evt.Project;
            if (!string.IsNullOrEmpty(evt.Detail)) sub += " · " + evt.Detail;

            subtitle = new DWText(island, DWText.Truncate(sub, 38), new Vec2(0, 12), UIAlignment.Center)
            {
                TextSize = 12,
                Font = Res.InterRegular
            };
            subtitle.Color = Theme.TextSecond;
            objects.Add(subtitle);

            return objects;
        }

        public override void Update()
        {
            base.Update();

            var delta = RendererMain.Instance.DeltaTime;
            openedFor += delta;
            sinCycle += delta * (evt.State == ClaudeState.WaitingPermission ? 5f : 2.5f);

            // Açılışta hafif büyüyüp yerine oturur.
            sizeMulti = Mathf.Lerp(sizeMulti, 1f, 6f * delta);

            if (evt.State == ClaudeState.WaitingPermission)
                title.Alpha = Mathf.Remap((float)Math.Sin(sinCycle), -1, 1, 0.6f, 1f);

            if (RendererMain.Instance.MainIsland.IsHovering && openedFor > 0.75f)
                MenuManager.CloseOverlay();
        }

        public override Vec2 IslandSize()
        {
            return new Vec2(300, 60) * sizeMulti;
        }

        public override Vec2 IslandSizeBig()
        {
            return IslandSize();
        }

        public override Col IslandBorderColor()
        {
            return ClaudeCodeWidget.ColorFor(evt.State).Override(a: 0.6f);
        }
    }
}
