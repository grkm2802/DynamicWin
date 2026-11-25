using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;

namespace DynamicWin.UI.Menu.Menus
{
    /// <summary>
    /// Windows bildirimini adadan "düşen" kart olarak gösterir: uygulama adı, başlık, gövde.
    /// Üzerine gelince kapanır. <see cref="RendererMain"/> bunu <see cref="NotificationWatcher"/> kuyruğundan açar.
    /// </summary>
    public class NotificationMenu : BaseMenu
    {
        IslandNotification data = new IslandNotification();

        DWText appText;
        DWText titleText;
        DWText bodyText;

        float sizeMulti = 0.85f;
        float openedFor = 0f;

        // BaseMenu kurucusu InitializeMenu'yu bu sınıfın kurucusundan önce çağırır (bkz. ClaudeCodeMenu).
        [ThreadStatic] static IslandNotification? pending;

        public static NotificationMenu Create(IslandNotification n)
        {
            pending = n;
            try { return new NotificationMenu(); }
            finally { pending = null; }
        }

        private NotificationMenu() { }

        bool HasBody => !string.IsNullOrEmpty(data.Body);

        public override List<UIObject> InitializeMenu(IslandObject island)
        {
            var objects = base.InitializeMenu(island);
            data = pending ?? new IslandNotification();

            float top = HasBody ? -16 : -8;

            appText = new DWText(island, DWText.Truncate(string.IsNullOrEmpty(data.App) ? "Bildirim" : data.App, 40), new Vec2(20, top), UIAlignment.MiddleLeft)
            {
                TextSize = 11,
                Font = Res.InterRegular
            };
            appText.Anchor.X = 0;
            appText.Color = Theme.TextThird;
            objects.Add(appText);

            titleText = new DWText(island, DWText.Truncate(data.Title, 42), new Vec2(20, top + 18), UIAlignment.MiddleLeft)
            {
                TextSize = 14,
                Font = Res.InterBold
            };
            titleText.Anchor.X = 0;
            titleText.Color = Theme.TextMain;
            objects.Add(titleText);

            bodyText = new DWText(island, DWText.Truncate(data.Body, 52), new Vec2(20, top + 36), UIAlignment.MiddleLeft)
            {
                TextSize = 12,
                Font = Res.InterRegular
            };
            bodyText.Anchor.X = 0;
            bodyText.Color = Theme.TextSecond;
            objects.Add(bodyText);

            return objects;
        }

        public override void Update()
        {
            base.Update();

            var delta = RendererMain.Instance.DeltaTime;
            openedFor += delta;
            sizeMulti = Mathf.Lerp(sizeMulti, 1f, 7f * delta);

            if (RendererMain.Instance.MainIsland.IsHovering && openedFor > 0.75f)
                MenuManager.CloseOverlay();
        }

        public override Vec2 IslandSize()
        {
            return new Vec2(360, HasBody ? 82 : 62) * sizeMulti;
        }

        public override Vec2 IslandSizeBig()
        {
            return IslandSize();
        }

        public override Col IslandBorderColor()
        {
            return Theme.Primary.Override(a: 0.4f);
        }
    }
}
