using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.UI.UIElements;
using DynamicWin.Utils;
using SkiaSharp;

namespace DynamicWin.UI.Widgets.Small
{
    class RegisterClaudeCodeWidget : IRegisterableWidget
    {
        public bool IsSmallWidget => true;

        public string WidgetName => "Claude Code";

        public WidgetBase CreateWidgetInstance(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter)
        {
            return new ClaudeCodeWidget(parent, position, alignment);
        }
    }

    /// <summary>
    /// Claude Code oturumunun canlı durumunu küçük widget olarak gösterir: renkli nabız noktası + kısa metin.
    /// Aktif oturum yoksa genişliği 0'a iner ve görünmez. Veriyi <see cref="ClaudeBridge"/> sağlar.
    /// </summary>
    public class ClaudeCodeWidget : SmallWidgetBase
    {
        public static readonly Col ClaudeOrange = Col.FromHex("#D97757");

        DWText label;

        float pulse = 0f;
        float dotSize = 0f;
        float currentWidth = 0f;
        ClaudeSession? session;
        int activeCount;

        const float dotRadius = 3.5f;
        const float textOffset = 14f;
        const int maxChars = 22;

        public ClaudeCodeWidget(UIObject? parent, Vec2 position, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, alignment)
        {
            label = new DWText(this, " ", new Vec2(textOffset, 0), UIAlignment.MiddleLeft)
            {
                TextSize = 12,
                Font = Res.InterRegular
            };
            label.Anchor.X = 0;
            AddLocalObject(label);
        }

        public static Col ColorFor(ClaudeState state) => state switch
        {
            ClaudeState.WaitingPermission => Theme.Error,
            ClaudeState.WaitingInput => Theme.Primary,
            ClaudeState.Done => Theme.Success,
            _ => ClaudeOrange
        };

        string GetLabel()
        {
            if (session == null) return " ";
            string text = session.Detail;
            if (string.IsNullOrEmpty(text)) text = session.Project;
            text = DWText.Truncate(text, maxChars);
            if (activeCount > 1) text += $"  +{activeCount - 1}";
            return text;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            session = ClaudeBridge.GetPrimary(out activeCount);

            // Çalışırken yavaş, dikkat isterken hızlı nabız.
            float speed = session?.State == ClaudeState.WaitingPermission ? 6f : 3f;
            pulse += speed * deltaTime;

            dotSize = Mathf.Lerp(dotSize, session != null ? dotRadius : 0f, 6f * deltaTime);

            string text = GetLabel();
            label.SilentSetText(text);

            float target = session != null ? textOffset + label.GetBoundsForString(text).X + 8f : 0f;
            currentWidth = Mathf.Lerp(currentWidth, target, 8f * deltaTime);

            label.Alpha = session != null ? 1f : 0f;
            label.Color = Theme.TextMain;
        }

        public override void DrawWidget(SKCanvas canvas)
        {
            if (dotSize < 0.05f) return;

            var paint = GetPaint();
            var baseColor = session != null ? ColorFor(session.State) : ClaudeOrange;
            float t = Mathf.Remap((float)Math.Sin(pulse), -1, 1, 0, 1);

            var dotPos = GetScreenPosFromRawPosition(new Vec2(6f, 0), new Vec2(0, dotSize / 2), UIAlignment.MiddleLeft, this);

            paint.Color = GetColor(Col.Lerp(baseColor, baseColor * 0.55f, t)).Value();
            canvas.DrawCircle(dotPos.X, dotPos.Y, dotSize, paint);

            // İzin beklerken etrafına genişleyen halka çiz.
            if (session?.State == ClaudeState.WaitingPermission)
            {
                paint.IsStroke = true;
                paint.StrokeWidth = 1.25f;
                paint.Color = GetColor(baseColor.Override(a: 1f - t)).Value();
                canvas.DrawCircle(dotPos.X, dotPos.Y, dotSize + 1f + t * 3f, paint);
            }
        }

        protected override float GetWidgetWidth()
        {
            return currentWidth;
        }
    }
}
